#nullable enable
using PTCalc.Core.Localization;
using MathNet.Numerics.Distributions;

namespace PTCalc.Core.Statistics;

/// <summary>Bir kalibrasyon standardı ölçümü ve fit'e göre değerleri.</summary>
public sealed record CalibrationPoint(
    double Conc, double Response, double Fitted, double Residual, double ResidualPct, double BackCalc, double AccuracyPct);

/// <summary>Aynı konsantrasyondaki tekrarların özeti.</summary>
public sealed record CalibrationLevel(double Conc, int N, double MeanResponse, double Sd, double RsdPct, double MeanAccuracyPct);

/// <summary>Bilinmeyen örnek için ters tahmin (Miller &amp; Miller).</summary>
public sealed record CalibrationUnknown(double Response, int Replicates, double Conc, double Se, double Lower, double Upper, bool OutsideRange);

/// <summary>Doğrusal kalibrasyon eğrisi sonucu.</summary>
public sealed record CalibrationResult(
    int N, int Levels, int Df,
    double Slope, double Intercept, double SeSlope, double SeIntercept,
    double SlopeLower, double SlopeUpper, double InterceptLower, double InterceptUpper,
    double InterceptT, double InterceptP,
    double R, double R2, double Syx, double TCrit,
    double Lod, double Loq,
    double MinConc, double MaxConc,
    IReadOnlyList<CalibrationPoint> Points,
    IReadOnlyList<CalibrationLevel> LevelSummary,
    double Alpha,
    IReadOnlyList<string> Flags)
{
    public double Predict(double conc) => Intercept + Slope * conc;

    /// <summary>
    /// Yanıtı ölçülen bilinmeyen için konsantrasyon ve güven aralığı; m = bilinmeyenin tekrar sayısı.
    /// s_x0 = (s_y/x / b) · √(1/m + 1/n + (y₀ − ȳ)² / (b² Σ(xᵢ − x̄)²)).
    /// </summary>
    public CalibrationUnknown Estimate(double response, int replicates = 1)
    {
        double x0 = (response - Intercept) / Slope;
        double xMean = Points.Average(p => p.Conc);
        double yMean = Points.Average(p => p.Response);
        double sxx = Points.Sum(p => (p.Conc - xMean) * (p.Conc - xMean));
        double se = Syx / Math.Abs(Slope) * Math.Sqrt(1.0 / Math.Max(1, replicates) + 1.0 / N + Math.Pow(response - yMean, 2) / (Slope * Slope * sxx));
        return new CalibrationUnknown(response, replicates, x0, se, x0 - TCrit * se, x0 + TCrit * se, x0 < MinConc || x0 > MaxConc);
    }
}

/// <summary>
/// Konsantrasyon–yanıt verisinden doğrusal kalibrasyon eğrisi (sıradan en küçük kareler; tekrarlar ayrı
/// gözlem olarak fit'e girer). LOD ve LOQ, ICH Q2(R2) "kalibrasyon eğrisinin standart sapması" yaklaşımıyla:
/// LOD = 3,3·σ/S, LOQ = 10·σ/S; σ = artık standart sapması s_y/x, S = eğim. Geri hesaplanan konsantrasyon
/// ve % doğruluk (geri hesap / nominal × 100) her standart için verilir.
/// </summary>
public static class CalibrationCurve
{
    public static CalibrationResult Fit(IReadOnlyList<double> conc, IReadOnlyList<double> response, double alpha = 0.05)
    {
        int n = conc.Count;
        if (n != response.Count) throw new ArgumentException(CoreText.T("Konsantrasyon ve yanıt sayıları eşleşmiyor."));
        var levels = conc.Distinct().OrderBy(c => c).ToList();
        if (levels.Count < 3)
            throw new ArgumentException(CoreText.T("Kalibrasyon için en az üç farklı konsantrasyon düzeyi gerekir."));
        if (n < 4) throw new ArgumentException(CoreText.T("En az dört gözlem gerekir."));

        double xm = conc.Average(), ym = response.Average();
        double sxx = 0, sxy = 0, syy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = conc[i] - xm, dy = response[i] - ym;
            sxx += dx * dx; sxy += dx * dy; syy += dy * dy;
        }
        double slope = sxy / sxx;
        double intercept = ym - slope * xm;
        int df = n - 2;

