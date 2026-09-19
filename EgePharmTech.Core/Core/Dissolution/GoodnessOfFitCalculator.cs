#nullable enable
namespace EgePharmTech.Core.Dissolution;

/// <summary>
/// İyilik-uyum hesapları (şartname §3).
///
/// <b>Kritik kural:</b> ŷ her zaman modelin orijinal (geri-dönüştürülmüş) F(%) biçiminden
/// gelir. Doğrusallaştırılmış uzaydaki artıklar KULLANILMAZ — eski motorun
/// KP/RRSBW için 11000+ SKT üretmesinin sebebi tam olarak buydu.
/// </summary>
public static class GoodnessOfFitCalculator
{
    /// <summary>Ağırlık vektörü (şartname §1.6).</summary>
    public static double[] Weights(IReadOnlyList<double> y, Weighting w)
    {
        var res = new double[y.Count];
        for (int i = 0; i < y.Count; i++)
        {
            res[i] = w switch
            {
                Weighting.InvY => Math.Abs(y[i]) > 1e-12 ? 1.0 / Math.Abs(y[i]) : 1.0,
                Weighting.InvY2 => Math.Abs(y[i]) > 1e-12 ? 1.0 / (y[i] * y[i]) : 1.0,
                _ => 1.0
            };
        }
        return res;
    }

    /// <summary>Ağırlıklı artık kareler toplamı: SSR = Σ wᵢ(yᵢ − ŷᵢ)². Optimizasyon hedefi.</summary>
    public static double Ssr(IReadOnlyList<double> y, IReadOnlyList<double> yHat, IReadOnlyList<double> w)
    {
        double s = 0;
        for (int i = 0; i < y.Count; i++)
        {
            double e = y[i] - yHat[i];
            s += w[i] * e * e;
        }
        return s;
    }

    /// <summary>
    /// Tüm GoF ölçütlerini hesaplar. <paramref name="p"/> = model parametre sayısı.
    /// </summary>
    public static GoodnessOfFit Compute(
        IReadOnlyList<double> y,
        IReadOnlyList<double> yHat,
        int p,
        Weighting weighting = Weighting.None)
    {
        int n = y.Count;
        var w = Weights(y, weighting);

        double ss = Ssr(y, yHat, w);

        // Ağırlıksız SS (w=1 ise WSS == SS)
        double ssUnweighted = 0;
        for (int i = 0; i < n; i++)
        {
            double e = y[i] - yHat[i];
            ssUnweighted += e * e;
        }

        double mean = y.Average();
        double ssTot = 0;
        for (int i = 0; i < n; i++)
        {
            double d = y[i] - mean;
            ssTot += w[i] * d * d;
        }

        int dof = n - p;
        double rsqr = ssTot > 0 ? 1 - ss / ssTot : 0;
        double rsqrAdj = dof > 0 && ssTot > 0
            ? 1 - (1 - rsqr) * (n - 1) / (double)dof
            : rsqr;

        double mse = dof > 0 ? ss / dof : double.NaN;
        double rmse = dof > 0 ? Math.Sqrt(mse) : double.NaN;

        // Mükemmel fit (SS = 0) için ln(0) = −∞ olur. −∞ AIC sıralamada en başa gelir
        // ama Akaike ağırlığı exp(−(AIC − AIC_min)/2) = exp(NaN) → NaN; UI'da o model "-"
        // gösterilirken ikinci sıradaki model %99 ağırlık alır. SS'i çok küçük bir tabana
        // sabitleyerek AIC/MSC sonlu tutulur; gerçek verideki SS'ler etkilenmez.
        const double ssFloor = 1e-300;
        double ssForLog = Math.Max(ss, ssFloor);

        // AIC = N·ln(SSR) + 2p   (küçük daha iyi)
        double aic = n * Math.Log(ssForLog) + 2 * p;

        // AICc = AIC + 2p(p+1)/(N−p−1)  — az veri / çok parametre durumunda
        double aicc = (n - p - 1) > 0
            ? aic + 2.0 * p * (p + 1) / (n - p - 1)
            : double.NaN;

        // MSC = ln(SStot / SSR) − 2p/N  (büyük daha iyi)
        double msc = ssTot > 0
            ? Math.Log(ssTot / ssForLog) - 2.0 * p / n
            : double.NaN;

        double r = Pearson(y, yHat);

        return new GoodnessOfFit(
            N: n, Dof: dof, R: r, Rsqr: rsqr, RsqrAdj: rsqrAdj,
            Mse: mse, RmsE: rmse, SS: ssUnweighted, WSS: ss,
            Aic: aic, AicC: aicc, Msc: msc, Weighting: weighting);
    }

    /// <summary>Pearson korelasyonu: R = corr(y, ŷ).</summary>
    public static double Pearson(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        if (n < 2) return 0;

        double ma = a.Average(), mb = b.Average();
        double sab = 0, saa = 0, sbb = 0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - ma, db = b[i] - mb;
            sab += da * db;
            saa += da * da;
            sbb += db * db;
        }
        double denom = Math.Sqrt(saa * sbb);
        return denom > 0 ? sab / denom : 0;
    }
}
