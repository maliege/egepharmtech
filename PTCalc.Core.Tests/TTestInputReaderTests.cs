// ============================================================================
//  TTestInputReaderTests.cs
//  T-testi veri giriş tablosunun okunmasına dair testler.
//
//  Bu testlerin varlık sebebi somut bir hatadır: veri girişi iki ayrı grid iken
//  eşleştirilmiş test, her sütunu ayrı ayrı sıkıştırıp (boş hücreleri atlayıp)
//  ortaya çıkan iki listeyi konumsal olarak eşliyordu. Bir sütundaki tek bir
//  boşluk sonraki bütün eşleri kaydırıyor, iki listenin uzunluğu eşit kaldığı
//  için de tek doğrulama olan "sayılar eşit mi" kontrolü bunu yakalamıyordu.
//  Sonuç sessizce yanlış çıkıyordu. Aşağıdaki ilk test tam o senaryoyu sabitler.
// ============================================================================
using System.Globalization;
using PTCalc.Core.Statistics;

namespace PTCalc.Core.Tests;

public class TTestInputReaderTests
{
    private static object?[]?[] Table(params object?[]?[] rows) => rows;
    private static object?[] Row(object? a, object? b) => new[] { a, b };

    // ---- Eşleştirilmiş okuma ----

    [Fact]
    public void ReadPaired_BirSutundaBosluk_EslerKaymaz_SatirBildirilir()
    {
        // Boşluklar iki sütunda da, farklı satırlarda: 3. satırda A boş,
        // 5. satırda B boş. Sıkıştırılmış uzunluklar eşit kaldığı (4'e 4) için
        // eski koddaki "eşit sayıda veri olmalı" kontrolü bu girdiyi geçirir,
        // ama konumsal eşleme (10,11) (12,13) (14,15) (16,17) üretir: 14 aslında
        // 17 ile eşlenmeli, 16'nın ise eşi yok. Sessiz yanlış sonucun kaynağı bu.
        var table = Table(
            Row(10, 11),
            Row(12, 13),
            Row(null, 15),
            Row(14, 17),
            Row(16, null));

        // Uzunluk kontrolünün neden yetersiz olduğunu sabitle:
        Assert.Equal(4, TTestInputReader.ReadColumn(table, 0).Count);
        Assert.Equal(4, TTestInputReader.ReadColumn(table, 1).Count);

        var result = TTestInputReader.ReadPaired(table);

        Assert.Equal(new[] { 3, 5 }, result.UnpairedRowNumbers);
        Assert.Equal(new[] { 10.0, 12.0, 14.0 }, result.GroupA);
        Assert.Equal(new[] { 11.0, 13.0, 17.0 }, result.GroupB);
    }

    [Fact]
    public void ReadPaired_TamVeri_SirasiylaEsler()
    {
        var result = TTestInputReader.ReadPaired(Table(
            Row(10, 11),
            Row(12, 14),
            Row(14, 15)));

        Assert.Empty(result.UnpairedRowNumbers);
        Assert.Equal(new[] { 10.0, 12.0, 14.0 }, result.GroupA);
        Assert.Equal(new[] { 11.0, 14.0, 15.0 }, result.GroupB);
    }

    [Fact]
    public void ReadPaired_TamamenBosSatirlar_EsSayilmaz()
    {
        // Grid minRows=30 ile geldiği için tablonun sonu boş satırlarla doludur;
        // bunlar hata sayılmamalı.
        var result = TTestInputReader.ReadPaired(Table(
            Row(1, 2),
            Row(null, null),
            Row(3, 4),
            Row("", "   ")));

        Assert.Empty(result.UnpairedRowNumbers);
        Assert.Equal(new[] { 1.0, 3.0 }, result.GroupA);
        Assert.Equal(new[] { 2.0, 4.0 }, result.GroupB);
    }

