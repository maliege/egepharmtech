#nullable enable
using PTCalc.Core.Dissolution.Models;

namespace PTCalc.Core.Dissolution;

/// <summary>
/// Taban model kataloğu ve varyant üretimi (şartname §5 + §7).
///
/// Şartname §6.2 uyarınca <b>tekrarlar temizlenmiştir</b>: eski motordaki
/// <c>Rrsbw</c>, <c>Langenbucher</c>, <c>ModifiedLangenbucher</c> aynı Weibull dağılımının
/// farklı doğrusallaştırmalarıydı → tek <see cref="WeibullModel"/>'de birleşti
/// (<c>ModifiedLangenbucher</c> = Weibull + Tlag varyantı). <c>BTa</c> (F = B·t^a) ise
/// güç yasasıdır → <see cref="KorsmeyerPeppasModel"/>'in ta kendisi.
/// </summary>
public static class ModelCatalog
{
    /// <summary>Tüm taban modeller (şartname §5).</summary>
    public static IReadOnlyList<DissolutionModelBase> BaseModels() => new DissolutionModelBase[]
    {
        new ZeroOrderModel(),
        new FirstOrderModel(),
        new HiguchiModel(),
        new HixsonCrowellModel(),
        new KorsmeyerPeppasModel(),
        new WeibullModel(),
        new HopfenbergModel(),
        new BakerLonsdaleModel(),
        new MakoidBanakarModel(),
        new PeppasSahlinModel(),
        new PeppasSahlin2Model(),
        new LogisticModel(),
        new GompertzModel(),
        new ProbitModel(),
        // Fmax'lı sigmoidler ayrı taban modellerdir (t-tabanlı parametrizasyon):
        new LogisticFmaxModel(),
        new GompertzFmaxModel()
    };

    /// <summary>Katalog sırasıyla model adları (arayüzdeki model seçim listesi bunu kullanır).</summary>
    public static IReadOnlyList<string> ModelNames() => BaseModels().Select(m => m.Name).ToArray();

    /// <summary>Ada göre taban model bulur (fikstür/test adları da bu adlarla eşleşir).</summary>
    public static DissolutionModelBase? Find(string name)
        => BaseModels().FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Bir taban modeli varyant seçenekleriyle sarmalar. Hiç varyant yoksa taban model
    /// olduğu gibi döner (gereksiz dekoratör katmanı eklenmez).
    /// </summary>
    public static DissolutionModelBase Build(DissolutionModelBase baseModel, VariantOptions opt)
        => opt.UseF0 || opt.UseTlag || opt.UseFmax
            ? new VariantModel(baseModel, opt)
            : baseModel;

    /// <summary>
    /// Bir taban model için şartname §7 matrisine uyan TÜM geçerli varyant kombinasyonlarını
    /// üretir (taban dahil).
    /// </summary>
    public static IEnumerable<(DissolutionModelBase Model, VariantOptions Options)> Variants(
        DissolutionModelBase baseModel, VariantOptions template)
    {
        foreach (bool f0 in Toggles(baseModel.SupportsF0))
        foreach (bool tlag in Toggles(baseModel.SupportsTlag))
        foreach (bool fmax in Toggles(baseModel.SupportsFmax))
        {
            var opt = template with { UseF0 = f0, UseTlag = tlag, UseFmax = fmax };
            yield return (Build(baseModel, opt), opt);
        }

        static IEnumerable<bool> Toggles(bool supported)
            => supported ? new[] { false, true } : new[] { false };
    }
}
