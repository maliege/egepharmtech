namespace EgePharmTech.Core.Services;

/// <summary>
/// ANOVA, çoklu regresyon ve kalibrasyon eğrisi sayfalarının veri girişleri; devre (kullanıcı oturumu)
/// ömürlüdür, sayfadan çıkıp dönünce korunur, sunucuda saklanmaz.
/// </summary>
public sealed class AnalysisStateStatistics
{
    public const int AnovaGroups = 6;
    public const int RegressionPredictors = 5;

    /// <summary>ANOVA: her sütun bir grup (en çok 6 grup).</summary>
    public object?[][] AnovaData { get; } = CreateMatrix(40, AnovaGroups);
    public string[] AnovaGroupNames { get; } = Enumerable.Range(1, AnovaGroups).Select(i => $"G{i}").ToArray();
    public double AnovaAlpha { get; set; } = 0.05;

    /// <summary>Regresyon: 1. sütun Y, sonrakiler X1..X5.</summary>
    public object?[][] RegressionData { get; } = CreateMatrix(50, 1 + RegressionPredictors);
    public string RegressionResponseName { get; set; } = "Y";
    public string[] RegressionPredictorNames { get; } = Enumerable.Range(1, RegressionPredictors).Select(i => $"X{i}").ToArray();
    public double RegressionAlpha { get; set; } = 0.05;
    public string[] RegressionPredictionInput { get; } = new string[RegressionPredictors];

    /// <summary>Kalibrasyon: konsantrasyon, yanıt.</summary>
    public object?[][] CalibrationData { get; } = CreateMatrix(40, 2);
    public double CalibrationAlpha { get; set; } = 0.05;
    public string CalibrationConcUnit { get; set; } = "µg/mL";

    private static object?[][] CreateMatrix(int rows, int cols)
    {
        var data = new object?[rows][];
        for (int r = 0; r < rows; r++) data[r] = new object?[cols];
        return data;
    }
}
