// dashboard/data/*.csv dosyalarını panelin kendiliğinden yüklediği data/csv-paket.js dosyasına paketler.
// Çalıştırma:  node dashboard/csv-paket.mjs   Sonra commit edip push edince panel herkeste aynı CSV'leri gösterir.
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const DATA = join(dirname(fileURLToPath(import.meta.url)), 'data');

const files = readdirSync(DATA).filter((name) => name.endsWith('.csv')).sort().map((name) => ({
  name,
  text: readFileSync(join(DATA, name), 'utf8'),
  at: Math.round(statSync(join(DATA, name)).mtimeMs),
}));

writeFileSync(join(DATA, 'csv-paket.js'), `// node dashboard/csv-paket.mjs üretir, elle düzenlemeyin.\nwindow.LANDFLOW_CSV = ${JSON.stringify(files)};\n`);
console.log(`${files.length} CSV paketlendi: dashboard/data/csv-paket.js`);
