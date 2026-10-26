using OpenHikeMap.Components;
using OpenHikeMap.Services;

namespace OpenHikeMap;

public static class WebApplicationHost
{
    public static WebApplication Create(string contentRoot, int port = 5186)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [], ApplicationName = typeof(WebApplicationHost).Assembly.GetName().Name,
            ContentRootPath = contentRoot, WebRootPath = Path.Combine(contentRoot, "wwwroot"),
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddUserSecrets(typeof(WebApplicationHost).Assembly, optional: true);
        builder.Logging.ClearProviders();
        builder.Configuration["AllowedHosts"] = "127.0.0.1;localhost";
        builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddHttpClient<SearchService>(c => c.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddHttpClient<RoutingService>(c => c.Timeout = TimeSpan.FromSeconds(45));
        builder.Services.AddHttpClient<ElevationService>();
        builder.Services.AddHttpClient<WeatherService>(c => c.Timeout = TimeSpan.FromSeconds(20));
        builder.Services.AddHttpClient("CivilProtection", c => { c.Timeout = TimeSpan.FromSeconds(30); c.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationInfo.UserAgent); });
        builder.Services.AddSingleton(sp => new CivilProtectionService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("CivilProtection")));
        builder.Services.AddSingleton<OsmTileService>();
        builder.Services.AddSingleton<AppSettingsStore>();
        builder.Services.AddHttpClient("OsmTiles", c => { c.Timeout = TimeSpan.FromSeconds(20); c.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationInfo.UserAgent); });
        builder.Services.AddScoped<GpxService>();
        var app = builder.Build();
        app.UseExceptionHandler("/error");
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/") context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.UseAntiforgery();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
        });
        app.MapGet("/tiles/osm/{z:int}/{x:int}/{y:int}.png", async (int z, int x, int y, OsmTileService tiles, HttpContext context) =>
        {
            if (z is < 0 or > 19 || x < 0 || y < 0 || x >= 1 << z || y >= 1 << z) return Results.BadRequest();
            try
            {
                var tile = await tiles.GetAsync(z, x, y);
                var maxAge = Math.Max(0, (int)(tile.Expires - DateTimeOffset.UtcNow).TotalSeconds);
                context.Response.Headers.CacheControl = tile.Cacheable ? $"public,max-age={maxAge}" : "no-store";
                return Results.File(tile.Bytes, "image/png", lastModified: tile.LastModified, entityTag: tile.ETag is { } tag ? Microsoft.Net.Http.Headers.EntityTagHeaderValue.Parse(tag) : null);
            }
            catch (TaskCanceledException) { context.Response.Headers.CacheControl = "no-store"; return Results.StatusCode(StatusCodes.Status504GatewayTimeout); }
            catch (HttpRequestException ex) { context.Response.Headers.CacheControl = "no-store"; return Results.StatusCode(ex.StatusCode is { } status ? (int)status : StatusCodes.Status502BadGateway); }
        });
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }
}
