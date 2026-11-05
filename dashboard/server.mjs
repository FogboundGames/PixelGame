// LandFlow Analiz Paneli sunucusu. Firebase, Meta ya da GameAnalytics PipelineIQ API'si bağlanacaksa gerekir (Node 18+).
// CSV ile kullanım için buna gerek yok: panel.html dosyasına çift tıklamak yeter.
// Çalıştırma:  node dashboard/server.mjs   →  tarayıcıda açılır (kapatmak için --no-open)
// API anahtarları yalnızca bu sunucuda (.env) durur; tarayıcıya gönderilmez. Yalnızca bu bilgisayardan erişilir.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEnv, env } from './lib/env.mjs';
import { fetchMeta, metaConfigured } from './lib/meta.mjs';
import { fetchGA, gaConfigured, gaCheck } from './lib/gameanalytics.mjs';
import { fetchFirebase, firebaseConfigured, firebaseCheck } from './lib/firebase.mjs';
import { readUpdateConfig, publishUpdateConfig, remoteConfigConfigured } from './lib/remoteconfig.mjs';

const ROOT = dirname(fileURLToPath(import.meta.url));
loadEnv(join(ROOT, '.env'));

const PORT = Number(env('PORT', '5173'));
const HOST = '127.0.0.1';
const CACHE_MS = Number(env('CACHE_MINUTES', '10')) * 60_000;

const cache = new Map();

const today = () => new Date().toISOString().slice(0, 10);
const isDate = (s) => /^\d{4}-\d{2}-\d{2}$/.test(s || '');

/** Hata sonuçları önbelleğe alınmaz; düzeltip yenileyince hemen denensin. */
async function cached(key, force, fn) {
  const hit = cache.get(key);
  if (!force && hit && Date.now() - hit.at < CACHE_MS) return hit.value;
  const value = await fn();
  if (!value.error && !value.errors?.length) cache.set(key, { at: Date.now(), value });
  return value;
}

async function loadSources(from, to, force) {
  let meta = null, metaState = { status: 'off', message: '.env içinde META_ACCESS_TOKEN ve META_AD_ACCOUNT_ID yok' };
  if (metaConfigured()) {
    const r = await cached(`meta_${from}_${to}`, force, () => fetchMeta(from, to).catch((e) => ({ error: e.message })));
    if (r.error) metaState = { status: 'error', message: r.error };
    else { meta = r; metaState = { status: 'live' }; }
  }

  let ga = null, gaState = { status: 'off', message: '.env içinde GA_API_KEY ve GA_GAME_ID yok' };
  if (gaConfigured()) {
    const r = await cached(`ga_${from}_${to}`, force, () => fetchGA(from, to, today()).catch((e) => ({ errors: [e.message], allFailed: true })));
    if (r.allFailed) gaState = { status: 'error', message: r.errors[0] };
    else { ga = r; gaState = r.errors.length ? { status: 'partial', message: r.errors.join(' · ') } : { status: 'live' }; }
  }

  let firebase = null, firebaseState = { status: 'off', message: '.env içinde FIREBASE_KEY_FILE ve FIREBASE_PROPERTY_ID yok' };
  if (firebaseConfigured()) {
    const r = await cached(`firebase_${from}_${to}`, force, () => fetchFirebase(from, to, today()).catch((e) => ({ errors: [e.message], allFailed: true })));
    if (r.allFailed) firebaseState = { status: 'error', message: r.errors[0] };
    else { firebase = r; firebaseState = r.errors.length ? { status: 'partial', message: r.errors.join(' · ') } : { status: 'live' }; }
  }
  return { meta, ga, firebase, metaState, gaState, firebaseState };
}

/** Yayın isteği yalnızca panelin kendi sayfasından gelebilir: başka bir site tarayıcı üzerinden buraya yazamasın. */
function isPanelRequest(req) {
  const origin = req.headers.origin;
  const sameOrigin = origin === `http://localhost:${PORT}` || origin === `http://127.0.0.1:${PORT}`;
  return sameOrigin && (req.headers['content-type'] || '').startsWith('application/json');
}

