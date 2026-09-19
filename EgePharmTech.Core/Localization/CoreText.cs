using System.Globalization;
using System.Reflection;

namespace EgePharmTech.Core.Localization;

/// <summary>
/// Core katmanının kullanıcıya dönen metinleri (uyarı bayrakları, girdi uyarıları, katsayı açıklamaları,
/// birimler, istisna mesajları) için yerelleştirme. Anahtar = Türkçe kaynak metnin kendisi; İngilizce
/// karşılıklar <c>Resources/CoreText.en.tsv</c> içinde (gömülü kaynak, satır = anahtar TAB çeviri).
/// Kültür İngilizce değilse ya da çeviri yoksa anahtar aynen döner; böylece Türkçe davranış ve metne
/// bakan testler değişmez. Uydu derlemesi kullanılmaz: derleme adı/kök ad alanı büyük-küçük harf
/// tuzağından (bkz. web projesi csproj) etkilenmesin.
/// </summary>
public static class CoreText
{
    private const string ResourceName = "EgePharmTech.Resources.CoreText.en.tsv";

    private static readonly Lazy<Dictionary<string, string>> English = new(Load);

    public static bool IsEnglish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>Düz metin: İngilizce kültürde çeviri, yoksa anahtar.</summary>
    public static string T(string key)
    {
        if (!IsEnglish) return key;
        return English.Value.TryGetValue(key, out var en) ? en : key;
    }

    /// <summary>Biçimli metin: yer tutucular geçerli kültürün sayı biçimiyle doldurulur.</summary>
    public static string T(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, T(key), args);

    private static Dictionary<string, string> Load()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null) return dict;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            var key = Unescape(line[..tab]);
            var value = Unescape(line[(tab + 1)..]);
            dict[key] = value;
        }
        return dict;
    }

    /// <summary>TSV'de satır sonu ve sekme <c>\n</c> / <c>\t</c> olarak yazılır.</summary>
    private static string Unescape(string s)
        => s.Contains('\\') ? s.Replace("\\n", "\n").Replace("\\t", "\t") : s;
}
