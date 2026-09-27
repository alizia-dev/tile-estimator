using TileEstimator.Contracts.Common;

namespace TileEstimator.Contracts.Dashboard;

/// <summary>SPEC 7 dashboard KPIs, pipeline and recent activity.</summary>
public sealed record DashboardResponse(
    DashboardKpis Kpis,
    IReadOnlyList<PipelineStage> QuotePipeline,
    IReadOnlyList<RecentProject> RecentProjects,
    IReadOnlyList<RecentEstimate> RecentEstimates,
    IReadOnlyList<RecentQuote> RecentQuotes);

public sealed record DashboardKpis(
    int TotalProjects,
    int ActiveProjects,
    int DraftEstimates,
    int QuotesSent,
    int QuotesAccepted,
    int QuotesRejected,
    decimal TotalQuotedValue,
    decimal AcceptedQuoteValue,
    decimal AverageEstimateValue,
    decimal WinRatePercentage,
    string Currency);

public sealed record PipelineStage(string Status, int Count, decimal Value);

public sealed record RecentProject(
    Guid Id, string ProjectNumber, string Name, string CustomerName, string Status, DateTime CreatedAt);

public sealed record RecentEstimate(
    Guid Id, string DisplayNumber, string ProjectName, string Status, decimal GrandTotal, DateTime CreatedAt);

public sealed record RecentQuote(
    Guid Id, string QuoteNumber, string CustomerName, string Status, decimal GrandTotal, DateTime QuoteDate);

// --- Audit (SPEC 18) -----------------------------------------------------------------------------

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string EntityName,
    string? EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    string? Summary,
    string? IpAddress,
    string? CorrelationId,
    DateTime CreatedAt);

public sealed record AuditLogQuery : PagedRequest
{
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public string? Action { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

// --- Organization settings (SPEC 5) ----------------------------------------------------------------

public sealed record OrganizationSettingsResponse(
    Guid OrganizationId,
    string OrganizationName,
    string? LegalName,
    string? Email,
    string? Phone,
    string? Website,
    string? LicenseNumber,
    Catalog.AddressResponse? Address,
    string Currency,
    decimal DefaultTaxRatePercentage,
    string TaxBasis,
    decimal DefaultOverheadPercentage,
    string DefaultPricingStrategy,
    decimal DefaultMarkupPercentage,
    decimal DefaultMarginPercentage,
    bool DiscountBeforeTax,
    int QuoteValidityDays,
    string CustomerNumberPrefix,
    string ProjectNumberPrefix,
    string EstimateNumberPrefix,
    string QuoteNumberPrefix,
    string? LogoPath,
    string? QuoteTermsAndConditions,
    string? QuoteFooterNote);

public sealed record UpdateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public string? LicenseNumber { get; init; }
    public Catalog.AddressRequest? Address { get; init; }
}

public sealed record UpdateOrganizationSettingsRequest
{
    public decimal DefaultTaxRatePercentage { get; init; }
    public string TaxBasis { get; init; } = "MaterialsAndLabor";
    public decimal DefaultOverheadPercentage { get; init; }
    public string DefaultPricingStrategy { get; init; } = "Markup";
    public decimal DefaultMarkupPercentage { get; init; }
    public decimal DefaultMarginPercentage { get; init; }
    public bool DiscountBeforeTax { get; init; } = true;
    public int QuoteValidityDays { get; init; } = 30;
    public string? QuoteTermsAndConditions { get; init; }
    public string? QuoteFooterNote { get; init; }
    public string CustomerNumberPrefix { get; init; } = "CUST";
    public string ProjectNumberPrefix { get; init; } = "PRJ";
    public string EstimateNumberPrefix { get; init; } = "EST";
    public string QuoteNumberPrefix { get; init; } = "QTE";
    public string ChangeOrderNumberPrefix { get; init; } = "CO";
}

// --- Reports (SPEC 21) --------------------------------------------------------------------------------

public sealed record MaterialTakeoffReportRow(
    string Category, string Description, decimal Quantity, string Unit,
    decimal UnitCost, decimal TotalCost, decimal UnitPrice, decimal TotalPrice);

public sealed record CostBreakdownReport(
    string EstimateNumber,
    string ProjectName,
    string CustomerName,
    decimal MaterialCost,
    decimal LaborCost,
    decimal OtherCost,
    decimal OverheadAmount,
    decimal TotalCost,
    decimal MarkupAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    decimal GrossProfit,
    decimal GrossMarginPercentage,
    string Currency,
    IReadOnlyList<MaterialTakeoffReportRow> Rows);

public sealed record PurchaseListRow(
    string Category, string Sku, string Description, decimal PurchaseQuantity, string Unit, decimal TotalCost);
