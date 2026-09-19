#nullable enable
using MathNet.Numerics.LinearAlgebra;

namespace EgePharmTech.Core.Dissolution;

/// <summary>
/// Doğrusal tohum (initial guess) için regresyon yardımcıları (şartname §5).
///
/// Kaldırılan eski motorun regresyon yardımcısı yalnız <b>kesişimli</b>
/// OLS yapar. Şartname §6.1 uyarınca orijinden geçmesi gereken modeller için
/// <see cref="ThroughOrigin"/>, çok katsayılı modeller (Makoid-Banakar, Peppas-Sahlin,
/// Peppas-Sahlin-2) için <see cref="Multi"/> gerekir.
/// </summary>
public static class SeedRegression
{
    /// <summary>Kesişimli OLS: y = a + b·x. Döndürür (Intercept a, Slope b).</summary>
    public static (double Intercept, double Slope) WithIntercept(
        IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        int n = x.Count;
        if (n < 2) return (0, 0);

        double sx = 0, sy = 0, sxy = 0, sx2 = 0;
        for (int i = 0; i < n; i++)
        {
            sx += x[i]; sy += y[i];
            sxy += x[i] * y[i]; sx2 += x[i] * x[i];
        }

        double denom = n * sx2 - sx * sx;
        double b = Math.Abs(denom) < 1e-15 ? 0 : (n * sxy - sx * sy) / denom;
        double a = (sy - b * sx) / n;
        return (a, b);
    }

    /// <summary>
    /// Kesişimsiz (orijinden geçen) OLS: y = k·x → k = Σxy / Σx².
    /// Şartname §6.1: Zero-order, First-order, Higuchi, Hixson-Crowell taban biçimleri.
    /// </summary>
    public static double ThroughOrigin(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double sxy = 0, sx2 = 0;
        for (int i = 0; i < x.Count; i++)
        {
            sxy += x[i] * y[i];
            sx2 += x[i] * x[i];
        }
        return Math.Abs(sx2) < 1e-15 ? 0 : sxy / sx2;
    }

    /// <summary>
    /// Çok değişkenli OLS (Excel LinEst muadili). <paramref name="columns"/> her biri bir
    /// bağımsız değişken sütunu. <paramref name="withIntercept"/> true ise sabit terim
    /// sonuncu eleman olarak döner.
    /// </summary>
    /// <returns>Katsayılar; sıra columns ile aynı, kesişim istenirse en sonda.</returns>
    public static double[] Multi(
        IReadOnlyList<double[]> columns,
        IReadOnlyList<double> y,
        bool withIntercept)
    {
        int n = y.Count;
        int k = columns.Count + (withIntercept ? 1 : 0);
        if (n < k) return new double[k];

        var design = Matrix<double>.Build.Dense(n, k, (i, j) =>
            withIntercept && j == columns.Count ? 1.0 : columns[j][i]);
        var yVec = Vector<double>.Build.Dense(y.ToArray());

        try
        {
            // QR ile en küçük kareler çözümü (normal denklemlerden sayısal olarak daha kararlı)
            return design.QR().Solve(yVec).ToArray();
        }
        catch
        {
            return new double[k];
        }
    }
}
