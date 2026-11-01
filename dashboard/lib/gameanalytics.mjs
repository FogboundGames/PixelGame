import { env, fetchSafe } from './env.mjs';

// GameAnalytics Metrics API (yalnızca PipelineIQ planında var). Şema: docs.gameanalytics.com/redocusaurus/metrics-api.yaml (v2.11.3)
// Bu API level (progression01) veya tasarım olayı kimliği (event_id) boyutu SUNMAZ; onlar data/*.csv dosyalarından gelir (README).

const BASE = () => env('GA_BASE_URL', 'https://metrics.gameanalytics.com/metrics/v1');
const MAX_DAY_SPAN = 31; // day granularity için API sınırı
const RETENTION_DAYS = [1, 3, 7, 14, 30];
const PLAYTIME_DAYS = [1, 3, 7, 14, 30];
const MIN_COHORTS = 7; // seçili dönemde N günü dolmuş kohort azsa geriye doğru bu kadarına tamamlanır

export function gaConfigured() {
  return Boolean(env('GA_API_KEY') && env('GA_GAME_ID'));
}

const gameId = () => String(env('GA_GAME_ID'));

// ---------- istek ----------

// API hız sınırı belgelenmemiş (429 dönebiliyor), aynı anda 4 istekle sınırlı
let active = 0;
const waiting = [];
async function limited(fn) {
  if (active >= 4) await new Promise((r) => waiting.push(r));
  active++;
  try { return await fn(); } finally { active--; waiting.shift()?.(); }
}

