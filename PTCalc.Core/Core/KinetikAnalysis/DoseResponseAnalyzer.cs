#nullable enable
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using PTCalc.Core.Localization;

namespace PTCalc.Core.KinetikAnalysis;

/// <summary>
/// Doz-yanıt (dose-response) verilerini 4 parametreli lojistik (sigmoid / Hill)
/// modeliyle Marquardt-Levenberg (Levenberg-Marquardt) iteratif yöntemiyle fit eder
/// ve LD50 / LD90 gibi letal doz değerlerini hesaplar.
///
/// Model (artan sigmoid, standart Hill formu):
///     y = A + (D - A) / (1 + (C / x)^B)
/// A = alt asimptot (minimum yanıt), D = üst asimptot (maksimum yanıt),
/// C = EC50 (yanıtın orta noktaya ulaştığı doz), B = Hill eğimi (pozitif).
///
/// Kinetik modüldeki (Weibull, Korsmeyer-Peppas vb.) doğrusal-olmayan fit ile
/// aynı çözücüyü (LevenbergMarquardtMinimizer) kullanır; motor bilinçli olarak
/// bu tek katmanda birleştirilmiştir.
/// </summary>
public static class DoseResponseAnalyzer
{
    /// <summary>
    /// Doz-yanıt verisini 4PL lojistik modele Marquardt-Levenberg yöntemiyle fit eder.
    /// </summary>
    /// <param name="doses">Doz değerleri (x, örn. mg/kg). Pozitif olmalıdır.</param>
    /// <param name="mortalities">İlgili yanıt/mortalite yüzdeleri (y, 0-100).</param>
    public static DoseResponseFitResult FitLogistic(
        IReadOnlyList<double> doses,
        IReadOnlyList<double> mortalities)
    {
        if (doses.Count != mortalities.Count)
            throw new ArgumentException("doses/mortalities length mismatch");
        if (doses.Count < 3)
            throw new ArgumentException(CoreText.T("En az 3 veri noktası gereklidir."));

        // Dozlara göre sırala (interpolasyon ve başlangıç tahmini için gerekli)
        var pairs = doses.Zip(mortalities, (x, y) => (x, y))
                         .Where(p => p.x > 0)
                         .OrderBy(p => p.x)
                         .ToArray();

        if (pairs.Length < 3)
            throw new ArgumentException(CoreText.T("En az 3 pozitif dozlu veri noktası gereklidir."));

        var x = pairs.Select(p => p.x).ToArray();
        var y = pairs.Select(p => p.y).ToArray();

        // --- Akıllı başlangıç tahminleri ---
        double a0 = Math.Max(0, y.Min());
        double d0 = Math.Min(100, y.Max());
        if (d0 <= a0) d0 = a0 + 1;         // dejenerasyonu önle
        double c0 = EstimateEc50(x, y);    // EC50 için lineer interpolasyon tahmini
        double b0 = 1.0;                   // Hill eğimi başlangıcı

        double a = a0, b = b0, c = c0, d = d0;
        bool converged = false;

        try
        {
            // Kinetik modülüyle aynı çözücü ve toleranslar
            var solver = new LevenbergMarquardtMinimizer(
                gradientTolerance: 1e-10,
                stepTolerance: 1e-10,
                functionTolerance: 1e-10,
                maximumIterations: 1000);

            var xVec = Vector<double>.Build.Dense(x);
            var yVec = Vector<double>.Build.Dense(y);

            // p[0]=A, p[1]=B, p[2]=C, p[3]=D
            Func<Vector<double>, Vector<double>, Vector<double>> model =
                (p, xs) => xs.Map(xi => Logistic(xi, p[0], p[1], p[2], p[3]));

            var initial = Vector<double>.Build.Dense(new[] { a0, b0, c0, d0 });
            var objective = ObjectiveFunction.NonlinearModel(model, xVec, yVec);
            var result = solver.FindMinimum(objective, initial);

            var mp = result.MinimizingPoint;
            double fa = mp[0], fb = mp[1], fc = mp[2], fd = mp[3];

            // Fiziksel/mantıksal geçerlilik kontrolü (EC50>0, eğim anlamlı, artan eğri)
            if (IsFinite(fa) && IsFinite(fb) && IsFinite(fc) && IsFinite(fd)
                && fc > 0 && Math.Abs(fb) > 1e-6 && fd > fa)
            {
                a = fa; b = fb; c = fc; d = fd;
                converged = true;
            }
        }
        catch
        {
            // Yakınsama başarısız → başlangıç tahminleriyle devam (LD değerleri için
            // aşağıda ham veri interpolasyonu fallback olarak devreye girer).
        }

        double rSquared = CalculateRSquared(x, y, a, b, c, d);

        double ld50 = InverseLogistic(50, a, b, c, d);
        double ld90 = InverseLogistic(90, a, b, c, d);

        // Model tersine fonksiyonu geçersizse (asimptot dışı hedef vb.)
        // ham veriden lineer interpolasyonla tahmin et.
        if (double.IsNaN(ld50)) ld50 = InterpolateDose(x, y, 50);
        if (double.IsNaN(ld90)) ld90 = InterpolateDose(x, y, 90);

        return new DoseResponseFitResult(a, b, c, d, rSquared, ld50, ld90, converged);
    }

