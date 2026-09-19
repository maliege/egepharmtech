using EgePharmTech.Core.Dissolution;
using Xunit;

namespace EgePharmTech.Core.Tests;

/// <summary>
/// Kullanıcı sıralamaya girecek modelleri seçebilir (VariantOptions.Models). null = katalogdaki hepsi;
/// küme verilirse yalnız o modeller fit edilir ve Akaike ağırlıkları yalnız onlar arasında dağılır.
/// </summary>
public class KineticModelSelectionTests
{
    private static readonly double[] T = { 0.5, 1, 2, 3, 4, 6, 8, 10, 12 };
    private static readonly double[] F = { 12, 22, 38, 50, 59, 72, 81, 87, 91 };

    [Fact]
    public void Secim_yoksa_katalogdaki_tum_modeller_fit_edilir()
    {
        var all = KineticEngine.FitAll(T, F, new VariantOptions { Geometry = HopfenbergGeometry.Cylinder });
        // Katalog 16 model; silindir geometrisinde Hopfenberg de sıralanır; KP kısıtlı kümede.
        Assert.Equal(ModelCatalog.ModelNames().Count - 1, all.Fits.Count);
        Assert.Single(all.MechanismFits);
    }

    [Fact]
    public void Secilen_kume_sadece_o_modelleri_verir_ve_agirliklar_aralarinda_dagilir()
    {
        var opt = new VariantOptions { Models = new HashSet<string> { "First-order", "Higuchi", "Weibull" } };
        var r = KineticEngine.FitAll(T, F, opt);
        Assert.Equal(3, r.Fits.Count);
        Assert.Equal(new[] { "First-order", "Higuchi", "Weibull" }.OrderBy(x => x), r.Fits.Select(x => x.ModelName).OrderBy(x => x));
        Assert.Empty(r.MechanismFits);
        Assert.Equal(1.0, r.AkaikeWeights.Values.Sum(), 6);
    }

    [Fact]
    public void Model_adi_buyuk_kucuk_harfe_duyarsiz()
    {
        var opt = new VariantOptions { Models = new HashSet<string> { "zero-order", "FIRST-ORDER" } };
        var r = KineticEngine.FitAll(T, F, opt);
        Assert.Equal(2, r.Fits.Count);
    }

    [Fact]
    public void Yalniz_KP_secilirse_siralama_bos_mekanizma_dolu()
    {
        var opt = new VariantOptions { Models = new HashSet<string> { "Korsmeyer-Peppas" } };
        var r = KineticEngine.FitAll(T, F, opt);
        Assert.Empty(r.Fits);
        Assert.Single(r.MechanismFits);
        Assert.Empty(r.AkaikeWeights);
    }

    [Fact]
    public void Bilinmeyen_ad_sessizce_yok_sayilir()
    {
        var opt = new VariantOptions { Models = new HashSet<string> { "Yok Böyle Model", "Higuchi" } };
        var r = KineticEngine.FitAll(T, F, opt);
        Assert.Single(r.Fits);
        Assert.Equal("Higuchi", r.Fits[0].ModelName);
    }

    [Fact]
    public void Bos_kume_hic_model_fit_etmez()
    {
        var r = KineticEngine.FitAll(T, F, new VariantOptions { Models = new HashSet<string>() });
        Assert.Empty(r.Fits);
        Assert.Empty(r.MechanismFits);
    }

    [Fact]
    public void Katalog_adlari_benzersiz_ve_sirali()
    {
        var names = ModelCatalog.ModelNames();
        Assert.Equal(16, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("Zero-order", names[0]);
    }
}
