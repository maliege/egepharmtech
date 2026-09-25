# PTCalc — Contributing / Geliştirici Kılavuzu

## Contributing (English summary)

Thank you for considering a contribution. The full developer guide below is in Turkish; the rules that matter
for a pull request are these.

**Report a problem.** Open an issue with the bug-report template: which tool, the input data (a small synthetic
table is fine), the settings, what you got and what you expected, and a reference result if you have one
(DDSolver, R, SciPy, a textbook example). Method proposals use the feature-request template and should cite the
method's source, preferably with a DOI.

**Set up.** .NET 10 SDK is the only requirement.

```bash
git clone https://github.com/maliege/ptcalc.git && cd ptcalc
dotnet build PTCalc.sln
dotnet test PTCalc.Core.Tests
dotnet run --project PTCalc/PTCalc.csproj --launch-profile http   # http://localhost:5277
```

Use `http://ptcalc.net.localhost:5277` to see the English default locally; plain `localhost` opens in Turkish.

**Where things go.**

| Layer | Path | Rule |
|---|---|---|
| Computation | `PTCalc.Core/Core/<Module>/` | Pure .NET + MathNet.Numerics; no ASP.NET, Blazor or JS interop. Namespace `PTCalc.Core.<Module>`. Every user-facing string goes through `CoreText.T("Turkish text", args)` with its English in `PTCalc.Core/Resources/CoreText.en.tsv`. |
| Web page | `PTCalc/Pages/Apps/` | Data entry, presentation and JS interop only; JS calls in `OnAfterRenderAsync`. UI strings `@L["Turkish text"]`, English in `PTCalc/Resources/SharedResource.en.resx` (key = Turkish text). Long bilingual blocks use `@if (Lang.IsEn) { … } else { … }`. |
| Guide | `PTCalc/Components/Guides/` | Method, prerequisites, how to read the output, limitations; sources with DOI. |
| Tests | `PTCalc.Core.Tests/` | xUnit. Numerical work needs a reference-value test: `DdsolverReferenceTests` (DDSolver 1.0 fixtures) and `StatisticsReferenceTests` (SciPy/statsmodels via `tools/make_stats_reference.py`) show the pattern. |

**Pull requests.** Branch from `main`, keep one topic per PR, add or update tests, run `dotnet test`, and fill the
PR template. CI builds on Ubuntu and Windows with warnings as errors. Pushing to `main` deploys the site, so
`main` is protected for maintainers only.

**Wording.** Relationships with other software are described as comparisons ("compared with DDSolver"), never
as validation or superiority. The site is not an official publication of the university.

**Licence.** Contributions are accepted under the MIT licence of the repository. Please follow the
[Code of Conduct](CODE_OF_CONDUCT.md).

---

# Geliştirici Kılavuzu (Türkçe)

> **Hedef framework:** .NET 10 · Blazor Server · iki dil (tr-TR / en-US)

Bu doküman, mevcut üç-proje mimarisine yeni bir **hesaplama modülü** eklerken izlenecek adımları ve
yerelleştirme kurallarını açıklar. Uygulamada veritabanı ve kullanıcı hesabı yoktur; her araç tarayıcı
oturumunda çalışır, veri sunucuda saklanmaz.

---

## Mimari

