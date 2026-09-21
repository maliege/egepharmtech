#nullable enable
using PTCalc.Core.Helpers;
using PTCalc.Core.Localization;

namespace PTCalc.Core.Dissolution;

/// <summary>
/// Kinetik veri giriş tablosundan okunan (t, F) profili ve okuma sırasında üretilen
/// uyarılar. <see cref="T"/> zamana göre sıralıdır; <see cref="F"/> daima salım
/// yüzdesidir (kalan formatı girildiyse çevrilmiştir, bkz. <see cref="ConvertedFromRetained"/>).
/// <see cref="T"/> boşsa <see cref="Warnings"/> nedenini söyler.
/// </summary>
public sealed record KineticProfile(
    double[] T,
    double[] F,
    IReadOnlyList<string> Warnings,
    bool ConvertedFromRetained);

/// <summary>
/// Kinetik analiz sayfasının 9 sütunlu tablosunu (Zaman, Tekrar 1–6, Ortalama, StdSapma)
/// motorun beklediği (t, F) profiline çevirir.
///
/// Eski sayfa kodu iki şeyi sessizce yapıyordu ve ikisi de kullanıcıyı yanıltıyordu:
/// yalnız "Ortalama" sütunu doldurulmuş satırları yok sayıyor (ellerinde sadece ortalama
/// olan kullanıcılar için sayfa "hiçbir şey yapmıyor" gibi görünüyordu) ve %100'e ulaşan
/// noktaları atıyordu (profilin sonu kayboluyordu). Bu okuyucu her iki durumu da ele
/// alır ve ne yaptığını <see cref="KineticProfile.Warnings"/> ile bildirir; sayfa bu
/// listeyi göstermelidir.
/// </summary>
public static class KineticInputReader
{
    public const int TimeColumn = 0;
    public const int FirstReplicateColumn = 1;
    public const int LastReplicateColumn = 6;
    public const int MeanColumn = 7;
    public const int StdDevColumn = 8;

    /// <summary>Bunun altındaki en büyük değer, verinin kesir (0–1) ölçeğinde girildiğini düşündürür.</summary>
    private const double FractionScaleCeiling = 1.5;

    /// <summary>Tabloyu okur. Hiç geçerli nokta yoksa boş dizilerle döner; uyarılar nedenini açıklar.</summary>
    public static KineticProfile Read(object?[][] table)
    {
        var rows = new List<(double t, double f)>();
        var warnings = new List<string>();

        int noTime = 0, nonPositiveTime = 0, noValue = 0, nonPositiveMean = 0, above100 = 0, fromMeanColumn = 0;

        foreach (var row in table)
        {
            if (row is null || row.All(IsBlank)) continue;   // tamamen boş satır → sessizce geç

            bool hasTime = TryGetDouble(Cell(row, TimeColumn), out var t);
            if (!hasTime) { noTime++; continue; }
            if (t <= 0) { nonPositiveTime++; continue; }

            double? mean = ReplicateMean(row);
            if (mean is null && TryGetDouble(Cell(row, MeanColumn), out var m))
            {
                mean = m;
                fromMeanColumn++;
            }
            if (mean is null) { noValue++; continue; }
            if (mean <= 0) { nonPositiveMean++; continue; }
            if (mean > 100) above100++;

            rows.Add((t, mean.Value));
        }

        if (noTime > 0)
            warnings.Add(CoreText.T("{0} satırda zaman boş ya da sayı değil; bu satırlar atlandı.", noTime));
        if (nonPositiveTime > 0)
            warnings.Add(CoreText.T("{0} satırda zaman ≤ 0; bu satırlar atlandı (t = 0 noktası F = 0 kabul edilir, girilmesi gerekmez).", nonPositiveTime));
        if (noValue > 0)
            warnings.Add(CoreText.T("{0} satırda ne tekrar ne ortalama değeri var; bu satırlar atlandı.", noValue));
        if (nonPositiveMean > 0)
            warnings.Add(CoreText.T("{0} satırda salım ≤ 0; bu satırlar atlandı.", nonPositiveMean));
        if (fromMeanColumn > 0)
            warnings.Add(CoreText.T("{0} satırda tekrar girilmediği için \"Ortalama\" sütunu kullanıldı.", fromMeanColumn));

        if (rows.Count == 0)
        {
            warnings.Add(CoreText.T("Geçerli veri noktası bulunamadı. Her satırda pozitif bir zaman ve en az bir tekrar (ya da Ortalama sütununda) pozitif bir salım değeri gerekir."));
            return new KineticProfile([], [], warnings, false);
        }

        rows.Sort((a, b) => a.t.CompareTo(b.t));

        if (above100 > 0)
            warnings.Add(CoreText.T("{0} noktada salım %100'ün üzerinde. Değerler olduğu gibi kullanıldı; miktar tayini kaynaklı küçük aşımlar normaldir, ancak doyuma giden modeller %100 tavanını varsayar.", above100));

        if (rows.Max(r => r.f) <= FractionScaleCeiling)
            warnings.Add(CoreText.T("Tüm değerler ≤ 1,5: veri kesir (0–1) ölçeğinde girilmiş görünüyor. Modeller salımı yüzde (0–100) olarak bekler; kesir girilirse doyuma giden modeller anlamsız sonuç verir. Değerleri 100 ile çarpın."));

        // Azalan profil → "kalan" formatı; salıma çevir. Karşılaştırma zamana göre ilk ve son
        // noktayla yapılır (girdi sırasına göre değil), yoksa karışık girilen veri yanlış çevrilir.
        bool retained = rows[0].f > rows[^1].f;
        if (retained)
            warnings.Add(CoreText.T("Profil zamanla azalıyor: veri \"kalan ilaç\" formatında varsayıldı ve 100 − F ile salıma çevrildi. Salım girdiyseniz ve profil gerçekten azalıyorsa (ör. bozunma) bu çevrim yanlıştır."));

        if (rows.Count < 3)
            warnings.Add(CoreText.T("Yalnız {0} nokta var. İki parametreli modellerde serbestlik derecesi kalmaz; en az 5–6 nokta önerilir.", rows.Count));

        var tArr = rows.Select(r => r.t).ToArray();
        var fArr = retained
            ? rows.Select(r => 100.0 - r.f).ToArray()
            : rows.Select(r => r.f).ToArray();

        return new KineticProfile(tArr, fArr, warnings, retained);
    }

