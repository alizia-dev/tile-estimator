namespace TileEstimator.Application.Authorization;

/// <summary>
/// SPEC 6 permission codes. These strings are the contract between the policy layer, the seed
/// data and the Angular permission directive, so they are declared once here.
/// </summary>
public static class Permissions
{
    public const string ProjectRead = "project.read";
    public const string ProjectCreate = "project.create";
    public const string ProjectUpdate = "project.update";
    public const string ProjectDelete = "project.delete";

    public const string EstimateRead = "estimate.read";
    public const string EstimateCreate = "estimate.create";
    public const string EstimateUpdate = "estimate.update";
    public const string EstimateDelete = "estimate.delete";
    public const string EstimateApprove = "estimate.approve";

    public const string QuoteRead = "quote.read";
    public const string QuoteCreate = "quote.create";
    public const string QuoteSend = "quote.send";
    public const string QuoteApprove = "quote.approve";

    public const string CustomerRead = "customer.read";
    public const string CustomerManage = "customer.manage";

    public const string CatalogRead = "catalog.read";
    public const string CatalogManage = "catalog.manage";

    public const string UsersRead = "users.read";
    public const string UsersInvite = "users.invite";
    public const string UsersManage = "users.manage";

    public const string SettingsRead = "settings.read";
    public const string SettingsManage = "settings.manage";

    public const string AuditRead = "audit.read";
    public const string ReportsRead = "reports.read";

    /// <summary>Every permission, with the group it belongs to and what it allows.</summary>
    public static readonly IReadOnlyList<(string Code, string Group, string Description)> All =
    [
        (ProjectRead, "Projects", "View projects, rooms and surfaces"),
        (ProjectCreate, "Projects", "Create projects"),
        (ProjectUpdate, "Projects", "Edit projects, rooms and surfaces"),
        (ProjectDelete, "Projects", "Delete projects"),

        (EstimateRead, "Estimates", "View estimates"),
        (EstimateCreate, "Estimates", "Create estimates"),
        (EstimateUpdate, "Estimates", "Edit estimate lines and pricing"),
        (EstimateDelete, "Estimates", "Delete draft estimates"),
        (EstimateApprove, "Estimates", "Finalize an estimate"),

        (QuoteRead, "Quotes", "View quotes"),
        (QuoteCreate, "Quotes", "Create quotes from finalized estimates"),
        (QuoteSend, "Quotes", "Send quotes to customers"),
        (QuoteApprove, "Quotes", "Record a quote decision on the customer's behalf"),

        (CustomerRead, "Customers", "View customers"),
        (CustomerManage, "Customers", "Create and edit customers"),

        (CatalogRead, "Catalog", "View tiles, materials, labor rates and assemblies"),
        (CatalogManage, "Catalog", "Edit the catalog, pricing and assemblies"),

        (UsersRead, "Users", "View organization members"),
        (UsersInvite, "Users", "Invite new members"),
        (UsersManage, "Users", "Change roles and deactivate members"),

        (SettingsRead, "Settings", "View organization settings"),
        (SettingsManage, "Settings", "Change organization settings"),

        (AuditRead, "System", "View the audit log"),
        (ReportsRead, "Reports", "View and export reports")
    ];
}

/// <summary>SPEC 6 roles and the matrix documented in docs/authorization.md.</summary>
public static class Roles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Estimator = "Estimator";
    public const string Sales = "Sales";
    public const string Installer = "Installer";
    public const string Viewer = "Viewer";

    /// <summary>
    /// The role to permission matrix. Owner holds everything; each other role holds the
    /// narrowest set that still lets the person do their job.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Matrix =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Owner] = Permissions.All.Select(p => p.Code).ToList(),

            [Admin] =
            [
                Permissions.ProjectRead, Permissions.ProjectCreate, Permissions.ProjectUpdate, Permissions.ProjectDelete,
                Permissions.EstimateRead, Permissions.EstimateCreate, Permissions.EstimateUpdate,
                Permissions.EstimateDelete, Permissions.EstimateApprove,
                Permissions.QuoteRead, Permissions.QuoteCreate, Permissions.QuoteSend, Permissions.QuoteApprove,
                Permissions.CustomerRead, Permissions.CustomerManage,
                Permissions.CatalogRead, Permissions.CatalogManage,
                Permissions.UsersRead, Permissions.UsersInvite, Permissions.UsersManage,
                Permissions.SettingsRead, Permissions.SettingsManage,
                Permissions.AuditRead, Permissions.ReportsRead
            ],

            // Estimators build the numbers but do not manage people or organization settings.
            [Estimator] =
            [
                Permissions.ProjectRead, Permissions.ProjectCreate, Permissions.ProjectUpdate,
                Permissions.EstimateRead, Permissions.EstimateCreate, Permissions.EstimateUpdate,
                Permissions.EstimateDelete, Permissions.EstimateApprove,
                Permissions.QuoteRead, Permissions.QuoteCreate,
                Permissions.CustomerRead, Permissions.CustomerManage,
                Permissions.CatalogRead, Permissions.CatalogManage,
                Permissions.SettingsRead, Permissions.ReportsRead
            ],

            // Sales owns the customer relationship and the quote, but does not re-price the job.
            [Sales] =
            [
                Permissions.ProjectRead, Permissions.ProjectCreate, Permissions.ProjectUpdate,
                Permissions.EstimateRead,
                Permissions.QuoteRead, Permissions.QuoteCreate, Permissions.QuoteSend, Permissions.QuoteApprove,
                Permissions.CustomerRead, Permissions.CustomerManage,
                Permissions.CatalogRead,
                Permissions.ReportsRead
            ],

            // Installers need the scope and the material list, never the pricing controls.
            [Installer] =
            [
                Permissions.ProjectRead,
                Permissions.EstimateRead,
                Permissions.CustomerRead,
                Permissions.CatalogRead
            ],

            [Viewer] =
            [
                Permissions.ProjectRead,
                Permissions.EstimateRead,
                Permissions.QuoteRead,
                Permissions.CustomerRead,
                Permissions.CatalogRead,
                Permissions.ReportsRead
            ]
        };

    public static readonly IReadOnlyList<(string Name, string Description, int SortOrder)> All =
    [
        (Owner, "Full control, including billing and organization ownership", 1),
        (Admin, "Manages people, catalog and settings", 2),
        (Estimator, "Builds takeoffs, estimates and pricing", 3),
        (Sales, "Manages customers and sends quotes", 4),
        (Installer, "Reads project scope and material lists", 5),
        (Viewer, "Read-only access", 6)
    ];
}
