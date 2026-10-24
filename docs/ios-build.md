# iOS build (Linux → GitHub Actions → TestFlight)

Workflow: `.github/workflows/ios-testflight.yml`, Unity tarafı: `Assets/Editor/IOSBuild.cs`.

- **Linux runner:** Unity (game-ci Docker imajı) Xcode projesini çıkarır.
- **macOS runner:** Xcode derler, dağıtım sertifikasıyla imzalar, TestFlight'a yükler.

Tek seferlik kurulum aşağıda. Hepsi Linux'tan yapılır, Mac gerekmez.
Sertifika/anahtar dosyalarını **repo dışında** tut (örn. `~/ios-signing/`), asla commit etme.

---

## 0. Ön koşul

Apple Developer üyeliği aktif olmalı ("Welcome to the Apple Developer Program" maili).

## 1. Bundle ID ve uygulama kaydı

Bundle ID sonradan değiştirilemez. Örnek: `com.fogboundgames.landflow`.

1. [developer.apple.com → Identifiers](https://developer.apple.com/account/resources/identifiers/list) → **+** → App IDs → App
   → Explicit Bundle ID'ye yukarıdakini yaz. Ek capability gerekmez.
2. [App Store Connect → Apps](https://appstoreconnect.apple.com/apps) → **+ New App** → iOS, ad: Land Flow,
   Bundle ID'yi listeden seç, SKU: `landflow`.

**Team ID:** developer.apple.com → Membership details (10 karakter, örn. `AB12CD34EF`).

## 2. Dağıtım sertifikası (Apple Distribution)

```bash
mkdir -p ~/ios-signing && cd ~/ios-signing
openssl genrsa -out dist.key 2048
openssl req -new -key dist.key -out dist.csr -subj "/emailAddress=fogboundgames@gmail.com/CN=Fogbound/C=TR"
```

[Certificates](https://developer.apple.com/account/resources/certificates/list) → **+** → **Apple Distribution**
→ `dist.csr`'ı yükle → `distribution.cer`'ı indirip `~/ios-signing/`'e koy.

```bash
cd ~/ios-signing
openssl x509 -inform der -in distribution.cer -out dist.pem
# -legacy şart: macOS 'security' aracı OpenSSL 3'ün varsayılan p12 şifrelemesini açamıyor
openssl pkcs12 -export -legacy -inkey dist.key -in dist.pem -out dist.p12
```

p12 için bir şifre sorar; bunu not al (`IOS_DIST_P12_PASSWORD`). `dist.key`'i kaybetme, sertifika onsuz işe yaramaz.

## 3. Provisioning profile

[Profiles](https://developer.apple.com/account/resources/profiles/list) → **+** → Distribution → **App Store Connect**
→ 1. adımdaki App ID → 2. adımdaki sertifika → ad: `LandFlow AppStore` → indir,
`~/ios-signing/LandFlow_AppStore.mobileprovision` olarak kaydet.

Sertifika yenilenirse (1 yılda bir) profil de yeniden oluşturulup secret güncellenmeli.

## 4. App Store Connect API anahtarı

App Store Connect → Users and Access → **Integrations** → App Store Connect API → Team Keys → **+**
→ ad: `GitHub CI`, rol: **App Manager** → `AuthKey_XXXXXXXXXX.p8`'i indir (yalnız bir kez indirilebilir).

Sayfadan not al: **Key ID** ve üstteki **Issuer ID**.

## 5. Unity lisans dosyası (.ulf)

Unity Hub → Preferences → Licenses → **Add** → Get a free personal license.
Dosya: `~/.local/share/unity3d/Unity/Unity_lic.ulf`.
Lisans zaten görünse bile **Add** adımını yap; yoksa .ulf oluşmaz.

## 6. GitHub secret ve variable'ları

Repo kökünde (`gh auth login` yapılmış olmalı). Değerler ekrana yazılmaz:

```bash
gh secret set UNITY_LICENSE < ~/.local/share/unity3d/Unity/Unity_lic.ulf
gh secret set UNITY_EMAIL
gh secret set UNITY_PASSWORD
base64 -w0 ~/ios-signing/dist.p12 | gh secret set IOS_DIST_P12
gh secret set IOS_DIST_P12_PASSWORD
base64 -w0 ~/ios-signing/LandFlow_AppStore.mobileprovision | gh secret set IOS_PROFILE
gh secret set ASC_KEY_P8 < ~/ios-signing/AuthKey_XXXXXXXXXX.p8

gh variable set IOS_BUNDLE_ID --body com.fogboundgames.landflow
gh variable set APPLE_TEAM_ID --body AB12CD34EF
gh variable set ASC_KEY_ID --body XXXXXXXXXX
gh variable set ASC_ISSUER_ID --body xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

(`gh secret set NAME` tek başına çalışınca değeri sorar.)

## 7. Build alma

GitHub → Actions → **iOS → TestFlight** → Run workflow → sürümü yaz (örn. `1.0`).
Ya da: `gh workflow run ios-testflight.yml -f version=1.0`

Build numarası her çalıştırmada otomatik artar (`github.run_number`).
Yükleme bittikten ~10–30 dk sonra build App Store Connect → TestFlight'ta görünür. Dahili test
kullanıcıları (App Store Connect'teki ekip) inceleme beklemeden yükleyebilir.

---

## Notlar

- **Süre/maliyet:** Linux kısmı ilk seferde ~30–60 dk (Library önbelleği yok), sonra ~15–25 dk.
  macOS kısmı ~10–20 dk; özel repoda macOS dakikası 10 kat sayılır (ücretsiz kota ≈ ayda 200 macOS dk).
- **Artifact kotası:** Xcode projesi sıkıştırılmış birkaç yüz MB; iş sonunda silinir. Kota hatası olursa
  Settings → Billing'te artifact depolamasına bak.
- **Sürüm:** Aynı sürüm (`1.0`) altında build numarası arttıkça yükleme yapılabilir; mağazada yayınlanan
  sürümden sonra yeni sürüm numarası gerekir.
- **Editör hook'ları:** `Assets/Editor` altındaki otomatik sahne kurulum script'leri CI'da da derlenir.
  Build sahneyi değiştirirse önce bunlara bak.
