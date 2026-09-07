using Microsoft.Extensions.DependencyInjection.Extensions;
using nuel;

namespace Microsoft.Extensions.DependencyInjection;

public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// Adds Captcha services (ICaptchaRenderer, ICaptchaStore, ICaptchaService) to the service collection.
    /// </summary>
    public static IServiceCollection AddCaptcha(
        this IServiceCollection services,
        Action<CaptchaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<CaptchaOptions>();
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<ICaptchaRenderer, CaptchaRenderer>();
        services.TryAddScoped<ICaptchaStore, SqlServerCaptchaStore>();
        services.TryAddScoped<ICaptchaService, CaptchaService>();

        return services;
    }

    /// <summary>
    /// Configures the SQL Server connection string for the captcha store.
    /// </summary>
    public static IServiceCollection AddSqlServerCaptchaStore(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.PostConfigure<CaptchaOptions>(options =>
        {
            options.ConnectionString = connectionString;
        });

        services.TryAddScoped<ICaptchaStore, SqlServerCaptchaStore>();
        return services;
    }
}

