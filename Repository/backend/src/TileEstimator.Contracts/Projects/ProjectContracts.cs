using System.ComponentModel.DataAnnotations;
using TileEstimator.Contracts.Catalog;
using TileEstimator.Contracts.Common;

namespace TileEstimator.Contracts.Projects;

// --- Customers ---------------------------------------------------------------------------------

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string Type,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Notes,
    string Status,
    AddressResponse? BillingAddress,
    AddressResponse? ServiceAddress,
    int ProjectCount,
    DateTime CreatedAt);

public sealed record SaveCustomerRequest
{
    /// <summary>Residential or Commercial.</summary>
    [Required]
    public string Type { get; init; } = "Residential";

    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    [MaxLength(200)]
    public string? CompanyName { get; init; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(40)]
    public string? Phone { get; init; }

    [MaxLength(4000)]
    public string? Notes { get; init; }

    public string Status { get; init; } = "Active";

    public AddressRequest? BillingAddress { get; init; }
    public AddressRequest? ServiceAddress { get; init; }
}

public sealed record CustomerQuery : PagedRequest
{
    public string? Status { get; init; }
    public string? Type { get; init; }
}

// --- Projects ----------------------------------------------------------------------------------

public sealed record ProjectResponse(
    Guid Id,
    string ProjectNumber,
    string Name,
    string? Description,
    string Type,
    string Status,
    Guid CustomerId,
    string CustomerName,
    AddressResponse? SiteAddress,
    DateTime? StartDate,
    DateTime? EstimatedCompletionDate,
    int RoomCount,
    decimal? LatestEstimateTotal,
    string? LatestEstimateNumber,
    string? LatestQuoteStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SaveProjectRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; init; }

    public string Type { get; init; } = "Residential";

    public AddressRequest? SiteAddress { get; init; }

    public DateTime? StartDate { get; init; }
    public DateTime? EstimatedCompletionDate { get; init; }
}

public sealed record UpdateProjectStatusRequest
{
    [Required]
    public string Status { get; init; } = string.Empty;
}

public sealed record ProjectQuery : PagedRequest
{
    public string? Status { get; init; }
    public Guid? CustomerId { get; init; }
}

// --- Rooms and surfaces --------------------------------------------------------------------------

public sealed record RoomResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Type,
    string? CustomTypeName,
    string? Notes,
    int SortOrder,
    IReadOnlyList<SurfaceResponse> Surfaces);

public sealed record SaveRoomRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Type { get; init; } = "Custom";

    [MaxLength(100)]
    public string? CustomTypeName { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public int SortOrder { get; init; }
}

public sealed record SurfaceResponse(
    Guid Id,
    Guid RoomId,
    string Name,
    string Type,
    decimal LengthFeet,
    decimal? WidthFeet,
    decimal? HeightFeet,
    decimal? AreaOverrideSquareFeet,
    decimal TrimLinearFeet,
    Guid? TileId,
    string? TileName,
    Guid? PatternId,
    string? PatternName,
    Guid? AssemblyId,
    string? AssemblyName,
    decimal? WasteOverridePercentage,
    string? Notes,
    int SortOrder,
    IReadOnlyList<OpeningResponse> Openings);

public sealed record SaveSurfaceRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Floor, Wall, ShowerFloor, ShowerWall, Backsplash or Custom.</summary>
    [Required]
    public string Type { get; init; } = "Floor";

    [Range(0.01, 100000)]
    public decimal LengthFeet { get; init; }

    [Range(0.01, 100000)]
    public decimal? WidthFeet { get; init; }

    [Range(0.01, 100000)]
    public decimal? HeightFeet { get; init; }

    /// <summary>Use for irregular shapes. When set, it replaces the dimension calculation.</summary>
    [Range(0.01, 1000000)]
    public decimal? AreaOverrideSquareFeet { get; init; }

    [Range(0, 100000)]
    public decimal TrimLinearFeet { get; init; }

    public Guid? TileId { get; init; }
    public Guid? PatternId { get; init; }
    public Guid? AssemblyId { get; init; }

    /// <summary>Overrides both the pattern default and any matching waste rule.</summary>
    [Range(0, 200)]
    public decimal? WasteOverridePercentage { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public int SortOrder { get; init; }
}

public sealed record OpeningResponse(
    Guid Id,
    Guid SurfaceId,
    string Name,
    string Type,
    decimal WidthFeet,
    decimal HeightFeet,
    int Quantity,
    bool AddsArea,
    decimal TotalAreaSquareFeet);

public sealed record SaveOpeningRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Door, Window, Niche, Bench, Cabinet, Fixture or Other.</summary>
    [Required]
    public string Type { get; init; } = "Door";

    [Range(0.01, 1000)]
    public decimal WidthFeet { get; init; }

    [Range(0.01, 1000)]
    public decimal HeightFeet { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; } = 1;

    /// <summary>Niches and benches are tiled, so their area is added rather than deducted.</summary>
    public bool? AddsArea { get; init; }
}

public sealed record ProjectNoteResponse(Guid Id, string Body, DateTime CreatedAt, Guid? CreatedBy);

public sealed record SaveProjectNoteRequest
{
    [Required, MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

public sealed record ProjectDocumentResponse(
    Guid Id, string FileName, string ContentType, long SizeBytes, string? Description, DateTime CreatedAt);
