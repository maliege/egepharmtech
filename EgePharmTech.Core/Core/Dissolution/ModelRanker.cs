#nullable enable
namespace EgePharmTech.Core.Dissolution;

/// <summary>
/// Model sıralaması (şartname §1.5).
///
/// Sıralama <b>AIC</b> ile yapılır — parametre sayısını cezalandırır. Ham R² veya SS
/// tek başına kullanılmaz: daha çok parametreli model her zaman daha düşük SS verir,
/// bu yüzden SS ile sıralamak karmaşık modelleri haksız yere öne çıkarır.
/// MSC ikincil ölçüttür (büyük = iyi).
/// </summary>
public static class ModelRanker
{
    /// <summary>AIC artan (küçük en iyi); eşitlikte MSC azalan (büyük en iyi).</summary>
    public static IReadOnlyList<ModelFit> RankByAic(IEnumerable<ModelFit> fits)
        => fits
            .OrderBy(f => double.IsNaN(f.Gof.Aic) ? double.MaxValue : f.Gof.Aic)
            .ThenByDescending(f => double.IsNaN(f.Gof.Msc) ? double.MinValue : f.Gof.Msc)
            .ToArray();

    /// <summary>
    /// Akaike ağırlıkları: modelin "en iyi" olma göreli olasılığı.
    /// wᵢ = exp(−Δᵢ/2) / Σexp(−Δⱼ/2), Δᵢ = AICᵢ − AIC_min.
    /// </summary>
    public static IReadOnlyDictionary<string, double> AkaikeWeights(IEnumerable<ModelFit> fits)
    {
        var list = fits.Where(f => !double.IsNaN(f.Gof.Aic) && !double.IsInfinity(f.Gof.Aic)).ToArray();
        if (list.Length == 0) return new Dictionary<string, double>();

        double min = list.Min(f => f.Gof.Aic);
        var raw = list.ToDictionary(f => f.ModelName, f => Math.Exp(-(f.Gof.Aic - min) / 2.0));
        double sum = raw.Values.Sum();
        return sum > 0
            ? raw.ToDictionary(kv => kv.Key, kv => kv.Value / sum)
            : raw;
    }
}
