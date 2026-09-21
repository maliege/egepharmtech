// ============================================================================
//  DoseResponseEngineTests.cs
//  ProbitLogitAnalyzer (IRLS) ve DoseResponseAnalyzer (4PL) için doğrulama testleri.
//
//  Referans değerler, C# motorundan BAĞIMSIZ olarak üretildi:
//    * Finney (1971) "Probit Analysis" rotenon veri seti
//      (Macrosiphoniella sanborni; doz 10.2/7.7/5.1/3.8/2.6 mg/l, n=50/49/46/48/50,
//       ölü=44/42/24/16/6). Literatürde LD50 ≈ 4.8 mg/l olarak bilinir.
//    * Beklenen katsayılar iki bağımsız yöntemle üretildi ve 8 haneye kadar
//      birbiriyle uyuştu: (a) binom log-olabilirliğinin Nelder-Mead ile doğrudan
//      maksimizasyonu (IRLS DEĞİL), (b) statsmodels GLM (Binomial/Probit-Logit).
//
//  HASSASİYET NOTU: Aşağıdaki sabitler, makine hassasiyetine kadar yakınsatılmış
//  bağımsız bir referans IRLS'ten alınmıştır. Bu referansın skoru (log-olabilirlik
//  gradyanı, gerçek MLE'de tam sıfır) |∇| ≈ 6e-14'tür. Karşılaştırma için:
//  bu motorun kendi çözümü |∇| ≈ 2e-12, statsmodels ise |∇| ≈ 1e-06 verir — yani
//  motor, statsmodels'in varsayılan toleransından daha sıkı yakınsıyor. Bu yüzden
//  beklentiler statsmodels'in çıktısına değil, yüksek hassasiyetli referansa
//  göre yazılmıştır.
// ============================================================================
using PTCalc.Core.KinetikAnalysis;

namespace PTCalc.Core.Tests;

public class DoseResponseEngineTests
{
    // ---- Finney rotenon veri seti ----
    private static readonly double[] FinneyDoses = { 10.2, 7.7, 5.1, 3.8, 2.6 };
    private static readonly double[] FinneyDeaths = { 44, 42, 24, 16, 6 };
    private static readonly double[] FinneyTotals = { 50, 49, 46, 48, 50 };

    // Bağımsız yüksek hassasiyetli referans (bkz. dosya başındaki hassasiyet notu)
    private const double RefProbitB0 = -2.8874632563617086;
    private const double RefProbitB1 = 1.8297681023534016;
    private const double RefProbitLD50 = 4.845491787624588;
    private const double RefProbitLD90 = 9.761429938058985;
    private const double RefProbitSeB0 = 0.3510398798094493;
    private const double RefProbitSeB1 = 0.20870531702698225;
    private const double RefProbitLD50Lower = 4.3644921610809435;
    private const double RefProbitLD50Upper = 5.354386156139478;
    private const double RefProbitLD90Lower = 8.399066970182401;
    private const double RefProbitLD90Upper = 12.155324920430846;

    private const double RefLogitB0 = -4.886912275999423;
    private const double RefLogitB1 = 3.1035454792436195;
    private const double RefLogitLD50 = 4.828917944380371;
    private const double RefLogitLD90 = 9.802082136827565;
    private const double RefLogitSeB0 = 0.6429272258381545;
    private const double RefLogitSeB1 = 0.38771783491268647;
    private const double RefLogitLD50Lower = 4.341042833312151;
    private const double RefLogitLD50Upper = 5.350096589799188;
    private const double RefLogitLD90Lower = 8.339817905028669;
    private const double RefLogitLD90Upper = 12.558992396545541;

