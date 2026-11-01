import { env, fetchSafe } from './env.mjs';

// Meta Marketing API (reklam hesabı insights). Gerekli: META_ACCESS_TOKEN (ads_read izni) + META_AD_ACCOUNT_ID.
const INSTALL_ACTIONS = ['mobile_app_install', 'omni_app_install', 'app_install'];

export function metaConfigured() {
  return Boolean(env('META_ACCESS_TOKEN') && env('META_AD_ACCOUNT_ID'));
}

function accountPath() {
  const id = env('META_AD_ACCOUNT_ID');
  return id.startsWith('act_') ? id : `act_${id}`;
}

const graphBase = () => env('META_BASE_URL', `https://graph.facebook.com/${env('META_API_VERSION', 'v26.0')}`);

async function graphGetAll(path, params) {
  const url = new URL(`${graphBase()}/${path}`);
  for (const [k, v] of Object.entries(params)) url.searchParams.set(k, typeof v === 'string' ? v : JSON.stringify(v));
  url.searchParams.set('access_token', env('META_ACCESS_TOKEN'));

  const rows = [];
  let next = url.toString();
  for (let page = 0; next && page < 20; page++) {
    const res = await fetchSafe(next);
    const body = await res.json().catch(() => ({}));
    if (!res.ok || body.error) {
      const msg = body.error?.message || `HTTP ${res.status}`;
      throw new Error(`Meta API: ${msg}`);
    }
    rows.push(...(body.data || []));
    next = body.paging?.next;
  }
  return rows;
}

function installsOf(row) {
  const actions = row.actions || [];
  // Aynı kurulum birden fazla action_type altında görünebilir; çift saymamak için ilk bulunanı al
  for (const type of INSTALL_ACTIONS) {
    const a = actions.find((x) => x.action_type === type);
    if (a) return Number(a.value) || 0;
  }
  return 0;
}

const num = (v) => Number(v) || 0;

function mapRow(row) {
  return {
    spend: num(row.spend),
    impressions: num(row.impressions),
    reach: num(row.reach),
    clicks: num(row.clicks),
    installs: installsOf(row),
  };
}

async function accountInfo() {
  const url = new URL(`${graphBase()}/${accountPath()}`);
  url.searchParams.set('fields', 'name,currency');
  url.searchParams.set('access_token', env('META_ACCESS_TOKEN'));
  const res = await fetchSafe(url.toString());
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body.error) throw new Error(`Meta API: ${body.error?.message || `HTTP ${res.status}`}`);
  return body;
}

export async function fetchMeta(from, to) {
  const base = {
    time_range: { since: from, until: to },
    fields: 'spend,impressions,reach,clicks,actions',
    limit: '500',
  };
  const path = `${accountPath()}/insights`;
  const [account, daily, totals, campaigns, countries, placements] = await Promise.all([
    accountInfo(),
    graphGetAll(path, { ...base, level: 'account', time_increment: '1' }),
    graphGetAll(path, { ...base, level: 'account' }),
    graphGetAll(path, { ...base, level: 'campaign', fields: 'campaign_name,spend,impressions,reach,clicks,actions' }),
    graphGetAll(path, { ...base, level: 'account', breakdowns: 'country' }),
    graphGetAll(path, { ...base, level: 'account', breakdowns: 'publisher_platform' }),
  ]);

  return {
    account: account.name,
    currency: account.currency,
    daily: daily.map((r) => ({ date: r.date_start, ...mapRow(r) })),
    // Erişim (reach) tekil kişi sayısıdır; günlükler toplanamaz, dönem toplamı ayrı istenir
    totals: totals[0] ? mapRow(totals[0]) : { spend: 0, impressions: 0, reach: 0, clicks: 0, installs: 0 },
    campaigns: campaigns
      .map((r) => ({ name: r.campaign_name, ...mapRow(r) }))
      .sort((a, b) => b.spend - a.spend),
    countries: countries.map((r) => ({ code: r.country, ...mapRow(r) })).sort((a, b) => b.spend - a.spend),
    placements: placements.map((r) => ({ name: r.publisher_platform, ...mapRow(r) })).sort((a, b) => b.spend - a.spend),
  };
}
