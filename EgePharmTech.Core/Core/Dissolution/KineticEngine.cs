#nullable enable
using EgePharmTech.Core.Dissolution.Models;

namespace EgePharmTech.Core.Dissolution;

/// <summary>
/// Bir profilin tüm analiz sonucu.
///
/// <b>Neden iki ayrı liste?</b> AIC yalnızca <i>aynı veriye</i> fit edilmiş modeller arasında
/// karşılaştırılabilir (AIC = N·ln(SS) + 2p — N değişirse ölçek değişir). Korsmeyer-Peppas
/// varsayılan olarak yalnız F≤60 noktalarına fit edilir (Costa &amp; Sousa Lobo), yani diğer
/// modellerden farklı bir veri kümesi kullanır. Bu yüzden tam profile fit edilen modeller
/// <see cref="Fits"/>'te sıralanır; kısıtlı veri kümesi kullananlar
/// <see cref="MechanismFits"/>'te ayrı sunulur ve sıralamaya/Akaike ağırlıklarına girmez.
/// </summary>
public sealed record AnalysisResult(
    IReadOnlyList<ModelFit> Fits,           // tam profile fit; AIC artan (en iyi ilk)
    IReadOnlyList<ModelFit> MechanismFits,  // kısıtlı veri kümesi (ör. KP F≤60) — sıralamaya girmez
    ProfileSummary Profile,
    IReadOnlyDictionary<string, double> AkaikeWeights);

/// <summary>
/// Dissolüsyon kinetiği motorunun dış yüzü.
///
/// Şartname §4 akışı: <b>doğrusal tohum → F(%)-uzayında nonlineer rafinasyon → GoF → AIC sıralaması</b>.
/// Kaldırılan doğrusallaştırma tabanlı eski motorun aksine burada gerçek bir nonlineer basamak vardır
/// ve tüm ölçütler tek bir uzayda (F) hesaplanır, dolayısıyla modeller karşılaştırılabilir.
/// </summary>
public static class KineticEngine
{
    /// <summary>Tek bir modeli (varyantlarıyla) fit eder.</summary>
    public static ModelFit Fit(
        string modelName,
        IReadOnlyList<double> t,
        IReadOnlyList<double> f,
        VariantOptions? options = null,
        Weighting weighting = Weighting.None)
    {
        var baseModel = ModelCatalog.Find(modelName)
            ?? throw new ArgumentException($"Bilinmeyen model: {modelName}");

        var opt = options ?? VariantOptions.Default;
        var model = ModelCatalog.Build(baseModel, opt);
        return new NonlinearFitter().Fit(model, t, f, weighting, opt);
    }

    /// <summary>
    /// Tüm taban modelleri (varyantsız) fit eder ve AIC'e göre sıralar.
    /// </summary>
    public static AnalysisResult FitAll(
        IReadOnlyList<double> t,
        IReadOnlyList<double> f,
        VariantOptions? options = null,
        Weighting weighting = Weighting.None)
    {
        var opt = options ?? VariantOptions.Default;
        var fitter = new NonlinearFitter();

        var full = new List<ModelFit>();       // N = tüm noktalar → AIC karşılaştırılabilir
        var restricted = new List<ModelFit>(); // N < tüm noktalar → ayrı sunulur

        foreach (var baseModel in ModelCatalog.BaseModels())
        {
            // Kullanıcının seçmediği modeller hiç fit edilmez (Akaike paydasına da girmez).
            if (!opt.Includes(baseModel.Name))
                continue;

            // Hopfenberg n=1 → 100·kHB·t (Zero-order), n=3 → Hixson-Crowell ile aynı eğri. Aynı eğriyi
            // iki ad altında sıralamak Akaike ağırlığını çift sayar (payda şişer, diğer modeller
            // haksız küçülür). Silindir (n=2), yarım küre (1,5) ve üçgen (4) ise başka hiçbir modele
            // eşdeğer değildir ve sıralanır. Tekil fit için Fit("Hopfenberg", …) her geometride çalışır.
            if (baseModel is HopfenbergModel && opt.Geometry is HopfenbergGeometry.Slab or HopfenbergGeometry.Sphere)
                continue;
            try
            {
                var applicable = Applicable(baseModel, opt);
                var model = ModelCatalog.Build(baseModel, applicable);
                var fit = fitter.Fit(model, t, f, weighting, opt);

                // Model kendi nokta kümesini kısıtladıysa (ör. KP F≤60) AIC'i diğerleriyle
                // kıyaslanamaz; sıralamaya sokmak yerine mekanizma listesine alınır.
                bool usesAllPoints = model.SelectPoints(t, f, applicable).T.Length == t.Count;
                (usesAllPoints ? full : restricted).Add(fit);
            }
            catch
            {
                // Tek bir modelin başarısız olması tüm analizi düşürmemeli
                // (ör. yetersiz geçerli nokta, tanımsız doğrusallaştırma).
            }
        }

        var ranked = ModelRanker.RankByAic(full);
        return new AnalysisResult(
            Fits: ranked,
            MechanismFits: restricted,
            Profile: ProfileMetrics.Summarize(t, f),
            AkaikeWeights: ModelRanker.AkaikeWeights(ranked));
    }

    /// <summary>
    /// Kullanıcının istediği varyantları, modelin şartname §7'de desteklediklerine kısıtlar
    /// (desteklenmeyen varyant istisna fırlatmak yerine sessizce düşürülür).
    /// </summary>
    private static VariantOptions Applicable(DissolutionModelBase m, VariantOptions opt) => opt with
    {
        UseF0 = opt.UseF0 && m.SupportsF0,
        UseTlag = opt.UseTlag && m.SupportsTlag,
        UseFmax = opt.UseFmax && m.SupportsFmax
    };
}
