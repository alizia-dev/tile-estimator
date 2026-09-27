using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Application.Engines.Costing;

/// <summary>One costed row going into the cost engine. Price is carried along but not used here.</summary>
public sealed record CostLineInput(
    EstimateLineCategory Category,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitCost,
    decimal UnitPrice);

/// <summary>
/// Costs the engine is given directly rather than deriving from a takeoff: equipment rental,
/// delivery, dump fees and anything else the estimator adds by hand.
/// </summary>
public sealed record AdditionalCostInput(
    EstimateLineCategory Category,
    string Description,
    decimal Amount);

public sealed record CostCalculationInput
{
    public IReadOnlyList<CostLineInput> Lines { get; init; } = [];
    public IReadOnlyList<AdditionalCostInput> AdditionalCosts { get; init; } = [];

    /// <summary>Overhead as a percentage of direct cost, e.g. 10 for 10%.</summary>
    public decimal OverheadPercentage { get; init; }
}

/// <summary>
/// What the job costs the contractor, broken out by category. Contains no markup, margin,
/// discount or tax: that is the pricing engine's job.
/// </summary>
public sealed record CostCalculationResult(
    decimal MaterialCost,
    decimal LaborCost,
    decimal EquipmentCost,
    decimal DeliveryCost,
    decimal OtherCost,
    decimal DirectCost,
    decimal OverheadPercentage,
    decimal OverheadAmount,
    decimal TotalCost,
    CalculationBreakdown Breakdown);

/// <summary>SPEC 14 cost engine: "what does the job cost us?". No markup logic lives here.</summary>
public interface ICostCalculationEngine
{
    CostCalculationResult Calculate(CostCalculationInput input);
}

/// <inheritdoc cref="ICostCalculationEngine"/>
public sealed class CostCalculationEngine : ICostCalculationEngine
{
    public CostCalculationResult Calculate(CostCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        DomainException.Require(input.OverheadPercentage >= 0m, "Overhead cannot be negative.");

        var material = 0m;
        var labor = 0m;
        var equipment = 0m;
        var delivery = 0m;
        var other = 0m;

        foreach (var line in input.Lines)
        {
            DomainException.Require(line.Quantity >= 0m, $"'{line.Description}' has a negative quantity.");
            DomainException.Require(line.UnitCost >= 0m, $"'{line.Description}' has a negative unit cost.");

            var extended = line.Quantity * line.UnitCost;
            switch (line.Category)
            {
                case EstimateLineCategory.Tile:
                case EstimateLineCategory.Material:
                    material += extended;
                    break;
                case EstimateLineCategory.Labor:
                    labor += extended;
                    break;
                case EstimateLineCategory.Equipment:
                    equipment += extended;
                    break;
                case EstimateLineCategory.Delivery:
                    delivery += extended;
                    break;
                default:
                    other += extended;
                    break;
            }
        }

        foreach (var cost in input.AdditionalCosts)
        {
            DomainException.Require(cost.Amount >= 0m, $"'{cost.Description}' has a negative amount.");
            switch (cost.Category)
            {
                case EstimateLineCategory.Tile:
                case EstimateLineCategory.Material:
                    material += cost.Amount;
                    break;
                case EstimateLineCategory.Labor:
                    labor += cost.Amount;
                    break;
                case EstimateLineCategory.Equipment:
                    equipment += cost.Amount;
                    break;
                case EstimateLineCategory.Delivery:
                    delivery += cost.Amount;
                    break;
                default:
                    other += cost.Amount;
                    break;
            }
        }

        material = Rounding.Money(material);
        labor = Rounding.Money(labor);
        equipment = Rounding.Money(equipment);
        delivery = Rounding.Money(delivery);
        other = Rounding.Money(other);

        var direct = Rounding.Money(material + labor + equipment + delivery + other);
        var overheadAmount = Rounding.Money(direct * (input.OverheadPercentage / 100m));
        var totalCost = Rounding.Money(direct + overheadAmount);

        var breakdown = new BreakdownBuilder()
            .Step("Material cost", material)
            .Step("Labor cost", labor)
            .Step("Equipment cost", equipment)
            .Step("Delivery cost", delivery)
            .Step("Other cost", other)
            .Step("Direct cost", direct)
            .Step("Overhead", input.OverheadPercentage, "%")
            .Step("Overhead amount", overheadAmount)
            .Step("Total cost", totalCost)
            .Formula($"{BreakdownBuilder.Money(direct)} + {BreakdownBuilder.Number(input.OverheadPercentage)}% overhead ({BreakdownBuilder.Money(overheadAmount)}) = {BreakdownBuilder.Money(totalCost)}")
            .Build();

        return new CostCalculationResult(material, labor, equipment, delivery, other, direct,
            input.OverheadPercentage, overheadAmount, totalCost, breakdown);
    }
}
