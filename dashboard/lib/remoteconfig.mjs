import { env, fetchSafe } from './env.mjs';
import { accessToken, serviceAccount } from './firebase.mjs';

// Oyundaki güncelleme bildirimi (LandFlowUpdate.cs) bu Remote Config parametrelerini okur. Panel yalnızca bunların varsayılan
// değerlerini değiştirir; şablondaki diğer parametrelere ve koşullara dokunmaz. Her yayın konsolun "Change history" listesine düşer.
// Gerekli: FIREBASE_KEY_FILE'daki servis hesabına "Firebase Remote Config Admin" rolü (README).

const SCOPE = 'https://www.googleapis.com/auth/firebase.remoteconfig';

const FIELDS = {
  latest: { key: 'update_latest_version', description: 'Bu sürümden eskilere isteğe bağlı güncelleme pop-up\'ı' },
  min: { key: 'update_min_version', description: 'Bundan eskiler oyuna devam edemez (zorunlu güncelleme)' },
  title: { key: 'update_title', description: 'Pop-up başlığı (İngilizce, emojisiz)' },
  message: { key: 'update_message', description: 'Pop-up mesajı (İngilizce, emojisiz)' },
  iosAppId: { key: 'update_ios_app_id', description: 'App Store sayısal uygulama numarası' },
};

const VERSION = /^\d+(\.\d+){0,3}$/;
// Oyunun yazı tipi (LilitaOne) yalnızca temel Latin harflerini içerir: Türkçe harf ve emoji kutu olarak çıkar
const PLAIN_TEXT = /^[\x20-\x7E]*$/;

export function remoteConfigConfigured() {
  return Boolean(env('FIREBASE_KEY_FILE'));
}

function templateUrl() {
  return `https://firebaseremoteconfig.googleapis.com/v1/projects/${serviceAccount().project_id}/remoteConfig`;
}

async function request(init = {}) {
  const res = await fetchSafe(templateUrl(), {
    ...init,
    headers: { Authorization: `Bearer ${await accessToken(SCOPE)}`, 'Content-Type': 'application/json; charset=utf-8', ...init.headers },
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    if (res.status === 403) {
      throw new Error(`Erişim reddedildi: servis hesabına "Firebase Remote Config Admin" rolü verilmeli ve Firebase Remote Config API açık olmalı (${json.error?.message ?? 'HTTP 403'})`);
    }
    throw new Error(json.error?.message || `HTTP ${res.status}`);
  }
  return { json, etag: res.headers.get('etag') };
}

/** Parametre konsolda bir gruba taşınmış olabilir; önce grupların içine bakılır. */
function parameterHome(template, key) {
  for (const group of Object.values(template.parameterGroups ?? {})) {
    if (group.parameters?.[key]) return group.parameters;
  }
  template.parameters ??= {};
  return template.parameters;
}

function describeVersion(version) {
  return version ? { number: version.versionNumber, time: version.updateTime, user: version.updateUser?.email ?? '' } : null;
}

/** Yayındaki güncelleme ayarları; parametre henüz yoksa boş gelir. */
export async function readUpdateConfig() {
  const { json } = await request();
  const values = {};
  const conditional = [];
  for (const [field, { key }] of Object.entries(FIELDS)) {
    const parameter = parameterHome(json, key)[key];
    values[field] = parameter?.defaultValue?.value ?? '';
    if (parameter?.conditionalValues && Object.keys(parameter.conditionalValues).length) conditional.push(key);
  }
  return { values, conditional, version: describeVersion(json.version) };
}

function validate(values) {
  const clean = {};
  for (const field of Object.keys(FIELDS)) clean[field] = String(values?.[field] ?? '').trim();

  if (!VERSION.test(clean.latest)) throw new Error('Son sürüm "1.9" biçiminde olmalı');
  if (clean.min && !VERSION.test(clean.min)) throw new Error('Zorunlu sürüm "1.7" biçiminde olmalı ya da boş kalmalı');
  if (!clean.title || clean.title.length > 40) throw new Error('Başlık 1–40 karakter olmalı');
  if (!clean.message || clean.message.length > 160) throw new Error('Mesaj 1–160 karakter olmalı');
  if (!PLAIN_TEXT.test(clean.title) || !PLAIN_TEXT.test(clean.message)) throw new Error('Başlık ve mesajda Türkçe harf, emoji ya da satır sonu olamaz (oyunun yazı tipinde yok)');
  if (clean.iosAppId && !/^\d+$/.test(clean.iosAppId)) throw new Error('iOS uygulama numarası yalnızca rakamlardan oluşur');
  return clean;
}

/** Güncel şablonu alır, yalnızca güncelleme parametrelerinin varsayılan değerini değiştirip yayınlar. */
export async function publishUpdateConfig(values) {
  const clean = validate(values);
  const { json: template, etag } = await request();

  for (const [field, { key, description }] of Object.entries(FIELDS)) {
    const home = parameterHome(template, key);
    home[key] ??= { description, valueType: 'STRING' };
    home[key].defaultValue = { value: clean[field] };
  }
  delete template.version;

  // If-Match: arada konsoldan başka bir değişiklik yayınlandıysa Google reddeder, üzerine yazılmaz
  const { json } = await request({ method: 'PUT', headers: { 'If-Match': etag }, body: JSON.stringify(template) });
  return { values: clean, version: describeVersion(json.version) };
}
