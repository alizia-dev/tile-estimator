using FluentAssertions;
using TileEstimator.Application.Engines.Costing;
using TileEstimator.Application.Engines.Pricing;
using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using Xunit;

namespace TileEstimator.UnitTests.Engines;

public class CostCalculationEngineTests
{
    private readonly CostCalculationEngine _engine = new();

    [Fact]
    public void Costs_are_grouped_by_category()
    {
        var input = new CostCalculationInput
        {
            Lines =
            [
                new CostLineInput(EstimateLineCategory.Tile, "Tile", 200m, "SF", 2.50m, 4m),
                new CostLineInput(EstimateLineCategory.Material, "Thinset", 3m, "BAG", 18m, 28m),
                new CostLineInput(EstimateLineCategory.Labor, "Installation", 200m, "SF", 5m, 8m)
            ]
        };

        var result = _engine.Calculate(input);

        result.MaterialCost.Should().Be(554m); // 500 tile + 54 thinset
        result.LaborCost.Should().Be(1000m);
        result.DirectCost.Should().Be(1554m);
    }

    [Fact]
    public void Overhead_is_a_percentage_of_direct_cost()
    {
        var input = new CostCalculationInput
        {
            Lines = [new CostLineInput(EstimateLineCategory.Material, "Material", 1m, "EA", 1000m, 1500m)],
            OverheadPercentage = 10m
        };

        var result = _engine.Calculate(input);

        result.OverheadAmount.Should().Be(100m);
        result.TotalCost.Should().Be(1100m);
    }

    [Fact]
    public void Additional_costs_land_in_their_own_categories()
    {
        var input = new CostCalculationInput
        {
            AdditionalCosts =
            [
                new AdditionalCostInput(EstimateLineCategory.Equipment, "Saw rental", 150m),
                new AdditionalCostInput(EstimateLineCategory.Delivery, "Delivery", 95m),
                new AdditionalCostInput(EstimateLineCategory.Other, "Dump fee", 60m)
            ]
        };

        var result = _engine.Calculate(input);

        result.EquipmentCost.Should().Be(150m);
        result.DeliveryCost.Should().Be(95m);
        result.OtherCost.Should().Be(60m);
        result.DirectCost.Should().Be(305m);
    }

    [Fact]
    public void The_cost_engine_applies_no_markup()
    {
        var input = new CostCalculationInput
        {
            Lines = [new CostLineInput(EstimateLineCategory.Material, "Material", 10m, "EA", 10m, 99m)]
        };

        // The line's selling price of $99 must not leak into the cost.
        _engine.Calculate(input).TotalCost.Should().Be(100m);
    }

    [Fact]
    public void Negative_overhead_is_rejected()
    {
        var act = () => _engine.Calculate(new CostCalculationInput { OverheadPercentage = -1m });

        act.Should().Throw<DomainException>();
    }
}

public class PricingEngineTests
{
    private readonly PricingEngine _engine = new();

    private static PricingInput Input(decimal cost = 1000m) => new()
    {
        TotalCost = cost,
        MaterialCost = cost / 2m,
        LaborCost = cost / 2m
    };

    // --- Markup vs margin ------------------------------------------------------------------

    [Fact]
    public void Markup_multiplies_the_cost()
    {
        var result = _engine.Calculate(Input() with
        {
            Strategy = PricingStrategy.Markup,
            MarkupPercentage = 25m
        });

        result.Subtotal.Should().Be(1250m);
        result.MarkupAmount.Should().Be(250m);
    }

    [Fact]
    public void Margin_divides_the_cost_and_gives_a_different_answer_than_markup()
    {
        var markup = _engine.Calculate(Input() with { Strategy = PricingStrategy.Markup, MarkupPercentage = 25m });
        var margin = _engine.Calculate(Input() with { Strategy = PricingStrategy.Margin, MarginPercentage = 25m });

        // 1000 x 1.25 = 1250, but 1000 / 0.75 = 1333.33. Confusing the two loses real money.
        markup.Subtotal.Should().Be(1250m);
        margin.Subtotal.Should().Be(1333.33m);
        margin.Subtotal.Should().NotBe(markup.Subtotal);
    }

    [Fact]
    public void A_25_percent_margin_really_yields_a_25_percent_margin()
    {
        var result = _engine.Calculate(Input() with { Strategy = PricingStrategy.Margin, MarginPercentage = 25m });

        result.GrossMarginPercentage.Should().BeApproximately(25m, 0.01m);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(100.01)]
    [InlineData(150)]
    public void A_margin_of_100_percent_or_more_is_rejected(decimal margin)
    {
        var act = () => _engine.Calculate(Input() with
        {
            Strategy = PricingStrategy.Margin,
            MarginPercentage = margin
        });

        act.Should().Throw<DomainException>().WithMessage("*below 100%*");
    }

    [Fact]
    public void A_fixed_markup_adds_a_flat_amount()
    {
        var result = _engine.Calculate(Input() with
        {
            Strategy = PricingStrategy.FixedMarkup,
            FixedMarkupAmount = 375m
        });

        result.Subtotal.Should().Be(1375m);
    }

    [Fact]
    public void The_strategy_actually_used_is_reported_back()
    {
        var result = _engine.Calculate(Input() with { Strategy = PricingStrategy.Margin, MarginPercentage = 20m });

        result.Strategy.Should().Be(PricingStrategy.Margin);
        result.Breakdown.Formula.Should().Contain("/ (1 - 20%)");
    }

    // --- Discounts -------------------------------------------------------------------------

    [Fact]
    public void A_percentage_discount_comes_off_the_subtotal()
    {
        var result = _engine.Calculate(Input() with
        {
            MarkupPercentage = 25m,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10m
        });

        result.DiscountAmount.Should().Be(125m);
        result.GrandTotal.Should().Be(1125m);
    }

