using EgePharmTech.Site;

namespace EgePharmTech.Core.Tests;

public class SiteProfileTests
{
    [Theory]
    [InlineData("egepharmtech.tr", "tr-TR")]
    [InlineData("WWW.EGEPHARMTECH.TR", "tr-TR")]
    [InlineData("egepharmtech.com", "en-US")]
    [InlineData("www.egepharmtech.com:443", "en-US")]
    [InlineData("egepharmtech.localhost:5277", "tr-TR")]
    [InlineData("egepharmtech.com.localhost:5277", "en-US")]
    [InlineData("localhost:5277", "tr-TR")]
    [InlineData("spps.tr", "tr-TR")]       // eski ad: profil aynı, yönlendirilir
    [InlineData("spps.tech", "en-US")]
    [InlineData("", "tr-TR")]
    [InlineData(null, "tr-TR")]
    public void Host_resolves_to_default_culture(string? host, string culture)
    {
        var p = SiteProfiles.Resolve(host);
        Assert.Equal(culture, p.DefaultCulture);
        Assert.Equal(culture.StartsWith("en"), p.IsEnglish);
        Assert.Equal("EgePharmTech", p.BrandName);
        Assert.Equal(culture.StartsWith("en") ? SiteProfiles.EnHost : SiteProfiles.TrHost, p.HomeHost);
    }

    [Theory]
    [InlineData("spps.tr", "egepharmtech.tr")]
    [InlineData("www.spps.tr:443", "egepharmtech.tr")]
    [InlineData("spps.tech", "egepharmtech.com")]
    [InlineData("WWW.SPPS.TECH", "egepharmtech.com")]
    [InlineData("egepharmtech.tr", null)]
    [InlineData("egepharmtech.com", null)]
    [InlineData("spps.localhost", null)]
    [InlineData("localhost", null)]
    [InlineData(null, null)]
    public void Legacy_hosts_redirect_to_new_hosts(string? host, string? expected)
        => Assert.Equal(expected, SiteProfiles.LegacyRedirectHost(host));

    [Theory]
    [InlineData("/", false)]
    [InlineData("/kinetik", false)]
    [InlineData("/f1f2/", false)]
    [InlineData("/about-kinetic-analysis", false)]
    [InlineData("/contact", false)]
    [InlineData("/bilinmeyen-sayfa", false)]              // sitenin kendi 404'ü
    [InlineData("/_blazor", false)]
    [InlineData("/css/site.css", false)]
    [InlineData("/site.webmanifest", false)]
    [InlineData("/culture/set", false)]
    [InlineData("/majistral", true)]
    [InlineData("/Maddeler", true)]
    [InlineData("/telsiz", true)]
    [InlineData("/Astro/SkyMap", true)]
    [InlineData("/login", true)]
    [InlineData("/admin/majistral", true)]
    [InlineData("/adminpanel", true)]
    public void Only_maege_sections_redirect_to_maege(string path, bool redirect)
        => Assert.Equal(redirect, SiteProfiles.ShouldRedirectToMaege(path));

    [Theory]
    [InlineData("/", "tr-TR", "https://egepharmtech.tr/")]
    [InlineData("/kinetik", "tr-TR", "https://egepharmtech.tr/kinetik")]
    [InlineData("/kinetik/", "en-US", "https://egepharmtech.com/kinetik")]
    [InlineData("/about", "en-US", "https://egepharmtech.com/about")]
    public void Canonical_url_follows_culture(string path, string culture, string expected)
        => Assert.Equal(expected, SiteProfiles.CanonicalUrl(path, culture));

    [Fact]
    public void Alternates_list_tr_en_and_default()
    {
        var alt = SiteProfiles.Alternates("/f1f2");
        Assert.Equal(["tr", "en", "x-default"], alt.Select(a => a.Lang).ToArray());
        Assert.Equal("https://egepharmtech.tr/f1f2", alt[0].Url);
        Assert.Equal("https://egepharmtech.com/f1f2", alt[1].Url);
        Assert.Equal("https://egepharmtech.tr/f1f2", alt[2].Url);
    }

    [Theory]
    [InlineData("tr-TR", "egepharmtech.tr")]
    [InlineData("en-US", "egepharmtech.com")]
    [InlineData("en", "egepharmtech.com")]
    public void Host_for_culture(string culture, string host)
        => Assert.Equal(host, SiteProfiles.HostFor(culture));
}