    /// <summary>
    /// Tekrarları olan satırlar için (satır indeksi, ortalama, standart sapma) üçlüleri;
    /// sayfa bunları Ortalama/StdSapma sütunlarına geri yazar. Tekrarı olmayan satırlar
    /// listede yer almaz — böylece kullanıcının elle yazdığı Ortalama değeri ezilmez.
    /// </summary>
    public static IReadOnlyList<(int Row, double Mean, double StdDev)> ReplicateStats(object?[][] table)
    {
        var result = new List<(int, double, double)>();
        for (int i = 0; i < table.Length; i++)
        {
            var row = table[i];
            if (row is null) continue;

            var reps = Replicates(row);
            if (reps.Count == 0) continue;

            double mean = reps.Average();
            double std = reps.Count > 1
                ? Math.Sqrt(reps.Sum(x => (x - mean) * (x - mean)) / (reps.Count - 1))
                : 0;
            result.Add((i, mean, std));
        }
        return result;
    }

    /// <summary>Grid'den gelen hücreyi sayıya çevirir; metinler <see cref="NumericCellParser"/> ile okunur.</summary>
    public static bool TryGetDouble(object? o, out double v)
    {
        v = default;
        switch (o)
        {
            case null: return false;
            case double d: v = d; return true;
            case float fl: v = fl; return true;
            case int i: v = i; return true;
            case long l: v = l; return true;
            case decimal m: v = (double)m; return true;
            default: return NumericCellParser.TryParse(o.ToString(), out v);
        }
    }

    private static double? ReplicateMean(object?[] row)
    {
        var reps = Replicates(row);
        return reps.Count > 0 ? reps.Average() : null;
    }

    private static List<double> Replicates(object?[] row)
    {
        var reps = new List<double>(6);
        for (int c = FirstReplicateColumn; c <= LastReplicateColumn; c++)
            if (TryGetDouble(Cell(row, c), out var v)) reps.Add(v);
        return reps;
    }

    private static object? Cell(object?[] row, int index) => index < row.Length ? row[index] : null;

    private static bool IsBlank(object? o) => o is null || (o is string s && string.IsNullOrWhiteSpace(s));
}
