#nullable enable
using PTCalc.Core.Localization;

namespace PTCalc.Core.Dissolution;

/// <summary>
/// Dissolüsyon profili benzerliği için model-bağımsız ölçütler: fark faktörü f1 ve
/// benzerlik faktörü f2 (Moore &amp; Flanner, 1996; FDA 1997 SUPAC-MR / EMA kılavuzları).
///
/// <para>Bu sınıf F1F2 sayfasının içinde duran hesap fonksiyonlarının Core'a taşınmış
/// hâlidir; sayfa yalnız veri okuma ve sunumdan sorumludur. Kılavuz kuralları
/// (12 tekrar, %85 kesmesi, RSD sınırları) burada <b>uygulanmaz</b>; yalnız kesme
/// noktasını bulan yardımcı vardır. Kuralı açıp kapama kararı sayfadadır.</para>
/// </summary>
public static class SimilarityFactors
{
    /// <summary>
    /// f1 = 100 · Σ|Rᵢ − Tᵢ| / ΣRᵢ. Toplamlar rapor için birlikte döner.
    /// </summary>
    /// <exception cref="ArgumentException">Uzunluklar farklı, dizi boş ya da ΣR = 0.</exception>
    public static (double F1, double SumR, double SumAbsDiff) F1(
        IReadOnlyList<double> reference, IReadOnlyList<double> test)
    {
        RequireAligned(reference, test);

        double sumAbsDiff = 0, sumR = 0;
        for (int i = 0; i < reference.Count; i++)
        {
            sumAbsDiff += Math.Abs(reference[i] - test[i]);
            sumR += reference[i];
        }

        if (sumR == 0) throw new ArgumentException(CoreText.T("Referans toplamı sıfır; f1 tanımsız."));

        return (100.0 * sumAbsDiff / sumR, sumR, sumAbsDiff);
    }

    /// <summary>
    /// f2 = 50 · log₁₀( 100 / √(1 + (1/n) Σ(Rᵢ − Tᵢ)²) ). Özdeş profiller için 100;
    /// her noktada 10 birim fark için ≈ 50 (kılavuzlardaki "benzer" eşiği).
    /// </summary>
    /// <exception cref="ArgumentException">Uzunluklar farklı ya da dizi boş.</exception>
    public static double F2(IReadOnlyList<double> reference, IReadOnlyList<double> test)
    {
        RequireAligned(reference, test);

        int n = reference.Count;
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            double d = reference[i] - test[i];
            sumSq += d * d;
        }

        return 50.0 * Math.Log10(100.0 / Math.Sqrt(1.0 + sumSq / n));
    }

    /// <summary>
    /// İki seriyi <b>tam eşleşen</b> zaman noktalarında hizalar; yalnız ortak zamanlar
    /// döner, zamana göre artan sırada. Aynı zaman birden fazla girilmişse sonuncusu
    /// geçerlidir. Ortak nokta yoksa boş diziler döner (istisna değil; çağıran taraf
    /// kullanıcıya söylemeli).
    /// </summary>
    public static (double[] T, double[] R, double[] P) AlignByExactTimepoints(
        IReadOnlyList<(double t, double yMean)> reference,
        IReadOnlyList<(double t, double yMean)> test)
    {
        var rMap = reference.GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Last().yMean);
        var pMap = test.GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Last().yMean);

        var times = rMap.Keys.Intersect(pMap.Keys).OrderBy(x => x).ToArray();
        var r = new double[times.Length];
        var p = new double[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            r[i] = rMap[times[i]];
            p[i] = pMap[times[i]];
        }
        return (times, r, p);
    }

    /// <summary>
    /// Kılavuz kesme kuralı: referans <i>ya da</i> testin eşiğe (varsayılan %85) ulaştığı
    /// ilk noktanın indeksi; o nokta dahil edilir. Hiçbiri ulaşmazsa son indeks.
    /// Boş dizide −1.
    /// </summary>
    public static int FindCutoffIndex(IReadOnlyList<double> reference, IReadOnlyList<double> test, double threshold = 85.0)
    {
        int n = Math.Min(reference.Count, test.Count);
        for (int i = 0; i < n; i++)
            if (reference[i] >= threshold || test[i] >= threshold) return i;
        return n - 1;
    }

    /// <summary>Üç diziyi <paramref name="lastInclusiveIndex"/>'e kadar (dahil) keser; indeks aralığa sıkıştırılır.</summary>
    public static (double[] T, double[] R, double[] P) TakeThrough(
        double[] t, double[] r, double[] p, int lastInclusiveIndex)
    {
        if (t.Length == 0) return (t, r, p);
        int last = Math.Clamp(lastInclusiveIndex, 0, t.Length - 1);
        return (t[..(last + 1)], r[..(last + 1)], p[..(last + 1)]);
    }

    /// <summary>Örneklem ortalaması ve standart sapması (n−1). Boş → (0, 0); tek değer → (x, 0).</summary>
    public static (double Mean, double StdDev) MeanStdDev(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return (0, 0);
        double mean = values.Average();
        if (values.Count == 1) return (mean, 0);
        double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return (mean, Math.Sqrt(variance));
    }

    private static void RequireAligned(IReadOnlyList<double> reference, IReadOnlyList<double> test)
    {
        if (reference.Count != test.Count)
            throw new ArgumentException(CoreText.T("Referans ve test serilerinin uzunlukları eşit olmalı."));
        if (reference.Count == 0)
            throw new ArgumentException(CoreText.T("Ortak zaman noktası yok."));
    }
}