```
PTCalc.sln
├── PTCalc.Core/              → Saf iş mantığı (hesaplama, modeller); UI/ASP.NET bağımlılığı yok
│   ├── Core/Dissolution/           → Kinetik motor: 16 model, NonlinearFitter, ModelRanker, f1/f2, profil ölçütleri
│   ├── Core/KinetikAnalysis/       → Doz–yanıt (probit/logit) analizörleri
│   ├── Core/Statistics/            → t-testi okuyucusu, tek yönlü ANOVA + Tukey (StudentizedRange), çoklu regresyon, kalibrasyon eğrisi
│   ├── Core/Alcoholometry/         → OIML R 22 su–etanol yoğunluk formülü, v/v ↔ w/w, yoğunluktan derece, seyreltme
│   ├── Services/                   → AnalysisState*, TernaryCalculationService, PolygonSmoother, TernaryTotals
│   ├── Localization/CoreText.cs    → Çekirdek mesajlarının TR/EN çevirisi (Resources/CoreText.en.tsv gömülü)
│   └── Helpers/NumericCellParser   → Hücre metninden sayı (virgül/nokta, boşluk, birim)
│
├── PTCalc/  (Web)            → Blazor Server host
│   ├── Pages/Apps/                 → Araç sayfaları (Kinetik, F1F2, TTest, LDCalc, TernaryPhaseDiagramApp)
│   ├── Pages/AboutApps/            → Rehber sayfaları (TR gövde + @if (Lang.IsEn) { <…GuideEn/> })
│   ├── Components/Guides/          → Hızlı rehberler ve İngilizce uzun rehberler
│   ├── Components/UI/              → HandsontableGrid, ModalComponent, ToastComponent, ThemeToggle, SiteTitle
│   ├── Localization/               → Lang.IsEn, HostRequestCultureProvider (alan adı → varsayılan dil)
│   ├── Site/SiteProfile.cs         → ptcalc.tr / .com, eski spps.* yönlendirmesi, canonical/hreflang
│   ├── Middleware/                 → BrandRedirectMiddleware
│   ├── Services/                   → IEmailService, SmtpEmailService, EmailOptions (iletişim formu)
│   ├── Resources/SharedResource.en.resx → Arayüz metinlerinin İngilizcesi (anahtar = Türkçe metin)
│   └── Program.cs                  → DI kayıtları, yerelleştirme, /culture/set, /site.webmanifest
│
└── PTCalc.Core.Tests/        → xUnit (motor, DDSolver karşılaştırması, servisler, SiteProfile)
```

### Bağımlılık kuralları

| Proje | Bağımlılık alabilir | Bağımlılık **alamaz** |
|-------|--------------------|-----------------------|
| **PTCalc.Core** | Yalnız .NET + MathNet.Numerics | ASP.NET Core, Blazor, JS interop |
| **PTCalc (Web)** | Core + Blazor + MailKit | — |

### Namespace kuralı

Her iki projede `<RootNamespace>PTCalc</RootNamespace>`; namespace klasör yapısını izler:

```
PTCalc.Core/Core/Dissolution/KineticEngine.cs  →  namespace PTCalc.Core.Dissolution;
PTCalc.Core/Services/AnalysisState.cs          →  namespace PTCalc.Core.Services;
PTCalc/Site/SiteProfile.cs                     →  namespace PTCalc.Site;
```

`AssemblyName` de açıkça `PTCalc`'tir: `IStringLocalizer<SharedResource>` kaynak adını derleme adından
türetir; ad değişirse İngilizce resx bulunmaz.

---

## Yerelleştirme

- **Arayüz metni:** Razor'da `@inject IStringLocalizer<SharedResource> L` ve `@L["Türkçe metin"]`. Anahtar
  Türkçe metnin kendisidir; İngilizcesi `PTCalc/Resources/SharedResource.en.resx`'e eklenir. Çeviri
  yoksa Türkçe görünür. Yer tutucu: `L["{0} model seçildi", n]`.
- **Uzun TR/EN blokları** (rehberler): `@if (Lang.IsEn) { <text>…</text> } else { … }`. `<text>` içinde
  sınır boşluklarını koruyun; kod bloklarında çıplak noktalama bırakmayın.
- **Çekirdek mesajları** (uyarı, hata, not metinleri): `CoreText.T("Türkçe metin", args)`; İngilizcesi
  `PTCalc.Core/Resources/CoreText.en.tsv` (satır = anahtar TAB çeviri). Testler `TestCulture.cs` ile
  tr-TR'ye sabitlenir.
- **Handsontable sütun başlıkları** ve JS grafik etiketleri `OnInitialized` içinde `L[...]` ile kurulur
  (alan başlatıcıda enjeksiyon henüz yoktur); JS'e `labels` nesnesi olarak geçer.
- Varsayılan dil alan adından gelir (`SiteProfiles.Resolve`), kullanıcı seçimi `/culture/set` çerezinden.
  Blazor Server devresi kültürü bağlantıda aldığı için dil değişimi tam sayfa yüklemesidir.

