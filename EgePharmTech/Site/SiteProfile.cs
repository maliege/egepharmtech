namespace EgePharmTech.Site;

/// <summary>
/// EgePharmTech iki alan adında yayınlanır: <b>egepharmtech.tr</b> (varsayılan dil Türkçe) ve
/// <b>egepharmtech.com</b> (İngilizce). Eski marka SPPS'in alan adları (spps.tr, spps.tech) kalıcı
/// yönlendirmeyle yenilerine gider. Varsayılan dil gelen alan adından çözülür; çerezle seçilen dil alan
/// adı varsayılanını ezer. Buradaki mantık saf fonksiyondur ve SiteProfileTests ile sınanır.
/// </summary>
public sealed record SiteProfile(
    string HomeHost,           // bu dildeki kanonik ana bilgisayar
    string DefaultCulture)     // "tr-TR" | "en-US"
{
    public string BrandName => SiteProfiles.BrandName;
    public bool IsEnglish => DefaultCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
}

public static class SiteProfiles
{
    public const string BrandName = "EgePharmTech";
    public const string TrHost = "egepharmtech.tr";
    public const string EnHost = "egepharmtech.com";

    /// <summary>Eski marka alan adları: 301 ile yenilerine gider (bkz. <see cref="LegacyRedirectHost"/>).</summary>
    public const string LegacyTrHost = "spps.tr";
    public const string LegacyEnHost = "spps.tech";

    /// <summary>Yalnız maege.tr'de bulunan sayfalar oraya kalıcı yönlendirilir (eski SPPS bağlantıları için).</summary>
    public const string MaegeBaseUrl = "https://maege.tr";

    public static readonly SiteProfile Tr = new(TrHost, "tr-TR");
    public static readonly SiteProfile En = new(EnHost, "en-US");

    /// <summary>
    /// Alan adından profil: ".com" (ya da eski "spps.tech") içeren adlar İngilizce, diğer her şey Türkçe.
    /// Yerel geliştirmede <c>egepharmtech.com.localhost</c> İngilizce varsayılanı verir (Chrome *.localhost'u
    /// 127.0.0.1'e çözer); düz <c>localhost</c> Türkçedir.
    /// </summary>
    public static SiteProfile Resolve(string? host)
    {
        var h = NormalizeHost(host);
        if (h.Contains("egepharmtech.com", StringComparison.Ordinal) || h.Contains("spps.tech", StringComparison.Ordinal))
            return En;
        return Tr;
    }

    /// <summary>
    /// Eski marka alan adından (spps.tr, spps.tech, www'lu biçimleri) gelen istek için yeni ana
    /// bilgisayar; başka her ad için null. Yerel *.localhost adları yönlendirilmez.
    /// </summary>
    public static string? LegacyRedirectHost(string? host)
    {
        var h = NormalizeHost(host);
        return h switch
        {
            LegacyTrHost => TrHost,
            LegacyEnHost => EnHost,
            _ => null
        };
    }

    /// <summary>Verilen kültür için kanonik ana bilgisayar.</summary>
    public static string HostFor(string cultureName)
        => cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? EnHost : TrHost;

    private static string NormalizeHost(string? host)
    {
        var h = (host ?? string.Empty).Trim().ToLowerInvariant();
        int colon = h.IndexOf(':');
        if (colon >= 0) h = h[..colon];
        if (h.StartsWith("www.", StringComparison.Ordinal)) h = h[4..];
        return h;
    }

    // ---------- yollar ----------

    /// <summary>
    /// Bir zamanlar aynı uygulamada bulunan ama yalnız maege.tr'de kalan bölümler. Eski SPPS bağlantıları
    /// ve arama motoru kayıtları kırılmasın diye 301 ile maege.tr'ye gönderilir; bunların dışındaki
    /// bilinmeyen yollar sitenin kendi "bulunamadı" sayfasına düşer.
    /// </summary>
    private static readonly string[] MaegeOnlyPrefixes =
    [
        "/majistral", "/maddeler", "/telsiz", "/mors-test", "/astro", "/botanical-app",
        "/login", "/logout", "/register", "/resetpassword", "/accessdenied",
        "/adminpanel", "/admin", "/profiledetails", "/changepassword", "/pages",
    ];

    public static bool ShouldRedirectToMaege(string? path)
    {
        var p = Normalize(path);
        return MaegeOnlyPrefixes.Any(prefix =>
            p.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Kanonik adres: sayfa hangi dilde sunuluyorsa o dilin ana bilgisayarı.</summary>
    public static string CanonicalUrl(string? path, string cultureName)
        => $"https://{HostFor(cultureName)}{Normalize(path)}";

    /// <summary>hreflang alternatifleri: tr → .tr, en → .com, x-default → .tr.</summary>
    public static IReadOnlyList<(string Lang, string Url)> Alternates(string? path)
    {
        var p = Normalize(path);
        return [("tr", $"https://{TrHost}{p}"), ("en", $"https://{EnHost}{p}"), ("x-default", $"https://{TrHost}{p}")];
    }

    private static string Normalize(string? path)
    {
        var p = string.IsNullOrEmpty(path) ? "/" : path;
        if (!p.StartsWith('/')) p = "/" + p;
        if (p.Length > 1 && p.EndsWith('/')) p = p.TrimEnd('/');
        return p;
    }
}
