# EgePharmTech — Geliştirici Kılavuzu

> **Hedef framework:** .NET 10 · Blazor Server · iki dil (tr-TR / en-US)

Bu doküman, mevcut üç-proje mimarisine yeni bir **hesaplama modülü** eklerken izlenecek adımları ve
yerelleştirme kurallarını açıklar. Uygulamada veritabanı ve kullanıcı hesabı yoktur; her araç tarayıcı
oturumunda çalışır, veri sunucuda saklanmaz.

---

## Mimari

```
EgePharmTech.sln
├── EgePharmTech.Core/              → Saf iş mantığı (hesaplama, modeller); UI/ASP.NET bağımlılığı yok
│   ├── Core/Dissolution/           → Kinetik motor: 16 model, NonlinearFitter, ModelRanker, f1/f2, profil ölçütleri
│   ├── Core/KinetikAnalysis/       → Doz–yanıt (probit/logit) analizörleri
│   ├── Core/Statistics/            → t-testi girdi okuyucusu
│   ├── Services/                   → AnalysisState*, TernaryCalculationService, PolygonSmoother, TernaryTotals
│   ├── Localization/CoreText.cs    → Çekirdek mesajlarının TR/EN çevirisi (Resources/CoreText.en.tsv gömülü)
│   └── Helpers/NumericCellParser   → Hücre metninden sayı (virgül/nokta, boşluk, birim)
│
├── EgePharmTech/  (Web)            → Blazor Server host
│   ├── Pages/Apps/                 → Araç sayfaları (Kinetik, F1F2, TTest, LDCalc, TernaryPhaseDiagramApp)
│   ├── Pages/AboutApps/            → Rehber sayfaları (TR gövde + @if (Lang.IsEn) { <…GuideEn/> })
│   ├── Components/Guides/          → Hızlı rehberler ve İngilizce uzun rehberler
│   ├── Components/UI/              → HandsontableGrid, ModalComponent, ToastComponent, ThemeToggle, SiteTitle
│   ├── Localization/               → Lang.IsEn, HostRequestCultureProvider (alan adı → varsayılan dil)
│   ├── Site/SiteProfile.cs         → egepharmtech.tr / .com, eski spps.* yönlendirmesi, canonical/hreflang
│   ├── Middleware/                 → BrandRedirectMiddleware
│   ├── Services/                   → IEmailService, SmtpEmailService, EmailOptions (iletişim formu)
│   ├── Resources/SharedResource.en.resx → Arayüz metinlerinin İngilizcesi (anahtar = Türkçe metin)
│   └── Program.cs                  → DI kayıtları, yerelleştirme, /culture/set, /site.webmanifest
│
└── EgePharmTech.Core.Tests/        → xUnit (motor, DDSolver karşılaştırması, servisler, SiteProfile)
```

### Bağımlılık kuralları

| Proje | Bağımlılık alabilir | Bağımlılık **alamaz** |
|-------|--------------------|-----------------------|
| **EgePharmTech.Core** | Yalnız .NET + MathNet.Numerics | ASP.NET Core, Blazor, JS interop |
| **EgePharmTech (Web)** | Core + Blazor + MailKit | — |

### Namespace kuralı

Her iki projede `<RootNamespace>EgePharmTech</RootNamespace>`; namespace klasör yapısını izler:

```
EgePharmTech.Core/Core/Dissolution/KineticEngine.cs  →  namespace EgePharmTech.Core.Dissolution;
EgePharmTech.Core/Services/AnalysisState.cs          →  namespace EgePharmTech.Core.Services;
EgePharmTech/Site/SiteProfile.cs                     →  namespace EgePharmTech.Site;
```

`AssemblyName` de açıkça `EgePharmTech`'tir: `IStringLocalizer<SharedResource>` kaynak adını derleme adından
türetir; ad değişirse İngilizce resx bulunmaz.

---

## Yerelleştirme

- **Arayüz metni:** Razor'da `@inject IStringLocalizer<SharedResource> L` ve `@L["Türkçe metin"]`. Anahtar
  Türkçe metnin kendisidir; İngilizcesi `EgePharmTech/Resources/SharedResource.en.resx`'e eklenir. Çeviri
  yoksa Türkçe görünür. Yer tutucu: `L["{0} model seçildi", n]`.
- **Uzun TR/EN blokları** (rehberler): `@if (Lang.IsEn) { <text>…</text> } else { … }`. `<text>` içinde
  sınır boşluklarını koruyun; kod bloklarında çıplak noktalama bırakmayın.
- **Çekirdek mesajları** (uyarı, hata, not metinleri): `CoreText.T("Türkçe metin", args)`; İngilizcesi
  `EgePharmTech.Core/Resources/CoreText.en.tsv` (satır = anahtar TAB çeviri). Testler `TestCulture.cs` ile
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
EgePharmTech.Core/Core/Bioavailability/
├── BioavailabilityOptions.cs
├── BioavailabilityAnalyzer.cs
└── BioavailabilityResult.cs
```

```csharp
namespace EgePharmTech.Core.Bioavailability;

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

Sayfa yeniden açıldığında veri korunacaksa `EgePharmTech.Core/Services/BioavailabilityState.cs`
(`namespace EgePharmTech.Core.Services`) ve `Program.cs`'te `builder.Services.AddScoped<BioavailabilityState>();`.

### 3 — Blazor sayfası

```
EgePharmTech/Pages/Apps/Bioavailability.razor
```

```razor
@page "/bioavailability"
@using EgePharmTech.Core.Bioavailability
@using EgePharmTech.Core.Services
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

- `EgePharmTech/Shared/NavMenu.razor`'a öğe; `SiteTitle` başlığının ve menü metninin İngilizcesi resx'e.
- Sayfa yolu arama motorları için `SiteProfiles` tarafından otomatik olarak canonical/hreflang alır.
- `EgePharmTech.Core.Tests/` altına en az bir sabit örnekli test (bilinen giriş → bilinen çıkış).

### 5 — Derleme ve doğrulama

```bash
dotnet build EgePharmTech.sln
dotnet test EgePharmTech.Core.Tests
dotnet run --project EgePharmTech/EgePharmTech.csproj --launch-profile http
```

`dotnet run` açıkken ikinci bir derleme başlatmayın; çıktılar çakışır.

### Kontrol listesi

- [ ] Hesaplama `EgePharmTech.Core/Core/<Modül>/` altında, namespace `EgePharmTech.Core.<Modül>`
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
