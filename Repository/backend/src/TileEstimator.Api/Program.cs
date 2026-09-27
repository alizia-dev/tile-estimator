using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using Serilog;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Middleware;
using TileEstimator.Api.Services;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Services;
using TileEstimator.Application.Services.Auth;
using TileEstimator.Application.Services.Estimating;
using TileEstimator.Application.Services.Quoting;
using TileEstimator.Infrastructure;
using TileEstimator.Infrastructure.Configuration;
using TileEstimator.Infrastructure.Persistence;
using TileEstimator.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (SPEC 19) ------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/tile-estimator-.log", rollingInterval: Serilog.RollingInterval.Day,
        retainedFileCountLimit: 14));

// QuestPDF's community licence covers this use; recorded in docs/adr/ADR-011.
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IOrganizationProvisioningService, OrganizationProvisioningService>();

// --- Application services ------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<TakeoffBuilder>();
builder.Services.AddScoped<EstimateService>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped(sp =>
{
    var app = sp.GetRequiredService<IOptions<ApplicationSettings>>().Value;
    var jwt = sp.GetRequiredService<IOptions<JwtSettings>>().Value;
    return new AuthOptions(
        TimeSpan.FromDays(jwt.RefreshTokenDays),
        TimeSpan.FromHours(app.EmailVerificationHours),
        TimeSpan.FromHours(app.PasswordResetHours),
        TimeSpan.FromDays(app.InvitationDays),
        app.MaxFailedLoginAttempts,
        app.LockoutMinutes,
        app.WebAppBaseUrl);
});

builder.Services.AddScoped(sp =>
{
    var app = sp.GetRequiredService<IOptions<ApplicationSettings>>().Value;
    return new QuoteOptions(app.WebAppBaseUrl, app.QuoteLinkGraceDays);
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// --- Authentication (SPEC 5) -----------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            NameClaimType = ClaimTypes.NameIdentifier,
            // No leeway: an expired token is expired.
            ClockSkew = TimeSpan.Zero
        };
    });

// --- Authorization (SPEC 6) -------------------------------------------------------------------
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// --- MVC ---------------------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Model-binding failures use the same ProblemDetails shape as everything else.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Title = "Validation error",
            Status = StatusCodes.Status400BadRequest
        };
        problem.Extensions["traceId"] = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]
                                        ?? context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});

// --- CORS --------------------------------------------------------------------------------------
var appSettings = builder.Configuration.GetSection(ApplicationSettings.SectionName).Get<ApplicationSettings>()
                  ?? new ApplicationSettings();

builder.Services.AddCors(options => options.AddPolicy("WebApp", policy => policy
    .WithOrigins(appSettings.CorsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

// --- Rate limiting: auth and the public quote page are the exposed surfaces -------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("public-quote", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

// --- Health checks ------------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

// --- Swagger with JWT ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tile Estimator API",
        Version = "v1",
        Description = "Estimating, quoting and customer approval for tile contractors."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/auth/login."
    });

    // Microsoft.OpenApi v2 references a scheme by id rather than by an inline reference object.
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = []
    });
});

var app = builder.Build();

// --- Pipeline -----------------------------------------------------------------------------------
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId",
            httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString());
        diagnosticContext.Set("OrganizationId",
            httpContext.RequestServices.GetService<ICurrentOrganizationService>()?.OrganizationId);
    });

// Security headers. A JSON API serves no scripts, so the CSP can be as tight as this.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tile Estimator API v1"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("WebApp");
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

await InitializeDatabaseAsync(app);

await app.RunAsync();

/// <summary>
/// Applies migrations and seeds reference data at startup. Migration and demo seeding are both
/// opt-in through configuration so a production deployment stays in control of its schema.
/// </summary>
static async Task InitializeDatabaseAsync(WebApplication app)
{
    var databaseSettings = app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (databaseSettings.AutoMigrate)
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
        }

        if (!await db.Database.CanConnectAsync())
        {
            logger.LogWarning("The database is not reachable. Reference data was not seeded.");
            return;
        }

        await scope.ServiceProvider.GetRequiredService<SystemSeeder>().SeedAsync();

        if (databaseSettings.SeedDemoData && app.Environment.IsDevelopment())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
    }
#pragma warning disable CA1031 // Startup seeding must not stop the API from coming up.
    catch (Exception ex)
#pragma warning restore CA1031
    {
        logger.LogError(ex, "Database initialization failed. The API is running, but check the connection string.");
    }
}

/// <summary>Exposed so the integration tests can start the API with WebApplicationFactory.</summary>
public partial class Program;