    [Fact]
    public void ReadPaired_BirdenFazlaYarimSatir_HepsiBirTabanliBildirilir()
    {
        var result = TTestInputReader.ReadPaired(Table(
            Row(1, null),     // satır 1
            Row(2, 3),
            Row(null, 4),     // satır 3
            Row(5, 6),
            Row(7, null)));   // satır 5

        Assert.Equal(new[] { 1, 3, 5 }, result.UnpairedRowNumbers);
        Assert.Equal(new[] { 2.0, 5.0 }, result.GroupA);
        Assert.Equal(new[] { 3.0, 6.0 }, result.GroupB);
    }

    [Fact]
    public void ReadPaired_BosVeNullTablo_BosSonucVerir()
    {
        Assert.Empty(TTestInputReader.ReadPaired(null).GroupA);
        Assert.Empty(TTestInputReader.ReadPaired(Table()).GroupA);
        Assert.Empty(TTestInputReader.ReadPaired(Table(null, null)).UnpairedRowNumbers);
    }

    [Fact]
    public void ReadPaired_EksikHucreliSatir_TasmaVermez()
    {
        // Grid'den gelen satır beklenenden kısa olabilir.
        var result = TTestInputReader.ReadPaired(Table(
            new object?[] { 5 },          // yalnızca A sütunu var
            Row(1, 2)));

        Assert.Equal(new[] { 1 }, result.UnpairedRowNumbers);
        Assert.Equal(new[] { 1.0 }, result.GroupA);
    }

    // ---- Bağımsız okuma ----

    [Fact]
    public void ReadColumn_SutunlariBagimsizSikistirir_FarkliNOlabilir()
    {
        var table = Table(
            Row(10, 11),
            Row(12, 14),
            Row(14, 15),
            Row(16, null),
            Row(18, null));

        Assert.Equal(new[] { 10.0, 12.0, 14.0, 16.0, 18.0 }, TTestInputReader.ReadColumn(table, 0));
        Assert.Equal(new[] { 11.0, 14.0, 15.0 }, TTestInputReader.ReadColumn(table, 1));
    }

    [Fact]
    public void ReadColumn_AradakiBosluklariAtlar()
    {
        var table = Table(Row(1, null), Row(null, null), Row(2, null));
        Assert.Equal(new[] { 1.0, 2.0 }, TTestInputReader.ReadColumn(table, 0));
        Assert.Empty(TTestInputReader.ReadColumn(table, 1));
    }

    [Fact]
    public void ReadColumn_AraliktakiSutun_BosDoner()
    {
        var table = Table(Row(1, 2));
        Assert.Empty(TTestInputReader.ReadColumn(table, 5));
        Assert.Empty(TTestInputReader.ReadColumn(table, -1));
    }

    // ---- Hücre çevrimi ----

    [Theory]
    [InlineData(3.5)]
    [InlineData(3.5f)]
    [InlineData(3)]
    [InlineData(3L)]
    public void TryGetDouble_SayisalTipler_DogrudanCevrilir(object input)
    {
        Assert.True(TTestInputReader.TryGetDouble(input, out var v));
        Assert.Equal(Convert.ToDouble(input), v, 6);
    }

    [Fact]
    public void TryGetDouble_BosVeNull_Basarisiz()
    {
        Assert.False(TTestInputReader.TryGetDouble(null, out _));
        Assert.False(TTestInputReader.TryGetDouble("", out _));
        Assert.False(TTestInputReader.TryGetDouble("   ", out _));
        Assert.False(TTestInputReader.TryGetDouble("abc", out _));
    }

    [Fact]
    public void TryGetDouble_NoktaliMetin_KulturdenBagimsizAyniOkunur()
    {
        // Grid JS tarafı sayıları nokta ondalıkla üretir; kullanıcının makine
        // kültürü ne olursa olsun aynı okunmalı.
        var eski = CultureInfo.CurrentCulture;
        try
        {
            foreach (var kultur in new[] { "tr-TR", "en-US" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(kultur);
                Assert.True(TTestInputReader.TryGetDouble("3.5", out var v));
                Assert.Equal(3.5, v, 10);
            }
        }
        finally { CultureInfo.CurrentCulture = eski; }
    }
}
