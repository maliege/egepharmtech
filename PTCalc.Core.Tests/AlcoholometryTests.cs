using System.Text.Json;
using PTCalc.Core.Alcoholometry;

namespace PTCalc.Core.Tests;

/// <summary>
/// OIML R 22 alkolometri formülü, v/v ↔ w/w dönüşümü, yoğunluktan derece ve seyreltme hesabı.
/// Tablo karşılaştırması oiml_r22_reference.json'daki basılı değerlerle (2 ondalık) yapılır; dosya
/// tools/make_oiml_reference.py ile tablo PDF'inden üretilir ve OCR hataları hesaplama kodu
/// kullanılmadan, tablonun iç tutarlılığıyla ayıklanır. Hesaplanan değer 2 ondalığa yuvarlanınca
/// basılı değerle aynı olmalıdır; yuvarlama sınırındaki (…5) hücreler için 0,01 pay bırakılır.
/// </summary>
public class AlcoholometryTests
{
    private static readonly JsonElement Tables =
        JsonDocument.Parse(File.ReadAllText("oiml_r22_reference.json")).RootElement.GetProperty("tables");

    public static TheoryData<string> TableNames => new() { "I", "II", "IIIa", "IIIb", "IVa", "IVb", "Va", "Vb" };

    private static double Compute(string table, double x, double t) => table switch
    {
        "I" => OimlAlcoholometry.DensityFromMassPercent(x, t),
        "II" => OimlAlcoholometry.DensityFromVolumePercent(x, t),
        "IIIa" => OimlAlcoholometry.DensityFromMassPercent(x, 20),
        "IIIb" => OimlAlcoholometry.MassToVolumePercent(x),
        "IVa" => OimlAlcoholometry.DensityFromVolumePercent(x, 20),
        "IVb" => OimlAlcoholometry.VolumeToMassPercent(x),
        "Va" => OimlAlcoholometry.MassPercentFromDensity(x, 20),
        "Vb" => OimlAlcoholometry.VolumePercentFromDensity(x, 20),
        _ => throw new ArgumentException(table),
    };

    [Theory]
    [MemberData(nameof(TableNames))]
    public void Reproduces_printed_OIML_table(string table)
    {
        var rows = Tables.GetProperty(table).EnumerateArray().ToList();
        Assert.True(rows.Count > 500, $"Tablo {table} için referans hücresi az: {rows.Count}");

        var failures = new List<string>();
        foreach (var row in rows)
        {
            double x = row[0].GetDouble(), t = row[1].GetDouble(), printed = row[2].GetDouble();
            double computed = Math.Round(Compute(table, x, t), 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(computed - printed) > 0.01 + 1e-9)
                failures.Add($"x={x}, t={t}: tablo {printed}, hesap {computed}");
        }
        Assert.True(failures.Count == 0, $"Tablo {table}: {failures.Count} hücre uyuşmuyor. " + string.Join("; ", failures.Take(10)));
    }

    [Fact]
    public void Pure_ethanol_and_water_densities_match_OIML()
    {
        // OIML R 22 errata: ρ20(%100) ≈ 789,24 kg/m³; havayla doymuş su 20 °C'de 998,20 kg/m³.
        Assert.Equal(789.24, OimlAlcoholometry.EthanolDensity20, 2);
        Assert.Equal(998.20, OimlAlcoholometry.WaterDensity(20), 2);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(40.0)]
    [InlineData(70.0)]
    [InlineData(96.0)]
    [InlineData(100.0)]
    public void Volume_mass_conversion_round_trips(double volumePercent)
    {
        double w = OimlAlcoholometry.VolumeToMassPercent(volumePercent);
        Assert.Equal(volumePercent, OimlAlcoholometry.MassToVolumePercent(w), 9);
    }

    [Theory]
    [InlineData(-20.0)]
    [InlineData(0.0)]
    [InlineData(20.0)]
    [InlineData(40.0)]
    public void Density_to_strength_inverts_the_formula_at_any_temperature(double t)
    {
        for (int p = 0; p <= 100; p += 5)
        {
            double rho = OimlAlcoholometry.DensityFromMassPercent(p, t);
            Assert.Equal(p, OimlAlcoholometry.MassPercentFromDensity(rho, t), 8);
        }
    }

    [Fact]
    public void Density_outside_water_ethanol_range_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => OimlAlcoholometry.MassPercentFromDensity(1000.5, 20));
        Assert.Throws<ArgumentException>(() => OimlAlcoholometry.MassPercentFromDensity(780, 20));
        Assert.Throws<ArgumentException>(() => OimlAlcoholometry.DensityFromMassPercent(50, 45));
    }

    /// <summary>
    /// 96° stoktan 20 °C'de 100 mL %70 v/v: stok hacmi basit orantıyla aynıdır (20 °C'de etanol hacmi
    /// korunur), su ise hacim büzülmesi kadar fazladır. Değerler OIML tablolarından elle: p(70) = 62,39,
    /// p(96) = 93,84, ρ20(70) = 885,56, ρ20(96) = 807,42 kg/m³.
    /// </summary>
    [Fact]
    public void Dilution_by_volume_accounts_for_contraction()
    {
        var r = AlcoholDilution.ByVolume(100, 70, 96);

        Assert.Equal(100 * 70 / 96.0, r.StockVolume, 6);
        Assert.Equal(88.556, r.FinalMass, 2);
        Assert.Equal(29.74, r.WaterVolume, 2);
        Assert.Equal(r.FinalMass, r.StockMass + r.WaterMass, 9);
        Assert.Equal(r.EthanolMass, r.StockMass * r.StockMassPercent / 100, 9);
        Assert.InRange(r.Contraction, 2.6, 2.7);
        Assert.Equal(100 - r.StockVolume, r.NaiveWaterVolume, 9);
    }

    [Fact]
    public void Dilution_by_mass_is_a_plain_mass_balance()
    {
        var r = AlcoholDilution.ByMass(120, 60, 90);

        Assert.Equal(80, r.StockMass, 9);
        Assert.Equal(40, r.WaterMass, 9);
        Assert.Equal(72, r.EthanolMass, 9);
    }

    /// <summary>Kütle dengesi sıcaklıktan bağımsızdır: 25 °C'de hazırlanan karışımın 20 °C derecesi hedefe eşit.</summary>
    [Fact]
    public void Dilution_away_from_20C_still_gives_the_target_strength()
    {
        var r = AlcoholDilution.ByVolume(50, 60, 90, temperature: 25);
        double wMix = r.EthanolMass / (r.StockMass + r.WaterMass) * 100;
        Assert.Equal(60, OimlAlcoholometry.MassToVolumePercent(wMix), 9);
        Assert.Equal(50, r.FinalVolume, 9);
    }

    [Fact]
    public void Dilution_rejects_target_not_below_stock()
    {
        Assert.Throws<ArgumentException>(() => AlcoholDilution.ByVolume(100, 96, 96));
        Assert.Throws<ArgumentException>(() => AlcoholDilution.ByVolume(0, 50, 96));
    }
}
