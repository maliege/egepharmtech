public sealed class AnalysisState
{
    // Kinetik Analiz sayfasının veri tablosu. 100 satır, 10 kolon
    // (Zaman + Tekrar 1..6 + Ortalama + StdSapma + yedek). Invivo tablosu ve
    // HopfenbergExponent, IvIvKorelasyon sayfasıyla birlikte kaldırıldı; Hopfenberg
    // geometrisi artık Kinetik sayfasının kendi VariantOptions'ında.
    public object?[][] Invitro { get; } = CreateMatrix(100, 10);

    /// <summary>Kinetik sayfasında seçili modeller; null = henüz dokunulmadı (hepsi). Sayfa değişse de korunur.</summary>
    public HashSet<string>? KinetikModels { get; set; }

    private static object?[][] CreateMatrix(int rows, int cols)
    {
        var data = new object?[rows][];
        for (int r = 0; r < rows; r++)
        {
            data[r] = new object?[cols];
            // varsayılanlar null kalsın; kullanıcı doldursun
        }
        return data;
    }
}
