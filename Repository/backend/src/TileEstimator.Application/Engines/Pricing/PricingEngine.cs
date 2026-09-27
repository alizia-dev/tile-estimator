using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.ValueObjects;

namespace TileEstimator.Application.Engines.Pricing;

public sealed record PricingInput
{
    /// <summary>Total cost from the cost engine: direct cost plus overhead.</summary>
    public required decimal TotalCost { get; init; }

    /// <summary>Material share of the direct cost. Used to apportion tax when labor is not taxable.</summary>
    public decimal MaterialCost { get; init; }

    /// <summary>Labor share of the direct cost.</summary>
    public decimal LaborCost { get; init; }

    public PricingStrategy Strategy { get; init; } = PricingStrategy.Markup;
    public decimal MarkupPercentage { get; init; }
    public decimal MarginPercentage { get; init; }
    public decimal FixedMarkupAmount { get; init; }

    public DiscountType DiscountType { get; init; } = DiscountType.None;
    public decimal DiscountValue { get; init; }

    public decimal TaxRatePercentage { get; init; }
    public TaxBasis TaxBasis { get; init; } = TaxBasis.MaterialsAndLabor;

    /// <summary>True discounts the pre-tax subtotal; false taxes the full subtotal and discounts afterwards.</summary>
    public bool DiscountBeforeTax { get; init; } = true;
}

public sealed record PricingResult(
    decimal TotalCost,
    PricingStrategy Strategy,
    decimal MarkupAmount,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    decimal GrossProfit,
    decimal GrossMarginPercentage,
    decimal EffectiveMarkupPercentage,
    CalculationBreakdown Breakdown);

/// <summary>
/// SPEC 14 pricing engine: "what do we charge?". Markup and margin are genuinely different
/// calculations and the strategy actually used is returned so it can be stored on the estimate.
/// </summary>
public interface IPricingEngine
{
    PricingResult Calculate(PricingInput input);
}

/// <summary>
/// Applies the documented order of operations (docs/pricing-engine.md):
/// <code>
/// 1. Cost      = direct cost + overhead          (from the cost engine)
/// 2. Subtotal  = cost with markup / margin / fixed markup applied
/// 3. Discount  = percentage or fixed amount off the subtotal
/// 4. Tax       = taxable base x tax rate, where the base depends on the tax basis
/// 5. Total     = discounted subtotal + tax
/// </code>
/// </summary>
public sealed class PricingEngine : IPricingEngine
{
    public PricingResult Calculate(PricingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        DomainException.Require(input.TotalCost >= 0m, "Total cost cannot be negative.");
        DomainException.Require(input.TaxRatePercentage >= 0m, "Tax rate cannot be negative.");

        var (subtotal, markupFormula) = ApplyStrategy(input);
        var markupAmount = Rounding.Money(subtotal - input.TotalCost);

        var discountAmount = CalculateDiscount(input, subtotal);
        var discountedSubtotal = Rounding.Money(subtotal - discountAmount);

        // Tax is charged on whichever part of the price the organization's state taxes. We
        // apportion the priced subtotal by the material/labor cost mix rather than taxing cost,
        // so the customer is taxed on what they are actually charged.
        var taxBase = input.DiscountBeforeTax ? discountedSubtotal : subtotal;
        var taxableAmount = Rounding.Money(taxBase * TaxableShare(input));
        var taxAmount = Rounding.Money(taxableAmount * (input.TaxRatePercentage / 100m));

        var grandTotal = input.DiscountBeforeTax
            ? Rounding.Money(discountedSubtotal + taxAmount)
            : Rounding.Money(subtotal + taxAmount - discountAmount);

        var grossProfit = Rounding.Money(discountedSubtotal - input.TotalCost);
        var grossMargin = discountedSubtotal == 0m
            ? 0m
            : Rounding.Quantity(grossProfit / discountedSubtotal * 100m);
        var effectiveMarkup = input.TotalCost == 0m
            ? 0m
            : Rounding.Quantity(grossProfit / input.TotalCost * 100m);

        var breakdown = new BreakdownBuilder()
            .Step("Total cost", input.TotalCost)
            .Step("Markup amount", markupAmount)
            .Step("Subtotal", subtotal)
            .Step("Discount", discountAmount)
            .Step("Taxable amount", taxableAmount)
            .Step("Tax", taxAmount)
            .Step("Grand total", grandTotal)
            .Step("Gross profit", grossProfit)
            .Step("Gross margin", grossMargin, "%")
            .Formula(markupFormula + $"; discount {BreakdownBuilder.Money(discountAmount)}; tax {BreakdownBuilder.Number(input.TaxRatePercentage)}% on {BreakdownBuilder.Money(taxableAmount)} = {BreakdownBuilder.Money(taxAmount)}; total {BreakdownBuilder.Money(grandTotal)}")
            .Build();

        return new PricingResult(input.TotalCost, input.Strategy, markupAmount, subtotal, discountAmount,
            taxableAmount, taxAmount, grandTotal, grossProfit, grossMargin, effectiveMarkup, breakdown);
    }

