using TileEstimator.Domain.Common;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Organizations;

namespace TileEstimator.Domain.Projects;

/// <summary>SPEC 9 project: the job being estimated, for one customer, made of rooms.</summary>
public class Project : TenantEntity
{
    public Guid CustomerId { get; private set; }
    public string ProjectNumber { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ProjectType Type { get; private set; } = ProjectType.Residential;
    public ProjectStatus Status { get; private set; } = ProjectStatus.Draft;
    public DateTime? StartDate { get; private set; }
    public DateTime? EstimatedCompletionDate { get; private set; }

    public Address? SiteAddress { get; private set; }

    public Customer? Customer { get; private set; }
    public ICollection<Room> Rooms { get; private set; } = new List<Room>();
    public ICollection<ProjectNote> Notes { get; private set; } = new List<ProjectNote>();
    public ICollection<ProjectDocument> Documents { get; private set; } = new List<ProjectDocument>();

    private Project() { }

    public static Project Create(Guid organizationId, Guid customerId, string projectNumber, string name,
        ProjectType type, string? description, Address? siteAddress)
    {
        DomainException.Require(customerId != Guid.Empty, "A project must belong to a customer.");
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Project name is required.");

        return new Project
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            ProjectNumber = projectNumber,
            Name = name.Trim(),
            Type = type,
            Description = description,
            SiteAddress = siteAddress
        };
    }

    public void Update(string name, string? description, ProjectType type, Address? siteAddress,
        DateTime? startDate, DateTime? estimatedCompletionDate)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Project name is required.");
        DomainException.Require(
            startDate is null || estimatedCompletionDate is null || estimatedCompletionDate >= startDate,
            "Estimated completion cannot be before the start date.");

        Name = name.Trim();
        Description = description;
        Type = type;
        SiteAddress = siteAddress;
        StartDate = startDate;
        EstimatedCompletionDate = estimatedCompletionDate;
    }

    /// <summary>
    /// Moves the project along its lifecycle. Closed and Cancelled are terminal, so a project
    /// cannot quietly come back to life once it has been closed out.
    /// </summary>
    public void ChangeStatus(ProjectStatus status)
    {
        DomainException.Require(
            Status is not (ProjectStatus.Closed or ProjectStatus.Cancelled) || status == Status,
            "A closed or cancelled project cannot change status.");
        Status = status;
    }
}

/// <summary>SPEC 9 room or area within a project. Holds one or more surfaces.</summary>
public class Room : TenantEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public RoomType Type { get; private set; } = RoomType.Custom;
    public string? CustomTypeName { get; private set; }
    public string? Notes { get; private set; }
    public int SortOrder { get; private set; }

    public Project? Project { get; private set; }
    public ICollection<Surface> Surfaces { get; private set; } = new List<Surface>();

    private Room() { }

    public static Room Create(Guid organizationId, Guid projectId, string name, RoomType type,
        string? customTypeName, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Room name is required.");
        DomainException.Require(type != RoomType.Custom || !string.IsNullOrWhiteSpace(customTypeName),
            "A custom room needs a type name.");

        return new Room
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Name = name.Trim(),
            Type = type,
            CustomTypeName = customTypeName?.Trim(),
            SortOrder = sortOrder
        };
    }

    public void Update(string name, RoomType type, string? customTypeName, string? notes, int sortOrder)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "Room name is required.");
        DomainException.Require(type != RoomType.Custom || !string.IsNullOrWhiteSpace(customTypeName),
            "A custom room needs a type name.");
        Name = name.Trim();
        Type = type;
        CustomTypeName = customTypeName?.Trim();
        Notes = notes;
        SortOrder = sortOrder;
    }
}

/// <summary>A note recorded against a project. Visible on the project Notes tab.</summary>
public class ProjectNote : TenantEntity
{
    public Guid ProjectId { get; private set; }
    public string Body { get; private set; } = string.Empty;

    private ProjectNote() { }

    public static ProjectNote Create(Guid organizationId, Guid projectId, string body)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(body), "A note cannot be empty.");
        return new ProjectNote { OrganizationId = organizationId, ProjectId = projectId, Body = body.Trim() };
    }

    public void UpdateBody(string body)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(body), "A note cannot be empty.");
        Body = body.Trim();
    }
}

/// <summary>A file attached to a project. The bytes live behind IFileStorageProvider, not in the database.</summary>
public class ProjectDocument : TenantEntity
{
    public Guid ProjectId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private ProjectDocument() { }

    public static ProjectDocument Create(Guid organizationId, Guid projectId, string fileName,
        string contentType, long sizeBytes, string storageKey, string? description)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(fileName), "File name is required.");
        DomainException.Require(sizeBytes > 0, "An empty file cannot be attached.");

        return new ProjectDocument
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            Description = description
        };
    }
}
