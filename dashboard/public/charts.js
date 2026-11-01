// Küçük, bağımlılıksız SVG grafik çizici: çizgi (çoklu seri, artı imleç + ipucu) ve çubuk (çubuk başına ipucu).
const NS = 'http://www.w3.org/2000/svg';

function el(tag, attrs = {}, parent) {
  const n = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs)) n.setAttribute(k, v);
  if (parent) parent.appendChild(n);
  return n;
}

// 4 veya 5 aralıktan "yuvarlak" olanını seç (ör. 50 → 0,10,20…; 100 → 0,25,50…)
function tickCount(max) {
  const q = max / 4;
  return Math.abs(q - Math.round(q / 10 ** Math.floor(Math.log10(q))) * 10 ** Math.floor(Math.log10(q))) < 1e-9 ? 4 : 5;
}

function niceMax(v) {
  if (!(v > 0)) return 1;
  const p = 10 ** Math.floor(Math.log10(v));
  const f = v / p;
  return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * p;
}

let tip;
function tooltip() {
  if (!tip) {
    tip = document.createElement('div');
    tip.className = 'tip';
    tip.setAttribute('role', 'tooltip');
    document.body.appendChild(tip);
  }
  return tip;
}
function showTip(html, x, y) {
  const t = tooltip();
  t.innerHTML = html;
  t.style.display = 'block';
  const w = t.offsetWidth, h = t.offsetHeight;
  let left = x + 14, top = y - h - 10;
  if (left + w > window.innerWidth - 8) left = x - w - 14;
  if (top < 8) top = y + 14;
  t.style.left = `${left + window.scrollX}px`;
  t.style.top = `${top + window.scrollY}px`;
}
function hideTip() { if (tip) tip.style.display = 'none'; }

function frame(container, height) {
  container.innerHTML = '';
  const width = Math.max(container.clientWidth, 260);
  const svg = el('svg', { viewBox: `0 0 ${width} ${height}`, width, height, class: 'chart-svg' }, container);
  return { svg, width, height };
}

function legend(container, series) {
  if (series.length < 2) return;
  const lg = document.createElement('div');
  lg.className = 'legend';
  for (const s of series) {
    const item = document.createElement('span');
    item.innerHTML = `<i style="background:${s.color}"></i>${s.name}`;
    lg.appendChild(item);
  }
  container.prepend(lg);
}

/** Çizgi grafiği. opts: { labels, series:[{name, values, color}], format, xFormat, height } */
export function lineChart(container, opts) {
  const { labels, series, format = String, xFormat = String, height = 220 } = opts;
  const { svg, width } = frame(container, height);
  const m = { t: 12, r: 14, b: 26, l: 52 };
  const iw = width - m.l - m.r, ih = height - m.t - m.b;
  const all = series.flatMap((s) => s.values).filter((v) => v !== null && Number.isFinite(v));
  if (!all.length) { container.innerHTML = '<p class="empty">Bu aralıkta veri yok</p>'; return; }
  const max = niceMax(Math.max(...all));
  const x = (i) => m.l + (labels.length === 1 ? iw / 2 : (i / (labels.length - 1)) * iw);
  const y = (v) => m.t + ih - (v / max) * ih;

  const nt = tickCount(max);
  for (let k = 0; k <= nt; k++) {
    const v = (max / nt) * k;
    el('line', { x1: m.l, x2: width - m.r, y1: y(v), y2: y(v), class: k ? 'grid' : 'axis' }, svg);
    el('text', { x: m.l - 8, y: y(v) + 4, 'text-anchor': 'end', class: 'tick' }, svg).textContent = format(v, true);
  }
  const step = Math.max(1, Math.ceil(labels.length / Math.floor(iw / 70)));
  labels.forEach((l, i) => {
    if (i % step && !(i === labels.length - 1 && i % step >= step / 2)) return;
    el('text', { x: x(i), y: height - 8, 'text-anchor': 'middle', class: 'tick' }, svg).textContent = xFormat(l);
  });

  for (const s of series) {
    let d = '';
    let pen = false;
    s.values.forEach((v, i) => {
      if (v === null || !Number.isFinite(v)) { pen = false; return; }
      d += `${pen ? 'L' : 'M'}${x(i).toFixed(1)},${y(v).toFixed(1)}`;
      pen = true;
    });
    el('path', { d, fill: 'none', stroke: s.color, 'stroke-width': 2, 'stroke-linejoin': 'round', 'stroke-linecap': 'round' }, svg);
    if (labels.length <= 12) {
      s.values.forEach((v, i) => { if (v !== null && Number.isFinite(v)) el('circle', { cx: x(i), cy: y(v), r: 4, fill: s.color, class: 'dot' }, svg); });
    }
  }

  const cross = el('line', { y1: m.t, y2: m.t + ih, class: 'cross', visibility: 'hidden' }, svg);
  const marks = series.map((s) => el('circle', { r: 5, fill: s.color, class: 'dot', visibility: 'hidden' }, svg));
  const hit = el('rect', { x: m.l, y: m.t, width: iw, height: ih, fill: 'transparent' }, svg);
  const move = (ev) => {
    const r = svg.getBoundingClientRect();
    const px = ((ev.clientX - r.left) / r.width) * width;
    const i = Math.round(((px - m.l) / iw) * (labels.length - 1));
    if (i < 0 || i >= labels.length) return;
    cross.setAttribute('x1', x(i)); cross.setAttribute('x2', x(i)); cross.setAttribute('visibility', 'visible');
    let rows = '';
    series.forEach((s, k) => {
      const v = s.values[i];
      const ok = v !== null && Number.isFinite(v);
      marks[k].setAttribute('visibility', ok ? 'visible' : 'hidden');
      if (ok) { marks[k].setAttribute('cx', x(i)); marks[k].setAttribute('cy', y(v)); }
      rows += `<div class="tip-row"><i style="background:${s.color}"></i><span>${s.name}</span><b>${ok ? format(v) : '—'}</b></div>`;
    });
    showTip(`<div class="tip-title">${xFormat(labels[i], true)}</div>${rows}`, ev.clientX, ev.clientY);
  };
  hit.addEventListener('pointermove', move);
  hit.addEventListener('pointerleave', () => { hideTip(); cross.setAttribute('visibility', 'hidden'); marks.forEach((mk) => mk.setAttribute('visibility', 'hidden')); });
  legend(container, series);
}

