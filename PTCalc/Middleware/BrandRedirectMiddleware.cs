using PTCalc.Site;

namespace PTCalc.Middleware;

/// <summary>
/// Marka yönlendirmeleri, iki adım:
/// 1) Eski SPPS alan adları (spps.tr, spps.tech) her yol için 301 ile ptcalc.tr / .com'a.
/// 2) Yalnız maege.tr'de bulunan eski bölümlere (majistral, telsiz, astronomi, hesap işlemleri) gelen
///    GET istekleri 301 ile maege.tr'ye. Karar <see cref="SiteProfiles.ShouldRedirectToMaege"/>'de.
/// </summary>
public sealed class BrandRedirectMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly string _maegeBase = (config["Site:MaegeBaseUrl"] ?? SiteProfiles.MaegeBaseUrl).TrimEnd('/');

    public Task InvokeAsync(HttpContext context)
    {
        var newHost = SiteProfiles.LegacyRedirectHost(context.Request.Host.Value);
        if (newHost is not null)
        {
            context.Response.Redirect($"https://{newHost}{context.Request.Path}{context.Request.QueryString}", permanent: true);
            return Task.CompletedTask;
        }

        if (HttpMethods.IsGet(context.Request.Method) && SiteProfiles.ShouldRedirectToMaege(context.Request.Path.Value))
        {
            context.Response.Redirect(_maegeBase + context.Request.Path + context.Request.QueryString, permanent: true);
            return Task.CompletedTask;
        }
        return next(context);
    }
}
