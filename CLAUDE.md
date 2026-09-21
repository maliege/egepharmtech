# PTCalc — çalışma kılavuzu

Farmasötik teknoloji hesaplama araçları (Blazor Server, .NET 10). Türkçe **ptcalc.tr**, İngilizce
**ptcalc.net**; dil alan adından, kullanıcı seçimi çerezden. Depo herkese açık; sır içermez.

## Derleme ve test

```bash
dotnet build PTCalc.sln
dotnet test PTCalc.Core.Tests
dotnet run --project PTCalc/PTCalc.csproj --launch-profile http   # http://localhost:5277
```

- Proje adlarını bu yazımla kullan; `RootNamespace`/`AssemblyName` açıkça `PTCalc` (IStringLocalizer kaynak
  adını derleme adından türetir).
- `dotnet run` açıkken `dotnet build`/`publish` çalıştırma; çıktılar çakışır.
- İngilizce varsayılanı yerelde denemek için `http://ptcalc.net.localhost:5277`.

## Mimari

- `PTCalc.Core/`: saf hesaplama (kinetik motor, f1/f2, doz–yanıt, t-testi/ANOVA/regresyon/kalibrasyon, üçgen faz geometrisi). ASP.NET/Blazor
  bağımlılığı yok; kullanıcıya dönen her metin `CoreText.T("Türkçe", args)`.
- `PTCalc/`: sayfalar `Pages/Apps`, rehberler `Pages/AboutApps` + `Components/Guides`, alan adı kuralları
  `Site/SiteProfile.cs`, yönlendirme `Middleware/BrandRedirectMiddleware.cs`, SMTP `Services/`.
- `PTCalc.Core.Tests/`: xUnit; `DdsolverReferenceTests` gerçek DDSolver 1.0 çıktılarıyla karşılaştırma fikstürü.
  Kinetik motorunda değişiklikte mutlaka çalıştır. `StatisticsReferenceTests` SciPy/statsmodels referanslarıyla (`stats_reference.json`)
  karşılaştırır; referans üretim betiği `tools/make_stats_reference.py`.

Ayrıntı: `CONTRIBUTING.md`.

## Yerelleştirme

- Arayüz: `@L["Türkçe metin"]`; İngilizcesi `PTCalc/Resources/SharedResource.en.resx` (anahtar = Türkçe metin).
- Uzun bloklar: `@if (Lang.IsEn) { <text>…</text> } else { … }`; `<text>` içinde sınır boşluklarını koru, kod bloğunda
  çıplak noktalama bırakma.
- Çekirdek: `PTCalc.Core/Resources/CoreText.en.tsv` (satır = anahtar TAB çeviri; csproj'da `WithCulture="false"`).
- Handsontable başlıkları ve JS grafik etiketleri `OnInitialized` içinde kurulur.
- Sayfa içi çapalar tam yolla (`href="/about-kinetic-analysis#bolum"`), `<base href>` yüzünden `#bolum` çalışmaz.

## Yazım ve içerik kuralları

- DDSolver ile ilişki "karşılaştırıldı" diye anlatılır; "doğrulandı/validated", "üstün/outperforms" yazılmaz.
- Üçgen faz diyagramında alan ve ağırlık merkezi hesabı yazarın 2001 tarihli yazılımından gelir; Berkman & Güleç (2021)
  "aynı geometriyi sonradan yayımlayan" olarak anılır, yöntemin sahibi gibi değil.
- Kaynaklar DOI ile verilir; Türk literatürü için `docs/kinetik-turk-kaynaklar.md`.
- Site üniversitenin resmi yayını değildir; PTCalc = "Pharmaceutical Technology Calculators" (Hakkında sayfasında yazar).
- Eski marka adı (EgePharmTech) ve alan adları koda, metne ya da yönlendirmeye geri eklenmez.

## Yayın

- `main`'e push → `.github/workflows/deploy.yml` (test → framework-dependent, `UseAppHost=false`, RID'siz yayın → lftp FTPS).
  Barındırma `.exe` dosyasına izin vermez; yayında `.exe` çıkarsa iş akışı durur. Belge değişiklikleri (`docs/**`, `*.md`,
  CITATION.cff, .zenodo.json) yayın tetiklemez.
- Sunucuda kalan dosyalar: `appsettings.Production.json` (yalnız `EmailSettings`), `App_Data/`, `wwwroot/UserFiles/`, `logs/`.
- Sırlar (`FTP_*`, SMTP parolası) asla depoya, çıktıya ya da sohbete yazılmaz; Plesk/Cloudflare/GitHub ayarlarını depo sahibi yapar.

## Sürüm ve atıf

Her yayında `CITATION.cff` (version, date-released) ve `PTCalc.Core.csproj` (Version) birlikte güncellenir, `vX.Y.Z`
etiketi ve GitHub Release atılır; Zenodo release'i arşivler.
