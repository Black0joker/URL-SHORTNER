using Microsoft.Extensions.DependencyInjection;
using URLShortener.Application.Interfaces;
using URLShortener.Application.Services;

namespace URLShortener.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUrlService, UrlService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRedirectService, RedirectService>();
        services.AddScoped<IExpiredUrlCleanupService, ExpiredUrlCleanupService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();

        return services;
    }
}
