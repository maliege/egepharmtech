#nullable enable
namespace PTCalc.Core.Dissolution;

/// <summary>
/// Bir dissolüsyon kinetiği modeli (şartname §8).
///
/// Sözleşme:
///  - <see cref="InitialGuess"/> kapalı-form doğrusallaştırma tohumudur (şartname §5).
///  - <see cref="Evaluate"/> modelin <b>orijinal F(%) biçimidir</b>; parçalı kurallar
///    (Tlag öncesi F=0 vb.) burada uygulanır. Fit ve tüm GoF bunun üzerinden yürür.
///  - <see cref="Secondary"/> T25..T90 gibi türetilmiş değerleri döndürür;
///    ulaşılamayan hedefler için değer null olur ("Non Calc").
///
/// Somut modeller <see cref="DissolutionModelBase"/>'den türer; oradaki "ceiling"
/// (tavan) aşırı yüklemesi Fmax varyant dekoratörünün taban denklemi yeniden
/// kullanabilmesini sağlar (100 → Fmax), böylece denklemler tek yerde kalır.
/// </summary>
public interface IDissolutionModel
{
    /// <summary>Model adı, ör. "Korsmeyer-Peppas".</summary>
    string Name { get; }

    /// <summary>İnsan-okur denklem, ör. "F = kKP*t^n".</summary>
    string Equation { get; }

    /// <summary>TÜM katsayıların sembolleri, <see cref="Evaluate"/>'in p dizisiyle aynı sırada.</summary>
    IReadOnlyList<string> ParamNames { get; }

    /// <summary>Katsayı meta verisi (birim/açıklama); <see cref="ParamNames"/> ile aynı sırada.</summary>
    IReadOnlyList<Coefficient> Describe(double[] p);

    bool SupportsF0 { get; }
    bool SupportsTlag { get; }
    bool SupportsFmax { get; }

    /// <summary>Alt sınırlar (şartname §4: hız sabitleri ve α ≥ 0; n, β ≥ 0; Tlag serbest).</summary>
    double[] LowerBounds { get; }

    /// <summary>Üst sınırlar. Sınırsız için double.PositiveInfinity.</summary>
    double[] UpperBounds { get; }

    /// <summary>Kapalı-form doğrusal tohum (şartname §5).</summary>
    double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt);

    /// <summary>ŷ(t) — orijinal F(%) uzayında, parçalı kurallar dahil.</summary>
    double Evaluate(double t, double[] p, VariantOptions opt);

    /// <summary>T25..T90 (ve modele özgü ekstralar, ör. Weibull Td).</summary>
    IReadOnlyList<SecondaryValue> Secondary(double[] p, VariantOptions opt);

    /// <summary>
    /// Fit için kullanılacak nokta kümesini süzer. Varsayılan: tüm noktalar.
    /// Korsmeyer-Peppas bunu F ≤ 60 filtresi için ezer (Costa &amp; Sousa Lobo).
    /// </summary>
    (double[] T, double[] F) SelectPoints(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt);

    /// <summary>Fit sonrası uyarı/yorum üretir (ör. KP n mekanizma yorumu, dejenerasyon).</summary>
    IReadOnlyList<string> Flags(double[] p, VariantOptions opt);
}
