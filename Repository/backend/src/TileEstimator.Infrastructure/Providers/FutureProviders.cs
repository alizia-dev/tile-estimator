using TileEstimator.Application.Abstractions;

namespace TileEstimator.Infrastructure.Providers;

// ---------------------------------------------------------------------------------------------
// SPEC 20 future providers. These are explicit "not available" implementations. They exist so
// the composition root, the DI graph and the call sites are already in place when AI document
// takeoff is built in V4. There is no AI package referenced anywhere in this solution.
// ---------------------------------------------------------------------------------------------

/// <summary>FUTURE PROVIDER. Reports that document takeoff is not part of the MVP.</summary>
public sealed class NotAvailableDocumentTakeoffProvider : IDocumentTakeoffProvider
{
    private const string Reason =
        "Document takeoff is not available. Enter measurements manually, or add a provider in V4.";

    public ProviderAvailability Availability { get; } = new(false, Reason);

    public Task<DocumentTakeoffResult> ExtractAsync(Stream document, string contentType,
        CancellationToken cancellationToken) =>
        Task.FromResult(new DocumentTakeoffResult(false, [], Reason));
}

/// <summary>FUTURE PROVIDER. Reports that automated takeoff extraction is not part of the MVP.</summary>
public sealed class NotAvailableTakeoffExtractionProvider : ITakeoffExtractionProvider
{
    private const string Reason =
        "Automated takeoff extraction is not available in this version.";

    public ProviderAvailability Availability { get; } = new(false, Reason);

    public Task<DocumentTakeoffResult> ExtractAsync(string content, CancellationToken cancellationToken) =>
        Task.FromResult(new DocumentTakeoffResult(false, [], Reason));
}

/// <summary>
/// FUTURE PROVIDER. Every price in the MVP comes from the organization's own catalog, so this
/// returns null rather than inventing a number.
/// </summary>
public sealed class NotAvailableExternalPricingProvider : IExternalPricingProvider
{
    public ProviderAvailability Availability { get; } =
        new(false, "External supplier pricing is not available. Prices come from your catalog.");

    public Task<decimal?> GetPriceAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult<decimal?>(null);
}
