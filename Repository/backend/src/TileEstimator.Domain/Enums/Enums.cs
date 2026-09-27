namespace TileEstimator.Domain.Enums;

public enum CustomerType { Residential = 1, Commercial = 2 }

public enum CustomerStatus { Active = 1, Inactive = 2, Prospect = 3 }

/// <summary>§9 project lifecycle.</summary>
public enum ProjectStatus
{
    Draft = 1, Estimating = 2, EstimateReady = 3, Quoted = 4, Accepted = 5,
    Scheduled = 6, InProgress = 7, Completed = 8, Cancelled = 9, Closed = 10
}

public enum ProjectType { Residential = 1, Commercial = 2, Remodel = 3, NewConstruction = 4, Repair = 5, Other = 99 }

/// <summary>§9 room types. Custom lets an organization name its own.</summary>
public enum RoomType
{
    Bathroom = 1, MasterBathroom = 2, Kitchen = 3, LivingRoom = 4, Bedroom = 5,
    Shower = 6, Backsplash = 7, Entry = 8, Laundry = 9, Custom = 99
}

/// <summary>§9 surface (zone) types. A room has many surfaces.</summary>
public enum SurfaceType
{
    Floor = 1, Wall = 2, ShowerFloor = 3, ShowerWall = 4, Backsplash = 5, Custom = 99
}

public enum OpeningType { Door = 1, Window = 2, Niche = 3, Bench = 4, Cabinet = 5, Fixture = 6, Other = 99 }

/// <summary>§10 tile material types.</summary>
public enum TileMaterialType
{
    Ceramic = 1, Porcelain = 2, NaturalStone = 3, Glass = 4, Mosaic = 5,
    Marble = 6, Granite = 7, Travertine = 8, Slate = 9, Other = 99
}

/// <summary>§10 material categories. Seeded, organization-editable via MaterialCategory rows.</summary>
public enum MaterialCategory
{
    Thinset = 1, Grout = 2, BackerBoard = 3, WaterproofingMembrane = 4, Sealer = 5,
    Caulk = 6, Primer = 7, Adhesive = 8, Trim = 9, Bullnose = 10, Spacers = 11, Other = 99
}

/// <summary>§12 how an assembly item's quantity is derived from the takeoff.</summary>
public enum QuantityMethod
{
    /// <summary>Quantity = area (SF).</summary>
    PerArea = 1,
    /// <summary>Quantity = linear feet.</summary>
    PerLinearFoot = 2,
    /// <summary>Quantity = a fixed count.</summary>
    PerEach = 3,
    /// <summary>Quantity = area / coverage per unit (thinset bags, membrane rolls...).</summary>
    PerCoverage = 4
}

/// <summary>§13 how a labor line is priced.</summary>
public enum LaborCalculationMethod
{
    /// <summary>Quantity x UnitRate.</summary>
    UnitRate = 1,
    /// <summary>Quantity / Productivity x HourlyRate.</summary>
    Productivity = 2
}

/// <summary>§14 pricing strategy. Markup and margin are different calculations.</summary>
public enum PricingStrategy
{
    /// <summary>Price = Cost x (1 + Markup).</summary>
    Markup = 1,
    /// <summary>Price = Cost / (1 - Margin).</summary>
    Margin = 2,
    /// <summary>Price = Cost + a fixed amount.</summary>
    FixedMarkup = 3
}

public enum DiscountType { None = 0, Percentage = 1, FixedAmount = 2 }

/// <summary>§15 estimate lifecycle. Finalized estimates are immutable; edits create a new version.</summary>
public enum EstimateStatus { Draft = 1, InReview = 2, Finalized = 3, Superseded = 4, Cancelled = 5 }

public enum EstimateLineCategory { Tile = 1, Material = 2, Labor = 3, Equipment = 4, Delivery = 5, Other = 99 }

/// <summary>§17 quote lifecycle.</summary>
public enum QuoteStatus { Draft = 1, Sent = 2, Viewed = 3, Accepted = 4, Rejected = 5, Expired = 6, Cancelled = 7 }

public enum ChangeOrderStatus { Draft = 1, Sent = 2, Approved = 3, Rejected = 4, Cancelled = 5 }

/// <summary>§18 audit actions.</summary>
public enum AuditAction
{
    Create = 1, Update = 2, Delete = 3, Login = 4, Logout = 5, LoginFailed = 6,
    Finalize = 7, Send = 8, Approve = 9, Reject = 10, PermissionChange = 11, PriceChange = 12
}

/// <summary>§17 what a customer did on the public quote page.</summary>
public enum QuoteApprovalDecision { Accepted = 1, Rejected = 2, ChangesRequested = 3 }

public enum TaxBasis { MaterialsOnly = 1, LaborOnly = 2, MaterialsAndLabor = 3 }
