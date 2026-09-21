#nullable enable
using PTCalc.Core.Localization;
using MathNet.Numerics.Distributions;

namespace PTCalc.Core.Statistics;

/// <summary>Bir grubun tanımlayıcı istatistikleri.</summary>
public sealed record AnovaGroup(string Name, int N, double Mean, double Sd, double Se, double Min, double Max);

/// <summary>Tukey HSD (Tukey-Kramer) ikili karşılaştırması. Fark = ortalama(A) − ortalama(B).</summary>
public sealed record TukeyPair(
    string A, string B, double Diff, double Se, double Q, double P, double Lower, double Upper, bool Significant);

/// <summary>Tek yönlü ANOVA sonucu.</summary>
public sealed record OneWayAnovaResult(
    IReadOnlyList<AnovaGroup> Groups,
    double GrandMean,
    double SsBetween, double SsWithin, double SsTotal,
    int DfBetween, int DfWithin,
    double MsBetween, double MsWithin,
    double F, double P,
    double Eta2, double Omega2,
    double LeveneF, double LeveneP,
    double Alpha, double QCritical,
    IReadOnlyList<TukeyPair> Tukey,
    IReadOnlyList<string> Flags)
{
    public bool Significant => P < Alpha;
}

/// <summary>
/// Tek yönlü varyans analizi + Levene varyans homojenliği + Tukey HSD post-hoc.
///
/// <para><b>Levene</b> ortalama-merkezli (Levene 1960) hesaplanır; t-testi sayfasıyla aynı tanım.
/// <b>Tukey</b> eşit olmayan n'lerde Tukey-Kramer düzeltmesini kullanır: SE = √(MSw/2 · (1/nᵢ + 1/nⱼ)),
/// q = |x̄ᵢ − x̄ⱼ| / SE, p = P(Q &gt; q | k, ν). Güven aralığı q_crit·SE ile kurulur.</para>
/// </summary>
public static class OneWayAnova
{
    public static OneWayAnovaResult Analyze(
        IReadOnlyList<IReadOnlyList<double>> groups,
        IReadOnlyList<string>? names = null,
        double alpha = 0.05)
    {
        var flags = new List<string>();
        var kept = new List<(string Name, IReadOnlyList<double> Values)>();
        for (int i = 0; i < groups.Count; i++)
        {
            string name = names is not null && i < names.Count && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : $"G{i + 1}";
            if (groups[i].Count == 0) continue;
            if (groups[i].Count < 2)
            {
                flags.Add(CoreText.T("{0} grubunda tek gözlem var; grup analize alınmadı.", name));
                continue;
            }
            kept.Add((name, groups[i]));
        }
        if (kept.Count < 2)
            throw new ArgumentException(CoreText.T("ANOVA için en az iki gruba ve her grupta en az iki gözleme gerek var."));

        int k = kept.Count;
        int n = kept.Sum(g => g.Values.Count);
        double grand = kept.Sum(g => g.Values.Sum()) / n;

        var desc = kept.Select(g =>
        {
            int ni = g.Values.Count;
            double mean = g.Values.Average();
            double variance = g.Values.Sum(v => (v - mean) * (v - mean)) / (ni - 1);
            double sd = Math.Sqrt(variance);
            return new AnovaGroup(g.Name, ni, mean, sd, sd / Math.Sqrt(ni), g.Values.Min(), g.Values.Max());
        }).ToList();

        double ssb = desc.Sum(d => d.N * (d.Mean - grand) * (d.Mean - grand));
        double ssw = kept.Zip(desc, (g, d) => g.Values.Sum(v => (v - d.Mean) * (v - d.Mean))).Sum();
        double sst = ssb + ssw;
        int dfb = k - 1, dfw = n - k;
        double msb = ssb / dfb, msw = ssw / dfw;

        double f, p;
        if (msw <= 0)
        {
            flags.Add(CoreText.T("Grup içi varyans sıfır: bütün gözlemler kendi grup ortalamasına eşit. F tanımsız."));
            f = double.PositiveInfinity; p = 0;
        }
        else
        {
            f = msb / msw;
            p = 1 - FisherSnedecor.CDF(dfb, dfw, f);
        }

        double eta2 = sst > 0 ? ssb / sst : 0;
        double omega2 = sst + msw > 0 ? Math.Max(0, (ssb - dfb * msw) / (sst + msw)) : 0;

        var (levF, levP) = Levene(kept.Select(g => g.Values).ToList());
        if (levP < alpha)
            flags.Add(CoreText.T("Levene testi anlamlı (p = {0:0.0000}): varyanslar homojen değil. Klasik F testi ve Tukey HSD eşit varyans varsayar; sonucu ihtiyatla yorumlayın (Welch ANOVA / Games-Howell düşünülebilir).", levP));

        double qCrit = msw > 0 ? StudentizedRange.InvCdf(1 - alpha, k, dfw) : double.NaN;
        var tukey = new List<TukeyPair>();
        if (msw > 0)
        {
            for (int i = 0; i < k; i++)
                for (int j = i + 1; j < k; j++)
                {
                    double diff = desc[i].Mean - desc[j].Mean;
                    double se = Math.Sqrt(msw / 2 * (1.0 / desc[i].N + 1.0 / desc[j].N));
                    double q = Math.Abs(diff) / se;
                    double pq = StudentizedRange.Sf(q, k, dfw);
                    double half = qCrit * se;
                    tukey.Add(new TukeyPair(desc[i].Name, desc[j].Name, diff, se, q, pq, diff - half, diff + half, pq < alpha));
                }
        }

        if (desc.Min(d => d.N) < 3)
            flags.Add(CoreText.T("Bazı gruplarda yalnız iki gözlem var; varyans tahminleri kararsızdır."));

        return new OneWayAnovaResult(desc, grand, ssb, ssw, sst, dfb, dfw, msb, msw, f, p, eta2, omega2,
            levF, levP, alpha, qCrit, tukey, flags);
    }

    /// <summary>Levene testi (ortalama-merkezli): |xᵢⱼ − x̄ᵢ| sapmaları üzerinde tek yönlü ANOVA.</summary>
    public static (double F, double P) Levene(IReadOnlyList<IReadOnlyList<double>> groups)
    {
        var dev = groups.Select(g => { double m = g.Average(); return g.Select(v => Math.Abs(v - m)).ToList(); }).ToList();
        int k = dev.Count, n = dev.Sum(d => d.Count);
        double grand = dev.Sum(d => d.Sum()) / n;
        double ssb = dev.Sum(d => d.Count * Math.Pow(d.Average() - grand, 2));
        double ssw = dev.Sum(d => { double m = d.Average(); return d.Sum(v => (v - m) * (v - m)); });
        int dfb = k - 1, dfw = n - k;
        if (ssw <= 0 || dfw <= 0) return (double.NaN, double.NaN);
        double f = (ssb / dfb) / (ssw / dfw);
        return (f, 1 - FisherSnedecor.CDF(dfb, dfw, f));
    }
}
