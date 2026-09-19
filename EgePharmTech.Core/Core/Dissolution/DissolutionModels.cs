#nullable enable
namespace EgePharmTech.Core.Dissolution;

/// <summary>
/// Adlandırılmış bir model katsayısı. Şartname §2: sabit (m, n) kolonları yerine
/// her model kendi katsayılarını değişken uzunlukta bu liste ile raporlar.
/// </summary>
public sealed record Coefficient(
    string Symbol,        // "kKP", "n", "a", "b", "Fmax", "Tlag", ...
    double Value,
    string Unit,          // "%/min", "min^-n", "-", "min", "%", ...
    string Description);  // "salım hız sabiti", "salım üsteli", ...

/// <summary>
/// Ulaşılamayan ikincil parametre (ör. Fmax &lt; hedef, ya da tepe yapan eğri).
/// Şartname §4/§5.9: bu durumda "Non Calc" gösterilir.
/// </summary>
public sealed record SecondaryValue(string Symbol, double? Value)
{
    public bool IsCalculable => Value.HasValue;
    public override string ToString() => Value?.ToString("G6") ?? "Non Calc";
}

/// <summary>Fit sırasında kullanılan ağırlıklandırma (şartname §1.6).</summary>
public enum Weighting
{
    /// <summary>w = 1 (varsayılan)</summary>
    None,
    /// <summary>w = 1/y</summary>
    InvY,
    /// <summary>w = 1/y²</summary>
    InvY2
}

/// <summary>Hopfenberg geometrisi. Costa &amp; Sousa Lobo (2001) Eq. 39: n = 1, 2, 3.</summary>
public enum HopfenbergGeometry
{
    /// <summary>Slab (ince tabaka) — n = 1</summary>
    Slab = 1,
    /// <summary>Silindir — n = 2</summary>
    Cylinder = 2,
    /// <summary>Küre — n = 3</summary>
    Sphere = 3,
    /// <summary>
    /// Yarım küre — n = 1,5. Karasulu, Ertan &amp; Köse (2000, EJPB 49:177): Katzhendler denklemi
    /// HPMC teofilin tabletlerine uyarlanmış, yarım küre için n = 1,5 önerilmiş. Costa &amp; Sousa Lobo'da yok.
    /// </summary>
    HalfSphere = 4,
    /// <summary>Üçgen prizma — n = 4 (Karasulu ve ark., 2000).</summary>
    Triangle = 5
}

/// <summary>
/// Taban modele eklenen opsiyonel varyantlar ve model-özgü ayarlar (şartname §7).
/// </summary>
public sealed record VariantOptions
{
    /// <summary>Gecikme süresi varyantı: t → (t − Tlag), t &lt; Tlag için F = 0.</summary>
    public bool UseTlag { get; init; }

    /// <summary>Başlangıç burst'ü: F → F0 + taban. Yalnız tavansız modellerde.</summary>
    public bool UseF0 { get; init; }

    /// <summary>Eksik salım platosu: 100 → Fmax. Yalnız doyuma giden modellerde.</summary>
    public bool UseFmax { get; init; }

    /// <summary>
    /// Hopfenberg üsteli. Costa &amp; Sousa Lobo Eq. 39'da n bir geometri sabitidir
    /// (slab/silindir/küre), fit edilen bir katsayı değildir.
    /// </summary>
    public HopfenbergGeometry Geometry { get; init; } = HopfenbergGeometry.Slab;

    /// <summary>
    /// Korsmeyer-Peppas'ta tüm noktaları kullan. Varsayılan false: Costa &amp; Sousa Lobo,
    /// n'in belirlenmesinde yalnız eğrinin F &lt; %60 kısmının kullanılmasını şart koşar.
    /// </summary>
    public bool KpUseAllPoints { get; init; }

    /// <summary>
    /// Sıralamaya girecek taban modeller (<see cref="ModelCatalog"/> adları, büyük/küçük harfe duyarsız).
    /// null = katalogdaki hepsi. Araştırmacı çoğu zaman belli birkaç modeli karşılaştırmak ister;
    /// küme daraltılınca Akaike ağırlıkları yalnız seçilen modeller arasında dağılır.
    /// </summary>
    public IReadOnlySet<string>? Models { get; init; }

    /// <summary>Model bu analize dahil mi?</summary>
    public bool Includes(string modelName)
        => Models is null || Models.Any(m => string.Equals(m, modelName, StringComparison.OrdinalIgnoreCase));

    public static VariantOptions Default { get; } = new();
}

/// <summary>
/// İyilik-uyum ölçütleri. Şartname §3: <b>hepsi F(%)-uzayında</b> hesaplanır;
/// doğrusallaştırılmış (log) uzaydaki artıklar asla kullanılmaz.
/// </summary>
public sealed record GoodnessOfFit(
    int N,
    int Dof,
    double R,
    double Rsqr,
    double RsqrAdj,
    double Mse,
    double RmsE,
    double SS,
    double WSS,
    double Aic,
    double AicC,
    double Msc,
    Weighting Weighting);

/// <summary>Tek bir modelin fit sonucu.</summary>
public sealed record ModelFit(
    string ModelName,
    string Equation,
    IReadOnlyList<Coefficient> Parameters,   // TÜM katsayılar (1..4 adet)
    IReadOnlyList<SecondaryValue> Secondary, // T25, T50, T75, T80, T90 (+ Weibull Td)
    GoodnessOfFit Gof,
    double[] PredTimes,                      // çizim için ince ızgara
    double[] PredValues,
    IReadOnlyList<string> Flags)             // uyarılar: "dejenere", yorum metni, ...
{
    /// <summary>UI tablosu için: "kKP = 7.272 · n = 0.490" (şartname §2).</summary>
    public string CoefficientSummary =>
        string.Join(" · ", Parameters.Select(p => $"{p.Symbol} = {p.Value:G6}"));

    /// <summary>Yakınsama başarısız olup tohumda kalındıysa false.</summary>
    public bool Converged { get; init; } = true;

    /// <summary>
    /// Fit edilmiş modeli herhangi bir t'de değerlendirir (varyant kuralları dahil).
    /// Grafik ve tahmin tablosu bunu kullanır; UI'ın denklemleri yeniden yazması gerekmez.
    /// </summary>
    public Func<double, double> Predict { get; init; } = _ => double.NaN;
}
