// ============================================================================
//  NumericCellParserTests.cs
//  Grid hücresi / dış kaynak sayı metinlerinin okunması.
//
//  Bu testlerin sebebi ölçülmüş bir kusurdur: eskiden ayrıştırma önce invariant
//  kültürle NumberStyles.Any kullanıyordu. O stil binlik ayırıcıyı kabul ettiği
//  için tek virgüllü metinler invariant'ta da "geçerli" sayılıyor ve sessizce
//  on katına çıkıyordu — "3,5" → 35, "100,00" → 10000, "12,345" → 12345.
//  Ternary'deki varyant ise hiç kültür belirtmiyordu, yani tr-TR'de "3.5" → 35.
//
//  Uygulamada bu yola girilemiyordu (Handsontable numeric hücresi virgüllü
//  ondalığı tarayıcıda gerçek sayıya çeviriyor), ama yardımcı her iki yönde de
//  bozuktu. Aşağıdaki testler doğru davranışı ve kültürden bağımsızlığı sabitler.
// ============================================================================
using System.Globalization;
using EgePharmTech.Helpers;

namespace EgePharmTech.Core.Tests;

public class NumericCellParserTests
{
    /// <summary>Testi verilen kültürlerin her birinde çalıştırır.</summary>
    private static void HerKulturde(Action govde)
    {
        var eski = CultureInfo.CurrentCulture;
        try
        {
            foreach (var ad in new[] { "tr-TR", "en-US", "de-DE", "" })
            {
                CultureInfo.CurrentCulture = ad.Length == 0
                    ? CultureInfo.InvariantCulture
                    : new CultureInfo(ad);
                govde();
            }
        }
        finally { CultureInfo.CurrentCulture = eski; }
    }

    private static void Bekle(string girdi, double beklenen)
    {
        Assert.True(NumericCellParser.TryParse(girdi, out var v), $"'{girdi}' ayrıştırılamadı");
        Assert.Equal(beklenen, v, 10);
    }

    // ---- Düzeltilen kusur ----

    [Fact]
    public void TekVirgul_OndalikOlarakOkunur_OnKatSismez()
    {
        HerKulturde(() =>
        {
            Bekle("3,5", 3.5);       // eskiden 35
            Bekle("0,5", 0.5);       // eskiden 5
            Bekle("100,00", 100);    // eskiden 10000
            Bekle("12,345", 12.345); // eskiden 12345
            Bekle("99,99", 99.99);
        });
    }

    [Fact]
    public void TekNokta_OndalikOlarakOkunur()
    {
        HerKulturde(() =>
        {
            Bekle("3.5", 3.5);       // tr-TR'de eskiden 35 (Ternary varyantı)
            Bekle("0.5", 0.5);
            Bekle("100.00", 100);
            Bekle("12.345", 12.345);
        });
    }

    // ---- Gruplu biçimler ----

    [Fact]
    public void IkiAyiriciVarsa_SondakiOndalikSayilir()
    {
        HerKulturde(() =>
        {
            Bekle("1.234,5", 1234.5);   // Türkçe Excel biçimi
            Bekle("1,234.5", 1234.5);   // İngilizce biçim
            Bekle("2.000,25", 2000.25);
            Bekle("1,234,567.89", 1234567.89);
            Bekle("1.234.567,89", 1234567.89);
        });
    }

    [Fact]
    public void AyniAyiriciBirdenFazlaysa_GruplamaSayilir()
    {
        HerKulturde(() =>
        {
            Bekle("1.234.567", 1234567);
            Bekle("1,234,567", 1234567);
        });
    }

    // ---- İşaret, üs, boşluk ----

    [Fact]
    public void IsaretUsVeBosluk_Desteklenir()
    {
        HerKulturde(() =>
        {
            Bekle("-3,5", -3.5);
            Bekle("+2.5", 2.5);
            Bekle("  7  ", 7);
            Bekle("1e3", 1000);
            Bekle("-1,5e2", -150);
        });
    }

    // ---- Geçersiz girdiler ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("%50")]
    [InlineData("1,2,3.4.5")]
    public void SayiOlmayanlar_BasarisizDoner(string? girdi)
    {
        HerKulturde(() =>
        {
            Assert.False(NumericCellParser.TryParse(girdi, out var v));
            Assert.Equal(0, v);
        });
    }

    // ---- Kültür bağımsızlığı ----

    [Fact]
    public void AyniGirdi_TumKulturlerdeAyniSonucuVerir()
    {
        var girdiler = new[] { "3,5", "3.5", "1.234,5", "1,234.5", "100,00", "12,345" };
        var eski = CultureInfo.CurrentCulture;
        try
        {
            foreach (var girdi in girdiler)
            {
                var sonuclar = new List<double>();
                foreach (var ad in new[] { "tr-TR", "en-US", "de-DE" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(ad);
                    Assert.True(NumericCellParser.TryParse(girdi, out var v));
                    sonuclar.Add(v);
                }
                Assert.All(sonuclar, s => Assert.Equal(sonuclar[0], s, 10));
            }
        }
        finally { CultureInfo.CurrentCulture = eski; }
    }

    // ---- Hücre tipleri ----

    [Fact]
    public void SayisalTipler_DogrudanAlinir()
    {
        Assert.True(NumericCellParser.TryGetDouble(3.5, out var d)); Assert.Equal(3.5, d, 10);
        Assert.True(NumericCellParser.TryGetDouble(3.5f, out var f)); Assert.Equal(3.5, f, 5);
        Assert.True(NumericCellParser.TryGetDouble(3, out var i)); Assert.Equal(3, i, 10);
        Assert.True(NumericCellParser.TryGetDouble(3L, out var l)); Assert.Equal(3, l, 10);
        Assert.True(NumericCellParser.TryGetDouble(3.5m, out var m)); Assert.Equal(3.5, m, 10);
        Assert.False(NumericCellParser.TryGetDouble(null, out _));
    }

    [Fact]
    public void MetinHucre_TryParseKuralinaUyar()
    {
        HerKulturde(() =>
        {
            Assert.True(NumericCellParser.TryGetDouble("3,5", out var v));
            Assert.Equal(3.5, v, 10);
        });
    }
}
