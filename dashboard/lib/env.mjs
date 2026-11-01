import { readFileSync, existsSync } from 'node:fs';

/** Bağımlılıksız .env okuyucu. Ortamda zaten tanımlı değişkenleri ezmez. */
export function loadEnv(path) {
  if (!existsSync(path)) return;
  for (const raw of readFileSync(path, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq < 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (process.env[key] === undefined) process.env[key] = value;
  }
}

export const env = (key, fallback = '') => (process.env[key] ?? '').trim() || fallback;

/** fetch sarmalayıcı: ağ hatalarını anlaşılır Türkçe mesaja çevirir. */
export async function fetchSafe(url, init) {
  try {
    return await fetch(url, init);
  } catch (e) {
    const host = new URL(url).host;
    throw new Error(`${host} adresine bağlanılamadı (internet bağlantısı / güvenlik duvarı?): ${e.cause?.code || e.message}`);
  }
}