async function readJsonBody(req) {
  let raw = '';
  for await (const chunk of req) {
    raw += chunk;
    if (raw.length > 10_000) throw new Error('İstek çok büyük');
  }
  return JSON.parse(raw || '{}');
}

function sendJson(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(body));
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://${HOST}:${PORT}`);
  try {
    if (url.pathname === '/api/sources') {
      const from = url.searchParams.get('from');
      const to = url.searchParams.get('to');
      if (!isDate(from) || !isDate(to) || from > to) return sendJson(res, 400, { error: 'Geçersiz tarih aralığı' });
      return sendJson(res, 200, await loadSources(from, to, url.searchParams.has('refresh')));
    }
    // Anahtar ve oyun kimliği doğru mu: API'nin gördüğü oyunlar ve desteklediği metrikler
    if (url.pathname === '/api/ga/check') {
      if (!gaConfigured()) return sendJson(res, 400, { error: 'GA_API_KEY ve GA_GAME_ID gerekli' });
      return sendJson(res, 200, await gaCheck());
    }
    // Firebase anahtarı doğru mu: son 30 dakikanın olayları ve tanımlı level alanları
    if (url.pathname === '/api/firebase/check') {
      if (!firebaseConfigured()) return sendJson(res, 400, { error: 'FIREBASE_KEY_FILE ve FIREBASE_PROPERTY_ID gerekli' });
      return sendJson(res, 200, await firebaseCheck());
    }
    // Oyundaki güncelleme bildirimi: Firebase Remote Config'teki update_* parametreleri
    if (url.pathname === '/api/update-config') {
      if (!remoteConfigConfigured()) return sendJson(res, 400, { error: '.env içinde FIREBASE_KEY_FILE yok' });
      if (req.method === 'GET') return sendJson(res, 200, await readUpdateConfig());
      if (req.method !== 'POST') return sendJson(res, 405, { error: 'Yalnızca GET ve POST' });
      if (!isPanelRequest(req)) return sendJson(res, 403, { error: 'Yalnızca panel sayfasından yayınlanabilir' });
      try {
        return sendJson(res, 200, await publishUpdateConfig(await readJsonBody(req)));
      } catch (e) {
        return sendJson(res, 400, { error: e.message });
      }
    }
    if (url.pathname === '/data/csv-paket.js') {
      res.writeHead(200, { 'Content-Type': 'text/javascript; charset=utf-8', 'Cache-Control': 'no-cache' });
      return res.end(await readFile(join(ROOT, 'data/csv-paket.js')).catch(() => ''));
    }
    if (url.pathname !== '/') { res.writeHead(404); return res.end('Bulunamadı'); }
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-cache' });
    res.end(await readFile(join(ROOT, 'panel.html')));
  } catch (e) {
    sendJson(res, 500, { error: e.message });
  }
});

function openBrowser(address) {
  if (process.argv.includes('--no-open')) return;
  const [cmd, args] = process.platform === 'win32' ? ['cmd', ['/c', 'start', '', address]]
    : process.platform === 'darwin' ? ['open', [address]] : ['xdg-open', [address]];
  const child = spawn(cmd, args, { stdio: 'ignore', detached: true });
  child.on('error', () => console.log(`Tarayıcı açılamadı, adresi elle açın: ${address}`));
  child.unref();
}

const address = `http://localhost:${PORT}`;
server.on('error', (e) => {
  if (e.code !== 'EADDRINUSE') throw e;
  console.log(`Port ${PORT} dolu; panel zaten açık olabilir: ${address}`);
  openBrowser(address);
});
server.listen(PORT, HOST, () => {
  console.log(`LandFlow Analiz Paneli → ${address}  (kapatmak için Ctrl+C)`);
  console.log(`  Meta: ${metaConfigured() ? 'bağlı' : 'anahtar yok'}  ·  GameAnalytics: ${gaConfigured() ? 'bağlı' : 'anahtar yok'}  ·  Firebase: ${firebaseConfigured() ? 'bağlı' : 'anahtar yok'}`);
  openBrowser(address);
});