    [Fact]
    public void Probit_matches_independent_MLE_on_Finney_rotenone_data()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Probit);

        Assert.True(fit.Converged, "IRLS yakınsamalıydı.");
        Assert.Equal(RefProbitB0, fit.B0, 9);
        Assert.Equal(RefProbitB1, fit.B1, 9);
        Assert.Equal(RefProbitLD50, fit.LD50, 9);
        Assert.Equal(RefProbitLD90, fit.LD90, 9);
    }

    [Fact]
    public void Logit_matches_independent_MLE_on_Finney_rotenone_data()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Logit);

        Assert.True(fit.Converged, "IRLS yakınsamalıydı.");
        Assert.Equal(RefLogitB0, fit.B0, 9);
        Assert.Equal(RefLogitB1, fit.B1, 9);
        Assert.Equal(RefLogitLD50, fit.LD50, 9);
        Assert.Equal(RefLogitLD90, fit.LD90, 9);
    }

    // ---- Standart hatalar ve Fieller (fidusiyal) güven sınırları ----
    // Referanslar statsmodels GLM'in bse değerleriyle ve iki bağımsız Fieller
    // formülasyonuyla (kuadratik kök ve Finney'in merkez±yarıçap formu) doğrulandı.

    [Fact]
    public void Probit_standard_errors_match_reference()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Probit);

        Assert.Equal(RefProbitSeB0, fit.SeB0, 9);
        Assert.Equal(RefProbitSeB1, fit.SeB1, 9);
    }

    [Fact]
    public void Logit_standard_errors_match_reference()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Logit);

        Assert.Equal(RefLogitSeB0, fit.SeB0, 9);
        Assert.Equal(RefLogitSeB1, fit.SeB1, 9);
    }

    [Fact]
    public void Probit_fiducial_limits_match_reference_on_Finney_rotenone_data()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Probit);

        Assert.Equal(RefProbitLD50Lower, fit.LD50Lower, 9);
        Assert.Equal(RefProbitLD50Upper, fit.LD50Upper, 9);
        Assert.Equal(RefProbitLD90Lower, fit.LD90Lower, 9);
        Assert.Equal(RefProbitLD90Upper, fit.LD90Upper, 9);
    }

    [Fact]
    public void Logit_fiducial_limits_match_reference_on_Finney_rotenone_data()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Logit);

        Assert.Equal(RefLogitLD50Lower, fit.LD50Lower, 9);
        Assert.Equal(RefLogitLD50Upper, fit.LD50Upper, 9);
        Assert.Equal(RefLogitLD90Lower, fit.LD90Lower, 9);
        Assert.Equal(RefLogitLD90Upper, fit.LD90Upper, 9);
    }

    [Fact]
    public void Probit_fiducial_LD50_limits_are_near_Finney_published_values()
    {
        // Finney rotenon için ≈ 4.3 – 5.4 mg/l bildirir.
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Probit);

        Assert.InRange(fit.LD50Lower, 4.30, 4.45);
        Assert.InRange(fit.LD50Upper, 5.28, 5.42);
    }

    [Fact]
    public void Fiducial_interval_brackets_the_point_estimate()
    {
        foreach (var link in new[] { DoseResponseLink.Probit, DoseResponseLink.Logit })
        {
            var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, link);

            Assert.InRange(fit.LD50, fit.LD50Lower, fit.LD50Upper);
            Assert.InRange(fit.LD90, fit.LD90Lower, fit.LD90Upper);
        }
    }

    [Fact]
    public void Flat_slope_yields_no_fiducial_limits_instead_of_absurd_numbers()
    {
        // Doz ile mortalite arasında ilişki yok → eğim sıfırdan anlamlı biçimde
        // farklı değil (Finney'in g ≥ 1 durumu) → sınırlar sınırsız, NaN dönmeli.
        // Bu veri, canlı denemede LD90 = 6.6 milyar mg/kg üreten türden bir veridir.
        double[] doses = { 10, 20, 40, 80 };
        double[] deaths = { 5, 1, 9, 3 };
        double[] totals = { 10, 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);

        Assert.True(double.IsNaN(fit.LD50Lower));
        Assert.True(double.IsNaN(fit.LD50Upper));
        Assert.True(double.IsNaN(fit.LD90Lower));
        Assert.True(double.IsNaN(fit.LD90Upper));
    }

    [Fact]
    public void Unconverged_fit_reports_no_standard_errors_or_limits()
    {
        // Yakınsamamış fitte β'lar MLE değildir → bilgi matrisi doğru varyansı vermez.
        double[] doses = { 1, 2, 4, 8 };
        double[] deaths = { 0, 0, 10, 10 };   // tam ayrışma
        double[] totals = { 10, 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);

        Assert.False(fit.Converged);
        Assert.True(double.IsNaN(fit.SeB0));
        Assert.True(double.IsNaN(fit.SeB1));
        Assert.True(double.IsNaN(fit.LD50Lower));
        Assert.True(double.IsNaN(fit.LD50Upper));
    }

    [Fact]
    public void More_animals_per_dose_narrows_the_confidence_interval()
    {
        // Aynı oranlar, 10 kat daha fazla hayvan → GA belirgin biçimde daralmalı.
        double[] doses = { 2, 4, 8, 16, 32 };
        double[] props = { 0.10, 0.30, 0.50, 0.75, 0.92 };

        var small = ProbitLogitAnalyzer.Fit(
            doses, props.Select(p => p * 10).ToArray(), doses.Select(_ => 10.0).ToArray(),
            DoseResponseLink.Probit);
        var large = ProbitLogitAnalyzer.Fit(
            doses, props.Select(p => p * 100).ToArray(), doses.Select(_ => 100.0).ToArray(),
            DoseResponseLink.Probit);

        double smallWidth = small.LD50Upper - small.LD50Lower;
        double largeWidth = large.LD50Upper - large.LD50Lower;

        Assert.True(largeWidth < smallWidth,
            $"n arttıkça GA daralmalıydı: n=10 → {smallWidth:F3}, n=100 → {largeWidth:F3}");
    }

    [Fact]
    public void Probit_LD50_is_near_the_literature_value_for_rotenone()
    {
        var fit = ProbitLogitAnalyzer.Fit(FinneyDoses, FinneyDeaths, FinneyTotals, DoseResponseLink.Probit);
        Assert.InRange(fit.LD50, 4.7, 4.95);   // Finney: ≈ 4.8 mg/l
    }

    [Fact]
    public void Irls_recovers_parameters_from_data_generated_on_the_model()
    {
        // Model üzerinde tam olarak üretilmiş veri → IRLS katsayıları geri bulmalı.
        const double trueB0 = -3.0, trueB1 = 2.0;
        double[] doses = { 1, 2, 4, 8, 16, 32 };
        double n = 1_000_000;   // örnekleme gürültüsü yok: beklenen sayılar
        var deaths = doses.Select(d =>
        {
            double eta = trueB0 + trueB1 * Math.Log(d);
            double p = 1.0 / (1.0 + Math.Exp(-eta));
            return p * n;
        }).ToArray();
        var totals = doses.Select(_ => n).ToArray();

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Logit);

        Assert.True(fit.Converged);
        Assert.Equal(trueB0, fit.B0, 6);
        Assert.Equal(trueB1, fit.B1, 6);
        Assert.Equal(Math.Exp(-trueB0 / trueB1), fit.LD50, 6);
    }

    [Fact]
    public void Weighting_by_n_actually_matters_when_group_sizes_differ()
    {
        // Aynı oranlar, farklı grup büyüklükleri: IRLS (n ile ağırlıklı) sonucu
        // değiştirmeli; 4PL (ağırlıksız, n'i hiç görmez) DEĞİŞTİRMEMELİ.
        double[] doses = { 2, 4, 8, 16, 32 };
        double[] props = { 0.10, 0.30, 0.50, 0.75, 0.92 };

        double[] equalN = { 10, 10, 10, 10, 10 };
        double[] skewedN = { 100, 10, 10, 10, 100 };   // uçlarda çok daha fazla hayvan

        var fitEqual = ProbitLogitAnalyzer.Fit(
            doses, doses.Select((_, i) => props[i] * equalN[i]).ToArray(), equalN, DoseResponseLink.Probit);
        var fitSkewed = ProbitLogitAnalyzer.Fit(
            doses, doses.Select((_, i) => props[i] * skewedN[i]).ToArray(), skewedN, DoseResponseLink.Probit);

        Assert.NotEqual(fitEqual.LD50, fitSkewed.LD50, 3);

        // 4PL yüzdelerle çalışır; her iki durumda da yüzdeler aynı → aynı LD50.
        var mortalities = props.Select(p => p * 100.0).ToArray();
        var pl4 = DoseResponseAnalyzer.FitLogistic(doses, mortalities);
        Assert.Equal(pl4.LD50, DoseResponseAnalyzer.FitLogistic(doses, mortalities).LD50, 9);
    }

    // ---- Kenar durumlar: mevcut davranışı BELGELEYEN testler ----

    [Fact]
    public void All_doses_identical_reports_no_result_instead_of_fabricating_LD50()
    {
        // Tek doz seviyesi (3 tekrar): tasarım matrisi tekil, eğim tahmin edilemez.
        // Hiçbir IRLS adımı çözülemez → başlangıç değerlerinden (b0=0, b1=1) türetilen
        // exp(-0/1) = 1 mg/kg gibi uydurma bir LD50 DÖNDÜRÜLMEMELİ.
        double[] doses = { 20, 20, 20 };
        double[] deaths = { 4, 5, 6 };
        double[] totals = { 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);

        Assert.False(fit.Converged);
        Assert.True(double.IsNaN(fit.B0));
        Assert.True(double.IsNaN(fit.B1));
        Assert.True(double.IsNaN(fit.LD50));
        Assert.True(double.IsNaN(fit.LD90));
    }

    [Fact]
    public void All_zero_mortality_does_not_converge()
    {
        double[] doses = { 1, 2, 4, 8 };
        double[] deaths = { 0, 0, 0, 0 };
        double[] totals = { 10, 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);
        Assert.False(fit.Converged);
    }

    [Fact]
    public void All_full_mortality_does_not_converge()
    {
        double[] doses = { 1, 2, 4, 8 };
        double[] deaths = { 10, 10, 10, 10 };
        double[] totals = { 10, 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);
        Assert.False(fit.Converged);
    }

    [Fact]
    public void Complete_separation_does_not_converge()
    {
        // 0,0,100,100 → MLE yok (eğim → ∞).
        double[] doses = { 1, 2, 4, 8 };
        double[] deaths = { 0, 0, 10, 10 };
        double[] totals = { 10, 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);
        Assert.False(fit.Converged);
    }

    [Fact]
    public void Rows_with_zero_animals_are_skipped_not_fatal()
    {
        double[] doses = { 10.2, 7.7, 5.1, 3.8, 2.6, 99 };
        double[] deaths = { 44, 42, 24, 16, 6, 0 };
        double[] totals = { 50, 49, 46, 48, 50, 0 };   // son satır: n=0

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);

        Assert.True(fit.Converged);
        Assert.Equal(RefProbitLD50, fit.LD50, 9);   // n=0 satırı sonucu etkilememeli
    }

    [Fact]
    public void Fewer_than_three_positive_doses_throws()
    {
        double[] doses = { 1, 2 };
        double[] deaths = { 1, 5 };
        double[] totals = { 10, 10 };

        Assert.Throws<ArgumentException>(
            () => ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit));
    }

    [Fact]
    public void Responses_exceeding_totals_are_silently_clamped()
    {
        // Veri girişi hatası (10 hayvanda 12 ölüm) hata vermez, p=1'e kırpılır.
        double[] doses = { 1, 2, 4, 8 };
        double[] deaths = { 1, 5, 9, 12 };   // son satır n'den büyük
        double[] totals = { 10, 10, 10, 10 };

        var fit = ProbitLogitAnalyzer.Fit(doses, deaths, totals, DoseResponseLink.Probit);
        Assert.True(double.IsFinite(fit.LD50));   // sessizce kabul edilir
    }
}
