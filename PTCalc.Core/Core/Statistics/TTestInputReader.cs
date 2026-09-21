#nullable enable
using PTCalc.Core.Helpers;

namespace PTCalc.Core.Statistics;

/// <summary>
/// İki sütunlu veri giriş tablosunu t-testi girdilerine çevirir.
///
/// Testin türü, tablonun nasıl okunacağını belirler:
///
/// <para><b>Bağımsız örneklem:</b> gruplar birbirinden bağımsızdır, n'ler farklı
/// olabilir. Her sütun kendi içinde sıkıştırılarak okunur (<see cref="ReadColumn"/>).</para>
///
/// <para><b>Eşleştirilmiş örneklem:</b> eşi belirleyen şey <i>satırdır</i>. Sütunları
/// ayrı ayrı sıkıştırıp konumsal olarak eşlemek, bir sütundaki tek bir boşluk
/// yüzünden sonraki bütün eşleri kaydırır; üstelik iki listenin uzunluğu eşit
/// kaldığı için basit bir sayı karşılaştırması bunu yakalamaz. Bu yüzden
/// <see cref="ReadPaired"/> satır bazında okur ve tek tarafı dolu satırları
/// hesaba katmak yerine <see cref="PairedInput.UnpairedRowNumbers"/> ile
/// bildirir; çağıran taraf bunu hata olarak göstermelidir.</para>
/// </summary>
public static class TTestInputReader
{
    /// <summary>
    /// Eşleştirilmiş okuma sonucu. <paramref name="GroupA"/> ve <paramref name="GroupB"/>
    /// yapıca aynı uzunluktadır; aynı indeksteki iki değer aynı satırdan gelir.
    /// <paramref name="UnpairedRowNumbers"/> boş değilse sonuç kullanılmamalıdır.
    /// </summary>
    /// <param name="GroupA">Birinci sütunun eşleşmiş değerleri.</param>
    /// <param name="GroupB">İkinci sütunun eşleşmiş değerleri.</param>
    /// <param name="UnpairedRowNumbers">
    /// Yalnızca bir sütunu dolu olan satırların 1 tabanlı numaraları.
    /// </param>
    public sealed record PairedInput(
        IReadOnlyList<double> GroupA,
        IReadOnlyList<double> GroupB,
        IReadOnlyList<int> UnpairedRowNumbers);

    /// <summary>
    /// Tabloyu satır bazında eşleştirerek okur. Boş satırlar atlanır; yalnızca bir
    /// sütunu dolu olan satırlar sonuca alınmaz, numaraları bildirilir.
    /// </summary>
    public static PairedInput ReadPaired(object?[]?[]? table, int columnA = 0, int columnB = 1)
    {
        var groupA = new List<double>();
        var groupB = new List<double>();
        var unpaired = new List<int>();

        if (table is null)
            return new PairedInput(groupA, groupB, unpaired);

        for (int r = 0; r < table.Length; r++)
        {
            var row = table[r];
            var hasA = TryGetCell(row, columnA, out var a);
            var hasB = TryGetCell(row, columnB, out var b);

            if (hasA && hasB)
            {
                groupA.Add(a);
                groupB.Add(b);
            }
            else if (hasA || hasB)
            {
                unpaired.Add(r + 1); // kullanıcıya gösterilecek satır numarası 1 tabanlı
            }
        }

        return new PairedInput(groupA, groupB, unpaired);
    }

    /// <summary>
    /// Tek bir sütunu sıkıştırarak okur: sayıya çevrilemeyen hücreler atlanır.
    /// Bağımsız örneklem testi için kullanılır.
    /// </summary>
    public static List<double> ReadColumn(object?[]?[]? table, int column)
    {
        var values = new List<double>();
        if (table is null) return values;

        foreach (var row in table)
            if (TryGetCell(row, column, out var value))
                values.Add(value);

        return values;
    }

    private static bool TryGetCell(object?[]? row, int column, out double value)
    {
        value = default;
        if (row is null || column < 0 || column >= row.Length) return false;
        return TryGetDouble(row[column], out value);
    }

    /// <summary>
    /// Hücreyi sayıya çevirir. Ayrıştırma kuralı için bkz.
    /// <see cref="NumericCellParser"/>; nokta/virgül belirsizliği orada
    /// kültürden bağımsız olarak çözülür.
    /// </summary>
    public static bool TryGetDouble(object? o, out double v) => NumericCellParser.TryGetDouble(o, out v);
}