    [Fact]
    public void A_fixed_discount_never_pushes_the_price_below_zero()
    {
        var result = _engine.Calculate(Input(100m) with
        {
            MarkupPercentage = 0m,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 500m
        });

        result.DiscountAmount.Should().Be(100m);
        result.GrandTotal.Should().Be(0m);
    }

    [Fact]
    public void A_percentage_discount_above_100_is_rejected()
    {
        var act = () => _engine.Calculate(Input() with
        {
            DiscountType = DiscountType.Percentage,
            DiscountValue = 101m
        });

        act.Should().Throw<DomainException>();
    }

    // --- Tax -------------------------------------------------------------------------------

    [Fact]
    public void Tax_applies_to_the_whole_price_by_default()
    {
        var result = _engine.Calculate(Input() with
        {
            MarkupPercentage = 0m,
            TaxRatePercentage = 8.25m,
            TaxBasis = TaxBasis.MaterialsAndLabor
        });

        result.TaxableAmount.Should().Be(1000m);
        result.TaxAmount.Should().Be(82.50m);
        result.GrandTotal.Should().Be(1082.50m);
    }

    [Fact]
    public void A_materials_only_basis_taxes_only_the_material_share_of_the_price()
    {
        var result = _engine.Calculate(new PricingInput
        {
            TotalCost = 1000m,
            MaterialCost = 600m,
            LaborCost = 400m,
            MarkupPercentage = 0m,
            TaxRatePercentage = 10m,
            TaxBasis = TaxBasis.MaterialsOnly
        });

        result.TaxableAmount.Should().Be(600m);
        result.TaxAmount.Should().Be(60m);
    }

    [Fact]
    public void A_labor_only_basis_taxes_only_the_labor_share()
    {
        var result = _engine.Calculate(new PricingInput
        {
            TotalCost = 1000m,
            MaterialCost = 600m,
            LaborCost = 400m,
            MarkupPercentage = 0m,
            TaxRatePercentage = 10m,
            TaxBasis = TaxBasis.LaborOnly
        });

        result.TaxableAmount.Should().Be(400m);
        result.TaxAmount.Should().Be(40m);
    }

    [Fact]
    public void Discounting_before_tax_gives_a_smaller_total_than_discounting_after()
    {
        var before = _engine.Calculate(Input() with
        {
            MarkupPercentage = 0m,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10m,
            TaxRatePercentage = 10m,
            DiscountBeforeTax = true
        });

        var after = _engine.Calculate(Input() with
        {
            MarkupPercentage = 0m,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10m,
            TaxRatePercentage = 10m,
            DiscountBeforeTax = false
        });

        before.TaxAmount.Should().Be(90m);  // tax on 900
        after.TaxAmount.Should().Be(100m);  // tax on 1000
        before.GrandTotal.Should().Be(990m);
        after.GrandTotal.Should().Be(1000m);
        before.GrandTotal.Should().BeLessThan(after.GrandTotal);
    }

    [Fact]
    public void Zero_tax_produces_no_tax_line()
    {
        _engine.Calculate(Input() with { TaxRatePercentage = 0m }).TaxAmount.Should().Be(0m);
    }

    [Fact]
    public void Negative_tax_is_rejected()
    {
        var act = () => _engine.Calculate(Input() with { TaxRatePercentage = -1m });

        act.Should().Throw<DomainException>();
    }

    // --- Order of operations and reporting ---------------------------------------------------

    [Fact]
    public void The_full_chain_runs_cost_markup_discount_then_tax()
    {
        var result = _engine.Calculate(new PricingInput
        {
            TotalCost = 10_000m,
            MaterialCost = 6_000m,
            LaborCost = 4_000m,
            Strategy = PricingStrategy.Markup,
            MarkupPercentage = 20m,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 1_000m,
            TaxRatePercentage = 8m,
            TaxBasis = TaxBasis.MaterialsAndLabor,
            DiscountBeforeTax = true
        });

        result.Subtotal.Should().Be(12_000m);       // 10,000 x 1.20
        result.DiscountAmount.Should().Be(1_000m);
        result.TaxableAmount.Should().Be(11_000m);
        result.TaxAmount.Should().Be(880m);         // 11,000 x 8%
        result.GrandTotal.Should().Be(11_880m);
        result.GrossProfit.Should().Be(1_000m);     // 11,000 - 10,000
    }

    [Fact]
    public void Gross_profit_is_measured_after_the_discount()
    {
        var result = _engine.Calculate(Input() with
        {
            MarkupPercentage = 50m,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20m
        });

        result.Subtotal.Should().Be(1500m);
        result.DiscountAmount.Should().Be(300m);
        result.GrossProfit.Should().Be(200m); // 1200 - 1000, not 500
    }

    [Fact]
    public void A_zero_cost_estimate_does_not_divide_by_zero()
    {
        var result = _engine.Calculate(new PricingInput { TotalCost = 0m, MarkupPercentage = 25m });

        result.GrandTotal.Should().Be(0m);
        result.GrossMarginPercentage.Should().Be(0m);
        result.EffectiveMarkupPercentage.Should().Be(0m);
    }

    [Fact]
    public void Negative_cost_is_rejected()
    {
        var act = () => _engine.Calculate(Input(-1m));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Every_result_explains_itself()
    {
        var result = _engine.Calculate(Input() with { MarkupPercentage = 20m, TaxRatePercentage = 5m });

        result.Breakdown.Formula.Should().NotBeNullOrWhiteSpace();
        result.Breakdown.Steps.Should().Contain(s => s.Label == "Grand total");
    }
}
