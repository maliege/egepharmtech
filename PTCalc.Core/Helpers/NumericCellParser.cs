#nullable enable
using System.Globalization;

namespace PTCalc.Core.Helpers;

/// <summary>
/// Grid hücrelerinden ve dış kaynaklardan gelen sayı metinlerini <b>kültürden
/// bağımsız</b> biçimde okur.
///
/// <para><b>Neden kültüre dayalı okuma yetmiyor:</b> ondalık ayırıcı hem nokta
/// hem virgül olabildiği için tek kültür yetersiz kalır; iki kültürü
/// <see cref="NumberStyles.Any"/> ile denemek ise daha kötüdür, çünkü o stil
/// binlik ayırıcıyı kabul ettiğinden tek ayırıcılı metinler <i>yanlış</i>
/// kültürde de "geçerli" sayılır. <c>"3,5"</c> invariant kültürde 35, <c>"3.5"</c>
/// tr-TR'de 35 olarak okunur — ikisi de sessizce on kat şişer. Denemeleri
/// <see cref="CultureInfo.CurrentCulture"/> sırasına bağlamak da çözmez: o zaman
/// sonuç sunucunun bölge ayarına göre değişir.
///
/// <para><b>Kural (deterministik):</b></para>
/// <list type="bullet">
///   <item>Metinde hem nokta hem virgül varsa, <i>sonda kalan</i> ondalık
///     ayırıcıdır, diğeri gruplamadır: <c>"1.234,5"</c> ve <c>"1,234.5"</c> →
///     1234.5.</item>
///   <item>Tek tür ayırıcı bir kez geçiyorsa <i>ondalık</i> kabul edilir:
///     <c>"3,5"</c> → 3.5, <c>"12,345"</c> → 12.345. Tarayıcıdaki Handsontable
///     (numbro) da aynı yorumu yapar; böylece iki katman aynı sonucu üretir.</item>
///   <item>Aynı ayırıcı birden fazla geçiyorsa gruplamadır:
///     <c>"1.234.567"</c> → 1234567.</item>
/// </list>
///
/// <para>Normalleştirmeden sonra ayrıştırma her zaman invariant kültürle ve
/// binlik ayırıcıya izin vermeden yapılır; sonuç makinenin bölge ayarından
/// etkilenmez. Sayıya benzemeyen değerler (<c>"abc"</c>, <c>"%50"</c>) başarısız
/// döner ve çağıran tarafından atlanır.</para>
/// </summary>
public static class NumericCellParser
{
    /// <summary>
    /// Grid hücresi veya JSON değerini <see cref="double"/>'a çevirir. Sayısal
    /// tipler doğrudan alınır, metinler <see cref="TryParse"/> kuralına uyar.
    /// </summary>
    public static bool TryGetDouble(object? cell, out double value)
    {
        value = default;
        switch (cell)
        {
            case null: return false;
            case double d: value = d; return true;
            case float f: value = f; return true;
            case int i: value = i; return true;
            case long l: value = l; return true;
            case decimal m: value = (double)m; return true;
        }

        return TryParse(cell.ToString(), out value);
    }

    /// <summary>
    /// Sayı metnini kültürden bağımsız okur; sınıf açıklamasındaki kuralı uygular.
    /// </summary>
    public static bool TryParse(string? text, out double value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim();

        int dots = 0, commas = 0;
        foreach (var c in s)
        {
            if (c == '.') dots++;
            else if (c == ',') commas++;
        }

        string normalized;

        if (dots > 0 && commas > 0)
        {
            // Sonda kalan ondalıktır, diğeri gruplamadır.
            var decimalIsDot = s.LastIndexOf('.') > s.LastIndexOf(',');
            normalized = decimalIsDot
                ? s.Replace(",", string.Empty)
                : s.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (dots + commas == 1)
        {
            // Tek ayırıcı: ondalık kabul edilir.
            normalized = s.Replace(',', '.');
        }
        else if (dots + commas > 1)
        {
            // Aynı ayırıcı birden fazla: gruplama.
            normalized = s.Replace(",", string.Empty).Replace(".", string.Empty);
        }
        else
        {
            normalized = s;
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
