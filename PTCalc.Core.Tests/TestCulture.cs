using System.Globalization;
using System.Runtime.CompilerServices;

namespace PTCalc.Core.Tests;

/// <summary>
/// Testler Core metinlerini Türkçe kaynak haliyle karşılaştırır (CoreText anahtar = Türkçe metin).
/// Arayüz kültürü İngilizce olan bir makinede/çalıştırıcıda CoreText İngilizce döndürür ve bu
/// testler kırılır; bu yüzden test sürecinin arayüz kültürü tr-TR'ye sabitlenir. Sayı biçimi
/// (CurrentCulture) dokunulmaz: NumericCellParserTests onu kendi ayarlar.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentUICulture = tr;
        CultureInfo.CurrentUICulture = tr;
    }
}
