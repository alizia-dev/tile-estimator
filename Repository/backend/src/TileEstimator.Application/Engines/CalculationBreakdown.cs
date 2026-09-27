using System.Globalization;

namespace TileEstimator.Application.Engines;

/// <summary>
/// The audit trail for a single calculated number. Every engine result carries one so the UI and
/// the PDF can explain a figure without recomputing it, e.g. "450 SF x $8.00/SF = $3,600.00".
/// </summary>
public sealed record CalculationBreakdown(string Formula, IReadOnlyList<CalculationStep> Steps)
{
    public static CalculationBreakdown Empty { get; } = new(string.Empty, Array.Empty<CalculationStep>());

    public override string ToString() => Formula;
}

/// <summary>One labelled intermediate value inside a breakdown.</summary>
public sealed record CalculationStep(string Label, decimal Value, string? Unit = null)
{
    public override string ToString() =>
        Unit is null
            ? $"{Label}: {Value.ToString("0.####", CultureInfo.InvariantCulture)}"
            : $"{Label}: {Value.ToString("0.####", CultureInfo.InvariantCulture)} {Unit}";
}

/// <summary>Builds a breakdown without every engine re-implementing string formatting.</summary>
public sealed class BreakdownBuilder
{
    private readonly List<CalculationStep> _steps = [];
    private string _formula = string.Empty;

    public BreakdownBuilder Step(string label, decimal value, string? unit = null)
    {
        _steps.Add(new CalculationStep(label, value, unit));
        return this;
    }

    public BreakdownBuilder Formula(string formula)
    {
        _formula = formula;
        return this;
    }

    public CalculationBreakdown Build() => new(_formula, _steps);

    public static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats money for a breakdown string. The app runs with invariant globalization, so the
    /// symbol is prepended rather than taken from a culture.
    /// </summary>
    public static string Money(decimal value) =>
        (value < 0m ? "-$" : "$") + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);
}
