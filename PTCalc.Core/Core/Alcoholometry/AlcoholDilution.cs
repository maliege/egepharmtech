using PTCalc.Core.Localization;

namespace PTCalc.Core.Alcoholometry;

/// <summary>Hedef alkol oranının ifade biçimi.</summary>
public enum StrengthBasis
{
    /// <summary>Hacimce derece, %v/v (20 °C); miktar mL.</summary>
    Volume,

    /// <summary>Kütlece oran, %w/w; miktar g.</summary>
    Mass,
}

/// <summary>
/// Stok alkolün suyla seyreltilmesi için hesaplanan miktarlar. Hacimler hazırlama sıcaklığındaki
/// hacimlerdir (mL), kütleler g. <see cref="Contraction"/> karışınca kaybolan hacim,
/// <see cref="NaiveWaterVolume"/> basit orantının (V_son − V_stok) önerdiği su hacmidir.
/// </summary>
public sealed record DilutionResult(
    StrengthBasis Basis,
    double Temperature,
    double TargetPercent,
    double StockPercent,
    double TargetMassPercent,
    double StockMassPercent,
    double FinalVolume,
    double FinalMass,
    double EthanolMass,
    double StockVolume,
    double StockMass,
    double WaterVolume,
    double WaterMass,
    double Contraction,
    double NaiveWaterVolume);

/// <summary>
/// Bilinen dereceli stok alkolden istenen derece ve miktarda alkol hazırlama. Hesap etanolün kütle
/// dengesine dayanır: m_stok·w_stok = m_son·w_son ve m_su = m_son − m_stok. Hacim korunmadığı için
/// (karışınca hacim küçülür) su miktarı hacim farkından değil kütle farkından bulunur; kütle dengesi
/// her sıcaklıkta geçerlidir. Yoğunluklar OIML R 22 formülünden (<see cref="OimlAlcoholometry"/>).
/// </summary>
public static class AlcoholDilution
{
    /// <summary>
    /// Hacimce: <paramref name="finalVolume"/> mL, <paramref name="targetPercent"/> %v/v alkol,
    /// <paramref name="stockPercent"/> %v/v stoktan, <paramref name="temperature"/> °C'de ölçülerek.
    /// </summary>
    public static DilutionResult ByVolume(double finalVolume, double targetPercent, double stockPercent,
        double temperature = OimlAlcoholometry.ReferenceTemperature)
    {
        CheckAmount(finalVolume);
        CheckStrengths(targetPercent, stockPercent);

        double wTarget = OimlAlcoholometry.VolumeToMassPercent(targetPercent);
        double wStock = OimlAlcoholometry.VolumeToMassPercent(stockPercent);
        double finalMass = finalVolume * OimlAlcoholometry.DensityFromMassPercent(wTarget, temperature) / 1000.0;
        return Build(StrengthBasis.Volume, temperature, targetPercent, stockPercent, wTarget, wStock, finalMass);
    }

    /// <summary>
    /// Kütlece: <paramref name="finalMass"/> g, <paramref name="targetPercent"/> %w/w alkol,
    /// <paramref name="stockPercent"/> %w/w stoktan. Sıcaklık yalnız bilgi amaçlı hacimler için kullanılır.
    /// </summary>
    public static DilutionResult ByMass(double finalMass, double targetPercent, double stockPercent,
        double temperature = OimlAlcoholometry.ReferenceTemperature)
    {
        CheckAmount(finalMass);
        CheckStrengths(targetPercent, stockPercent);
        return Build(StrengthBasis.Mass, temperature, targetPercent, stockPercent, targetPercent, stockPercent, finalMass);
    }

    private static DilutionResult Build(StrengthBasis basis, double t, double target, double stock,
        double wTarget, double wStock, double finalMass)
    {
        double rhoFinal = OimlAlcoholometry.DensityFromMassPercent(wTarget, t) / 1000.0; // g/mL
        double rhoStock = OimlAlcoholometry.DensityFromMassPercent(wStock, t) / 1000.0;
        double rhoWater = OimlAlcoholometry.WaterDensity(t) / 1000.0;

        double ethanolMass = finalMass * wTarget / 100.0;
        double stockMass = ethanolMass / (wStock / 100.0);
        double waterMass = finalMass - stockMass;

        double finalVolume = finalMass / rhoFinal;
        double stockVolume = stockMass / rhoStock;
        double waterVolume = waterMass / rhoWater;

        return new DilutionResult(
            basis, t, target, stock, wTarget, wStock,
            FinalVolume: finalVolume,
            FinalMass: finalMass,
            EthanolMass: ethanolMass,
            StockVolume: stockVolume,
            StockMass: stockMass,
            WaterVolume: waterVolume,
            WaterMass: waterMass,
            Contraction: stockVolume + waterVolume - finalVolume,
            NaiveWaterVolume: finalVolume - stockVolume);
    }

    private static void CheckAmount(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0)
            throw new ArgumentException(CoreText.T("Hazırlanacak miktar sıfırdan büyük olmalıdır."));
    }

    private static void CheckStrengths(double target, double stock)
    {
        if (double.IsNaN(stock) || stock <= 0 || stock > 100)
            throw new ArgumentException(CoreText.T("Stok alkol derecesi 0'dan büyük, en çok 100 olmalıdır."));
        if (double.IsNaN(target) || target <= 0 || target >= stock)
            throw new ArgumentException(CoreText.T("Hedef derece 0'dan büyük ve stok dereceden küçük olmalıdır."));
    }
}
