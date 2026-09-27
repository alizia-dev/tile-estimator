using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Engines.Costing;
using TileEstimator.Application.Engines.Pricing;
using TileEstimator.Application.Engines.Takeoff;
using TileEstimator.Infrastructure.Configuration;
using TileEstimator.Infrastructure.Jobs;
using TileEstimator.Infrastructure.Pdf;
using TileEstimator.Infrastructure.Persistence;
using TileEstimator.Infrastructure.Persistence.Seeding;
using TileEstimator.Infrastructure.Providers;
using TileEstimator.Infrastructure.Services;

namespace TileEstimator.Infrastructure;

/// <summary>Composition root for Infrastructure. The API calls this once at startup.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);
        AddPersistence(services, configuration);
        AddEngines(services);
        AddProviders(services);

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        // Options are validated on start so a missing JWT secret fails the deployment loudly
        // rather than at the first login attempt.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageSettings>()
            .Bind(configuration.GetSection(StorageSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection(EmailSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ApplicationSettings>()
            .Bind(configuration.GetSection(ApplicationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var databaseSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>()
                               ?? new DatabaseSettings();

        services.AddScoped<TenantSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            options.UseSqlServer(databaseSettings.ConnectionString, sql =>
            {
                sql.CommandTimeout(databaseSettings.CommandTimeoutSeconds);
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });

            options.AddInterceptors(provider.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // One instance per request, shared by the context's query filters and the API middleware
        // that populates it.
        services.AddScoped<CurrentOrganizationService>();
        services.AddScoped<ICurrentOrganizationService>(p => p.GetRequiredService<CurrentOrganizationService>());

        services.AddSingleton<IDateTimeProvider, SystemClock>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<SystemSeeder>();
        services.AddScoped<OrganizationProvisioner>();
        services.AddScoped<DemoDataSeeder>();
    }

    private static void AddEngines(IServiceCollection services)
    {
        // The engines are pure and stateless, so a singleton is both safe and cheap.
        services.AddSingleton<ITakeoffEngine, TakeoffEngine>();
        services.AddSingleton<ICostCalculationEngine, CostCalculationEngine>();
        services.AddSingleton<IPricingEngine, PricingEngine>();
    }

    private static void AddProviders(IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddSingleton<IFileStorageProvider, LocalFileStorageProvider>();
        services.AddScoped<IEmailSender, FileSystemEmailSender>();
        services.AddScoped<INotificationProvider, LoggingNotificationProvider>();
        services.AddScoped<IQuoteDeliveryProvider, EmailQuoteDeliveryProvider>();
        services.AddSingleton<IQuotePdfGenerator, QuotePdfGenerator>();
        services.AddSingleton<IBackgroundJobScheduler, BackgroundJobScheduler>();
        services.AddHostedService<BackgroundJobRunner>();

        // SPEC 20 future providers: registered so the call sites exist, implemented as explicit
        // "not available" so nothing in the MVP silently depends on AI.
        services.AddSingleton<IDocumentTakeoffProvider, NotAvailableDocumentTakeoffProvider>();
        services.AddSingleton<ITakeoffExtractionProvider, NotAvailableTakeoffExtractionProvider>();
        services.AddSingleton<IExternalPricingProvider, NotAvailableExternalPricingProvider>();
    }
}