    /// <summary>4PL lojistik model: y = A + (D - A) / (1 + (C/x)^B).</summary>
    public static double Logistic(double x, double a, double b, double c, double d)
    {
        if (x <= 0) return a;
        return a + (d - a) / (1 + Math.Pow(c / x, b));
    }

    /// <summary>
    /// Lojistik eğrinin tersi: hedef yanıt (%) için doz döndürür.
    /// x = C * ((y - A) / (D - y))^(1/B). Geçersizse NaN.
    /// </summary>
    public static double InverseLogistic(double y, double a, double b, double c, double d)
    {
        double lo = Math.Min(a, d), hi = Math.Max(a, d);
        if (y <= lo || y >= hi) return double.NaN;

        double ratio = (y - a) / (d - y);
        if (ratio <= 0) return double.NaN;

        double x = c * Math.Pow(ratio, 1.0 / b);
        return IsFinite(x) && x > 0 ? x : double.NaN;
    }

    /// <summary>
    /// EC50 başlangıç tahmini: yanıtın alt/üst asimptot ortasını geçtiği dozu
    /// lineer interpolasyonla bulur; bulunamazsa orta dozu döndürür.
    /// </summary>
    private static double EstimateEc50(double[] x, double[] y)
    {
        double target = (y.Min() + y.Max()) / 2.0;
        double interp = InterpolateDose(x, y, target);
        if (!double.IsNaN(interp) && interp > 0) return interp;

        return x[x.Length / 2];
    }

    /// <summary>
    /// Ham veri üzerinde hedef yanıt için lineer interpolasyonla doz tahmini.
    /// Model fiti mümkün olmadığında fallback olarak kullanılır.
    /// </summary>
    private static double InterpolateDose(double[] x, double[] y, double target)
    {
        for (int i = 0; i < x.Length - 1; i++)
        {
            double y0 = y[i], y1 = y[i + 1];
            if ((y0 <= target && target <= y1) || (y1 <= target && target <= y0))
            {
                if (Math.Abs(y1 - y0) < 1e-12) return x[i];
                double t = (target - y0) / (y1 - y0);
                return x[i] + t * (x[i + 1] - x[i]);
            }
        }
        return double.NaN;
    }

    private static double CalculateRSquared(double[] x, double[] y, double a, double b, double c, double d)
    {
        double mean = y.Average();
        double ssTot = y.Sum(v => (v - mean) * (v - mean));
        if (ssTot <= 0) return 0;

        double ssRes = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double e = y[i] - Logistic(x[i], a, b, c, d);
            ssRes += e * e;
        }
        return 1 - (ssRes / ssTot);
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}

/// <summary>
/// 4PL lojistik doz-yanıt fit sonucu. A=Min, B=Hill eğimi, C=EC50, D=Max.
/// </summary>
public sealed record DoseResponseFitResult(
    double A,
    double B,
    double C,
    double D,
    double RSquared,
    double LD50,
    double LD90,
    bool Converged);
