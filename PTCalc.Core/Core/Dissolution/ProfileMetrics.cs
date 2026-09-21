#nullable enable
using PTCalc.Core.Localization;

namespace PTCalc.Core.Dissolution;

/// <summary>
/// Model-bağımsız profil ölçütleri (Costa &amp; Sousa Lobo 2001, §2.9 "Other release parameters").
///
/// Bunlar hiçbir modele fit gerektirmez — doğrudan ham (t, F) profilinden hesaplanır ve
/// formülasyonları tek sayıyla karşılaştırmaya yarar. Şartnamede yer almazlar; literatürden
/// eklenmiştir.
/// </summary>
public sealed record ProfileSummary(
    double Auc,      // eğri altı alan (%·zaman)
    double De,       // dissolüsyon etkinliği (%)
    double Mdt,      // ortalama dissolüsyon süresi (zaman)
    double TLast,    // profilin son zaman noktası
    double FLast);   // son zaman noktasındaki salım (%)

public static class ProfileMetrics
{
    /// <summary>
    /// Trapez kuralıyla eğri altı alan (AUC). Profil t=0, F=0'dan başlatılır
    /// (ilk ölçüm noktasından önceki alanı da kapsasın diye).
    /// </summary>
    public static double Auc(IReadOnlyList<double> t, IReadOnlyList<double> f)
    {
        var (ts, fs) = WithOrigin(t, f);
        double area = 0;
        for (int i = 1; i < ts.Count; i++)
            area += (ts[i] - ts[i - 1]) * (fs[i] + fs[i - 1]) / 2.0;
        return area;
    }

    /// <summary>
    /// Dissolüsyon etkinliği (Costa Eq. 41):
    /// <c>DE = ∫₀ᵗ y·dt / (y₁₀₀ · t) × 100</c> — eğri altı alanın, aynı sürede %100 salımı
    /// temsil eden dikdörtgenin alanına oranı.
    /// </summary>
    /// <param name="yHundred">Dikdörtgenin yüksekliği; varsayılan %100.</param>
    public static double De(IReadOnlyList<double> t, IReadOnlyList<double> f, double yHundred = 100.0)
    {
        if (t.Count == 0) return double.NaN;
        double tMax = t.Max();
        double denom = yHundred * tMax;
        return denom > 0 ? Auc(t, f) / denom * 100.0 : double.NaN;
    }

    /// <summary>
    /// Ortalama dissolüsyon süresi (Costa Eq. 42):
    /// <c>MDT = Σ(t̂ⱼ · ΔMⱼ) / Σ ΔMⱼ</c>, burada <c>t̂ⱼ = (tⱼ + tⱼ₋₁)/2</c> ve
    /// <c>ΔMⱼ</c> = tⱼ₋₁ ile tⱼ arasında çözünen ek ilaç miktarı.
    /// </summary>
    public static double Mdt(IReadOnlyList<double> t, IReadOnlyList<double> f)
    {
        var (ts, fs) = WithOrigin(t, f);
        double num = 0, den = 0;
        for (int i = 1; i < ts.Count; i++)
        {
            double dM = fs[i] - fs[i - 1];
            double tMid = (ts[i] + ts[i - 1]) / 2.0;
            num += tMid * dM;
            den += dM;
        }
        return Math.Abs(den) > 1e-12 ? num / den : double.NaN;
    }

    /// <summary>Tüm profil ölçütlerini tek seferde hesaplar.</summary>
    public static ProfileSummary Summarize(IReadOnlyList<double> t, IReadOnlyList<double> f)
    {
        if (t.Count != f.Count) throw new ArgumentException(CoreText.T("t/F uzunlukları eşleşmiyor"));
        if (t.Count == 0) return new ProfileSummary(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

        // Son nokta = en büyük zamandaki gözlem (veri sıralı gelmeyebilir)
        int last = 0;
        for (int i = 1; i < t.Count; i++) if (t[i] > t[last]) last = i;

        return new ProfileSummary(
            Auc: Auc(t, f),
            De: De(t, f),
            Mdt: Mdt(t, f),
            TLast: t[last],
            FLast: f[last]);
    }

    /// <summary>Profili t'ye göre sıralar ve başına (0, 0) ekler (yoksa).</summary>
    private static (List<double> T, List<double> F) WithOrigin(
        IReadOnlyList<double> t, IReadOnlyList<double> f)
    {
        var pairs = t.Zip(f, (ti, fi) => (ti, fi))
                     .Where(p => !double.IsNaN(p.ti) && !double.IsNaN(p.fi))
                     .OrderBy(p => p.ti)
                     .ToList();

        var ts = new List<double>();
        var fs = new List<double>();
        if (pairs.Count == 0 || pairs[0].ti > 0) { ts.Add(0); fs.Add(0); }
        foreach (var (ti, fi) in pairs) { ts.Add(ti); fs.Add(fi); }
        return (ts, fs);
    }
}
