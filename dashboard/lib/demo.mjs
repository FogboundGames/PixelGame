import { enumerateDays } from './gameanalytics.mjs';

// Anahtarlar girilmeden önce paneli denemek için örnek veri. Arayüzde "ÖRNEK VERİ" etiketiyle gösterilir.
const NAMES = ['Kedi', 'Balon', 'Ok', 'Pizza', 'Gemi', 'Karpuz', 'Hayalet', 'Burger', 'Balina', 'Ada', 'Dolunay', 'Kilic',
  'Mantar Ev', 'Kruvasan', 'Kardan Adam', 'Hediye Kutusu', 'Peynir Dilimi', 'Deniz Rozeti', 'Balkabagi', 'Rat'];

function rng(seed) {
  let s = seed >>> 0;
  return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 2 ** 32);
}

export function demoMeta(from, to) {
  const r = rng(7);
  const daily = enumerateDays(from, to).map((date, i) => {
    const spend = 40 + 25 * Math.sin(i / 4) + r() * 20 + i * 0.6;
    const impressions = Math.round(spend * (900 + r() * 250));
    const clicks = Math.round(impressions * (0.011 + r() * 0.007));
    const installs = Math.round(clicks * (0.32 + r() * 0.12));
    return { date, spend: +spend.toFixed(2), impressions, reach: Math.round(impressions * 0.62), clicks, installs };
  });
  const sum = (k) => daily.reduce((a, d) => a + d[k], 0);
  const totals = { spend: +sum('spend').toFixed(2), impressions: sum('impressions'), reach: Math.round(sum('impressions') * 0.41), clicks: sum('clicks'), installs: sum('installs') };
  const split = [['LF_US_Broad_Video', 0.46], ['LF_TR_Playable', 0.24], ['LF_EU_Static', 0.18], ['LF_Retarget_Test', 0.12]];
  const campaigns = split.map(([name, share], i) => {
    const q = 0.75 + i * 0.18;
    return {
      name,
      spend: +(totals.spend * share).toFixed(2),
      impressions: Math.round(totals.impressions * share * q),
      reach: Math.round(totals.reach * share * q),
      clicks: Math.round(totals.clicks * share * (1.2 - i * 0.15)),
      installs: Math.round(totals.installs * share * (1.25 - i * 0.2)),
    };
  });
  return { currency: 'USD', daily, totals, campaigns };
}

export function demoGA(from, to, metaDaily) {
  const r = rng(11);
  let dauCarry = 0;
  const daily = enumerateDays(from, to).map((date, i) => {
    const installs = metaDaily?.[i]?.installs ?? 300;
    const organic = Math.round(installs * (0.18 + r() * 0.08));
    const newUsers = installs + organic;
    dauCarry = dauCarry * 0.78 + newUsers * 0.55;
    const dau = Math.round(newUsers + dauCarry);
    const sessions = Math.round(dau * (1.7 + r() * 0.4));
    const avgSessionSec = 300 + r() * 90;
    return { date, newUsers, dau, sessions, avgSessionSec, playtimeSec: (sessions * avgSessionSec) / dau };
  });
  const retention = [[1, 34.2], [2, 22.8], [3, 17.9], [4, 14.6], [5, 12.4], [6, 11.1], [7, 9.8], [14, 6.1], [21, 4.4], [30, 3.2]]
    .map(([day, pct]) => ({ day, pct }));

  const totalNew = daily.reduce((a, d) => a + d.newUsers, 0);
  let players = totalNew;
  const levels = NAMES.map((name, i) => {
    const hard = [4, 9, 13].includes(i); // birkaç "tıkanma" leveli
    const winRate = hard ? 0.28 + r() * 0.08 : Math.max(0.45, 0.92 - i * 0.022 - r() * 0.06);
    const completes = Math.round(players * (hard ? 0.82 : 0.95 - r() * 0.03));
    const starts = Math.round(completes / winRate);
    const lv = {
      id: `Level_${String(i + 1).padStart(3, '0')}`,
      name,
      starts,
      completes,
      fails: Math.round((starts - completes) * 0.86),
      avgCompleteSec: Math.round(55 + i * 6 + (hard ? 40 : 0) + r() * 20),
      avgFailSec: Math.round(40 + i * 4 + r() * 15),
      players,
    };
    players = completes;
    return lv;
  });
  const design = [
    { id: 'Level:Restart', count: Math.round(totalNew * 1.9) },
    { id: 'Speed:2X', count: Math.round(totalNew * 0.8) },
    { id: 'Speed:3X', count: Math.round(totalNew * 0.35) },
    { id: 'Speed:Depleted', count: Math.round(totalNew * 0.22) },
  ];
  return { daily, retention, levels, design, errors: [] };
}
