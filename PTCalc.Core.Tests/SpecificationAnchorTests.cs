using PTCalc.Core.Dissolution;

namespace PTCalc.Core.Tests;

/// <summary>
/// Şartname §9'un elle yazılmış DDSolver çapası ve literatür (Costa &amp; Sousa Lobo 2001)
/// kaynaklı davranışların doğrulanması. Fikstürden bağımsızdır: fikstür dosyası bozulsa/
/// değişse bile bu değerler motorun doğruluğunu bağımsız olarak sabitler.
/// </summary>
public class SpecificationAnchorTests
{
    // Şartname §9: tek-örnek veri (t = 1..20 saat, F = 8..95)
    private static readonly double[] T = { 1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
    private static readonly double[] F = { 8, 24, 38, 48, 58, 66, 73, 78, 84, 88, 92, 95 };

    /// <summary>Şartname §9 tablosu: Zero-order beklenen değerleri birebir tutmalı.</summary>
    [Fact]
    public void ZeroOrder_matches_specification_section_9_anchor()
    {
        var fit = KineticEngine.Fit("Zero-order", T, F);

        Assert.Equal(5.83483871, fit.Parameters.Single(p => p.Symbol == "k0").Value, 6);
        Assert.Equal(3039.71871, fit.Gof.SS, 4);
        Assert.Equal(98.23424312, fit.Gof.Aic, 5);
        Assert.Equal(0.8831273705, fit.Gof.Msc, 6);
        Assert.Equal(0.6499901693, fit.Gof.Rsqr, 6);
    }

    /// <summary>
    /// Costa Eq. 39: Hopfenberg slab (n=1) → F = 100·kHB·t, yani matris tükenene kadar
    /// Zero-order ile <b>aynı eğri</b>. Tek fark: Hopfenberg tükenme sonrası %100'de
    /// tavanlanır (bracket negatife düşer), Zero-order ise sınırsızdır — bu veri setinde
    /// Zero-order t=20'de %116'ya çıkar. Motor bu inceliği kullanıcıya açıkça bildirir.
    /// </summary>
    [Fact]
    public void Hopfenberg_slab_is_a_ceilinged_zero_order_and_says_so()
    {
        var opt = VariantOptions.Default with { Geometry = HopfenbergGeometry.Slab };
        var hop = KineticEngine.Fit("Hopfenberg", T, F, opt);
        var zero = KineticEngine.Fit("Zero-order", T, F);

        double kHb = hop.Parameters.Single(p => p.Symbol == "kHB").Value;
        double k0 = zero.Parameters.Single(p => p.Symbol == "k0").Value;

        // Tavanın altında (t < 1/kHB) iki model aynı eğriyi verir: F = 100·kHB·t
        double tExhaust = 1 / kHb;
        double tSafe = tExhaust * 0.5;
        Assert.Equal(100 * kHb * tSafe, EvaluateHopfenberg(hop, tSafe), 6);

        // Tavan sonrası ayrışırlar: Hopfenberg %100'de kalır, Zero-order aşar
        Assert.Equal(100.0, EvaluateHopfenberg(hop, tExhaust * 1.5), 6);
        Assert.True(k0 * 20 > 100, "Zero-order bu veride sınırsızdır ve %100'ü aşar");

        // Kullanıcı bu özdeşlik/ayrışma inceliğinden haberdar edilmeli
        Assert.Contains(hop.Flags, f => f.Contains("Zero-order"));
        Assert.Contains(hop.Flags, f => f.Contains("tavanlan"));
    }

    /// <summary>Fit edilmiş Hopfenberg eğrisini belirli bir t'de tahmin ızgarasından okur.</summary>
    private static double EvaluateHopfenberg(ModelFit fit, double t)
    {
        var opt = VariantOptions.Default with { Geometry = HopfenbergGeometry.Slab };
        var model = ModelCatalog.Find("Hopfenberg")!;
        var p = fit.Parameters.Where(c => c.Symbol == "kHB").Select(c => c.Value).ToArray();
        return model.Evaluate(t, p, opt);
    }

    /// <summary>
    /// Şartname §6.4'ün asıl derdi: eski motorda Hopfenberg Zero-order'a çöküyordu.
    /// Küre geometrisinde artık gerçekten farklı bir model fit edilir.
    /// </summary>
    [Fact]
    public void Hopfenberg_sphere_does_not_collapse_to_zero_order()
    {
        var opt = VariantOptions.Default with { Geometry = HopfenbergGeometry.Sphere };
        var hop = KineticEngine.Fit("Hopfenberg", T, F, opt);
        var zero = KineticEngine.Fit("Zero-order", T, F);

        Assert.True(hop.Gof.SS < zero.Gof.SS,
            $"Küre geometrisinde Hopfenberg (SS={hop.Gof.SS:F3}) Zero-order'dan " +
            $"(SS={zero.Gof.SS:F3}) daha iyi uymalıydı.");
    }

    /// <summary>
    /// Costa &amp; Sousa Lobo: KP'de n yalnız F &lt; %60 bölgesinden belirlenir. Filtre açıkken
    /// yalnız 5 nokta (F=8..58) kullanılır; kapatıldığında 12 noktanın tamamı.
    /// </summary>
    [Fact]
    public void KorsmeyerPeppas_respects_the_60_percent_rule_by_default()
    {
        var filtered = KineticEngine.Fit("Korsmeyer-Peppas", T, F);
        var all = KineticEngine.Fit("Korsmeyer-Peppas", T, F,
            VariantOptions.Default with { KpUseAllPoints = true });

        Assert.Equal(5, filtered.Gof.N);   // F ≤ 60 → 8, 24, 38, 48, 58
        Assert.Equal(12, all.Gof.N);

        // Filtre kapalıyken kullanıcı uyarılmalı
        Assert.Contains(all.Flags, f => f.Contains("UYARI"));
        Assert.DoesNotContain(filtered.Flags, f => f.Contains("UYARI"));
    }

    /// <summary>Costa Tablo 1: n'in mekanizma yorumu (slab eşikleri).</summary>
    [Theory]
    [InlineData(0.50, "Fickian")]
    [InlineData(0.75, "Anormal")]
    [InlineData(1.00, "Case-II")]
    [InlineData(1.50, "Süper Case-II")]
    public void KorsmeyerPeppas_n_interpretation_follows_costa_table1(double n, string expected)
        => Assert.Contains(expected,
            PTCalc.Core.Dissolution.Models.KorsmeyerPeppasModel.InterpretN(n));

    /// <summary>
    /// Şartname §6.2: Weibull, Langenbucher, Modified Langenbucher ve RRSBW aynı modeldir;
    /// BTa ise Korsmeyer-Peppas'tır. Katalogda tekrar bulunmamalı.
    /// </summary>
    [Fact]
    public void Catalog_has_no_duplicate_weibull_family_or_bta()
    {
        var names = ModelCatalog.BaseModels().Select(m => m.Name).ToArray();

        Assert.DoesNotContain("RRSBW", names);
        Assert.DoesNotContain("Langenbucher", names);
        Assert.DoesNotContain("Modified Langenbucher", names);
        Assert.DoesNotContain("BTa", names);

        Assert.Contains("Weibull", names);
        Assert.Contains("Korsmeyer-Peppas", names);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    /// <summary>
    /// Şartname §7 kuralı: doyuma giden modele additive F0 eklenemez — asimptot
    /// 100+F0 &gt; %100 verirdi. Motor bunu sessizce kabul etmek yerine reddetmeli.
    /// </summary>
    [Fact]
    public void Additive_F0_on_a_saturating_model_is_rejected()
    {
        var firstOrder = ModelCatalog.Find("First-order")!;
        var opt = VariantOptions.Default with { UseF0 = true };

        var ex = Assert.Throws<ArgumentException>(() => ModelCatalog.Build(firstOrder, opt));
        Assert.Contains("Fmax", ex.Message);
    }

    /// <summary>Costa §2.9: model-bağımsız profil ölçütleri; DE bilinen bir kapalı-form ile doğrulanır.</summary>
    [Fact]
    public void Profile_metrics_match_hand_computed_values()
    {
        // Basit üçgen profil: t=0→0, t=10→100 (doğrusal). AUC = 10*100/2 = 500.
        double[] t = { 0, 10 };
        double[] f = { 0, 100 };

        var s = ProfileMetrics.Summarize(t, f);

        Assert.Equal(500, s.Auc, 6);
        Assert.Equal(50, s.De, 6);      // 500 / (100*10) * 100 = %50
        Assert.Equal(5, s.Mdt, 6);      // tek aralığın orta noktası
    }

    /// <summary>Costa: a = (Td)^b → Td = a^(1/b), %63.2 salım süresi.</summary>
    [Fact]
    public void Weibull_reports_Td_consistent_with_63_percent_release()
    {
        var fit = KineticEngine.Fit("Weibull", T, F);
        var td = fit.Parameters.SingleOrDefault(p => p.Symbol == "Td");
        Assert.NotNull(td);

        // Td anında salım %63.2 olmalı
        double a = fit.Parameters.Single(p => p.Symbol == "a").Value;
        double b = fit.Parameters.Single(p => p.Symbol == "b").Value;
        double fAtTd = 100 * (1 - Math.Exp(-Math.Pow(td!.Value, b) / a));

        Assert.Equal(63.212, fAtTd, 2);
    }

    /// <summary>Şartname §1.5: sıralama AIC ile yapılır, ham SS ile değil.</summary>
    [Fact]
    public void FitAll_ranks_by_aic_not_by_raw_ss()
    {
        var result = KineticEngine.FitAll(T, F);

        Assert.NotEmpty(result.Fits);
        var aics = result.Fits.Select(f => f.Gof.Aic).ToArray();
        Assert.True(aics.SequenceEqual(aics.OrderBy(a => a)),
            "Sonuçlar AIC'e göre artan sırada olmalı");

        // Akaike ağırlıkları normalize edilmiş olmalı
        Assert.Equal(1.0, result.AkaikeWeights.Values.Sum(), 6);
    }

    /// <summary>
    /// AIC = N·ln(SS) + 2p olduğundan yalnız <b>aynı N</b> ile fit edilmiş modeller arasında
    /// karşılaştırılabilir. KP varsayılan olarak F≤60'a (N=5) fit edilirken diğerleri N=12
    /// kullanır; KP sıralamaya girerse AIC'i (25.6) yalnız az terim topladığı için en düşük
    /// çıkar ve haksız yere 1. olur. Bu yüzden kısıtlı veri kümesi kullanan modeller
    /// sıralamadan çıkarılıp MechanismFits'te sunulur.
    /// </summary>
    [Fact]
    public void Models_fitted_on_fewer_points_are_excluded_from_the_aic_ranking()
    {
        var result = KineticEngine.FitAll(T, F);

        // Sıralanan her model TÜM noktalara fit edilmiş olmalı → AIC kıyaslanabilir
        Assert.All(result.Fits, f => Assert.Equal(T.Length, f.Gof.N));

        // KP (F≤60) sıralamada olmamalı, mekanizma listesinde olmalı
        Assert.DoesNotContain(result.Fits, f => f.ModelName == "Korsmeyer-Peppas");
        var kp = Assert.Single(result.MechanismFits, f => f.ModelName == "Korsmeyer-Peppas");
        Assert.Equal(5, kp.Gof.N);

        // Akaike ağırlıkları yalnız sıralanan modeller üzerinden normalize edilmeli
        Assert.DoesNotContain("Korsmeyer-Peppas", result.AkaikeWeights.Keys);
    }

    /// <summary>
    /// Katsayılarında doğrusal modellerde (Zero-order, Higuchi, Peppas-Sahlin-2)
    /// OLS tohumu zaten global optimumdur; çözücü onu iyileştiremez. Bu bir başarısızlık
    /// DEĞİLDİR — Converged=false işaretlenirse UI kullanıcıya haksız yere "yakınsama
    /// başarısız" uyarısı gösterir.
    /// </summary>
    [Theory]
    [InlineData("Zero-order")]
    [InlineData("Higuchi")]
    [InlineData("Peppas-Sahlin-2")]
    public void Intrinsically_linear_models_report_convergence(string modelName)
    {
        var fit = KineticEngine.Fit(modelName, T, F);

        Assert.True(fit.Converged,
            $"{modelName}: OLS tohumu global optimum olduğu için çözücü onu iyileştiremez; " +
            "bu yakınsama başarısızlığı olarak raporlanmamalı.");
        Assert.True(double.IsFinite(fit.Gof.SS));
    }

    /// <summary>Filtre kapatılırsa KP tüm noktaları kullanır → sıralamaya katılır.</summary>
    [Fact]
    public void KorsmeyerPeppas_joins_the_ranking_when_the_filter_is_off()
    {
        var result = KineticEngine.FitAll(T, F,
            VariantOptions.Default with { KpUseAllPoints = true });

        Assert.Contains(result.Fits, f => f.ModelName == "Korsmeyer-Peppas");
        Assert.Empty(result.MechanismFits);
        Assert.All(result.Fits, f => Assert.Equal(T.Length, f.Gof.N));
    }
}
