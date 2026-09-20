#nullable enable
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Integration;

namespace EgePharmTech.Core.Statistics;

/// <summary>
/// Studentized range (q) dağılımı: Tukey HSD post-hoc testinin p-değeri ve kritik değeri için.
/// MathNet'te bulunmadığından burada doğrudan tanımından hesaplanır (Harter 1960; Copenhaver &amp; Holland 1988):
///
/// <para>P(Q ≤ q | k, ν) = ∫₀^∞ f_ν(s) · P_∞(q·s; k) ds,  P_∞(w; k) = k ∫ φ(z) [Φ(z) − Φ(z − w)]^{k−1} dz</para>
///
/// <para>f_ν(s), s = √(χ²_ν/ν) değişkeninin yoğunluğudur. İki katlı integral Gauss-Legendre panelleriyle alınır;
/// SciPy <c>studentized_range</c> ile 1e-6 düzeyinde uyuşur (bkz. StatisticsReferenceTests). ν çok büyükse
/// (ν &gt; 10 000) yalnız iç integral kullanılır.</para>
/// </summary>
public static class StudentizedRange
{
    private const int InnerPanels = 24, InnerOrder = 16;
    private const int OuterPanels = 32, OuterOrder = 16;

    /// <summary>P(Q ≤ q) — k grup ortalaması, ν hata serbestlik derecesi.</summary>
    public static double Cdf(double q, int k, double nu)
    {
        if (k < 2) throw new ArgumentOutOfRangeException(nameof(k), "k ≥ 2 olmalı");
        if (nu <= 0) throw new ArgumentOutOfRangeException(nameof(nu), "ν > 0 olmalı");
        if (q <= 0) return 0;
        if (double.IsPositiveInfinity(q)) return 1;
        if (nu > 10_000) return RangeCdfInfinite(q, k);

        // s'nin etkili aralığı: χ²_ν kuantillerinden
        double lo = Math.Sqrt(ChiSquared.InvCDF(nu, 1e-12) / nu);
        double hi = Math.Sqrt(ChiSquared.InvCDF(nu, 1 - 1e-12) / nu);
        double logC = 0.5 * nu * Math.Log(nu) - SpecialFunctions.GammaLn(0.5 * nu) - (0.5 * nu - 1) * Math.Log(2);

        double Density(double s) => Math.Exp(logC + (nu - 1) * Math.Log(s) - 0.5 * nu * s * s);

        double total = 0, h = (hi - lo) / OuterPanels;
        for (int i = 0; i < OuterPanels; i++)
        {
            double a = lo + i * h, b = a + h;
            total += GaussLegendreRule.Integrate(s => Density(s) * RangeCdfInfinite(q * s, k), a, b, OuterOrder);
        }
        return Math.Clamp(total, 0, 1);
    }

    /// <summary>Üst kuyruk olasılığı: p-değeri.</summary>
    public static double Sf(double q, int k, double nu) => Math.Max(0, 1 - Cdf(q, k, nu));

    /// <summary>Kritik değer: P(Q ≤ q) = p olan q (ikiye bölme; p ∈ (0,1)).</summary>
    public static double InvCdf(double p, int k, double nu)
    {
        if (p <= 0 || p >= 1) throw new ArgumentOutOfRangeException(nameof(p));
        double lo = 0, hi = 10;
        while (Cdf(hi, k, nu) < p) hi *= 2;
        for (int i = 0; i < 60 && hi - lo > 1e-9; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (Cdf(mid, k, nu) < p) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>ν = ∞ için aralık dağılımı: k φ(z)[Φ(z) − Φ(z − w)]^{k−1} integrali.</summary>
    private static double RangeCdfInfinite(double w, int k)
    {
        if (w <= 0) return 0;
        const double zLo = -8.5, zHi = 8.5;
        double total = 0, h = (zHi - zLo) / InnerPanels;
        for (int i = 0; i < InnerPanels; i++)
        {
            double a = zLo + i * h, b = a + h;
            total += GaussLegendreRule.Integrate(z =>
            {
                double inner = Normal.CDF(0, 1, z) - Normal.CDF(0, 1, z - w);
                return inner <= 0 ? 0 : k * Normal.PDF(0, 1, z) * Math.Pow(inner, k - 1);
            }, a, b, InnerOrder);
        }
        return Math.Clamp(total, 0, 1);
    }
}
