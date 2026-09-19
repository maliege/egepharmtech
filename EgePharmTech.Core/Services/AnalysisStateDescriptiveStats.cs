namespace EgePharmTech.Core.Services;

/// <summary>
/// Tanımlayıcı istatistik ve LD50/LD90 hesaplama için veri state yönetimi
/// </summary>
public sealed class AnalysisStateDescriptiveStats
{
    /// <summary>
    /// Doz-Yanıt verisi: [Doz, Yanıt (ölüm sayısı), Hayvan sayısı]
    /// Minimum 3 satır, maksimum 20 satır
    /// </summary>
    public object?[][] DoseResponseData { get; } = CreateMatrix(20, 3);

    private static object?[][] CreateMatrix(int rows, int cols)
    {
        var data = new object?[rows][];
        for (int r = 0; r < rows; r++)
        {
            data[r] = new object?[cols];
        }
        return data;
    }
}
