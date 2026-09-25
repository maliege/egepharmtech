# Üçüncü Taraf Bileşenler ve Lisansları

Depodaki özgün kod MIT lisansıyla dağıtılır (bkz. [LICENSE](LICENSE)). Aşağıdaki bileşenler kendi
lisanslarıyla kullanılır; hiçbiri bu depoda değiştirilmiş biçimde yeniden dağıtılmaz (NuGet paketi
ya da CDN üzerinden çalışma anında yüklenir), veri dosyaları hariç.

## .NET paketleri (NuGet)

| Paket | Sürüm | Lisans | Kullanım |
|---|---|---|---|
| MathNet.Numerics | 5.0.0 | MIT | Doğrusal cebir, optimizasyon yardımcıları (kinetik motor) |
| MailKit / MimeKit | 4.17.0 | MIT | İletişim formu e-postası |
| ChartJs.Blazor.Fork | 2.0.2 | MIT | Grafik sarmalayıcı |
| Newtonsoft.Json | 13.0.4 | MIT | ChartJs.Blazor.Fork bağımlılığı (güncel sürüme yükseltilmiş) |

## Tarayıcı kitaplıkları (CDN'den yüklenir)

| Kitaplık | Sürüm | Lisans | Not |
|---|---|---|---|
| Bootstrap | 5.3.3 | MIT | |
| Bootstrap Icons | 1.11.3 | MIT | |
| Tabler Icons | 3.31.0 | MIT | |
| Ionicons | 7.1.0 | MIT | |
| Chart.js | 2.9.4 | MIT | |
| Handsontable | 14.1.0 | Handsontable Non-Commercial License | Veri giriş ızgarası. `licenseKey: 'non-commercial-and-evaluation'` ile, ücretsiz akademik/kişisel siteye uygun kullanım. **Ticari kullanım için ayrı lisans gerekir.** |
| Noto Sans (Google Fonts) | — | SIL OFL 1.1 | Grafik yazı tipi (Türkçe glifler) |

## Veri

- **DDSolver karşılaştırma verileri** (`PTCalc.Core.Tests/ddsolver_reference_cases.json`): DDSolver 1.0
  Excel eklentisiyle (Zhang ve ark., 2010, *AAPS J* 12:263–271) eklentinin örnek profilleri üzerinde
  Mehmet Ali Ege tarafından üretilen çıktılar; yalnız birim testlerinde motor karşılaştırması için kullanılır.
- **OIML R 22 alkolometri tablo değerleri** (`PTCalc.Core.Tests/oiml_r22_reference.json`): OIML R 22 (1975)
  *International Alcoholometric Tables* (BIML, Paris) basılı tablolarından `tools/make_oiml_reference.py` ile
  okunan sayısal değerler; yalnız birim testlerinde karşılaştırma için kullanılır. Tabloların PDF'i depoda yoktur.
  Hesap kodundaki formül katsayıları aynı yayından alınmıştır.
- **Rehber metinleri** (`PTCalc/Pages/AboutApps`, `Components/Guides`): özgün içerik, MIT kapsamı
  dışında; telif hakkı Mehmet Ali Ege'ye aittir.
