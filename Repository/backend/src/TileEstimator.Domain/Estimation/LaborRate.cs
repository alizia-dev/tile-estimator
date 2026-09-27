using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Domain.Estimation;

/// <summary>
/// SPEC 13 labor rate. Two pricing methods, chosen per rate:
/// <list type="bullet">
///   <item><c>UnitRate</c>: Quantity x Rate, e.g. 450 SF x $8.00/SF = $3,600.00</item>
///   <item><c>Productivity</c>: Quantity / Productivity x Rate, e.g. 450 SF / 50 SF-per-hour x $65/hr</item>
/// </list>
/// In the productivity method <see cref="Rate"/> is the hourly rate and
/// <see cref="Productivity"/> is units completed per hour.
/// </summary>
public class LaborRate : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Trade { get; private set; } = string.Empty;

    /// <summary>The unit the quantity is measured in: SF, LF, EA.</summary>
    public string Unit { get; private set; } = UnitOfMeasure.SquareFeet;

    public LaborCalculationMethod CalculationMethod { get; private set; } = LaborCalculationMethod.UnitRate;

    /// <summary>Cost per unit, or the hourly rate when the productivity method is used.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Units completed per hour. Required by the productivity method.</summary>
    public decimal? Productivity { get; private set; }

    public string? Description { get; private set; }
    public bool Active { get; private set; } = true;

    private LaborRate() { }

    public static LaborRate Create(Guid organizationId, string name, string trade, string unit,
        LaborCalculationMethod method, decimal rate, decimal? productivity)
    {
        Validate(name, trade, unit, method, rate, productivity);

        return new LaborRate
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Trade = trade.Trim(),
            Unit = unit.Trim().ToUpperInvariant(),
            CalculationMethod = method,
            Rate = Rounding.Quantity(rate),
            Productivity = productivity.HasValue ? Rounding.Quantity(productivity.Value) : null
        };
    }

    public void Update(string name, string trade, string unit, LaborCalculationMethod method,
        decimal rate, decimal? productivity, string? description, bool active)
    {
        Validate(name, trade, unit, method, rate, productivity);

        Name = name.Trim();
        Trade = trade.Trim();
        Unit = unit.Trim().ToUpperInvariant();
        CalculationMethod = method;
        Rate = Rounding.Quantity(rate);
        Productivity = productivity.HasValue ? Rounding.Quantity(productivity.Value) : null;
        Description = description;
        Active = active;
    }

    private static void Validate(string name, string trade, string unit, LaborCalculationMethod method,
        decimal rate, decimal? productivity)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Labor rate name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(trade), "Trade is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(unit), "Unit is required.");
        DomainException.Require(rate >= 0m, "Rate cannot be negative.");
        DomainException.Require(
            method != LaborCalculationMethod.Productivity || productivity is > 0m,
            "The productivity method needs a productivity greater than zero.");
    }
}
