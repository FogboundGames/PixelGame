import { createSign } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { env, fetchSafe } from './env.mjs';
import { addDays, enumerateDays, sortLevels } from './gameanalytics.mjs';

// Firebase / Google Analytics Data API (ücretsiz planda erişilebilir). Gerekli: FIREBASE_KEY_FILE (servis hesabı anahtarı) + FIREBASE_PROPERTY_ID.
// Level verisi Google Analytics'te tanımlanan level_id boyutu ve seconds metriğinden gelir (README).

const BASE = 'https://analyticsdata.googleapis.com/v1beta';
const SCOPE = 'https://www.googleapis.com/auth/analytics.readonly';
const RETENTION_DAYS = [1, 3, 7, 14, 30];
const COHORTS_PER_REQUEST = 12; // API sınırı
const MAX_COHORTS = 36;

const LEVEL_EVENTS = {
  level_start: { count: 'starts' },
  level_complete: { count: 'completes', avg: 'avgCompleteSec' },
  level_fail: { count: 'fails', avg: 'avgFailSec' },
  level_restart: { count: 'restarts', avg: 'restartAvgSec' },
  level_quit: { count: 'quits', avg: 'quitAvgSec' },
};

export function firebaseConfigured() {
  return Boolean(env('FIREBASE_KEY_FILE') && env('FIREBASE_PROPERTY_ID'));
}

// ---------- kimlik doğrulama ----------

const base64url = (value) => Buffer.from(value).toString('base64url');
const tokens = new Map();

/** Servis hesabı anahtarı (proje kimliği Remote Config için de buradan okunur). */
export function serviceAccount() {
  const path = env('FIREBASE_KEY_FILE').replace(/^~(?=\/)/, homedir());
  try {
    return JSON.parse(readFileSync(path, 'utf8'));
  } catch (e) {
    throw new Error(`Anahtar dosyası okunamadı (${path}): ${e.code || e.message}`);
  }
}

/** Kapsam başına önbelleğe alınan erişim jetonu; varsayılan kapsam Analytics okumadır. */
export async function accessToken(scope = SCOPE) {
  const token = tokens.get(scope);
  if (token && Date.now() < token.expires) return token.value;

  const key = serviceAccount();

  const now = Math.floor(Date.now() / 1000);
  const unsigned = `${base64url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }))}.${base64url(JSON.stringify({
    iss: key.client_email, scope, aud: key.token_uri, iat: now, exp: now + 3600,
  }))}`;
  const signature = createSign('RSA-SHA256').update(unsigned).sign(key.private_key, 'base64url');

  const res = await fetchSafe(key.token_uri, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion: `${unsigned}.${signature}` }),
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(`Google girişi reddetti: ${body.error_description || body.error || `HTTP ${res.status}`}`);

  tokens.set(scope, { value: body.access_token, expires: Date.now() + (body.expires_in - 60) * 1000 });
  return body.access_token;
}

// ---------- istek ----------

