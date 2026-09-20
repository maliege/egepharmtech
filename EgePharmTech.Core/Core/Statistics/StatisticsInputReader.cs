#nullable enable
using EgePharmTech.Core.Helpers;

namespace EgePharmTech.Core.Statistics;

/// <summary>
/// Veri giriş tablosunu (Handsontable satır dizisi) istatistik araçlarına uygun listelere çevirir.
/// Sayı ayrıştırma kuralı <see cref="NumericCellParser"/>'dadır (virgül/nokta, boşluk, birim).
/// </summary>
public static class StatisticsInputReader
{
    /// <summary>Satır bazlı okuma sonucu: tüm istenen hücreleri sayı olan satırlar ve atlanan satırların numaraları.</summary>
    public sealed record RowInput(IReadOnlyList<double[]> Rows, IReadOnlyList<int> SkippedRowNumbers);

    /// <summary>
    /// Her sütunu kendi içinde sıkıştırarak okur (ANOVA: gruplar bağımsız, n'ler farklı olabilir).
    /// Tamamen boş sütunlar da listede boş olarak yer alır; çağıran taraf n'e göre eler.
    /// </summary>
    public static List<List<double>> ReadColumns(object?[]?[]? table, int columnCount)
    {
        var cols = new List<List<double>>(columnCount);
        for (int c = 0; c < columnCount; c++) cols.Add(new List<double>());
        if (table is null) return cols;
        foreach (var row in table)
        {
            if (row is null) continue;
            for (int c = 0; c < columnCount && c < row.Length; c++)
                if (NumericCellParser.TryGetDouble(row[c], out var v)) cols[c].Add(v);
        }
        return cols;
    }

    /// <summary>
    /// Satır bazlı okur (regresyon, kalibrasyon): <paramref name="columns"/> ile verilen hücrelerin
    /// hepsi sayı olan satırlar alınır. Tamamen boş satırlar sessizce geçilir; kısmen dolu satırlar
    /// atlanır ve 1 tabanlı numaraları bildirilir (kullanıcı uyarısı için).
    /// </summary>
    public static RowInput ReadCompleteRows(object?[]?[]? table, IReadOnlyList<int> columns)
    {
        var rows = new List<double[]>();
        var skipped = new List<int>();
        if (table is null) return new RowInput(rows, skipped);

        for (int r = 0; r < table.Length; r++)
        {
            var row = table[r];
            if (row is null) continue;
            var vals = new double[columns.Count];
            int filled = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                int c = columns[i];
                if (c < row.Length && NumericCellParser.TryGetDouble(row[c], out var v)) { vals[i] = v; filled++; }
            }
            bool anyText = row.Any(cell => cell is string s && !string.IsNullOrWhiteSpace(s)) ;
            if (filled == columns.Count) rows.Add(vals);
            else if (filled > 0 || anyText) skipped.Add(r + 1);
        }
        return new RowInput(rows, skipped);
    }
}
