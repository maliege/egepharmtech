using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using PTCalc.Core.Dissolution;
using PTCalc.Core.Dissolution.Models;
using PTCalc.Core.Localization;
using Xunit;

namespace PTCalc.Core.Tests;

/// <summary>
/// CoreText: Türkçe kültürde anahtar aynen döner; İngilizce kültürde TSV karşılığı gelir.
/// Ayrıca gömülü TSV'nin her satırında yer tutucu kümesi anahtar ile çeviri arasında aynı olmalı
/// (aksi hâlde string.Format çalışma zamanında patlar).
/// </summary>
public class CoreTextTests
{
    private static T WithUiCulture<T>(string name, Func<T> f)
    {
        var old = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name); return f(); }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [Fact]
    public void Turkce_kulturde_anahtar_aynen_doner()
    {
        var s = WithUiCulture("tr-TR", () => CoreText.T("Küre"));
        Assert.Equal("Küre", s);
    }

    [Fact]
    public void Ingilizce_kulturde_ceviri_gelir()
    {
        Assert.Equal("Sphere", WithUiCulture("en-US", () => CoreText.T("Küre")));
        Assert.Equal("Cylinder", WithUiCulture("en-GB", () => CoreText.T("Silindir")));
    }

    [Fact]
    public void Cevirisi_olmayan_anahtar_ingilizcede_de_aynen_doner()
    {
        Assert.Equal("böyle bir anahtar yok", WithUiCulture("en-US", () => CoreText.T("böyle bir anahtar yok")));
    }

    [Fact]
    public void Bicimli_metin_yer_tutuculari_doldurur()
    {
        var tr = WithUiCulture("tr-TR", () => CoreText.T("{0} grup hesaplandı", 3));
        var en = WithUiCulture("en-US", () => CoreText.T("{0} grup hesaplandı", 3));
        Assert.Equal("3 grup hesaplandı", tr);
        Assert.Equal("3 group(s) calculated", en);
    }

    [Fact]
    public void Model_bayraklari_ingilizce_kulturde_ingilizce()
    {
        var name = WithUiCulture("en-US", () => HopfenbergModel.GeometryName(HopfenbergGeometry.HalfSphere));
        Assert.Equal("Half sphere", name);
        var interp = WithUiCulture("en-US", () => KorsmeyerPeppasModel.InterpretN(0.5));
        Assert.StartsWith("Fickian diffusion", interp);
    }

    [Fact]
    public void Tsv_yer_tutucu_kumeleri_anahtar_ve_ceviride_ayni()
    {
        using var stream = typeof(CoreText).Assembly.GetManifestResourceStream("PTCalc.Resources.CoreText.en.tsv");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var rx = new Regex(@"\{(\d+)(?::[^}]*)?\}");
        int n = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            Assert.True(parts.Length == 2, $"TSV satırı tam olarak bir sekme içermeli: {line[..Math.Min(60, line.Length)]}");
            var k = rx.Matches(parts[0]).Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
            var v = rx.Matches(parts[1]).Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
            Assert.True(k.SequenceEqual(v), $"Yer tutucular farklı: {parts[0][..Math.Min(50, parts[0].Length)]}");
            n++;
        }
        Assert.True(n > 80, $"TSV beklenenden kısa: {n} satır");
    }
}
