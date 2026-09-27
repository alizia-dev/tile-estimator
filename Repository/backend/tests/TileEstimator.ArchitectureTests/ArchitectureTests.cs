using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace TileEstimator.ArchitectureTests;

/// <summary>
/// Guards the dependency rules from SPEC 3 so a stray using directive cannot quietly turn the
/// layering into a ball of mud. These fail the build, not a code review.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(TileEstimator.Domain.Common.Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(TileEstimator.Application.Engines.Takeoff.TakeoffEngine).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(TileEstimator.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(TileEstimator.Api.Controllers.AuthController).Assembly;

    private const string DomainNamespace = "TileEstimator.Domain";
    private const string ApplicationNamespace = "TileEstimator.Application";
    private const string InfrastructureNamespace = "TileEstimator.Infrastructure";
    private const string ApiNamespace = "TileEstimator.Api";

    [Fact]
    public void Domain_must_not_depend_on_EF_Core()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain must stay persistence-ignorant. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_must_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "dependencies point inward. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_must_not_depend_on_Application_or_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain sits at the centre. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_must_not_depend_on_ASP_NET_Core()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain knows nothing about HTTP. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_must_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application defines the interfaces; Infrastructure implements them. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_must_not_depend_on_the_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must be usable without a web host. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_must_not_reference_a_concrete_database_provider()
    {
        // Referencing EF Core's abstractions (DbSet, IQueryable) is fine; binding to SQL Server
        // is not, because that is an infrastructure choice.
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore.SqlServer")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the provider is chosen in Infrastructure. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_calculation_engines_must_not_touch_the_database()
    {
        // SPEC 14: the engines are pure and synchronous. A DbContext in here would mean a
        // formula could quietly depend on live catalog data instead of the caller's snapshot.
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("TileEstimator.Application.Engines")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the engines are pure. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Controllers_must_not_reference_the_concrete_DbContext()
    {
        // Controllers work against IApplicationDbContext, never the EF type directly.
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("TileEstimator.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("TileEstimator.Infrastructure.Persistence.ApplicationDbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "controllers depend on the abstraction. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Controllers_must_be_sealed_so_behaviour_is_not_inherited_into_surprises()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("TileEstimator.Api.Controllers")
            .And().AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Offenders: {0}", string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void No_project_may_reference_an_AI_or_ML_package()
    {
        // CLAUDE.md rule 1: the MVP is 100% deterministic and non-AI. This test is what keeps
        // that true as dependencies get added.
        string[] forbidden =
        [
            "OpenAI", "Azure.AI", "Anthropic", "Microsoft.ML", "TensorFlow",
            "Microsoft.SemanticKernel", "LangChain", "Microsoft.CognitiveServices"
        ];

        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly })
        {
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

            foreach (var name in referenced)
            {
                forbidden.Should().NotContain(
                    f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase),
                    "{0} references '{1}', but the MVP must contain no AI or ML dependency",
                    assembly.GetName().Name, name);
            }
        }
    }

    [Fact]
    public void Every_tenant_owned_entity_must_expose_an_OrganizationId()
    {
        // SPEC 4 depends on this: the query filters and the save interceptor both key off it.
        var tenantOwned = Types.InAssembly(DomainAssembly)
            .That().ImplementInterface(typeof(TileEstimator.Domain.Common.ITenantOwned))
            .GetTypes();

        tenantOwned.Should().NotBeEmpty("the domain has tenant-owned entities");

        foreach (var type in tenantOwned)
        {
            type.GetProperty(nameof(TileEstimator.Domain.Common.ITenantOwned.OrganizationId))
                .Should().NotBeNull("{0} must carry an OrganizationId", type.Name);
        }
    }

    [Fact]
    public void Money_and_quantity_properties_must_never_be_double_or_float()
    {
        // CLAUDE.md rule 3. Binary floating point cannot represent 0.1, so it must not come
        // near a price, a quantity that feeds a price, or a percentage.
        string[] moneyish =
        [
            "Cost", "Price", "Amount", "Total", "Rate", "Percentage", "Quantity",
            "Coverage", "Feet", "Inches", "Area", "Subtotal", "Markup", "Margin",
            "Discount", "Tax", "Overhead", "Factor", "Productivity"
        ];

        var offenders = new List<string>();

        foreach (var type in Types.InAssembly(DomainAssembly).GetTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if ((underlying == typeof(double) || underlying == typeof(float)) &&
                    moneyish.Any(m => property.Name.Contains(m, StringComparison.Ordinal)))
                {
                    offenders.Add($"{type.Name}.{property.Name} ({underlying.Name})");
                }
            }
        }

        offenders.Should().BeEmpty("money and quantities must be decimal");
    }
}
