#nullable enable
using MathNet.Numerics.Distributions;
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.KinetikAnalysis;

/// <summary>
/// Doz-yanıt verisi için kullanılacak bağlantı (link) fonksiyonu.
/// </summary>
public enum DoseResponseLink
{
    /// <summary>Logit link: p = 1 / (1 + e^-η). Klasik "logit" analizi.</summary>
    Logit = 1,

    /// <summary>Probit link: p = Φ(η) (standart normal CDF). Klasik Finney probit analizi.</summary>
    Probit = 2
}

/// <summary>
/// Klasik toksikoloji Probit ve Logit doz-yanıt analizini,
/// <b>Iteratively Reweighted Least Squares (IRLS / Fisher scoring)</b> yöntemiyle
/// çözer. Bu yöntem, klasik Finney probit tablolarının ("working probit",
/// ağırlık katsayıları) elle yaptığı ağırlıklı iterasyonun modern, tablosuz
/// karşılığıdır; her dozdaki hayvan sayısına (n) göre doğru binom
/// ağırlıklandırma yapar.
///
/// Model (log-doz ekseninde genelleştirilmiş lineer model):
///     η = β0 + β1·ln(x)
///     Logit  → p = 1 / (1 + e^-η)
///     Probit → p = Φ(η)
///
/// Letal doz: p = hedef → LDp = exp((link(p) - β0) / β1).
/// Örn. LD50 için link(0.5)=0 ⇒ LD50 = exp(-β0/β1).
/// </summary>
public static class ProbitLogitAnalyzer
{
    private const double Eps = 1e-10;
    private static readonly Normal StdNormal = new(0.0, 1.0);

    /// <summary>%95 güven sınırları için standart normal kuantil (≈1.96).</summary>
    private static readonly double Z975 = StdNormal.InverseCumulativeDistribution(0.975);