---

## Yeni hesaplama modülü ekleme

**Örnek:** "Biyoyararlanım" modülü.

### 1 — Core'da klasör ve sınıflar

```
PTCalc.Core/Core/Bioavailability/
├── BioavailabilityOptions.cs
├── BioavailabilityAnalyzer.cs
└── BioavailabilityResult.cs
```

```csharp
namespace PTCalc.Core.Bioavailability;

public static class BioavailabilityAnalyzer
{
    public static BioavailabilityResult Analyze(double[] t, double[] c, BioavailabilityOptions options)
    {
        if (t.Length < 3) throw new ArgumentException(CoreText.T("En az üç zaman noktası gerekir."));
        // MathNet.Numerics kullanılabilir
        throw new NotImplementedException();
    }
}
```

Kurallar: `static` ya da durumsuz sınıflar; kullanıcıya dönen her metin `CoreText.T(...)`; `HttpClient`,
`IJSRuntime` gibi dış bağımlılık yok.

### 2 — Durum servisi (gerekiyorsa)

Sayfa yeniden açıldığında veri korunacaksa `PTCalc.Core/Services/BioavailabilityState.cs`
(`namespace PTCalc.Core.Services`) ve `Program.cs`'te `builder.Services.AddScoped<BioavailabilityState>();`.

### 3 — Blazor sayfası

```
PTCalc/Pages/Apps/Bioavailability.razor
```

```razor
@page "/bioavailability"
@using PTCalc.Core.Bioavailability
@using PTCalc.Core.Services
<SiteTitle Text="Biyoyararlanım" />
@inject BioavailabilityState State
@inject IJSRuntime JS
@inject IStringLocalizer<SharedResource> L

<h5>@L["Biyoyararlanım"]</h5>

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) { /* JS interop yalnız burada */ }
    }
}
```

### 4 — Menü, resx, test

- `PTCalc/Shared/NavMenu.razor`'a öğe; `SiteTitle` başlığının ve menü metninin İngilizcesi resx'e.
- Sayfa yolu arama motorları için `SiteProfiles` tarafından otomatik olarak canonical/hreflang alır.
- `PTCalc.Core.Tests/` altına en az bir sabit örnekli test (bilinen giriş → bilinen çıkış).

### 5 — Derleme ve doğrulama

```bash
dotnet build PTCalc.sln
dotnet test PTCalc.Core.Tests
dotnet run --project PTCalc/PTCalc.csproj --launch-profile http
```

`dotnet run` açıkken ikinci bir derleme başlatmayın; çıktılar çakışır.

### Kontrol listesi

- [ ] Hesaplama `PTCalc.Core/Core/<Modül>/` altında, namespace `PTCalc.Core.<Modül>`
- [ ] Çekirdek metinleri `CoreText.T`, İngilizcesi `CoreText.en.tsv`'de
- [ ] Arayüz metinleri `L[...]`, İngilizcesi `SharedResource.en.resx`'te
- [ ] JS interop yalnız `OnAfterRenderAsync`
- [ ] Türkçe karakterler (ı, İ, ş, ç, ö, ü, ğ) ve sayı biçimi (tr-TR virgül, en-US nokta) düşünüldü
- [ ] Test eklendi, `dotnet test` geçiyor

---

## Rehber sayfası ekleme

Rehberler `Pages/AboutApps/<Araç>About.razor` (Türkçe gövde, sürüm notu) ve `Components/Guides/<Araç>GuideEn.razor`
(İngilizce) çiftidir; stil paylaşımlı `wwwroot/css/rehber.css`'tedir (Razor kapsamlı CSS alt bileşenlere
ulaşmaz). Kaynaklar DOI ile verilir; başka yazılımlarla ilişki "ile karşılaştırıldı" diye anlatılır,
üstünlük iddiası yazılmaz.

---

## Sırlar

Tek sır SMTP parolasıdır. Yerelde `dotnet user-secrets` (`UserSecretsId` csproj'da), sunucuda
`appsettings.Production.json`. Depoya sır yazılmaz; `appsettings.json` boş şablondur.