    private static (decimal Subtotal, string Formula) ApplyStrategy(PricingInput input)
    {
        switch (input.Strategy)
        {
            case PricingStrategy.Markup:
            {
                DomainException.Require(input.MarkupPercentage >= 0m, "Markup cannot be negative.");
                var subtotal = Rounding.Money(input.TotalCost * (1m + input.MarkupPercentage / 100m));
                return (subtotal,
                    $"{BreakdownBuilder.Money(input.TotalCost)} x (1 + {BreakdownBuilder.Number(input.MarkupPercentage)}%) = {BreakdownBuilder.Money(subtotal)}");
            }

            case PricingStrategy.Margin:
            {
                // Price = Cost / (1 - Margin). At 100% the divisor is zero and the price is infinite,
                // which is why a margin of 100% or more is rejected rather than clamped.
                DomainException.Require(input.MarginPercentage >= 0m && input.MarginPercentage < 100m,
                    "Gross margin must be at least 0% and below 100%.");
                var subtotal = Rounding.Money(input.TotalCost / (1m - input.MarginPercentage / 100m));
                return (subtotal,
                    $"{BreakdownBuilder.Money(input.TotalCost)} / (1 - {BreakdownBuilder.Number(input.MarginPercentage)}%) = {BreakdownBuilder.Money(subtotal)}");
            }

            case PricingStrategy.FixedMarkup:
            {
                DomainException.Require(input.FixedMarkupAmount >= 0m, "Fixed markup cannot be negative.");
                var subtotal = Rounding.Money(input.TotalCost + input.FixedMarkupAmount);
                return (subtotal,
                    $"{BreakdownBuilder.Money(input.TotalCost)} + {BreakdownBuilder.Money(input.FixedMarkupAmount)} = {BreakdownBuilder.Money(subtotal)}");
            }

            default:
                throw new DomainException($"Unsupported pricing strategy '{input.Strategy}'.");
        }
    }

    private static decimal CalculateDiscount(PricingInput input, decimal subtotal)
    {
        switch (input.DiscountType)
        {
            case DiscountType.None:
                return 0m;

            case DiscountType.Percentage:
                DomainException.Require(input.DiscountValue >= 0m && input.DiscountValue <= 100m,
                    "A percentage discount must be between 0% and 100%.");
                return Rounding.Money(subtotal * (input.DiscountValue / 100m));

            case DiscountType.FixedAmount:
                DomainException.Require(input.DiscountValue >= 0m, "A discount cannot be negative.");
                // A fixed discount never takes the price below zero.
                return Rounding.Money(Math.Min(input.DiscountValue, subtotal));

            default:
                throw new DomainException($"Unsupported discount type '{input.DiscountType}'.");
        }
    }

    /// <summary>
    /// The fraction of the price that is taxable, derived from the material/labor cost mix.
    /// When there is no cost to apportion, everything is taxable so tax is never silently skipped.
    /// </summary>
    private static decimal TaxableShare(PricingInput input)
    {
        if (input.TaxBasis == TaxBasis.MaterialsAndLabor)
        {
            return 1m;
        }

        var material = Math.Max(0m, input.MaterialCost);
        var labor = Math.Max(0m, input.LaborCost);
        var basis = material + labor;

        if (basis == 0m)
        {
            return 1m;
        }

        return input.TaxBasis == TaxBasis.MaterialsOnly
            ? material / basis
            : labor / basis;
    }
}
