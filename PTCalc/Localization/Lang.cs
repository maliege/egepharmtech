using System.Globalization;

namespace PTCalc.Localization;

/// <summary>
/// Geçerli arayüz dili için kısa yol. Sayfalar kısa metinleri <c>IStringLocalizer</c> ile, uzun
/// rehber bloklarını ise <c>@if (Lang.IsEn)</c> ile iki ayrı işaretleme bloğu olarak sunar; uzun
/// paragrafları resx anahtarı yapmak hem okunmaz hem de bakımı zordur.
/// </summary>
public static class Lang
{
    public static bool IsEn => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    /// <summary>Kültüre göre ondalık gösterimi (grafik/etiket metinleri için).</summary>
    public static string Tag => IsEn ? "en" : "tr";
}