async function gaRequest(path, body, retried = false) {
  const res = await limited(() => fetchSafe(`${BASE()}${path}`, {
    method: body ? 'POST' : 'GET',
    headers: { 'X-API-Key': env('GA_API_KEY'), 'Content-Type': 'application/json', Accept: 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  }));
  if (res.status === 429 && !retried) {
    await new Promise((r) => setTimeout(r, 2000));
    return gaRequest(path, body, true);
  }
  const text = await res.text();
  let json;
  try { json = JSON.parse(text); } catch { json = null; }
  if (!res.ok) {
    if (res.status === 401 || res.status === 403) {
      throw new Error(`Anahtar reddedildi (HTTP ${res.status}). Anahtar yanlış olabilir ya da hesapta PipelineIQ planı yok.`);
    }
    throw new Error(json?.error?.message || `HTTP ${res.status}: ${text.slice(0, 160)}`);
  }
  return json ?? {};
}

export async function gaCheck() {
  const to = addDays(new Date().toISOString().slice(0, 10), -1);
  const games = await gaRequest(`/games?interval=${encodeURIComponent(interval(addDays(to, -29), to))}&limit=1000`);
  const list = games.games ?? games.result ?? games;
  return { gameId: gameId(), games: list, metrics: await gaRequest('/metrics') };
}

const interval = (from, to) => `${from}T00:00:00.000/${addDays(to, 1)}T00:00:00.000`;

function chunks(from, to) {
  const out = [];
  for (let start = from; start <= to; start = addDays(start, MAX_DAY_SPAN)) {
    const end = addDays(start, MAX_DAY_SPAN - 1);
    out.push([start, end < to ? end : to]);
  }
  return out;
}

/** Günlük zaman serisi: 31 günlük parçalar hâlinde ister, tarih → sonuç haritası döner. */
async function series(metric, from, to, body = { games: [gameId()], granularity: 'day', query: { type: 'timeseries' } }) {
  const parts = await Promise.all(chunks(from, to).map(([a, b]) =>
    gaRequest(`/metrics/${metric}`, { ...body, interval: interval(a, b) })));
  const map = new Map();
  for (const part of parts) for (const row of part.result ?? []) map.set(row.timestamp.slice(0, 10), row.result ?? {});
  return map;
}

/** Tek değer: seçilen aralığın tamamı için. */
async function total(metric, from, to) {
  const json = await gaRequest(`/metrics/${metric}`, { games: [gameId()], granularity: 'all', interval: interval(from, to), query: { type: 'timeseries' } });
  return json.result?.[0]?.result ?? {};
}

/** Bir boyuta göre gruplama: boyut değeri → satır. */
async function group(metric, from, to, dimension, limit = 100) {
  const json = await gaRequest(`/metrics/${metric}`, { games: [gameId()], granularity: 'all', interval: interval(from, to), query: { type: 'group', dimension, limit } });
  const map = new Map();
  for (const row of json.result?.[0]?.result ?? []) map.set(row[dimension], row);
  return map;
}

// ---------- veri toplama ----------

export async function fetchGA(from, to, today) {
  const errors = [];
  let attempted = 0;
  const safe = async (label, fn) => {
    attempted++;
    try { return await fn(); } catch (e) { errors.push(`${label}: ${e.message}`); return null; }
  };
  const lastDay = addDays(today, -1);
  const earlier = (a, b) => (a < b ? a : b);
  // Kısa dönemlerde de D30 görünsün diye kohort verisi dönemin gerisine uzanabilir
  const cohortStart = earlier(from, addDays(lastDay, -(Math.max(...RETENTION_DAYS) + MIN_COHORTS - 1)));

  const [newUsers, dau, sessions, sessionLen, perUser, mau, countries, countryNew, countryPlay, platforms, categories] = await Promise.all([
    safe('Yeni oyuncu', () => series('new_users', cohortStart, to)),
    safe('DAU', () => series('user_unique', from, to)),
    safe('Oturum sayısı', () => series('session_unique', from, to)),
    safe('Oturum süresi', () => series('session_length', from, to)),
    safe('Oynama süresi', () => series('session_length_per_user', from, to)),
    safe('MAU', () => total('user_unique', addDays(to, -29), to)),
    safe('Ülkeler', () => group('user_unique', from, to, 'country_code')),
    safe('Ülke yeni oyuncu', () => group('new_users', from, to, 'country_code')),
    safe('Ülke oynama süresi', () => group('session_length_per_user', from, to, 'country_code')),
    safe('Platform', () => group('user_unique', from, to, 'platform')),
    safe('Olay kategorileri', () => group('event_count', from, to, 'category')),
  ]);

  const daily = enumerateDays(from, to).map((date) => ({
    date,
    newUsers: newUsers?.get(date)?.new_users ?? null,
    dau: dau?.get(date)?.user_unique ?? null,
    sessions: sessions?.get(date)?.session_unique ?? null,
    avgSessionSec: sessionLen?.get(date)?.mean ?? null,
    playtimeSec: perUser?.get(date)?.session_length_per_user ?? null,
  }));

  // Retention: kohortlar kurulum gününe göre; N gün dolmamış kohortlar istenmez, ortalama yeni oyuncu sayısıyla ağırlıklı
  const weightOf = (date) => newUsers?.get(date)?.new_users ?? 1;
  const retentionByDay = await Promise.all(RETENTION_DAYS.map(async (n) => {
    const end = earlier(addDays(lastDay, -n), to);
    const start = earlier(from, addDays(end, -(MIN_COHORTS - 1)));
    const map = await safe(`Retention D${n}`, () => series('retention_ahead', start, end, { game: gameId(), days_ahead: n, query: { type: 'timeseries' } }));
    return map && { n, map };
  }));
  const retention = [];
  for (const r of retentionByDay.filter(Boolean)) {
    let sum = 0, weight = 0;
    for (const [date, row] of r.map) {
      if (!Number.isFinite(row.retention)) continue;
      sum += row.retention * weightOf(date);
      weight += weightOf(date);
    }
    if (weight > 0) retention.push({ day: r.n, pct: (sum / weight) * 100 });
  }
  const trendOf = (n) => retentionByDay.find((r) => r?.n === n)?.map;
  const retentionTrend = enumerateDays(from, to).map((date) => ({
    date,
    d1: pctOrNull(trendOf(1)?.get(date)?.retention),
    d7: pctOrNull(trendOf(7)?.get(date)?.retention),
  }));

  // Ülke bazında D1: son en çok 14 kohortun düz ortalaması (kohort büyüklüğü ülke bazında alınmıyor)
  const d1End = earlier(addDays(lastDay, -1), to);
  const d1Start = addDays(d1End, -13) > from ? addDays(d1End, -13) : earlier(from, d1End);
  const countryD1 = d1End >= from ? await safe('Ülke retention', async () => {
    const json = await gaRequest('/metrics/retention_ahead', { game: gameId(), days_ahead: 1, interval: interval(d1Start, d1End), query: { type: 'group', dimension: 'country_code', limit: 100 } });
    const acc = new Map();
    for (const day of json.result ?? []) for (const row of day.result ?? []) {
      if (!Number.isFinite(row.retention)) continue;
      const a = acc.get(row.country_code) ?? { sum: 0, n: 0 };
      a.sum += row.retention; a.n++;
      acc.set(row.country_code, a);
    }
    return new Map([...acc].map(([code, a]) => [code, (a.sum / a.n) * 100]));
  }) : null;

  // Kümülatif oynama: yalnızca N günü dolmuş kohortlar, gün 0 büyüklüğüyle ağırlıklı
  const playtime = await safe('Kümülatif oynama', () => series('cumulative_playtime_per_user', cohortStart, to, { game: gameId(), days_since_install: PLAYTIME_DAYS, query: { type: 'timeseries' } }));
  const cumulativePlaytime = [];
  if (playtime) {
    for (const n of [0, ...PLAYTIME_DAYS]) {
      let sum = 0, weight = 0;
      for (const [date, row] of playtime) {
        // Dönem içindeki kohortlar esas; N günü dolmamışsa yerine önceki kohortlar girer
        if (addDays(date, n) > lastDay || (date < from && addDays(from, n) <= lastDay)) continue;
        const days = row.days_since_install ?? [];
        const point = days.find((d) => Number(d.day) === n);
        if (!point || !Number.isFinite(point.value)) continue;
        const size = days.find((d) => Number(d.day) === 0)?.total_unique_users ?? 1;
        sum += point.value * size; weight += size;
      }
      if (weight > 0) cumulativePlaytime.push({ day: n, sec: sum / weight });
    }
  }

  return {
    daily,
    mau: mau?.user_unique ?? null,
    retention,
    retentionTrend,
    countries: countries ? [...countries.values()].map((row) => ({
      code: row.country_code,
      users: row.user_unique,
      newUsers: countryNew?.get(row.country_code)?.new_users ?? null,
      playtimeSec: countryPlay?.get(row.country_code)?.session_length_per_user ?? null,
      d1: countryD1?.get(row.country_code) ?? null,
    })).sort((a, b) => b.users - a.users) : [],
    platforms: platforms ? [...platforms.values()].map((row) => ({ name: row.platform, users: row.user_unique })).sort((a, b) => b.users - a.users) : [],
    categories: categories ? [...categories.values()].map((row) => ({ id: row.category, count: row.event_count })).sort((a, b) => b.count - a.count) : [],
    cumulativePlaytime,
    errors,
    allFailed: attempted > 0 && errors.length >= attempted,
  };
}

const pctOrNull = (v) => (Number.isFinite(v) ? v * 100 : null);

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
