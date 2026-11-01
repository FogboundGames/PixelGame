import { lineChart, barChart } from './charts.js';

// ---------- biçimlendirme ----------
const nf = new Intl.NumberFormat('tr-TR');
const nf1 = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 });
const nfc = new Intl.NumberFormat('tr-TR', { notation: 'compact', maximumFractionDigits: 1 });
const ok = (v) => v !== null && v !== undefined && Number.isFinite(v);
const int = (v) => (ok(v) ? nf.format(Math.round(v)) : '—');
const pct = (v, d = 1) => (ok(v) ? `%${v.toFixed(d).replace('.', ',')}` : '—');
const mins = (sec) => (ok(sec) ? `${nf1.format(sec / 60)} dk` : '—');
const secs = (s) => (ok(s) ? (s >= 60 ? `${Math.floor(s / 60)} dk ${Math.round(s % 60)} sn` : `${Math.round(s)} sn`) : '—');
let currency = 'USD';
const money = (v, d = 2) => (ok(v) ? new Intl.NumberFormat('tr-TR', { style: 'currency', currency, maximumFractionDigits: d }).format(v) : '—');
const dayLabel = (iso, long) => {
  const d = new Date(`${iso}T00:00:00Z`);
  return d.toLocaleDateString('tr-TR', long ? { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' } : { day: 'numeric', month: 'short', timeZone: 'UTC' });
};
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const css = (name) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const sum = (arr, k) => arr.reduce((a, d) => a + (ok(d[k]) ? d[k] : 0), 0);
const avg = (arr, k) => { const v = arr.map((d) => d[k]).filter(ok); return v.length ? v.reduce((a, b) => a + b, 0) / v.length : null; };
const div = (a, b) => (ok(a) && ok(b) && b > 0 ? a / b : null);

// ---------- durum ----------
let days = 30;
let data = null;

async function load(refresh = false) {
  const btn = document.getElementById('refresh');
  btn.disabled = true; btn.textContent = 'Yükleniyor…';
  const box = document.getElementById('errorBox');
  try {
    const res = await fetch(`/api/dashboard?days=${days}${refresh ? '&refresh=1' : ''}`);
    const body = await res.json();
    if (!res.ok) throw new Error(body.error || `HTTP ${res.status}`);
    data = body;
    box.hidden = true;
    render();
  } catch (e) {
    box.hidden = false;
    box.textContent = `Veri alınamadı: ${e.message}. Sunucu çalışıyor mu? (cd dashboard && node server.mjs)`;
  } finally {
    btn.disabled = false; btn.textContent = 'Yenile';
  }
}

document.querySelectorAll('.seg button').forEach((b) => b.addEventListener('click', () => {
  document.querySelectorAll('.seg button').forEach((x) => x.classList.toggle('on', x === b));
  days = Number(b.dataset.days);
  load();
}));
document.getElementById('refresh').addEventListener('click', () => load(true));
let resizeTimer;
window.addEventListener('resize', () => { clearTimeout(resizeTimer); resizeTimer = setTimeout(() => data && renderCharts(), 150); });
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => data && renderCharts());

// ---------- türetilmiş metrikler ----------
function derive() {
  const d = data.daily;
  const mt = data.metaTotals || {};
  const newUsers = sum(d, 'newUsers');
  const metaInstalls = mt.installs ?? sum(d, 'metaInstalls');
  const ret = (n) => data.retention.find((r) => r.day === n)?.pct ?? null;
  const levels = deriveLevels(data.levels, newUsers);
  return {
    spend: mt.spend, impressions: mt.impressions, reach: mt.reach, clicks: mt.clicks, metaInstalls, newUsers,
    ctr: div(mt.clicks, mt.impressions) !== null ? div(mt.clicks, mt.impressions) * 100 : null,
    cpi: div(mt.spend, metaInstalls),
    cpm: div(mt.spend, mt.impressions) !== null ? div(mt.spend, mt.impressions) * 1000 : null,
    freq: div(mt.impressions, mt.reach),
    installRate: div(metaInstalls, mt.clicks) !== null ? div(metaInstalls, mt.clicks) * 100 : null,
    organic: ok(newUsers) && newUsers > 0 ? Math.max(0, newUsers - metaInstalls) : null,
    dau: avg(d, 'dau'),
    sessionsPerUser: div(sum(d, 'sessions'), sum(d, 'dau')),
    avgSession: avg(d, 'avgSessionSec'),
    playtime: avg(d, 'playtimeSec'),
    d1: ret(1), d3: ret(3), d7: ret(7), d14: ret(14), d30: ret(30),
    levels,
  };
}

function deriveLevels(levels, newUsers) {
  const out = [];
  let base = null;
  levels.forEach((l, i) => {
    const prev = out[i - 1];
    // Levele ulaşan oyuncu: varsa GA'nın tekil oyuncu sayısı, yoksa önceki leveli bitirenler (1. level için yeni oyuncular)
    const reached = ok(l.players) ? l.players : (i === 0 ? (newUsers > 0 ? newUsers : l.starts) : prev.completes);
    if (i === 0) base = reached;
    const winRate = div(l.completes, l.starts);
    const lv = {
      ...l,
      reached,
      funnel: div(reached, base) !== null ? div(reached, base) * 100 : null,
      winRate: winRate !== null ? winRate * 100 : null,
      attempts: div(l.starts, l.completes),
      drop: div(reached - l.completes, reached) !== null ? Math.max(0, div(reached - l.completes, reached) * 100) : null,
    };
    lv.flags = [];
    if (ok(lv.winRate) && lv.winRate < 40) lv.flags.push(['bad', 'Tıkanma', `Başlayanların yalnızca ${pct(lv.winRate, 0)}'i bitiriyor`]);
    else if (ok(lv.winRate) && lv.winRate < 55) lv.flags.push(['warn', 'Zor', `Kazanma oranı ${pct(lv.winRate, 0)}`]);
    if (ok(lv.drop) && lv.drop >= 15) lv.flags.push(['bad', 'Kopma', `Ulaşanların ${pct(lv.drop, 0)}'i bu leveli bitirmeden bırakıyor`]);
    else if (ok(lv.drop) && lv.drop >= 8) lv.flags.push(['warn', 'Kopma', `Ulaşanların ${pct(lv.drop, 0)}'i bitirmiyor`]);
    out.push(lv);
  });
  return out;
}

// ---------- karne ----------
function grade(v, good, warn, higherIsBetter = true) {
  if (!ok(v)) return 'na';
  if (higherIsBetter) return v >= good ? 'good' : v >= warn ? 'warn' : 'bad';
  return v <= good ? 'good' : v <= warn ? 'warn' : 'bad';
}
const STATUS = { good: '✓ İyi', warn: '! Sınırda', bad: '✕ Zayıf', na: '— Veri yok' };

function renderVerdicts(m) {
  const lv10 = m.levels[9]?.funnel ?? null;
  const stuck = m.levels.filter((l) => l.flags.some((f) => f[0] === 'bad')).length;
  const items = [
    ['D1 retention', pct(m.d1), grade(m.d1, 40, 30), 'Ertesi gün geri gelen yeni oyuncu. Hedef ≥ %40, %30 altı oyunun ilk izlenimi zayıf demek.'],
    ['D7 retention', pct(m.d7), grade(m.d7, 15, 8), 'Bir hafta sonra hâlâ oynayan. Hedef ≥ %15; %8 altı uzun vadede tutmuyor.'],
    ['Günlük oynama süresi', mins(m.playtime), grade(ok(m.playtime) ? m.playtime / 60 : null, 15, 8), 'Aktif oyuncu başına günde. Hedef ≥ 15 dk; 8 dk altı içerik/akış sıkıcı olabilir.'],
    ['CPI (kurulum maliyeti)', money(m.cpi), grade(m.cpi, 0.5, 1.0, false), 'Meta reklamlarıyla bir kurulumun maliyeti. Casual için ≤ 0,5 iyi, 1’in üstü pahalı (ABD ölçeği).'],
    ['Reklam tıklanma (CTR)', pct(m.ctr, 2), grade(m.ctr, 1.5, 0.8), 'Görenlerin yüzde kaçı tıkladı. Kreatif gücünü gösterir; ≥ %1,5 iyi.'],
    ['10. levele ulaşan', pct(lv10, 0), grade(lv10, 50, 30), 'Yeni oyuncuların yüzde kaçı Level 10’a geliyor. ≥ %50 iyi bir erken oyun akışı.'],
    ['Tıkanma noktası', ok(m.levels.length) && m.levels.length ? `${stuck} level` : '—', m.levels.length ? (stuck === 0 ? 'good' : stuck <= 2 ? 'warn' : 'bad') : 'na', 'Kazanma oranı %40 altı veya oyuncuların %15+ bıraktığı level sayısı.'],
  ];
  document.getElementById('verdicts').innerHTML = items.map(([label, value, g, text]) => `
    <div class="verdict ${g}">
      <div class="head"><span>${label}</span><span class="status">${STATUS[g]}</span></div>
      <div class="value">${value}</div>
      <p>${text}</p>
    </div>`).join('');
}

function kpis(id, items) {
  document.getElementById(id).innerHTML = items.map(([label, value, sub]) => `
    <div class="kpi"><div class="label">${label}</div><div class="value">${value}</div>${sub ? `<div class="sub">${sub}</div>` : ''}</div>`).join('');
}

// ---------- tablolar ----------
function table(containerId, columns, rows, { sortKey, desc = true, rowTitle } = {}) {
  const host = document.getElementById(containerId);
  if (!rows.length) { host.innerHTML = '<p class="empty">Veri yok</p>'; return; }
  let key = sortKey, isDesc = desc;
  const draw = () => {
    const sorted = key ? [...rows].sort((a, b) => {
      const col = columns.find((c) => c.key === key);
      const va = col.sort ? col.sort(a) : a[key], vb = col.sort ? col.sort(b) : b[key];
      const cmp = typeof va === 'string' ? va.localeCompare(vb, 'tr') : (ok(va) ? va : -Infinity) - (ok(vb) ? vb : -Infinity);
      return isDesc ? -cmp : cmp;
    }) : rows;
    host.innerHTML = `<table><thead><tr>${columns.map((c) => `<th data-k="${c.key}" class="${c.left ? 'l' : ''} ${c.key === key ? `sorted ${isDesc ? '' : 'asc'}` : ''}" title="${esc(c.help || '')}">${c.label}</th>`).join('')}</tr></thead>
      <tbody>${sorted.map((r) => `<tr title="${esc(rowTitle ? rowTitle(r) : '')}">${columns.map((c) => `<td class="${c.left ? 'l' : ''} ${c.cls ? c.cls(r) : ''}">${c.fmt ? c.fmt(r) : esc(r[c.key])}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
    host.querySelectorAll('th').forEach((th) => th.addEventListener('click', () => {
      if (key === th.dataset.k) isDesc = !isDesc; else { key = th.dataset.k; isDesc = true; }
      draw();
    }));
  };
  draw();
}

function renderTables(m) {
  const levelNum = (l) => Number(String(l.id).replace(/\D/g, '')) || 0;
  table('levelTable', [
    { key: 'id', label: 'Level', left: true, sort: levelNum, fmt: (l) => `${esc(l.id)}${l.name ? ` <span class="muted">${esc(l.name)}</span>` : ''}` },
    { key: 'reached', label: 'Ulaşan', fmt: (l) => int(l.reached), help: 'Bu levele gelen oyuncu sayısı' },
    { key: 'funnel', label: 'Ulaşan %', fmt: (l) => pct(l.funnel, 0), help: 'Level 1’e başlayanlara göre' },
    { key: 'starts', label: 'Başlama', fmt: (l) => int(l.starts), help: 'Tekrar denemeler dahil' },
    { key: 'completes', label: 'Bitirme', fmt: (l) => int(l.completes) },
    { key: 'fails', label: 'Kayıp', fmt: (l) => int(l.fails), help: 'Slotlar dolup kaybedilen oyun' },
    { key: 'winRate', label: 'Kazanma %', fmt: (l) => pct(l.winRate, 0), cls: (l) => (l.winRate < 40 ? 'v-bad' : l.winRate < 55 ? 'v-warn' : '') },
    { key: 'attempts', label: 'Deneme / bitirme', fmt: (l) => (ok(l.attempts) ? nf1.format(l.attempts) : '—'), cls: (l) => (l.attempts >= 2.5 ? 'v-bad' : l.attempts >= 1.8 ? 'v-warn' : ''), help: 'Bir kez bitirmek için ortalama kaç kez başlanıyor' },
    { key: 'avgCompleteSec', label: 'Ort. bitirme süresi', fmt: (l) => secs(l.avgCompleteSec) },
    { key: 'drop', label: 'Bırakan %', fmt: (l) => pct(l.drop, 0), cls: (l) => (l.drop >= 15 ? 'v-bad' : l.drop >= 8 ? 'v-warn' : ''), help: 'Levele ulaşıp bitirmeyenler' },
    { key: 'flags', label: 'Durum', left: true, sort: (l) => l.flags.length, fmt: (l) => l.flags.map(([c, t]) => `<span class="flag ${c}">${t}</span>`).join('') || '<span class="flag good">Normal</span>' },
  ], m.levels, { rowTitle: (l) => l.flags.map((f) => f[2]).join(' · ') });

  table('campTable', [
    { key: 'name', label: 'Kampanya', left: true },
    { key: 'spend', label: 'Harcama', fmt: (c) => money(c.spend) },
    { key: 'impressions', label: 'Gösterim', fmt: (c) => int(c.impressions) },
    { key: 'reach', label: 'Erişim', fmt: (c) => int(c.reach) },
    { key: 'ctr', label: 'CTR', sort: (c) => div(c.clicks, c.impressions), fmt: (c) => pct(div(c.clicks, c.impressions) * 100, 2) },
    { key: 'installs', label: 'Kurulum', fmt: (c) => int(c.installs) },
    { key: 'cpi', label: 'CPI', sort: (c) => div(c.spend, c.installs), fmt: (c) => money(div(c.spend, c.installs)), cls: (c) => { const v = div(c.spend, c.installs); return v > 1 ? 'v-bad' : v > 0.5 ? 'v-warn' : ''; } },
    { key: 'cpm', label: 'CPM', sort: (c) => div(c.spend, c.impressions), fmt: (c) => money(div(c.spend, c.impressions) * 1000) },
  ], data.campaigns || [], { sortKey: 'spend' });

  const players = m.newUsers || 0;
  table('designTable', [
    { key: 'id', label: 'Olay', left: true },
    { key: 'count', label: 'Toplam', fmt: (e) => int(e.count) },
    { key: 'per', label: 'Oyuncu başına', sort: (e) => e.count, fmt: (e) => (players ? nf1.format(e.count / players) : '—'), help: 'Dönemdeki yeni oyuncu sayısına göre' },
  ], data.design || [], { sortKey: 'count' });
}

function renderLevelSummary(m) {
  const L = m.levels;
  const host = document.getElementById('levelSummary');
  if (!L.length) { host.innerHTML = '<div class="callout">Level verisi yok. README → "Level verisi" bölümüne bakın.</div>'; return; }
  const name = (l) => `${l.id}${l.name ? ` (${esc(l.name)})` : ''}`;
  const hardest = [...L].filter((l) => ok(l.winRate)).sort((a, b) => a.winRate - b.winRate)[0];
  const leaker = [...L].filter((l) => ok(l.drop)).sort((a, b) => b.drop - a.drop)[0];
  const half = L.find((l) => ok(l.funnel) && l.funnel < 50);
  const longest = [...L].filter((l) => ok(l.avgCompleteSec)).sort((a, b) => b.avgCompleteSec - a.avgCompleteSec)[0];
  host.innerHTML = [
    hardest && ['En zor level', `${name(hardest)} — kazanma ${pct(hardest.winRate, 0)}, ortalama ${nf1.format(hardest.attempts || 0)} deneme`],
    leaker && ['En çok oyuncu kaybettiren', `${name(leaker)} — ulaşanların ${pct(leaker.drop, 0)}'i bırakıyor`],
    ['Oyuncuların yarısı', half ? `${name(half)}'e gelmeden bırakıyor` : `son levele (${L.at(-1).id}) kadar devam ediyor`],
    longest && ['En uzun süren level', `${name(longest)} — ortalama ${secs(longest.avgCompleteSec)}`],
  ].filter(Boolean).map(([t, v]) => `<div class="callout"><b>${t}</b>${v}</div>`).join('');
}

// ---------- grafikler ----------
function renderCharts() {
  const m = derive();
  const d = data.daily;
  const labels = d.map((x) => x.date);
  const s1 = css('--s1'), s2 = css('--s2'), crit = css('--critical'), warn = css('--warning');
  const xf = (iso, long) => dayLabel(iso, long);
  const compact = (v, axis) => (axis ? nfc.format(v) : int(v));

  lineChart(document.getElementById('chInstalls'), {
    labels, xFormat: xf, format: compact,
    series: [
      { name: 'Meta kurulum', values: d.map((x) => x.metaInstalls), color: s1 },
      { name: 'GA yeni oyuncu', values: d.map((x) => x.newUsers), color: s2 },
    ],
  });
  barChart(document.getElementById('chSpend'), {
    labels, xFormat: xf, name: 'Harcama', values: d.map((x) => x.spend), color: () => s1,
    format: (v, axis) => (axis ? nfc.format(v) : money(v)),
    tipExtra: (i) => `<div class="tip-note">CPI: ${money(div(d[i].spend, d[i].metaInstalls))} · ${int(d[i].metaInstalls)} kurulum</div>`,
  });
  lineChart(document.getElementById('chDau'), {
    labels, xFormat: xf, format: compact, series: [{ name: 'DAU', values: d.map((x) => x.dau), color: s1 }],
  });
  lineChart(document.getElementById('chPlay'), {
    labels, xFormat: xf,
    format: (v, axis) => (axis ? nf1.format(v) : `${nf1.format(v)} dk`),
    series: [{ name: 'Oynama süresi', values: d.map((x) => (ok(x.playtimeSec) ? x.playtimeSec / 60 : null)), color: s1 }],
  });
  const ret = data.retention;
  barChart(document.getElementById('chRet'), {
    labels: ret.map((r) => r.day), values: ret.map((r) => r.pct), name: 'Geri dönen', color: () => s1,
    xFormat: (n, long) => (long ? `${n}. gün` : `D${n}`), format: (v, axis) => (axis ? `%${Math.round(v)}` : pct(v)),
  });

  const L = m.levels;
  const shortId = (id) => String(id).replace(/^Level_0*/, '');
  const levelX = (id, long) => {
    if (!long) return shortId(id);
    const l = L.find((x) => x.id === id);
    return `${id}${l?.name ? ` · ${esc(l.name)}` : ''}`;
  };
  barChart(document.getElementById('chWin'), {
    labels: L.map((l) => l.id), values: L.map((l) => l.winRate), name: 'Kazanma', max: 100, xFormat: levelX,
    color: (i, v) => (v < 40 ? crit : v < 55 ? warn : s1),
    format: (v, axis) => (axis ? `%${Math.round(v)}` : pct(v, 0)),
    tipExtra: (i) => `<div class="tip-note">${int(L[i].starts)} başlama · ${int(L[i].fails)} kayıp · ${nf1.format(L[i].attempts || 0)} deneme/bitirme${L[i].flags.length ? `<br>${L[i].flags.map((f) => `<b>${f[1]}:</b> ${f[2]}`).join('<br>')}` : ''}</div>`,
  });
  lineChart(document.getElementById('chFunnel'), {
    labels: L.map((l) => l.id), xFormat: levelX,
    format: (v, axis) => (axis ? `%${Math.round(v)}` : pct(v, 0)),
    series: [{ name: 'Ulaşan oyuncu', values: L.map((l) => l.funnel), color: s1 }],
  });
}

// ---------- ana çizim ----------
function render() {
  currency = data.currency || 'USD';
  const m = derive();
  document.getElementById('rangeText').textContent = `${dayLabel(data.range.from, true)} – ${dayLabel(data.range.to, true)} (UTC)`;

  const label = { live: 'Canlı', demo: 'ÖRNEK VERİ', error: 'Hata — örnek veri', csv: 'CSV dosyası' };
  const names = { meta: 'Meta Ads', ga: 'GameAnalytics', levels: 'Level verisi' };
  document.getElementById('sources').innerHTML = Object.entries(data.sources).map(([k, s]) =>
    `<span class="src ${s.status}" title="${esc(s.message || '')}">${names[k]}: <b>${label[s.status] || s.status}</b>${s.message && s.status !== 'live' ? ` · ${esc(s.message)}` : ''}</span>`).join('')
    + (data.sources.ga?.status === 'live' && data.sources.ga.message ? `<span class="src error">GA eksik metrikler: ${esc(data.sources.ga.message)}</span>` : '');

  renderVerdicts(m);
  kpis('kpiAcq', [
    ['Harcama', money(m.spend, 0), `CPM ${money(m.cpm)}`],
    ['Gösterim', int(m.impressions), `Sıklık ${ok(m.freq) ? nf1.format(m.freq) : '—'} / kişi`],
    ['Erişilen kişi', int(m.reach), 'Reklamı gören tekil kişi'],
    ['Tıklama', int(m.clicks), `CTR ${pct(m.ctr, 2)}`],
    ['Meta kurulum', int(m.metaInstalls), `Tıklayanların ${pct(m.installRate)}'i indirdi`],
    ['CPI', money(m.cpi), 'Kurulum başına maliyet'],
    ['Yeni oyuncu (GA)', int(m.newUsers), ok(m.organic) ? `≈ ${int(m.organic)} organik / diğer` : ''],
  ]);
  kpis('kpiBeh', [
    ['Ort. günlük aktif', int(m.dau), 'DAU'],
    ['D1 retention', pct(m.d1), `D3 ${pct(m.d3)}`],
    ['D7 retention', pct(m.d7), `D14 ${pct(m.d14)}`],
    ['D30 retention', pct(m.d30)],
    ['Oturum / oyuncu / gün', ok(m.sessionsPerUser) ? nf1.format(m.sessionsPerUser) : '—'],
    ['Ort. oturum süresi', secs(m.avgSession)],
    ['Günlük oynama süresi', mins(m.playtime), 'Aktif oyuncu başına'],
  ]);
  renderLevelSummary(m);
  renderTables(m);
  renderCharts();
  document.getElementById('footer').textContent = `Son güncelleme: ${new Date(data.generatedAt).toLocaleString('tr-TR')} · Günler UTC'ye göre · Sunucu önbelleği için "Yenile"`;
}

load();
