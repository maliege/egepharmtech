using PTCalc.Site;
using Microsoft.AspNetCore.Localization;

namespace PTCalc.Localization;

/// <summary>
/// Dil varsayılanını alan adından alır: ptcalc.net → en-US, diğerleri → tr-TR. Çerez sağlayıcısı
/// bundan önce çalışır; kullanıcı dil anahtarıyla seçim yaptıysa o kazanır.
/// </summary>
public sealed class HostRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var culture = SiteProfiles.Resolve(httpContext.Request.Host.Value).DefaultCulture;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }
}
