// LandFlow Analiz Paneli — bağımlılıksız Node sunucusu (Node 18+).
// Çalıştırma:  cd dashboard && node server.mjs   →  http://localhost:5173
// API anahtarları yalnızca bu sunucuda (.env) durur; tarayıcıya gönderilmez.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { existsSync, readFileSync } from 'node:fs';
import { join, extname, dirname, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEnv, env } from './lib/env.mjs';
import { fetchMeta, metaConfigured } from './lib/meta.mjs';
import { fetchGA, gaConfigured, gaListMetrics, gaQuery, addDays, sortLevels } from './lib/gameanalytics.mjs';
import { demoMeta, demoGA } from './lib/demo.mjs';

const ROOT = dirname(fileURLToPath(import.meta.url));
loadEnv(join(ROOT, '.env'));

const PORT = Number(env('PORT', '5173'));
const HOST = env('HOST', '127.0.0.1');
const CACHE_MS = Number(env('CACHE_MINUTES', '10')) * 60_000;
const PUBLIC = join(ROOT, 'public');
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml', '.json': 'application/json' };

const cache = new Map();

const today = () => new Date().toISOString().slice(0, 10);
const isDate = (s) => /^\d{4}-\d{2}-\d{2}$/.test(s || '');

async function buildDashboard(from, to, force) {
  const key = `${from}_${to}`;
  const hit = cache.get(key);
  if (!force && hit && Date.now() - hit.at < CACHE_MS) return hit.data;

  const sources = {};
  let meta;
  if (metaConfigured()) {
    try { meta = await fetchMeta(from, to); sources.meta = { status: 'live' }; }
    catch (e) { meta = demoMeta(from, to); sources.meta = { status: 'error', message: e.message }; }
  } else {
    meta = demoMeta(from, to);
    sources.meta = { status: 'demo', message: '.env içinde META_ACCESS_TOKEN ve META_AD_ACCOUNT_ID yok' };
  }

  let ga;
  if (gaConfigured()) {
    try {
      ga = await fetchGA(from, to);
      const allFailed = ga.errors.length >= 7;
      sources.ga = { status: allFailed ? 'error' : 'live', message: ga.errors.join(' · ') || undefined };
      if (allFailed) ga = demoGA(from, to, meta.daily);
    } catch (e) { ga = demoGA(from, to, meta.daily); sources.ga = { status: 'error', message: e.message }; }
  } else {
    ga = demoGA(from, to, meta.daily);
    sources.ga = { status: 'demo', message: '.env içinde GA_API_KEY ve GA_GAME_ID yok' };
  }

  // Level verisi için elle dışa aktarılmış CSV her zaman önceliklidir (README → "Level CSV")
  const csvPath = join(ROOT, 'data', 'levels.csv');
  if (existsSync(csvPath)) {
    try { ga.levels = readLevelsCsv(csvPath); sources.levels = { status: 'csv', message: 'data/levels.csv' }; }
    catch (e) { sources.levels = { status: 'error', message: `levels.csv okunamadı: ${e.message}` }; }
  }

  // Meta günlük verisini GA tarihleriyle birleştir
  const metaByDate = new Map(meta.daily.map((d) => [d.date, d]));
  const daily = ga.daily.map((g) => {
    const m = metaByDate.get(g.date) || {};
    return { ...g, spend: m.spend ?? null, impressions: m.impressions ?? null, reach: m.reach ?? null, clicks: m.clicks ?? null, metaInstalls: m.installs ?? null };
  });

  const data = {
    generatedAt: new Date().toISOString(),
    range: { from, to },
    currency: meta.currency,
    sources,
    daily,
    metaTotals: meta.totals,
    campaigns: meta.campaigns,
    retention: ga.retention,
    levels: ga.levels,
    design: ga.design,
  };
  cache.set(key, { at: Date.now(), data });
  return data;
}

function readLevelsCsv(path) {
  const lines = readFileSync(path, 'utf8').split(/\r?\n/).filter((l) => l.trim());
  const sep = lines[0].includes(';') ? ';' : ',';
  const head = lines[0].split(sep).map((h) => h.trim().toLowerCase());
  const col = (row, ...names) => {
    for (const n of names) { const i = head.indexOf(n); if (i >= 0 && row[i] !== undefined && row[i] !== '') return row[i].trim(); }
    return undefined;
  };
  const numOrNull = (v) => (v === undefined ? null : Number(String(v).replace(',', '.')));
  return sortLevels(lines.slice(1).map((l) => {
    const row = l.split(sep);
    return {
      id: col(row, 'id', 'level', 'progression01'),
      name: col(row, 'name', 'ad', 'progression02') || '',
      starts: numOrNull(col(row, 'starts', 'start')) || 0,
      completes: numOrNull(col(row, 'completes', 'complete')) || 0,
      fails: numOrNull(col(row, 'fails', 'fail')) || 0,
      avgCompleteSec: numOrNull(col(row, 'avg_complete_sec')),
      avgFailSec: numOrNull(col(row, 'avg_fail_sec')),
      players: numOrNull(col(row, 'players', 'users')),
    };
  }).filter((l) => l.id));
}

function sendJson(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(body));
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  try {
    if (url.pathname === '/api/dashboard') {
      const to = isDate(url.searchParams.get('to')) ? url.searchParams.get('to') : addDays(today(), -1);
      const days = Math.min(Math.max(Number(url.searchParams.get('days')) || 30, 1), 365);
      const from = isDate(url.searchParams.get('from')) ? url.searchParams.get('from') : addDays(to, -(days - 1));
      if (from > to) return sendJson(res, 400, { error: 'Başlangıç tarihi bitişten sonra olamaz' });
      return sendJson(res, 200, await buildDashboard(from, to, url.searchParams.has('refresh')));
    }
    // Hata ayıklama: GA'nın desteklediği metrikler ve tek bir metriğin ham yanıtı
    if (url.pathname === '/api/ga/metrics') {
      if (!gaConfigured()) return sendJson(res, 400, { error: 'GA_API_KEY ve GA_GAME_ID gerekli' });
      return sendJson(res, 200, await gaListMetrics());
    }
    if (url.pathname === '/api/ga/raw') {
      if (!gaConfigured()) return sendJson(res, 400, { error: 'GA_API_KEY ve GA_GAME_ID gerekli' });
      const to = addDays(today(), -1);
      const from = addDays(to, -6);
      const split = (url.searchParams.get('split') || '').split(',').filter(Boolean);
      return sendJson(res, 200, await gaQuery(url.searchParams.get('metric') || 'new_users', from, to, {
        split, granularity: url.searchParams.get('granularity') || 'day',
      }));
    }

    const rel = url.pathname === '/' ? 'index.html' : decodeURIComponent(url.pathname).replace(/^\/+/, '');
    const file = normalize(join(PUBLIC, rel));
    if (!file.startsWith(PUBLIC) || !existsSync(file)) { res.writeHead(404); return res.end('Bulunamadı'); }
    res.writeHead(200, { 'Content-Type': MIME[extname(file)] || 'application/octet-stream' });
    res.end(await readFile(file));
  } catch (e) {
    sendJson(res, 500, { error: e.message });
  }
});

server.listen(PORT, HOST, () => {
  console.log(`LandFlow Analiz Paneli → http://${HOST === '0.0.0.0' ? 'localhost' : HOST}:${PORT}`);
  console.log(`  Meta: ${metaConfigured() ? 'bağlı' : 'örnek veri'}  ·  GameAnalytics: ${gaConfigured() ? 'bağlı' : 'örnek veri'}`);
});