    /// <summary>
    /// Doz-yanıt verisini seçilen link (Probit/Logit) ile IRLS kullanarak fit eder.
    /// </summary>
    /// <param name="doses">Doz değerleri (x). Pozitif olmalıdır.</param>
    /// <param name="responses">Her dozdaki yanıt (ölüm) sayısı.</param>
    /// <param name="totals">Her dozdaki toplam denek (hayvan) sayısı.</param>
    /// <param name="link">Kullanılacak bağlantı fonksiyonu (Probit veya Logit).</param>
    public static ProbitLogitFitResult Fit(
        IReadOnlyList<double> doses,
        IReadOnlyList<double> responses,
        IReadOnlyList<double> totals,
        DoseResponseLink link)
    {
        if (doses.Count != responses.Count || doses.Count != totals.Count)
            throw new ArgumentException("doses/responses/totals length mismatch");

        // Geçerli noktalar: doz > 0 ve toplam denek > 0
        var pts = new List<(double lx, double p, double n)>();
        for (int i = 0; i < doses.Count; i++)
        {
            double x = doses[i], n = totals[i];
            if (x <= 0 || n <= 0) continue;
            double p = responses[i] / n;               // gözlenen oran
            p = Math.Clamp(p, 0.0, 1.0);
            pts.Add((Math.Log(x), p, n));
        }

        if (pts.Count < 3)
            throw new ArgumentException(CoreText.T("En az 3 pozitif dozlu veri noktası gereklidir."));

        // --- Başlangıç tahmini: gözlenen oranların link'i üzerinde ağırlıklı doğru ---
        double b0 = 0, b1 = 1;
        {
            // Basit lineer regresyon: link(p_clamped) ~ ln(x)
            var lxs = pts.Select(t => t.lx).ToArray();
            var etas = pts.Select(t => LinkFunction(Math.Clamp(t.p, 0.02, 0.98), link)).ToArray();
            double mlx = lxs.Average(), me = etas.Average();
            double sxx = 0, sxy = 0;
            for (int i = 0; i < lxs.Length; i++)
            {
                sxx += (lxs[i] - mlx) * (lxs[i] - mlx);
                sxy += (lxs[i] - mlx) * (etas[i] - me);
            }
            if (sxx > 0) { b1 = sxy / sxx; b0 = me - b1 * mlx; }
            if (!IsFinite(b0) || !IsFinite(b1) || b1 == 0) { b0 = 0; b1 = 1; }
        }

        bool converged = false;
        bool solved = false;   // en az bir IRLS adımı başarıyla çözüldü mü?

        // --- IRLS iterasyonları ---
        for (int iter = 0; iter < 100; iter++)
        {
            // Ağırlıklı normal denklem birikimleri (2x2 sistem)
            double sw = 0, swx = 0, swx2 = 0, swz = 0, swxz = 0;

            foreach (var (lx, p, n) in pts)
            {
                double eta = b0 + b1 * lx;
                double mu = MeanFunction(eta, link);          // tahmini oran
                mu = Math.Clamp(mu, Eps, 1 - Eps);
                double dmu = Derivative(eta, link);           // dμ/dη
                if (Math.Abs(dmu) < Eps) dmu = dmu < 0 ? -Eps : Eps;

                // Binom IRLS ağırlığı ve working response
                double w = n * dmu * dmu / (mu * (1 - mu));
                double z = eta + (p - mu) / dmu;

                sw += w;
                swx += w * lx;
                swx2 += w * lx * lx;
                swz += w * z;
                swxz += w * lx * z;
            }

            // 2x2 ağırlıklı en küçük kareler çözümü
            double det = sw * swx2 - swx * swx;
            if (Math.Abs(det) < 1e-15) break;

            double nb0 = (swz * swx2 - swx * swxz) / det;
            double nb1 = (sw * swxz - swx * swz) / det;

            if (!IsFinite(nb0) || !IsFinite(nb1)) break;

            double delta = Math.Abs(nb0 - b0) + Math.Abs(nb1 - b1);
            b0 = nb0;
            b1 = nb1;
            solved = true;

            if (delta < 1e-10) { converged = true; break; }
        }

        // Hiçbir IRLS adımı çözülemediyse (tekil tasarım matrisi; pratikte tüm dozların
        // aynı olması) β0/β1 hâlâ başlangıç değerleridir (0 ve 1). Bunlardan LD üretmek
        // exp(-0/1) = 1 gibi tamamen uydurma ama makul görünen bir doz verir; bu yüzden
        // sonucu hesaplanamamış olarak işaretle.
        if (!solved)
            return new ProbitLogitFitResult(
                link, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, false);

        // --- Uyum iyiliği (mortalite% 0-100 ekseninde R², diğer modellerle kıyaslanabilir) ---
        double rSquared = CalculateRSquared(pts, b0, b1, link);

        // --- Letal dozlar ---
        double ld50 = InverseDose(0.50, b0, b1, link);
        double ld90 = InverseDose(0.90, b0, b1, link);

        // --- Standart hatalar ve fidusiyal (Fieller) güven sınırları ---
        // Yalnızca yakınsamış fit için hesaplanır: yakınsamamışsa β0/β1 maksimum
        // olabilirlik tahmini değildir, dolayısıyla bilgi matrisi de doğru varyansı
        // vermez ve üretilecek güven aralığı yanıltıcı olur.
        double seB0 = double.NaN, seB1 = double.NaN;
        double ld50Lo = double.NaN, ld50Hi = double.NaN;
        double ld90Lo = double.NaN, ld90Hi = double.NaN;

        if (converged && TryVariance(pts, b0, b1, link, out double v00, out double v01, out double v11))
        {
            seB0 = Math.Sqrt(v00);
            seB1 = Math.Sqrt(v11);
            (ld50Lo, ld50Hi) = FiducialLimits(0.50, b0, b1, v00, v01, v11, link);
            (ld90Lo, ld90Hi) = FiducialLimits(0.90, b0, b1, v00, v01, v11, link);
        }

        return new ProbitLogitFitResult(
            link, b0, b1, rSquared, ld50, ld90, converged,
            seB0, seB1, ld50Lo, ld50Hi, ld90Lo, ld90Hi);
    }

