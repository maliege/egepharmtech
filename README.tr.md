# PTCalc (Türkçe)

*English README: [README.md](README.md).*

Farmasötik teknoloji için açık hesaplama araçları · Türkçe **https://ptcalc.tr** · English **https://ptcalc.net**

[![Lisans: MIT](https://img.shields.io/badge/lisans-MIT-0f766e.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4.svg)](https://dotnet.microsoft.com/)
[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.22894811.svg)](https://doi.org/10.5281/zenodo.22894811)

Araçlar, Ege Üniversitesi Eczacılık Fakültesi Farmasötik Teknoloji Anabilim Dalı'nda 1995'ten bu yana
geliştirilen masaüstü uygulamalarının web sürümleridir. PTCalc adı "Pharmaceutical Technology Calculators"
kısaltmasıdır; site üniversitenin resmi yayını değildir. Girilen veri tarayıcı oturumunda kalır,
sunucuda saklanmaz; hesap gerekmez. Yazarın diğer uygulamaları (majistral preparatlar, amatör telsiz,
gök atlası, botanik bağlantıları) **https://maege.tr** adresindedir.

## Uygulamalar

| Uygulama | Yol | Çekirdek | Rehber |
|---|---|---|---|
| Kinetik Analiz — 16 salım modeli, model seçimi, varyantlar (Tlag, F0, Fmax), AIC/AICc/MSC, Akaike ağırlıkları, KP F ≤ %60, Hopfenberg geometrileri | `/kinetik` | `PTCalc.Core/Core/Dissolution` | [/about-kinetic-analysis](https://ptcalc.tr/about-kinetic-analysis) |
| Üçgen (psödo-üçlü) Faz Diyagramı — çokgen alanı ve ağırlık merkezi, kapatma seçenekleri, yumuşatılmış sınır, SVG | `/ternary-phase-diagram-app` | `PTCalc.Core/Services/TernaryCalculationService.cs`, `PolygonSmoother.cs` | [/about-ternary-phase-diagram](https://ptcalc.tr/about-ternary-phase-diagram) |
| f1 / f2 benzerlik faktörleri ve model-bağımsız profil ölçütleri | `/f1f2` | `PTCalc.Core/Core/Dissolution/SimilarityFactors.cs`, `ProfileMetrics.cs` | sayfa içi |
| LD50/LD90 doz–yanıt (probit/logit) | `/ldcalc` | `PTCalc.Core/Core/KinetikAnalysis` | sayfa içi |
| t-testi ve tanımlayıcı istatistik | `/ttest` | `PTCalc.Core/Core/Statistics` | sayfa içi |
| Tek yönlü ANOVA — F testi, Levene, Tukey HSD (Tukey-Kramer), η²/ω² | `/anova` | `PTCalc.Core/Core/Statistics/OneWayAnova.cs`, `StudentizedRange.cs` | sayfa içi |
| Çoklu doğrusal regresyon — katsayı testleri, model ANOVA, VIF, Durbin-Watson, tahmin/öngörü aralığı | `/regression` | `PTCalc.Core/Core/Statistics/MultipleRegression.cs` | sayfa içi |
| Kalibrasyon eğrisi — doğrusal fit, LOD/LOQ (ICH Q2(R2)), geri hesap doğruluğu, düzey %RSD | `/calibration` | `PTCalc.Core/Core/Statistics/CalibrationCurve.cs` | sayfa içi |
| Alkol seyreltme — stok alkolden istenen derece ve miktarda seyreltme tarifi (hacimce ya da tartarak, hacim büzülmesiyle), dereceden yoğunluk, yoğunluktan derece; OIML R 22 formülü, −20…40 °C | `/alcohol-dilution` | `PTCalc.Core/Core/Alcoholometry` | [/about-alcohol-dilution](https://ptcalc.tr/about-alcohol-dilution) |

Kinetik motorun sonuçları DDSolver 1.0 çıktılarıyla karşılaştırılmıştır (32 çalışma sayfası, 37 formülasyon):
aynı parametrelerde SS ve uyum ölçütleri birebir yeniden üretilir; kareler toplamlarındaki küçük farklar
çözücülerin durma kriterlerinden gelir (`PTCalc.Core.Tests/DdsolverReferenceTests.cs`). ANOVA, Tukey HSD, regresyon
ve kalibrasyon sonuçları SciPy/statsmodels çıktılarıyla karşılaştırılır (`StatisticsReferenceTests.cs`, `stats_reference.json`).
Alkolometri hesabı basılı OIML R 22 tablolarıyla karşılaştırılır: Tablo I, II, IIIa, IIIb, IVa, IVb, Va ve Vb'den
`tools/make_oiml_reference.py` ile okunan 6 889 hücrenin (OCR hataları kodla değil tablonun kendi satır tutarlılığıyla
ayıklandı) 6 884'ü iki ondalığa yuvarlanınca basılı değerle aynıdır, 5'i yuvarlama sınırında 0,01 farklıdır
(`AlcoholometryTests.cs`, `oiml_r22_reference.json`).

## Mimari

```
PTCalc.sln
├── PTCalc.Core/        saf hesaplama kütüphanesi (yalnız .NET + MathNet.Numerics); mesajlar TR/EN (CoreText)
├── PTCalc/             Blazor Server web uygulaması: sayfalar, rehberler, yerelleştirme, SMTP iletişim formu
└── PTCalc.Core.Tests/  xUnit testleri (motor, DDSolver karşılaştırması, servisler, alan adı → dil kuralları)
```

Veritabanı ve kullanıcı hesabı yoktur. Dil: arayüz metinleri `PTCalc/Resources/SharedResource.en.resx`
(anahtar = Türkçe metin), çekirdek mesajları `PTCalc.Core/Resources/CoreText.en.tsv`. Varsayılan dil alan
adından (`PTCalc/Site/SiteProfile.cs`), kullanıcı seçimi çerezden. Eski adres spps.tr / spps.tech 301 ile
yenilerine yönlenir. Site kurulabilir web uygulamasıdır (PWA: `/site.webmanifest`, `wwwroot/sw.js`).

Ayrıntılı geliştirici kılavuzu ve yeni modül ekleme adımları: [CONTRIBUTING.md](CONTRIBUTING.md).

## Derleme ve çalıştırma

Gereksinim: .NET 10 SDK.

```bash
git clone https://github.com/maliege/ptcalc.git
cd ptcalc
dotnet build PTCalc.sln
dotnet test PTCalc.Core.Tests
dotnet run --project PTCalc/PTCalc.csproj --launch-profile http   # http://localhost:5277
```

Yerelde İngilizce varsayılanı denemek için `http://ptcalc.net.localhost:5277` (Chrome `*.localhost`'u
127.0.0.1'e çözer); düz `localhost` Türkçe açılır. Sağ üstteki TR/EN anahtarı çerezle dili değiştirir.

Kütüphaneyi tek başına kullanma örneği:

```csharp
using PTCalc.Core.Dissolution;

double[] t = [1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20];
double[] f = [8, 24, 38, 48, 58, 66, 73, 78, 84, 88, 92, 95];
var result = KineticEngine.FitAll(t, f);          // AIC'e göre sıralı
var best = result.Fits[0];                         // ör. Probit, w = 0,46
Console.WriteLine($"{best.ModelName}: {best.CoefficientSummary}, AIC {best.Gof.Aic:F2}");
```

## Atıf

Yazılımı bir çalışmada kullanırsanız [CITATION.cff](CITATION.cff) dosyasındaki bilgilerle atıf yapın;
GitHub sayfasındaki "Cite this repository" kutusu aynı dosyadan beslenir. Her yayın etiketi (`vX.Y.Z`)
Zenodo'da arşivlenir ve sürüm DOI'si alır. Kalıcı üst DOI: 10.5281/zenodo.22894811 (tüm sürümler); v1.0.0 sürüm DOI'si 10.5281/zenodo.22894812.

## Lisans

Kod [MIT lisansı](LICENSE) ile dağıtılır. Üçüncü taraf bileşenler ve veri lisansları (Handsontable ticari
olmayan lisans, Chart.js, DDSolver örnek verileri) için [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
Rehber metinleri MIT kapsamı dışındadır.

---

## Dağıtım notları

### Sırlar

Tek sır iletişim formunun SMTP parolasıdır; **depoya yazılmaz**. `PTCalc/appsettings.json` boş şablondur.

**Geliştirme (yerel):** `dotnet user-secrets` (`UserSecretsId` csproj'da tanımlı):

```
dotnet user-secrets set "EmailSettings:SmtpServer" "..." --project PTCalc/PTCalc.csproj
dotnet user-secrets set "EmailSettings:ContactForm:Email" "..." --project PTCalc/PTCalc.csproj
dotnet user-secrets set "EmailSettings:ContactForm:Password" "..." --project PTCalc/PTCalc.csproj
```

**Canlı (Natro/Plesk):** barındırmada ortam değişkeni tanımlama yeri yok; ayarlar sunucudaki
`httpdocs/appsettings.Production.json` dosyasında (yalnız `EmailSettings` bölümü) durur. Deploy iş akışı bu
dosyayı hariç tuttuğu için ezilmez.

### Deploy

`main` dalına push, GitHub Actions ile lftp/FTPS üzerinden Plesk IIS'e yayımlar
(`.github/workflows/deploy.yml`; sırlar `FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`). Yayın
framework-dependent ve `.exe`'sizdir: barındırma `.exe` dosyasına izin vermez, ANCM in-process modda
`processPath="dotnet"` ile sunucudaki .NET 10 çalışma zamanını kullanır. `appsettings.Production.json`,
`App_Data/`, `wwwroot/UserFiles/` ve `logs/` sunucu tarafında kalır.
