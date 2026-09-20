// ============================================================================
//  KineticEngineInvariantTests.cs
//  Fikstürden BAĞIMSIZ davranış testleri.
//
//  KineticEngineTests DDSolver'la sayısal eşleşmeyi doğrular; burada ise motorun
//  her veri kümesinde tutması gereken genel özellikler sınanır:
//   1. Sentetik veri → parametre geri kazanımı (gürültüsüz veriyle SS ≈ 0)
//   2. İkincil parametre tutarlılığı: Predict(T50) ≈ 50 vb.
//   3. Ölçek değişmezliği: saat → dakika dönüşümü SS'i ve sıralamayı değiştirmemeli
//   4. Sıra değişmezliği: karışık girilen noktalar aynı sonucu vermeli
//   5. Uç durumlar: 2-3 nokta, %100'e ulaşan profil, tekrarlı zamanlar
// ============================================================================
using EgePharmTech.Core.Dissolution;
using EgePharmTech.Core.Dissolution.Models;

namespace EgePharmTech.Core.Tests;

public class KineticEngineInvariantTests
{
    private static readonly double[] T = { 1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
    private static readonly double[] F = { 8, 24, 38, 48, 58, 66, 73, 78, 84, 88, 92, 95 };

    // ------------------------------------------------------------------
    // 1) Sentetik veri: bilinen parametrelerle üretilen gürültüsüz profil,
    //    aynı modele fit edildiğinde SS ≈ 0 vermeli ve parametreler geri gelmeli.
    // ------------------------------------------------------------------
    public static IEnumerable<object[]> SyntheticCases()
    {
        object[] C(string name, VariantOptions opt, params double[] p) => new object[] { name, opt, p };
        var d = VariantOptions.Default;

        yield return C("Zero-order", d, 4.0);
        yield return C("First-order", d, 0.15);
        yield return C("Higuchi", d, 20.0);
        yield return C("Hixson-Crowell", d, 0.03);
        yield return C("Korsmeyer-Peppas", d with { KpUseAllPoints = true }, 10.0, 0.6);
        yield return C("Weibull", d, 8.0, 1.2);
        yield return C("Hopfenberg", d with { Geometry = HopfenbergGeometry.Sphere }, 0.03);
        yield return C("Hopfenberg", d with { Geometry = HopfenbergGeometry.Cylinder }, 0.03);
        yield return C("Baker-Lonsdale", d, 0.02);
        yield return C("Peppas-Sahlin-2", d, 10.0, 1.5);
        yield return C("Logistic", d, -2.0, 2.5);
        yield return C("Gompertz", d, 3.0, 2.5);
        yield return C("Probit", d, -1.4, 2.0);
        yield return C("Logistic (Fmax)", d, 0.3, 6.0, 90.0);
        yield return C("Gompertz (Fmax)", d, 0.3, 5.0, 90.0);

        // Varyantlar — parametre sırası: [taban..., F0, Tlag, Fmax]
        yield return C("Zero-order", d with { UseF0 = true }, 3.0, 10.0);
        yield return C("Zero-order", d with { UseTlag = true }, 4.5, 1.5);
        yield return C("Higuchi", d with { UseF0 = true }, 18.0, 6.0);
        yield return C("First-order", d with { UseTlag = true }, 0.15, 1.5);
        yield return C("First-order", d with { UseFmax = true }, 0.15, 85.0);
        yield return C("First-order", d with { UseTlag = true, UseFmax = true }, 0.15, 1.5, 85.0);
        yield return C("Weibull", d with { UseTlag = true, UseFmax = true }, 8.0, 1.2, 1.0, 88.0);
        yield return C("Hixson-Crowell", d with { UseTlag = true }, 0.03, 1.0);
        yield return C("Korsmeyer-Peppas", d with { UseF0 = true, KpUseAllPoints = true }, 8.0, 0.55, 5.0);
    }

    [Theory]
    [MemberData(nameof(SyntheticCases))]
    public void Noise_free_synthetic_profile_is_recovered(string name, VariantOptions opt, double[] truth)
    {
        var baseModel = ModelCatalog.Find(name)!;
        var model = ModelCatalog.Build(baseModel, opt);
        var f = T.Select(t => model.Evaluate(t, truth, opt)).ToArray();

        var fit = new NonlinearFitter().Fit(model, T, f, Weighting.None, opt);

        // Gürültüsüz veri: fit hatası sıfıra yakın olmalı
        Assert.True(fit.Gof.SS < 1e-3,
            $"{model.Name}: SS={fit.Gof.SS:G6} — gürültüsüz veri geri kazanılamadı " +
            $"(bulunan: {fit.CoefficientSummary})");

        // Parametreler (Weibull Td türetilmiş katsayı, listede fazladan durur → ada göre eşle)
        var names = model.ParamNames;
        for (int i = 0; i < names.Count; i++)
        {
            double got = fit.Parameters.First(c => c.Symbol == names[i]).Value;
            double tol = 1e-2 * Math.Max(1.0, Math.Abs(truth[i]));
            Assert.True(Math.Abs(got - truth[i]) <= tol,
                $"{model.Name}/{names[i]}: beklenen {truth[i]:G6}, gelen {got:G6}");
        }
    }

    // ------------------------------------------------------------------
    // 2) İkincil parametre tutarlılığı: model kendi T50'sinde %50 vermeli.
    //    Kapalı-form terslerin, Tlag kaydırmasının ve F0 sayısal çözümünün
    //    hepsini tek bir kuralla yakalar.
    // ------------------------------------------------------------------
    public static IEnumerable<object[]> AllFitsOnSpecData()
    {
        var toggles = new[]
        {
            VariantOptions.Default,
            VariantOptions.Default with { UseTlag = true },
            VariantOptions.Default with { UseF0 = true },
            VariantOptions.Default with { UseFmax = true },
            VariantOptions.Default with { UseTlag = true, UseF0 = true, UseFmax = true },
        };
        foreach (var opt in toggles)
        {
            var res = KineticEngine.FitAll(T, F, opt);
            foreach (var fit in res.Fits.Concat(res.MechanismFits))
                yield return new object[] { fit.ModelName, fit };
        }
    }

    [Theory]
    [MemberData(nameof(AllFitsOnSpecData))]
    public void Secondary_times_are_consistent_with_the_fitted_curve(string name, ModelFit fit)
    {
        var targets = new Dictionary<string, double>
        {
            ["T25"] = 25, ["T50"] = 50, ["T75"] = 75, ["T80"] = 80, ["T90"] = 90
        };

        foreach (var s in fit.Secondary)
        {
            if (!s.IsCalculable || !targets.TryGetValue(s.Symbol, out var target)) continue;
            double at = fit.Predict(s.Value!.Value);
            Assert.True(Math.Abs(at - target) < 0.05,
                $"{name}: {s.Symbol}={s.Value:G6} noktasında eğri {at:G6} veriyor, {target} bekleniyordu");
        }
    }

    [Theory]
    [MemberData(nameof(AllFitsOnSpecData))]
    public void Secondary_times_are_monotone(string name, ModelFit fit)
    {
        var order = new[] { "T25", "T50", "T75", "T80", "T90" };
        var vals = order.Select(k => fit.Secondary.FirstOrDefault(s => s.Symbol == k)?.Value).ToArray();
        for (int i = 1; i < vals.Length; i++)
        {
            if (vals[i] is null || vals[i - 1] is null) continue;
            Assert.True(vals[i] >= vals[i - 1] - 1e-9,
                $"{name}: {order[i - 1]}={vals[i - 1]:G6} > {order[i]}={vals[i]:G6}");
        }
    }

    [Theory]
    [MemberData(nameof(AllFitsOnSpecData))]
    public void Fit_reports_finite_gof_and_parameters(string name, ModelFit fit)
    {
        Assert.True(double.IsFinite(fit.Gof.SS) && fit.Gof.SS >= 0, $"{name}: SS={fit.Gof.SS}");
        Assert.True(double.IsFinite(fit.Gof.Aic), $"{name}: AIC={fit.Gof.Aic}");
        Assert.True(double.IsFinite(fit.Gof.RsqrAdj), $"{name}: R²adj={fit.Gof.RsqrAdj}");
        Assert.All(fit.Parameters, c => Assert.True(double.IsFinite(c.Value), $"{name}/{c.Symbol} sonlu değil"));
        Assert.All(fit.PredValues, v => Assert.True(double.IsFinite(v), $"{name}: tahmin ızgarasında sonlu olmayan değer"));
    }

    // ------------------------------------------------------------------
    // 3) Ölçek değişmezliği: aynı profil saat yerine dakika ile girilirse
    //    SS ve sıralama değişmemeli (yalnız hız sabitleri ölçeklenir).
    // ------------------------------------------------------------------
    [Fact]
    public void Rescaling_time_units_does_not_change_ss_or_ranking()
    {
        var hours = KineticEngine.FitAll(T, F);
        var minutes = KineticEngine.FitAll(T.Select(t => t * 60).ToArray(), F);

        var hNames = hours.Fits.Select(f => f.ModelName).ToArray();
        var mNames = minutes.Fits.Select(f => f.ModelName).ToArray();
        Assert.Equal(hNames, mNames);

        foreach (var (h, m) in hours.Fits.Zip(minutes.Fits))
        {
            double tol = 1e-3 * Math.Max(1.0, h.Gof.SS);
            Assert.True(Math.Abs(h.Gof.SS - m.Gof.SS) <= tol,
                $"{h.ModelName}: saat SS={h.Gof.SS:G8}, dakika SS={m.Gof.SS:G8}");
        }
    }

    // ------------------------------------------------------------------
    // 4) Sıra değişmezliği: noktalar karışık girilse de sonuç aynı.
    // ------------------------------------------------------------------
    [Fact]
    public void Shuffled_input_gives_identical_fits()
    {
        var idx = new[] { 5, 0, 11, 3, 8, 1, 10, 2, 7, 4, 9, 6 };
        var ts = idx.Select(i => T[i]).ToArray();
        var fs = idx.Select(i => F[i]).ToArray();

        var sorted = KineticEngine.FitAll(T, F);
        var shuffled = KineticEngine.FitAll(ts, fs);

        Assert.Equal(sorted.Fits.Select(f => f.ModelName), shuffled.Fits.Select(f => f.ModelName));
        foreach (var (a, b) in sorted.Fits.Zip(shuffled.Fits))
            Assert.True(Math.Abs(a.Gof.SS - b.Gof.SS) <= 1e-6 * Math.Max(1, a.Gof.SS),
                $"{a.ModelName}: sıralı SS={a.Gof.SS:G8}, karışık SS={b.Gof.SS:G8}");

        Assert.Equal(sorted.Profile.Auc, shuffled.Profile.Auc, 6);
        Assert.Equal(sorted.Profile.Mdt, shuffled.Profile.Mdt, 6);
    }

    // ------------------------------------------------------------------
    // 5) Uç durumlar
    // ------------------------------------------------------------------
    [Fact]
    public void Three_points_still_produce_a_ranking_without_throwing()
    {
        var res = KineticEngine.FitAll(new double[] { 1, 2, 4 }, new double[] { 20, 38, 60 });
        Assert.NotEmpty(res.Fits);
        Assert.All(res.Fits, f => Assert.True(double.IsFinite(f.Gof.SS)));
    }

    [Fact]
    public void Two_points_do_not_throw()
    {
        var res = KineticEngine.FitAll(new double[] { 1, 2 }, new double[] { 20, 38 });
        Assert.All(res.Fits, f => Assert.True(double.IsFinite(f.Gof.SS)));
    }

    [Fact]
    public void Profile_reaching_100_percent_is_handled()
    {
        double[] t = { 1, 2, 4, 6, 8, 12 };
        double[] f = { 30, 55, 85, 97, 100, 100 };
        var res = KineticEngine.FitAll(t, f);

        Assert.NotEmpty(res.Fits);
        Assert.All(res.Fits, fit => Assert.True(double.IsFinite(fit.Gof.SS), $"{fit.ModelName}: SS sonlu değil"));
        // First-order gibi doyuma giden modeller bu veriyi makul uydurmalı
        var fo = res.Fits.Single(f => f.ModelName == "First-order");
        Assert.True(fo.Gof.RsqrAdj > 0.95, $"First-order R²adj={fo.Gof.RsqrAdj:G4}");
    }

    [Fact]
    public void Duplicate_time_points_are_accepted()
    {
        double[] t = { 1, 1, 2, 2, 4, 4, 8, 8 };
        double[] f = { 10, 12, 22, 25, 40, 43, 65, 68 };
        var res = KineticEngine.FitAll(t, f);
        Assert.NotEmpty(res.Fits);
        Assert.All(res.Fits, fit => Assert.Equal(8, fit.Gof.N));
    }

    /// <summary>Tlag varyantı: gecikmeden önce salım sıfır olmalı.</summary>
    [Fact]
    public void Tlag_variant_is_zero_before_the_lag()
    {
        var opt = VariantOptions.Default with { UseTlag = true };
        var fit = KineticEngine.Fit("First-order", T, F, opt);
        double tlag = fit.Parameters.Single(c => c.Symbol == "Tlag").Value;
        if (tlag > 0)
            Assert.Equal(0, fit.Predict(tlag * 0.5), 9);
    }

    /// <summary>Fmax varyantı: Fmax'ın üstündeki hedefler "Non Calc" olmalı.</summary>
    [Fact]
    public void Targets_above_Fmax_are_not_calculable()
    {
        // Platoya %70'te ulaşan profil
        double[] f = T.Select(t => 70 * (1 - Math.Exp(-0.3 * t))).ToArray();
        var fit = KineticEngine.Fit("First-order", T, f, VariantOptions.Default with { UseFmax = true });

        double fmax = fit.Parameters.Single(c => c.Symbol == "Fmax").Value;
        Assert.InRange(fmax, 69, 71);
        Assert.False(fit.Secondary.Single(s => s.Symbol == "T75").IsCalculable);
        Assert.False(fit.Secondary.Single(s => s.Symbol == "T90").IsCalculable);
        Assert.True(fit.Secondary.Single(s => s.Symbol == "T50").IsCalculable);
    }

    /// <summary>Costa Tablo 1: küre için eşikler 0.43 / 0.85'tir.</summary>
    [Theory]
    [InlineData(HopfenbergGeometry.Slab, 0.50, "Fickian")]
    [InlineData(HopfenbergGeometry.Cylinder, 0.45, "Fickian")]
    [InlineData(HopfenbergGeometry.Sphere, 0.43, "Fickian")]
    [InlineData(HopfenbergGeometry.Sphere, 0.85, "Case-II")]
    [InlineData(HopfenbergGeometry.Sphere, 0.50, "Anormal")]
    public void KP_n_interpretation_uses_geometry_specific_thresholds(HopfenbergGeometry g, double n, string expected)
        => Assert.Contains(expected, KorsmeyerPeppasModel.InterpretN(n, g));
}