    /// <summary>
    /// Fisher bilgi matrisinin tersi: β0/β1 için 2x2 kovaryans matrisi.
    /// IRLS döngüsünün her adımda biriktirdiği (Σw, Σw·x, Σw·x²) toplamlarının
    /// aynısıdır; burada son (yakınsamış) β değerlerinde bir kez hesaplanır.
    ///     I = [[Σw, Σw·x], [Σw·x, Σw·x²]],  V = I⁻¹
    /// </summary>
    private static bool TryVariance(
        List<(double lx, double p, double n)> pts, double b0, double b1, DoseResponseLink link,
        out double v00, out double v01, out double v11)
    {
        v00 = v01 = v11 = double.NaN;

        double sw = 0, swx = 0, swx2 = 0;
        foreach (var (lx, _, n) in pts)
        {
            double eta = b0 + b1 * lx;
            double mu = Math.Clamp(MeanFunction(eta, link), Eps, 1 - Eps);
            double dmu = Derivative(eta, link);
            if (Math.Abs(dmu) < Eps) dmu = dmu < 0 ? -Eps : Eps;

            double w = n * dmu * dmu / (mu * (1 - mu));
            sw += w;
            swx += w * lx;
            swx2 += w * lx * lx;
        }

        double det = sw * swx2 - swx * swx;
        if (!IsFinite(det) || Math.Abs(det) < 1e-15) return false;

        v00 = swx2 / det;
        v01 = -swx / det;
        v11 = sw / det;

        return IsFinite(v00) && IsFinite(v01) && IsFinite(v11) && v00 > 0 && v11 > 0;
    }

    /// <summary>
    /// Hedef oran p için dozun fidusiyal (Fieller) %95 güven sınırları — klasik
    /// Finney probit analizinin bildirdiği sınırlar bunlardır.
    ///
    /// m = log-doz olmak üzere β0 + m·β1 − link(p) = 0 kökü aranır. Fieller eşitsizliği
    ///     (β0 + m·β1 − link(p))² ≤ z²·Var(β0 + m·β1)
    /// m cinsinden bir kuadratiğe indirgenir (c0 = β0 − link(p)):
    ///     m²(β1² − z²·v11) + 2m(c0·β1 − z²·v01) + (c0² − z²·v00) ≤ 0
    /// Baş katsayı ≤ 0 ise — Finney'in g ≥ 1 durumu — eğim sıfırdan anlamlı biçimde
    /// farklı değildir; sınırlar sınırsızdır ve sayı yerine NaN döndürülür.
    /// </summary>
    private static (double lo, double hi) FiducialLimits(
        double p, double b0, double b1, double v00, double v01, double v11, DoseResponseLink link)
    {
        double z2 = Z975 * Z975;
        double c0 = b0 - LinkFunction(p, link);

        double a = b1 * b1 - z2 * v11;          // g ≥ 1 ⇔ a ≤ 0 ⇒ sınırlar sınırsız
        if (a <= 0) return (double.NaN, double.NaN);

        double b = 2.0 * (c0 * b1 - z2 * v01);
        double c = c0 * c0 - z2 * v00;

        double disc = b * b - 4 * a * c;
        if (disc < 0) return (double.NaN, double.NaN);

        double sq = Math.Sqrt(disc);
        double m1 = (-b - sq) / (2 * a);
        double m2 = (-b + sq) / (2 * a);

        double lo = Math.Exp(Math.Min(m1, m2));
        double hi = Math.Exp(Math.Max(m1, m2));
        return IsFinite(lo) && IsFinite(hi) ? (lo, hi) : (double.NaN, double.NaN);
    }