async function apiRequest(path, body) {
  const res = await fetchSafe(`${BASE}/properties/${env('FIREBASE_PROPERTY_ID')}${path}`, {
    method: body ? 'POST' : 'GET',
    headers: { Authorization: `Bearer ${await accessToken()}`, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    if (res.status === 403) {
      throw new Error(`Erişim reddedildi: servis hesabı Google Analytics'te Viewer olmalı ve Data API açık olmalı (${json.error?.message ?? 'HTTP 403'})`);
    }
    throw new Error(json.error?.message || `HTTP ${res.status}`);
  }
  return json;
}

/** Yanıt satırlarını sütun adıyla okunan nesnelere çevirir. */
function readRows(json) {
  const dims = (json.dimensionHeaders ?? []).map((h) => h.name);
  const mets = (json.metricHeaders ?? []).map((h) => h.name);
  return (json.rows ?? []).map((row) => {
    const out = {};
    dims.forEach((name, i) => { out[name] = row.dimensionValues[i].value; });
    mets.forEach((name, i) => { out[name] = Number(row.metricValues[i].value); });
    return out;
  });
}

const names = (list) => list.map((name) => ({ name }));
const range = (from, to) => [{ startDate: from, endDate: to }];

async function report(from, to, dimensions, metrics, extra = {}, onThreshold = () => {}) {
  const json = await apiRequest(':runReport', { dateRanges: range(from, to), dimensions: names(dimensions), metrics: names(metrics), limit: 10000, ...extra });
  if (json.metadata?.subjectToThresholding) onThreshold();
  return readRows(json);
}

// ---------- kontrol ----------

/** Anahtar ve özellik doğru mu: son 30 dakikanın olayları ve level tanımlarının durumu. */
export async function firebaseCheck() {
  const realtime = readRows(await apiRequest(':runRealtimeReport', { dimensions: names(['eventName']), metrics: names(['eventCount']) }));
  const metadata = await apiRequest('/metadata');
  const custom = (list) => list.filter((d) => d.customDefinition).map((d) => d.apiName);
  return {
    realtimeEvents: realtime,
    customDimensions: custom(metadata.dimensions ?? []),
    customMetrics: custom(metadata.metrics ?? []),
  };
}

// ---------- veri toplama ----------

async function fetchRetention(cohortDates, lastDay, onThreshold) {
  const byDate = new Map();
  const endOffset = Math.max(...RETENTION_DAYS);
  for (let i = 0; i < cohortDates.length; i += COHORTS_PER_REQUEST) {
    const cohorts = cohortDates.slice(i, i + COHORTS_PER_REQUEST).map((date) => ({ name: date, dimension: 'firstSessionDate', dateRange: { startDate: date, endDate: date } }));
    const json = await apiRequest(':runReport', {
      dimensions: names(['cohort', 'cohortNthDay']),
      metrics: names(['cohortActiveUsers', 'cohortTotalUsers']),
      cohortSpec: { cohorts, cohortsRange: { granularity: 'DAILY', startOffset: 0, endOffset } },
    });
    if (json.metadata?.subjectToThresholding) onThreshold();
    for (const row of readRows(json)) {
      const day = Number(row.cohortNthDay);
      if (!RETENTION_DAYS.includes(day) || addDays(row.cohort, day) > lastDay || row.cohortTotalUsers <= 0) continue;
      if (!byDate.has(row.cohort)) byDate.set(row.cohort, new Map());
      byDate.get(row.cohort).set(day, { active: row.cohortActiveUsers, total: row.cohortTotalUsers });
    }
  }
  return byDate;
}

function levelsFrom(rows) {
  const levels = new Map();
  for (const row of rows) {
    const id = row['customEvent:level_id'];
    const event = LEVEL_EVENTS[row.eventName];
    if (!event || !id || id === '(not set)') continue;
    if (!levels.has(id)) levels.set(id, { id, name: '', starts: 0, completes: 0, fails: 0, avgCompleteSec: null, avgFailSec: null, players: null, restarts: null, restartAvgSec: null, quits: null, quitAvgSec: null });
    const level = levels.get(id);
    level[event.count] = row.eventCount;
    if (event.avg && row.eventCount > 0) level[event.avg] = row['customEvent:seconds'] / row.eventCount;
    if (row.eventName === 'level_start') level.players = row.totalUsers;
  }
  return sortLevels([...levels.values()]);
}

export async function fetchFirebase(from, to, today) {
  const errors = [];
  let attempted = 0;
  let thresholded = false;
  const onThreshold = () => { thresholded = true; };
  const safe = async (label, fn) => {
    attempted++;
    try { return await fn(); } catch (e) { errors.push(`${label}: ${e.message}`); return null; }
  };
  const lastDay = addDays(today, -1);

  const [dailyRows, mauRows, countryRows, platformRows, eventRows, levelRows] = await Promise.all([
    safe('Günlük veri', () => report(from, to, ['date'], ['activeUsers', 'newUsers', 'sessions', 'averageSessionDuration', 'userEngagementDuration'], {}, onThreshold)),
    safe('MAU', () => report(addDays(to, -29), to, [], ['activeUsers'], {}, onThreshold)),
    safe('Ülkeler', () => report(from, to, ['countryId'], ['activeUsers', 'newUsers', 'userEngagementDuration'], {}, onThreshold)),
    safe('Platform', () => report(from, to, ['platform'], ['activeUsers'], {}, onThreshold)),
    safe('Olaylar', () => report(from, to, ['eventName'], ['eventCount'], {}, onThreshold)),
    safe('Level verisi', () => report(from, to, ['customEvent:level_id', 'eventName'], ['eventCount', 'totalUsers', 'customEvent:seconds'], {
      dimensionFilter: { filter: { fieldName: 'eventName', inListFilter: { values: Object.keys(LEVEL_EVENTS) } } },
    }, onThreshold)),
  ]);

  const byDate = new Map((dailyRows ?? []).map((r) => [`${r.date.slice(0, 4)}-${r.date.slice(4, 6)}-${r.date.slice(6, 8)}`, r]));
  const daily = enumerateDays(from, to).map((date) => {
    const r = byDate.get(date);
    return {
      date,
      newUsers: r?.newUsers ?? null,
      dau: r?.activeUsers ?? null,
      sessions: r?.sessions ?? null,
      avgSessionSec: r?.averageSessionDuration ?? null,
      playtimeSec: r && r.activeUsers > 0 ? r.userEngagementDuration / r.activeUsers : null,
    };
  });

  // Retention: kurulum gününe göre kohortlar; N günü dolmamış kohortlar sayılmaz, ortalama kohort büyüklüğüyle ağırlıklı
  const cohortDates = enumerateDays(from, to).slice(-MAX_COHORTS);
  const cohorts = await safe('Retention', () => fetchRetention(cohortDates, lastDay, onThreshold));
  const retention = [];
  for (const day of RETENTION_DAYS) {
    let active = 0, total = 0;
    for (const values of (cohorts ?? new Map()).values()) {
      const v = values.get(day);
      if (v) { active += v.active; total += v.total; }
    }
    if (total > 0) retention.push({ day, pct: (active / total) * 100 });
  }
  const trendOf = (date, day) => {
    const v = cohorts?.get(date)?.get(day);
    return v ? (v.active / v.total) * 100 : null;
  };

  if (thresholded) errors.push('Google az kullanıcılı bazı satırları gizliyor olabilir');

  return {
    daily,
    mau: mauRows?.[0]?.activeUsers ?? null,
    retention,
    retentionTrend: cohortDates.map((date) => ({ date, d1: trendOf(date, 1), d7: trendOf(date, 7) })),
    countries: (countryRows ?? []).map((r) => ({
      code: r.countryId,
      users: r.activeUsers,
      newUsers: r.newUsers,
      playtimeSec: r.activeUsers > 0 ? r.userEngagementDuration / r.activeUsers : null,
      d1: null,
    })).sort((a, b) => b.users - a.users),
    platforms: (platformRows ?? []).map((r) => ({ name: r.platform, users: r.activeUsers })).sort((a, b) => b.users - a.users),
    categories: (eventRows ?? []).map((r) => ({ id: r.eventName, count: r.eventCount })).sort((a, b) => b.count - a.count),
    cumulativePlaytime: [],
    levels: levelRows ? levelsFrom(levelRows) : [],
    design: [],
    errors,
    allFailed: attempted > 0 && errors.length >= attempted,
  };
}
