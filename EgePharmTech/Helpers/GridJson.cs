using System.Text.Json;

namespace EgePharmTech.Helpers;

/// <summary>
/// Handsontable'dan gelen JSON satır dizisini <c>object?[][]</c> tabloya çevirir (sayı → double,
/// metin → string, boş → null). Sayı ayrıştırma <c>NumericCellParser</c>'da yapılır; buradaki iş yalnız
/// JSON türlerini ham hücre değerine indirmektir.
/// </summary>
public static class GridJson
{
    public static object?[][] ToTable(object? data)
    {
        if (data is not JsonElement el || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<object?[]>();

        var rows = new List<object?[]>();
        foreach (var row in el.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) continue;
            rows.Add(row.EnumerateArray().Select(c => CellValue(c)).ToArray());
        }
        return rows.ToArray();
    }

    /// <summary>Tek hücre: JsonElement ya da ham değer → double / string / null.</summary>
    public static object? CellValue(object? v) => v switch
    {
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.Number => je.GetDouble(),
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => je.ToString()
        },
        _ => v
    };

    /// <summary>
    /// afterChange değişikliklerini ([satır, sütun, eski, yeni]) durum matrisine yazar; sayfadan çıkıp
    /// dönünce veri korunur. Matris sınırı dışındaki hücreler yok sayılır.
    /// </summary>
    public static void ApplyChanges(object?[][] target, List<object[]> changes)
    {
        foreach (var ch in changes)
        {
            if (ch.Length < 4) continue;
            if (!int.TryParse(CellValue(ch[0])?.ToString(), out int row)) continue;
            if (!int.TryParse(CellValue(ch[1])?.ToString(), out int col)) continue;
            if (row < 0 || row >= target.Length || col < 0 || col >= target[row].Length) continue;
            target[row][col] = CellValue(ch[3]);
        }
    }
}