    /// <summary>
    /// Verilen doz için tahmini mortalite yüzdesi (0-100).
    /// Grafik çizimi ve tahmin için kullanılır.
    /// </summary>
    public static double PredictPercent(double dose, double b0, double b1, DoseResponseLink link)
    {
        if (dose <= 0) return 0;
        double eta = b0 + b1 * Math.Log(dose);
        return 100.0 * MeanFunction(eta, link);
    }

    /// <summary>Hedef oran (p, 0-1) için doz döndürür: x = exp((link(p) - β0) / β1).</summary>
    public static double InverseDose(double p, double b0, double b1, DoseResponseLink link)
    {
        if (p <= 0 || p >= 1) return double.NaN;
        if (!IsFinite(b1) || Math.Abs(b1) < Eps) return double.NaN;

        double eta = LinkFunction(p, link);
        double x = Math.Exp((eta - b0) / b1);
        return IsFinite(x) && x > 0 ? x : double.NaN;
    }

    // ---- Link matematiği ----

    /// <summary>link(p): orandan lineer öngörücüye (η).</summary>
    private static double LinkFunction(double p, DoseResponseLink link)
    {
        p = Math.Clamp(p, Eps, 1 - Eps);
        return link switch
        {
            DoseResponseLink.Logit => Math.Log(p / (1 - p)),
            DoseResponseLink.Probit => StdNormal.InverseCumulativeDistribution(p),
            _ => throw new ArgumentOutOfRangeException(nameof(link))
        };
    }

    /// <summary>link⁻¹(η) = μ: lineer öngörücüden orana (0-1).</summary>
    private static double MeanFunction(double eta, DoseResponseLink link) => link switch
    {
        DoseResponseLink.Logit => 1.0 / (1.0 + Math.Exp(-eta)),
        DoseResponseLink.Probit => StdNormal.CumulativeDistribution(eta),
        _ => throw new ArgumentOutOfRangeException(nameof(link))
    };

    /// <summary>dμ/dη.</summary>
    private static double Derivative(double eta, DoseResponseLink link)
    {
        switch (link)
        {
            case DoseResponseLink.Logit:
                // logistic pdf: μ(1-μ)
                double m = 1.0 / (1.0 + Math.Exp(-eta));
                return m * (1 - m);
            case DoseResponseLink.Probit:
                // normal pdf: φ(η)
                return StdNormal.Density(eta);
            default:
                throw new ArgumentOutOfRangeException(nameof(link));
        }
    }

    private static double CalculateRSquared(
        List<(double lx, double p, double n)> pts, double b0, double b1, DoseResponseLink link)
    {
        // Gözlenen/tahmini mortalite yüzdeleri üzerinden R²
        double meanObs = pts.Average(t => t.p * 100.0);
        double ssTot = pts.Sum(t => Math.Pow(t.p * 100.0 - meanObs, 2));
        if (ssTot <= 0) return 0;

        double ssRes = 0;
        foreach (var (lx, p, _) in pts)
        {
            double pred = 100.0 * MeanFunction(b0 + b1 * lx, link);
            ssRes += Math.Pow(p * 100.0 - pred, 2);
        }
        return 1 - (ssRes / ssTot);
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}

/// <summary>
/// Probit/Logit IRLS fit sonucu. Model: η = β0 + β1·ln(doz).
///
/// Standart hatalar (<see cref="SeB0"/>, <see cref="SeB1"/>) ve LD güven sınırları
/// yalnızca fit yakınsadığında doldurulur; aksi hâlde NaN'dır. Güven sınırları
/// Fieller (fidusiyal) yöntemiyle hesaplanır ve eğim anlamsızsa (Finney'in g ≥ 1
/// durumu) sınırsız oldukları için NaN döner.
/// </summary>
public sealed record ProbitLogitFitResult(
    DoseResponseLink Link,
    double B0,
    double B1,
    double RSquared,
    double LD50,
    double LD90,
    bool Converged,
    double SeB0 = double.NaN,
    double SeB1 = double.NaN,
    double LD50Lower = double.NaN,
    double LD50Upper = double.NaN,
    double LD90Lower = double.NaN,
    double LD90Upper = double.NaN);
