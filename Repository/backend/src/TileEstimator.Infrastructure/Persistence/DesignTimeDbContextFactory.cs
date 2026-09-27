using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TileEstimator.Infrastructure.Services;

namespace TileEstimator.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> at design time. It builds a context with no tenant context,
/// which is correct here: migrations describe the schema and never read tenant data.
/// The connection string is a placeholder unless one is supplied via the environment.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string FallbackConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TileEstimator;Trusted_Connection=True;TrustServerCertificate=True";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("Database__ConnectionString") ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options, new CurrentOrganizationService());
    }
}