/** Çubuk grafiği. opts: { labels, values, color(i,v), format, xFormat, tipExtra(i), height, name } */
export function barChart(container, opts) {
  const { labels, values, color, format = String, xFormat = String, tipExtra, height = 220, name = '', max: fixedMax } = opts;
  const { svg, width } = frame(container, height);
  const m = { t: 12, r: 10, b: 26, l: 52 };
  const iw = width - m.l - m.r, ih = height - m.t - m.b;
  const finite = values.filter((v) => v !== null && Number.isFinite(v));
  if (!finite.length) { container.innerHTML = '<p class="empty">Bu aralıkta veri yok</p>'; return; }
  const max = fixedMax ?? niceMax(Math.max(...finite));
  const y = (v) => m.t + ih - (v / max) * ih;
  const band = iw / labels.length;
  const bw = Math.max(2, Math.min(28, band - 2));

  const nt = tickCount(max);
  for (let k = 0; k <= nt; k++) {
    const v = (max / nt) * k;
    el('line', { x1: m.l, x2: width - m.r, y1: y(v), y2: y(v), class: k ? 'grid' : 'axis' }, svg);
    el('text', { x: m.l - 8, y: y(v) + 4, 'text-anchor': 'end', class: 'tick' }, svg).textContent = format(v, true);
  }
  const step = Math.max(1, Math.ceil(labels.length / Math.floor(iw / 56)));
  values.forEach((v, i) => {
    const cx = m.l + band * i + band / 2;
    if (!(i % step) || (i === labels.length - 1 && i % step >= step / 2)) {
      el('text', { x: cx, y: height - 8, 'text-anchor': 'middle', class: 'tick' }, svg).textContent = xFormat(labels[i]);
    }
    if (v === null || !Number.isFinite(v)) return;
    const h = Math.max(0, m.t + ih - y(v));
    const r = Math.min(4, bw / 2, h);
    const x0 = cx - bw / 2, y0 = y(v), yb = m.t + ih;
    // Yalnızca üst köşeler yuvarlak, taban düz
    const d = `M${x0},${yb}V${y0 + r}Q${x0},${y0} ${x0 + r},${y0}H${x0 + bw - r}Q${x0 + bw},${y0} ${x0 + bw},${y0 + r}V${yb}Z`;
    const bar = el('path', { d, fill: color(i, v), class: 'bar' }, svg);
    const hit = el('rect', { x: m.l + band * i, y: m.t, width: band, height: ih, fill: 'transparent' }, svg);
    const on = (ev) => {
      bar.classList.add('hover');
      showTip(`<div class="tip-title">${xFormat(labels[i], true)}</div><div class="tip-row"><i style="background:${color(i, v)}"></i><span>${name}</span><b>${format(v)}</b></div>${tipExtra ? tipExtra(i) : ''}`, ev.clientX, ev.clientY);
    };
    hit.addEventListener('pointermove', on);
    hit.addEventListener('pointerleave', () => { bar.classList.remove('hover'); hideTip(); });
  });
}
