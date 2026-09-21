using PTCalc.Core.Dissolution;
using PTCalc.Core.Dissolution.Models;

namespace PTCalc.Core.Tests;

/// <summary>DDSolver karşılaştırması sırasında (2026-09) yapılan motor düzeltmelerinin kalıcı testleri.</summary>
public class DdsolverReviewTests
{
    // Deneme.xlsx / _Ornekler.xlsx'teki 12 noktalı örnek profil (saat)
    private static readonly double[] T12 = { 1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
    private static readonly double[] F12 = { 8, 24, 38, 48, 58, 66, 73, 78, 84, 88, 92, 95 };

    /// <summary>
    /// Sınırsız F0 ile çözücü F0 = −430, kKP = 438, n = 0.06 (logaritmik eğri) buluyordu; SS düşük ama anlamsız.
    /// F0 ≥ 0 sınırıyla fit fiziksel kalır ve ölçek (saat/dakika) değişse de aynı eğriye varır.
    /// </summary>
    [Fact]
    public void KP_with_F0_is_bounded_and_time_scale_invariant()
    {
        var opt = VariantOptions.Default with { UseF0 = true, KpUseAllPoints = true };
        var hours = KineticEngine.Fit("Korsmeyer-Peppas", T12, F12, opt);
        var minutes = KineticEngine.Fit("Korsmeyer-Peppas", T12.Select(t => t * 60).ToArray(), F12, opt);

        double F0h = hours.Parameters.First(p => p.Symbol == "F0").Value;
        double F0m = minutes.Parameters.First(p => p.Symbol == "F0").Value;
        Assert.True(F0h >= 0 && F0m >= 0, $"F0 negatif: {F0h}, {F0m}");
        Assert.Equal(hours.Gof.SS, minutes.Gof.SS, 1e-3);
        Assert.Equal(hours.Parameters.First(p => p.Symbol == "n").Value,
                     minutes.Parameters.First(p => p.Symbol == "n").Value, 1e-3);
        Assert.DoesNotContain(hours.Flags, f => f.Contains("fiziksel aralığın dışında"));
    }

    /// <summary>Makoid-Banakar T25–T90 artık sayısal çözülür: F(Tx) = x ve tepe ötesindeki hedefler "Non Calc".</summary>
    [Fact]
    public void Makoid_Banakar_secondary_times_invert_the_curve()
    {
        var fit = KineticEngine.Fit("Makoid-Banakar", T12, F12);
        var targets = new Dictionary<string, double> { ["T25"] = 25, ["T50"] = 50, ["T75"] = 75, ["T80"] = 80, ["T90"] = 90 };
        foreach (var s in fit.Secondary)
        {
            Assert.True(s.IsCalculable, $"{s.Symbol} hesaplanamadı");
            Assert.Equal(targets[s.Symbol], fit.Predict(s.Value!.Value), 1e-6);
        }
        Assert.Contains(fit.Flags, f => f.StartsWith("Tepe:"));

        // Tepe %60'ta kalan sentetik eğri: T75+ "Non Calc", T25/T50 hesaplanır ve doğru
        var model = ModelCatalog.Find("Makoid-Banakar")!;
        double[] p = { 5.0, 1.0, 0.03 };                          // t* = n/k = 33.3, F* = 5·33.3·e^-1 ≈ 61.3
        var sec = model.Secondary(p, VariantOptions.Default);
        Assert.True(sec.First(s => s.Symbol == "T50").IsCalculable);
        Assert.False(sec.First(s => s.Symbol == "T75").IsCalculable);
        double t50 = sec.First(s => s.Symbol == "T50").Value!.Value;
        Assert.Equal(50, model.Evaluate(t50, p, VariantOptions.Default), 1e-6);
        Assert.True(t50 < p[1] / p[2], "T50 yükselen dalda olmalı");
    }

    /// <summary>Plato gözlenmemiş veride (Kitap1/PP: max F %5.5) Fmax = %82 çıkar → uyarı şart.</summary>
    [Fact]
    public void Fmax_far_above_observed_maximum_is_flagged()
    {
        double[] t = { 30, 60, 120, 180, 240, 300, 360 };
        double[] f = { 1.69863, 1.67098, 1.11704, 1.79715, 4.87738, 4.43281, 5.45713 };
        var fit = KineticEngine.Fit("First-order", t, f, VariantOptions.Default with { UseFmax = true });
        Assert.Contains(fit.Flags, x => x.Contains("platoya ulaşmamış"));

        // Platoya ulaşan veride uyarı yok
        var ok = KineticEngine.Fit("First-order", T12, F12, VariantOptions.Default with { UseFmax = true });
        Assert.DoesNotContain(ok.Flags, x => x.Contains("platoya ulaşmamış"));
    }

    /// <summary>Peppas-Sahlin m≈0 dejenerasyonu uyarılır (KP'deki n≈0 ile aynı mekanizma).</summary>
    [Fact]
    public void Peppas_Sahlin_degenerate_m_is_flagged()
    {
        var model = ModelCatalog.Find("Peppas-Sahlin")!;
        Assert.Contains(model.Flags(new[] { 35.0, 13.0, 0.07 }, VariantOptions.Default), f => f.Contains("dejenere"));
        Assert.Empty(model.Flags(new[] { 0.165, 0.13, 0.42 }, VariantOptions.Default));
    }

    /// <summary>
    /// Hopfenberg slab = Zero-order, küre = Hixson-Crowell eğrisi. Sıralamada çift sayım Akaike ağırlığını
    /// çarpıtır; FitAll onu yalnız silindirde listeler. Tekil Fit her geometride çalışır.
    /// </summary>
    [Theory]
    [InlineData(HopfenbergGeometry.Slab, false)]
    [InlineData(HopfenbergGeometry.Cylinder, true)]
    [InlineData(HopfenbergGeometry.Sphere, false)]
    [InlineData(HopfenbergGeometry.HalfSphere, true)]
    [InlineData(HopfenbergGeometry.Triangle, true)]
    public void Hopfenberg_is_ranked_unless_it_duplicates_another_model(HopfenbergGeometry g, bool expected)
    {
        var opt = VariantOptions.Default with { Geometry = g };
        var all = KineticEngine.FitAll(T12, F12, opt);
        Assert.Equal(expected, all.Fits.Concat(all.MechanismFits).Any(f => f.ModelName.StartsWith("Hopfenberg")));
        Assert.Equal("Hopfenberg", KineticEngine.Fit("Hopfenberg", T12, F12, opt).ModelName);
    }

    [Fact]
    public void Quadratic_is_no_longer_in_the_catalog()
        => Assert.Null(ModelCatalog.Find("Quadratic"));
    /// <summary>Karasulu, Ertan &amp; Köse (2000): yarım küre n = 1,5, üçgen n = 4; başka modelle çakışmaz.</summary>
    [Theory]
    [InlineData(HopfenbergGeometry.Slab, 1.0)]
    [InlineData(HopfenbergGeometry.HalfSphere, 1.5)]
    [InlineData(HopfenbergGeometry.Cylinder, 2.0)]
    [InlineData(HopfenbergGeometry.Sphere, 3.0)]
    [InlineData(HopfenbergGeometry.Triangle, 4.0)]
    public void Hopfenberg_exponent_follows_geometry(HopfenbergGeometry g, double n)
        => Assert.Equal(n, HopfenbergModel.Exponent(g));

    [Fact]
    public void Karasulu_geometries_give_distinct_curves_and_are_flagged()
    {
        var tri = KineticEngine.Fit("Hopfenberg", T12, F12, VariantOptions.Default with { Geometry = HopfenbergGeometry.Triangle });
        var half = KineticEngine.Fit("Hopfenberg", T12, F12, VariantOptions.Default with { Geometry = HopfenbergGeometry.HalfSphere });
        var hc = KineticEngine.Fit("Hixson-Crowell", T12, F12);
        var zo = KineticEngine.Fit("Zero-order", T12, F12);
        foreach (var f in new[] { tri, half })
        {
            Assert.NotEqual(hc.Gof.SS, f.Gof.SS, 3);
            Assert.NotEqual(zo.Gof.SS, f.Gof.SS, 3);
            Assert.Contains(f.Flags, x => x.Contains("Karasulu"));
            Assert.True(f.Converged);
        }
        Assert.NotEqual(tri.Gof.SS, half.Gof.SS, 3);
        // T50: 100(1-(1-kt)^n) = 50 → t = (1 - 0.5^(1/n)) / k
        double k = tri.Parameters.Single(p => p.Symbol == "kHB").Value;
        double t50 = tri.Secondary.Single(s => s.Symbol == "T50").Value!.Value;
        Assert.Equal((1 - Math.Pow(0.5, 1.0 / 4)) / k, t50, 6);
        Assert.Equal(50, tri.Predict(t50), 6);
    }
}
