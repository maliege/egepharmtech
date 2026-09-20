using EgePharmTech.Localization;
using EgePharmTech.Middleware;
using EgePharmTech.Site;
using EgePharmTech.Services;
using EgePharmTech.Core.Services;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Sayı/tarih biçimi sunucunun işletim sistemi diline bırakılmaz: aynı analiz makineye göre "105,0000" ya da
// "105.0000" yazardı. İki kültür: tr-TR ve en-US. Varsayılan alan adından gelir (egepharmtech.com → en-US,
// diğerleri tr-TR); kullanıcı dil anahtarıyla seçim yaptıysa çerez bunu ezer. Arayüz metinleri
// Resources/SharedResource.en.resx'te (anahtar = Türkçe metin), çekirdek mesajları Core/Resources/CoreText.en.tsv'de.
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var desteklenen = new[] { new CultureInfo("tr-TR"), new CultureInfo("en-US") };
    options.DefaultRequestCulture = new RequestCulture("tr-TR");
    options.SupportedCultures = desteklenen;
    options.SupportedUICultures = desteklenen;
    options.RequestCultureProviders = [new CookieRequestCultureProvider(), new HostRequestCultureProvider()];
});

// Analiz durumları devre (kullanıcı oturumu) ömürlüdür; veri sunucuda saklanmaz.
builder.Services.AddScoped<AnalysisState>();
builder.Services.AddScoped<AnalysisStateF1F2>();
builder.Services.AddScoped<AnalysisStateDescriptiveStats>();
builder.Services.AddScoped<AnalysisStateStatistics>();

// İletişim formu: SMTP ayarları appsettings.Production.json (sunucuda) ya da kullanıcı sırlarında (yerelde).
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Eski spps.* adları egepharmtech.*'e; yalnız maege.tr'de kalan eski bölümler oraya
app.UseMiddleware<BrandRedirectMiddleware>();

app.UseRequestLocalization();
app.UseRouting();

// Dil anahtarı: çerezi yazar ve aynı sayfaya döner (Blazor Server devresi kültürü bağlantıda alır,
// bu yüzden tam sayfa yüklemesiyle çağrılır). Yalnız desteklenen kültürler ve yerel yollar kabul edilir.
app.MapGet("/culture/set", (string c, string? r, HttpContext ctx) =>
{
    var supported = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value.SupportedUICultures!;
    var culture = supported.FirstOrDefault(x => string.Equals(x.Name, c, StringComparison.OrdinalIgnoreCase))?.Name ?? "tr-TR";
    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, Secure = ctx.Request.IsHttps });
    var target = string.IsNullOrEmpty(r) || !r.StartsWith('/') || r.StartsWith("//") ? "/" : r;
    return Results.LocalRedirect(target);
});

// Web uygulaması manifesti dile göre. wwwroot'ta statik manifest yok; <link rel="manifest"
// crossorigin="use-credentials"> ile dil çerezi de gelir, böylece egepharmtech.tr Türkçe,
// egepharmtech.com İngilizce açıklamayla kurulur.
app.MapGet("/site.webmanifest", (HttpContext ctx) =>
{
    var en = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
    var manifest = new
    {
        id = "/",
        name = en ? "EgePharmTech · Pharmaceutical technology calculation tools" : "EgePharmTech · Farmasötik teknoloji hesaplama araçları",
        short_name = SiteProfiles.BrandName,
        description = en
            ? "Statistical and computational tools for pharmaceutical technology"
            : "Farmasötik teknoloji için istatistik ve hesaplama araçları",
        lang = en ? "en" : "tr",
        start_url = "/",
        scope = "/",
        display = "standalone",
        background_color = "#ffffff",
        theme_color = "#0d9488",
        icons = new object[]
        {
            new { src = "/img/egepharmtech/icon-192.png", sizes = "192x192", type = "image/png" },
            new { src = "/img/egepharmtech/icon-512.png", sizes = "512x512", type = "image/png" },
            new { src = "/img/egepharmtech/icon-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" }
        }
    };
    ctx.Response.Headers.CacheControl = "public, max-age=3600";
    return Results.Json(manifest, contentType: "application/manifest+json");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
