import { env, fetchSafe } from './env.mjs';

// GameAnalytics Metrics API (Pro abonelik gerekir). Anahtar: GA panel → Organization settings → Metrics API.
// Uç nokta: POST https://metrics.gameanalytics.com/metrics/v1/metrics/{metrik}
// Metrik adları ve bölme (split) alanları .env'den değiştirilebilir; yanıt biçimi esnek okunur.
// Uyuşmazlık olursa /api/ga/metrics ve /api/ga/raw?metric=... ile ham yanıta bakın (README).

const BASE = 'https://metrics.gameanalytics.com/metrics/v1';

export function gaConfigured() {
  return Boolean(env('GA_API_KEY') && env('GA_GAME_ID'));
}

const metricName = (key, fallback) => env(`GA_METRIC_${key}`, fallback);

async function gaRequest(path, body) {
  const res = await fetchSafe(`${BASE}${path}`, {
    method: body ? 'POST' : 'GET',
    headers: { 'X-API-Key': env('GA_API_KEY'), 'Content-Type': 'application/json', Accept: 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json;
  try { json = JSON.parse(text); } catch { json = { raw: text }; }
  if (!res.ok) {
    const msg = json?.message || json?.error || json?.detail || text.slice(0, 200) || `HTTP ${res.status}`;
    throw new Error(`GameAnalytics ${path}: ${typeof msg === 'string' ? msg : JSON.stringify(msg)}`);
  }
  return json;
}

export function gaListMetrics() {
  return gaRequest('/metrics');
}

export function gaQuery(metric, from, to, { split, granularity = 'day', extra } = {}) {
  const body = {
    game_ids: [Number(env('GA_GAME_ID'))],
    interval: `${from}T00:00:00.000Z/${addDays(to, 1)}T00:00:00.000Z`,
    granularity,
    ...(split?.length ? { split } : {}),
    ...(extra || {}),
  };
  return gaRequest(`/metrics/${metric}`, body);
}

// ---------- yanıtı esnek okuma ----------

function findRows(json) {
  if (Array.isArray(json)) return json;
  for (const key of ['result', 'results', 'data', 'rows', 'values', 'timeseries', 'items']) {
    const v = json?.[key];
    if (Array.isArray(v)) return v;
    if (v && typeof v === 'object') {
      const inner = findRows(v);
      if (inner.length) return inner;
    }
  }
  return [];
}

function rowDate(row) {
  const v = row.date ?? row.day ?? row.timestamp ?? row.time ?? row.ts ?? row.interval ?? row.period;
  if (v === undefined || v === null) return null;
  if (typeof v === 'number') return new Date(v > 1e12 ? v : v * 1000).toISOString().slice(0, 10);
  const s = String(v);
  return s.slice(0, 10);
}

function rowValue(row, preferred = []) {
  for (const k of [...preferred, 'value', 'count', 'sum', 'avg', 'mean', 'average', 'result', 'total']) {
    const v = row[k];
    if (typeof v === 'number') return v;
    if (typeof v === 'string' && v !== '' && !Number.isNaN(Number(v))) return Number(v);
    if (v && typeof v === 'object' && typeof v.value === 'number') return v.value;
  }
  return 0;
}

function rowDim(row, dim) {
  return row[dim] ?? row.dimensions?.[dim] ?? row.split?.[dim] ?? row.group?.[dim] ?? row.groups?.[dim];
}

function toDaily(json) {
  const map = new Map();
  for (const row of findRows(json)) {
    const d = rowDate(row);
    if (d) map.set(d, (map.get(d) || 0) + rowValue(row));
  }
  return map;
}

// ---------- veri toplama ----------

async function safe(label, fn, errors) {
  try { return await fn(); } catch (e) { errors.push(`${label}: ${e.message}`); return null; }
}

export async function fetchGA(from, to) {
  const errors = [];
  const [newUsers, dau, sessions, sessionLen, retention, progression, design] = await Promise.all([
    safe('Yeni oyuncu', () => gaQuery(metricName('NEW_USERS', 'new_users'), from, to), errors),
    safe('DAU', () => gaQuery(metricName('DAU', 'user_unique'), from, to), errors),
    safe('Oturum sayısı', () => gaQuery(metricName('SESSIONS', 'session_count'), from, to), errors),
    safe('Oturum süresi', () => gaQuery(metricName('SESSION_LENGTH', 'session_length'), from, to), errors),
    safe('Retention', () => gaQuery(metricName('RETENTION', 'retention_retro'), from, to, {
      split: env('GA_RETENTION_SPLIT', 'retention_day').split(',').filter(Boolean),
      granularity: 'all',
    }), errors),
    safe('Level ilerleme', () => gaQuery(metricName('PROGRESSION', 'progression_count'), from, to, {
      split: env('GA_PROGRESSION_SPLIT', 'progression01,progression02,progression_status').split(',').filter(Boolean),
      granularity: 'all',
    }), errors),
    safe('Tasarım olayları', () => gaQuery(metricName('DESIGN', 'design_count'), from, to, {
      split: env('GA_DESIGN_SPLIT', 'event_id').split(',').filter(Boolean),
      granularity: 'all',
    }), errors),
  ]);

  const dates = enumerateDays(from, to);
  const nu = newUsers ? toDaily(newUsers) : new Map();
  const du = dau ? toDaily(dau) : new Map();
  const se = sessions ? toDaily(sessions) : new Map();
  const sl = sessionLen ? toDaily(sessionLen) : new Map();

  const daily = dates.map((date) => {
    const users = du.get(date) ?? null;
    const sess = se.get(date) ?? null;
    const avgSessionSec = sl.get(date) ?? null;
    return {
      date,
      newUsers: nu.get(date) ?? null,
      dau: users,
      sessions: sess,
      avgSessionSec,
      // Oyuncu başına günlük oynama = oturum sayısı × ort. oturum süresi / DAU
      playtimeSec: users && sess && avgSessionSec ? (sess * avgSessionSec) / users : null,
    };
  });

  return {
    daily,
    retention: retention ? parseRetention(retention) : [],
    levels: progression ? parseProgression(progression) : [],
    design: design ? parseDesign(design) : [],
    errors,
  };
}

function parseRetention(json) {
  const out = new Map();
  const dim = env('GA_RETENTION_SPLIT', 'retention_day').split(',')[0];
  for (const row of findRows(json)) {
    let day = rowDim(row, dim) ?? row.day_n ?? row.n ?? row.retention_day;
    if (day === undefined) continue;
    day = Number(String(day).replace(/\D/g, ''));
    if (!Number.isFinite(day) || day <= 0) continue;
    let v = rowValue(row, ['retention', 'rate', 'percentage']);
    if (v <= 1) v *= 100; // oran 0-1 geldiyse yüzdeye çevir
    out.set(day, v);
  }
  // Bazı yanıtlar düz nesne olarak gelebilir: { d1: 0.35, d7: 0.1 }
  if (!out.size && json && typeof json === 'object') {
    for (const [k, v] of Object.entries(json.result || json)) {
      const m = /^d(?:ay)?_?(\d+)$/i.exec(k);
      if (m && typeof v === 'number') out.set(Number(m[1]), v <= 1 ? v * 100 : v);
    }
  }
  return [...out.entries()].sort((a, b) => a[0] - b[0]).map(([day, pct]) => ({ day, pct }));
}

function parseProgression(json) {
  const [p1, p2, statusDim] = env('GA_PROGRESSION_SPLIT', 'progression01,progression02,progression_status').split(',');
  const levels = new Map();
  for (const row of findRows(json)) {
    const id = rowDim(row, p1) ?? row.progression ?? row.event_id?.split(':')[1];
    if (!id) continue;
    const status = String(rowDim(row, statusDim) ?? row.status ?? row.event_id?.split(':')[0] ?? '').toLowerCase();
    const lv = levels.get(id) || { id, name: '', starts: 0, completes: 0, fails: 0, avgCompleteSec: null, avgFailSec: null, players: null };
    const name = rowDim(row, p2);
    if (name && name !== 'undefined') lv.name = name;
    const count = rowValue(row, ['count', 'event_count']);
    const avgScore = typeof row.avg_score === 'number' ? row.avg_score : (typeof row.score === 'number' ? row.score : null);
    if (status.includes('start')) lv.starts += count;
    else if (status.includes('complete')) { lv.completes += count; if (avgScore !== null) lv.avgCompleteSec = avgScore; }
    else if (status.includes('fail')) { lv.fails += count; if (avgScore !== null) lv.avgFailSec = avgScore; }
    if (typeof row.users === 'number' && status.includes('start')) lv.players = row.users;
    levels.set(id, lv);
  }
  return sortLevels([...levels.values()]);
}

function parseDesign(json) {
  const dim = env('GA_DESIGN_SPLIT', 'event_id').split(',')[0];
  const out = new Map();
  for (const row of findRows(json)) {
    const id = rowDim(row, dim) ?? row.event_id;
    if (!id) continue;
    out.set(id, (out.get(id) || 0) + rowValue(row, ['count', 'event_count']));
  }
  return [...out.entries()].map(([id, count]) => ({ id, count })).sort((a, b) => b.count - a.count);
}

// ---------- yardımcılar ----------

export function sortLevels(levels) {
  const n = (id) => Number(String(id).replace(/\D/g, '')) || 0;
  return levels.sort((a, b) => n(a.id) - n(b.id));
}

export function addDays(date, days) {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

export function enumerateDays(from, to) {
  const out = [];
  for (let d = from; d <= to; d = addDays(d, 1)) out.push(d);
  return out;
}