        double ssRes = 0;
        var fitted = new double[n];
        for (int i = 0; i < n; i++)
        {
            fitted[i] = intercept + slope * conc[i];
            ssRes += Math.Pow(response[i] - fitted[i], 2);
        }
        double syx = Math.Sqrt(ssRes / df);
        double seSlope = syx / Math.Sqrt(sxx);
        double seIntercept = syx * Math.Sqrt(1.0 / n + xm * xm / sxx);
        double r = syy > 0 ? sxy / Math.Sqrt(sxx * syy) : 0;
        double tCrit = StudentT.InvCDF(0, 1, df, 1 - alpha / 2);
        double tInt = seIntercept > 0 ? intercept / seIntercept : double.NaN;
        double pInt = double.IsNaN(tInt) ? double.NaN : 2 * (1 - StudentT.CDF(0, 1, df, Math.Abs(tInt)));

        double lod = 3.3 * syx / Math.Abs(slope);
        double loq = 10 * syx / Math.Abs(slope);

        var points = new List<CalibrationPoint>(n);
        for (int i = 0; i < n; i++)
        {
            double res = response[i] - fitted[i];
            double back = (response[i] - intercept) / slope;
            points.Add(new CalibrationPoint(conc[i], response[i], fitted[i], res,
                fitted[i] != 0 ? res / fitted[i] * 100 : double.NaN,
                back, conc[i] != 0 ? back / conc[i] * 100 : double.NaN));
        }

        var summary = levels.Select(c =>
        {
            var pts = points.Where(p => p.Conc == c).ToList();
            double mean = pts.Average(p => p.Response);
            double sd = pts.Count > 1 ? Math.Sqrt(pts.Sum(p => Math.Pow(p.Response - mean, 2)) / (pts.Count - 1)) : double.NaN;
            return new CalibrationLevel(c, pts.Count, mean, sd, mean != 0 ? sd / mean * 100 : double.NaN, pts.Average(p => p.AccuracyPct));
        }).ToList();

        var flags = new List<string>();
        double minC = levels[0], maxC = levels[^1];
        if (r * r < 0.99)
            flags.Add(CoreText.T("r² = {0:0.0000} < 0,99: doğrusallık zayıf. Aralığı daraltmayı, ağırlıklı regresyonu ya da başka bir modeli düşünün.", r * r));
        if (pInt < 0.05)
            flags.Add(CoreText.T("Kesim noktası sıfırdan anlamlı farklı (p = {0:0.0000}). Tek noktalı kalibrasyon ya da orijinden geçen doğru bu veri için uygun değildir.", pInt));
        if (loq > minC)
            flags.Add(CoreText.T("LOQ ({0:0.###}) en düşük standardın ({1:0.###}) üstünde: en düşük düzey nicel olarak güvenilir değildir ya da LOQ'yu deneysel olarak doğrulayın.", loq, minC));
        var bad = summary.Where(s => Math.Abs(s.MeanAccuracyPct - 100) > (s.Conc == minC ? 20 : 15)).Select(s => s.Conc).ToList();
        if (bad.Count > 0)
            flags.Add(CoreText.T("Ortalama geri hesap doğruluğu kabul sınırı dışında (±%15, en düşük düzeyde ±%20): {0}.", string.Join(", ", bad.Select(c => c.ToString("0.###")))));
        var wideRsd = summary.Where(s => s.N > 1 && s.RsdPct > 15).Select(s => s.Conc).ToList();
        if (wideRsd.Count > 0)
            flags.Add(CoreText.T("Tekrar %RSD'si %15'i aşan düzey(ler): {0}.", string.Join(", ", wideRsd.Select(c => c.ToString("0.###")))));
        if (summary.Any(s => s.N == 1))
            flags.Add(CoreText.T("Bazı düzeylerde tek ölçüm var; tekrar kesinliği (%RSD) hesaplanamaz."));
        if (levels.Count < 5)
            flags.Add(CoreText.T("ICH Q2(R2) doğrusallık için en az beş konsantrasyon düzeyi önerir; burada {0} düzey var.", levels.Count));

        return new CalibrationResult(n, levels.Count, df, slope, intercept, seSlope, seIntercept,
            slope - tCrit * seSlope, slope + tCrit * seSlope, intercept - tCrit * seIntercept, intercept + tCrit * seIntercept,
            tInt, pInt, r, r * r, syx, tCrit, lod, loq, minC, maxC, points, summary, alpha, flags);
    }
}
