namespace TileEstimator.Application.Abstractions;

// ---------------------------------------------------------------------------------------------
// SPEC 20 extension points. These exist so AI document takeoff can be added later WITHOUT
// touching the domain, the engines, the database or the public API.
//
// There is no AI, LLM or ML anywhere in this MVP, and no package that provides one. Every
// implementation shipped today is an explicit "not available" provider.
//
// The intended future pipeline, for context:
//   PDF/Image -> Document Parser -> AI Takeoff Provider -> Structured Takeoff
//   -> Validation -> Human Review -> Takeoff Engine -> Cost Engine -> Pricing Engine -> Estimate
//
// AI is only ever an INPUT provider. It proposes measurements a human reviews. It never
// produces money and never bypasses the deterministic engines, which remain the source of
// truth for every quantity and every dollar.
// ---------------------------------------------------------------------------------------------

/// <summary>Whether an optional provider is usable, and why not when it is not.</summary>
public sealed record ProviderAvailability(bool IsAvailable, string Reason);

/// <summary>A measurement a future provider proposes. Always reviewed by a person before use.</summary>
public sealed record ExtractedSurfaceCandidate(
    string SuggestedName,
    string SurfaceType,
    decimal? LengthFeet,
    decimal? WidthFeet,
    decimal? HeightFeet,
    decimal Confidence,
    string SourceReference);

public sealed record DocumentTakeoffResult(
    bool Succeeded,
    IReadOnlyList<ExtractedSurfaceCandidate> Candidates,
    string? Message);

/// <summary>
/// FUTURE PROVIDER (V4). Turns an uploaded plan into candidate surfaces for human review.
/// Not implemented in the MVP.
/// </summary>
public interface IDocumentTakeoffProvider
{
    ProviderAvailability Availability { get; }

    Task<DocumentTakeoffResult> ExtractAsync(Stream document, string contentType,
        CancellationToken cancellationToken);
}

/// <summary>
/// FUTURE PROVIDER (V4). Extracts structured takeoff data from free-form notes or photos.
/// Not implemented in the MVP.
/// </summary>
public interface ITakeoffExtractionProvider
{
    ProviderAvailability Availability { get; }

    Task<DocumentTakeoffResult> ExtractAsync(string content, CancellationToken cancellationToken);
}

/// <summary>
/// FUTURE PROVIDER (V2). Looks prices up from a supplier feed. In the MVP every price comes
/// from the organization's own catalog.
/// </summary>
public interface IExternalPricingProvider
{
    ProviderAvailability Availability { get; }

    Task<decimal?> GetPriceAsync(string sku, CancellationToken cancellationToken);
}
