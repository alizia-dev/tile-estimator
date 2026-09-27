/**
 * Typed mirrors of the DTOs in TileEstimator.Contracts.
 *
 * Money and quantities arrive as numbers that the backend has already rounded to its single
 * policy. The frontend formats them for display and never recalculates them: every figure in
 * this app comes from the server's calculation engines.
 */

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface PagedQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDescending?: boolean;
}

// --- Auth -------------------------------------------------------------------------------------

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
  companyName: string;
  acceptTerms: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
  organizationId?: string | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: CurrentUser;
}

export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  emailConfirmed: boolean;
  activeOrganizationId: string | null;
  activeOrganizationName: string | null;
  roleName: string | null;
  permissions: string[];
  organizations: OrganizationMembership[];
}

export interface OrganizationMembership {
  organizationId: string;
  organizationName: string;
  roleName: string;
}

export interface Member {
  membershipId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  isActive: boolean;
  emailConfirmed: boolean;
  joinedAt: string;
  lastLoginAt: string | null;
}

export interface Invitation {
  id: string;
  email: string;
  roleName: string;
  expiresAt: string;
  isPending: boolean;
}

// --- Shared -----------------------------------------------------------------------------------

export interface Address {
  line1: string;
  line2?: string | null;
  city: string;
  state: string;
  postalCode: string;
  country: string;
  singleLine?: string;
}

// --- Customers --------------------------------------------------------------------------------

export type CustomerType = 'Residential' | 'Commercial';
export type CustomerStatus = 'Active' | 'Inactive' | 'Prospect';

export interface Customer {
  id: string;
  customerNumber: string;
  type: CustomerType;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  notes: string | null;
  status: CustomerStatus;
  billingAddress: Address | null;
  serviceAddress: Address | null;
  projectCount: number;
  createdAt: string;
}

export interface SaveCustomerRequest {
  type: CustomerType;
  firstName?: string | null;
  lastName?: string | null;
  companyName?: string | null;
  email?: string | null;
  phone?: string | null;
  notes?: string | null;
  status: CustomerStatus;
  billingAddress?: Address | null;
  serviceAddress?: Address | null;
}

// --- Projects ---------------------------------------------------------------------------------

export type ProjectStatus =
  | 'Draft' | 'Estimating' | 'EstimateReady' | 'Quoted' | 'Accepted'
  | 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled' | 'Closed';

export interface Project {
  id: string;
  projectNumber: string;
  name: string;
  description: string | null;
  type: string;
  status: ProjectStatus;
  customerId: string;
  customerName: string;
  siteAddress: Address | null;
  startDate: string | null;
  estimatedCompletionDate: string | null;
  roomCount: number;
  latestEstimateTotal: number | null;
  latestEstimateNumber: string | null;
  latestQuoteStatus: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface SaveProjectRequest {
  customerId: string;
  name: string;
  description?: string | null;
  type: string;
  siteAddress?: Address | null;
  startDate?: string | null;
  estimatedCompletionDate?: string | null;
}

export type SurfaceType = 'Floor' | 'Wall' | 'ShowerFloor' | 'ShowerWall' | 'Backsplash' | 'Custom';

export interface Room {
  id: string;
  projectId: string;
  name: string;
  type: string;
  customTypeName: string | null;
  notes: string | null;
  sortOrder: number;
  surfaces: Surface[];
}

export interface SaveRoomRequest {
  name: string;
  type: string;
  customTypeName?: string | null;
  notes?: string | null;
  sortOrder: number;
}

export interface Surface {
  id: string;
  roomId: string;
  name: string;
  type: SurfaceType;
  lengthFeet: number;
  widthFeet: number | null;
  heightFeet: number | null;
  areaOverrideSquareFeet: number | null;
  trimLinearFeet: number;
  tileId: string | null;
  tileName: string | null;
  patternId: string | null;
  patternName: string | null;
  assemblyId: string | null;
  assemblyName: string | null;
  wasteOverridePercentage: number | null;
  notes: string | null;
  sortOrder: number;
  openings: Opening[];
}

export interface SaveSurfaceRequest {
  name: string;
  type: SurfaceType;
  lengthFeet: number;
  widthFeet?: number | null;
  heightFeet?: number | null;
  areaOverrideSquareFeet?: number | null;
  trimLinearFeet: number;
  tileId?: string | null;
  patternId?: string | null;
  assemblyId?: string | null;
  wasteOverridePercentage?: number | null;
  notes?: string | null;
  sortOrder: number;
}

export interface Opening {
  id: string;
  surfaceId: string;
  name: string;
  type: string;
  widthFeet: number;
  heightFeet: number;
  quantity: number;
  addsArea: boolean;
  totalAreaSquareFeet: number;
}

export interface SaveOpeningRequest {
  name: string;
  type: string;
  widthFeet: number;
  heightFeet: number;
  quantity: number;
  addsArea?: boolean | null;
}

export interface ProjectNote {
  id: string;
  body: string;
  createdAt: string;
  createdBy: string | null;
}

// --- Catalog ----------------------------------------------------------------------------------

export interface Tile {
  id: string;
  sku: string;
  brand: string | null;
  productName: string;
  collection: string | null;
  materialType: string;
  lengthInches: number;
  widthInches: number;
  thicknessInches: number | null;
  coverageSqFt: number;
  tilesPerBox: number;
  sqFtPerBox: number;
  costPerSqFt: number;
  sellingPricePerSqFt: number;
  finish: string | null;
  color: string | null;
  description: string | null;
  active: boolean;
}

export interface SaveTileRequest {
  sku: string;
  productName: string;
  brand?: string | null;
  collection?: string | null;
  materialType: string;
  lengthInches: number;
  widthInches: number;
  thicknessInches?: number | null;
  tilesPerBox: number;
  sqFtPerBoxOverride?: number | null;
  costPerSqFt: number;
  sellingPricePerSqFt: number;
  finish?: string | null;
  color?: string | null;
  description?: string | null;
  active: boolean;
}

export interface Material {
  id: string;
  sku: string;
  name: string;
  category: string;
  unit: string;
  coverage: number | null;
  cost: number;
  sellingPrice: number;
  supplierId: string | null;
  supplierName: string | null;
  description: string | null;
  active: boolean;
}

export interface SaveMaterialRequest {
  sku: string;
  name: string;
  category: string;
  unit: string;
  coverage?: number | null;
  cost: number;
  sellingPrice: number;
  supplierId?: string | null;
  description?: string | null;
  active: boolean;
}

export interface Pattern {
  id: string;
  name: string;
  description: string | null;
  defaultWastePercentage: number;
  active: boolean;
  sortOrder: number;
}

export interface SavePatternRequest {
  name: string;
  description?: string | null;
  defaultWastePercentage: number;
  active: boolean;
  sortOrder: number;
}

export interface WasteRule {
  id: string;
  name: string;
  patternId: string | null;
  patternName: string | null;
  surfaceType: string | null;
  tileMaterialType: string | null;
  roomType: string | null;
  wastePercentage: number;
  priority: number;
  active: boolean;
}

export interface SaveWasteRuleRequest {
  name: string;
  patternId?: string | null;
  surfaceType?: string | null;
  tileMaterialType?: string | null;
  roomType?: string | null;
  wastePercentage: number;
  priority: number;
  active: boolean;
}

export interface LaborRate {
  id: string;
  name: string;
  trade: string;
  unit: string;
  calculationMethod: 'UnitRate' | 'Productivity';
  rate: number;
  productivity: number | null;
  description: string | null;
  active: boolean;
}

export interface SaveLaborRateRequest {
  name: string;
  trade: string;
  unit: string;
  calculationMethod: 'UnitRate' | 'Productivity';
  rate: number;
  productivity?: number | null;
  description?: string | null;
  active: boolean;
}

export interface Assembly {
  id: string;
  name: string;
  description: string | null;
  appliesToSurfaceType: string | null;
  active: boolean;
  items: AssemblyItem[];
}

export interface AssemblyItem {
  id: string;
  materialId: string | null;
  materialName: string | null;
  laborRateId: string | null;
  laborRateName: string | null;
  usesSurfaceTile: boolean;
  quantityMethod: 'PerArea' | 'PerLinearFoot' | 'PerEach' | 'PerCoverage';
  unit: string | null;
  factor: number;
  fixedQuantity: number | null;
  coverageOverride: number | null;
  wasteOverridePercentage: number | null;
  description: string | null;
  sortOrder: number;
}

export interface SaveAssemblyRequest {
  name: string;
  description?: string | null;
  appliesToSurfaceType?: string | null;
  active: boolean;
  items: SaveAssemblyItemRequest[];
}

export interface SaveAssemblyItemRequest {
  materialId?: string | null;
  laborRateId?: string | null;
  usesSurfaceTile: boolean;
  quantityMethod: string;
  unit?: string | null;
  factor: number;
  fixedQuantity?: number | null;
  coverageOverride?: number | null;
  wasteOverridePercentage?: number | null;
  description?: string | null;
  sortOrder: number;
}

export interface Supplier {
  id: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  accountNumber: string | null;
  active: boolean;
}

// --- Takeoff ----------------------------------------------------------------------------------

export interface TakeoffLine {
  surfaceId: string | null;
  roomId: string | null;
  category: string;
  description: string;
  quantity: number;
  purchaseQuantity: number;
  unit: string;
  wastePercentage: number;
  unitCost: number;
  unitPrice: number;
  totalCost: number;
  totalPrice: number;
  /** The server's own explanation of how this number was reached. Displayed verbatim. */
  calculation: string;
}

export interface SurfaceArea {
  surfaceId: string | null;
  surfaceName: string;
  grossAreaSquareFeet: number;
  openingDeductionSquareFeet: number;
  openingAdditionSquareFeet: number;
  netAreaSquareFeet: number;
  wastePercentage: number;
  wasteSource: string;
  wasteExplanation: string;
  adjustedAreaSquareFeet: number;
  calculation: string;
  lines: TakeoffLine[];
}

export interface TakeoffResult {
  surfaces: SurfaceArea[];
  lines: TakeoffLine[];
  totalNetAreaSquareFeet: number;
  totalAdjustedAreaSquareFeet: number;
  totalMaterialCost: number;
  totalLaborCost: number;
  totalCost: number;
  totalPrice: number;
}

export interface QuickOpening {
  name: string;
  type: string;
  widthFeet: number;
  heightFeet: number;
  quantity: number;
  addsArea?: boolean | null;
}

export interface FloorCalculatorRequest {
  lengthFeet: number;
  widthFeet: number;
  trimLinearFeet: number;
  tileId?: string | null;
  patternId?: string | null;
  assemblyId?: string | null;
  wasteOverridePercentage?: number | null;
  openings: QuickOpening[];
}

export interface WallCalculatorRequest {
  lengthFeet: number;
  heightFeet: number;
  tileId?: string | null;
  patternId?: string | null;
  assemblyId?: string | null;
  wasteOverridePercentage?: number | null;
  openings: QuickOpening[];
}

export interface BacksplashCalculatorRequest extends WallCalculatorRequest {}

export interface ShowerCalculatorRequest {
  widthFeet: number;
  depthFeet: number;
  wallHeightFeet: number;
  floorTileId?: string | null;
  wallTileId?: string | null;
  floorAssemblyId?: string | null;
  wallAssemblyId?: string | null;
  patternId?: string | null;
  wasteOverridePercentage?: number | null;
  includeNiche: boolean;
  nicheWidthFeet: number;
  nicheHeightFeet: number;
  includeBench: boolean;
  benchWidthFeet: number;
  benchDepthFeet: number;
  doorWidthFeet: number;
  doorHeightFeet: number;
}

export interface BathroomCalculatorRequest {
  floorLengthFeet: number;
  floorWidthFeet: number;
  floorTileId?: string | null;
  floorAssemblyId?: string | null;
  wallLengthFeet: number;
  wallHeightFeet: number;
  wallTileId?: string | null;
  wallAssemblyId?: string | null;
  shower?: ShowerCalculatorRequest | null;
  backsplashLengthFeet: number;
  backsplashHeightFeet: number;
  backsplashTileId?: string | null;
  backsplashAssemblyId?: string | null;
  patternId?: string | null;
  wasteOverridePercentage?: number | null;
}

// --- Estimates --------------------------------------------------------------------------------

export type EstimateStatus = 'Draft' | 'InReview' | 'Finalized' | 'Superseded' | 'Cancelled';
export type PricingStrategy = 'Markup' | 'Margin' | 'FixedMarkup';
export type DiscountType = 'None' | 'Percentage' | 'FixedAmount';
export type TaxBasis = 'MaterialsOnly' | 'LaborOnly' | 'MaterialsAndLabor';

export interface Estimate {
  id: string;
  projectId: string;
  projectName: string;
  estimateNumber: string;
  version: number;
  displayNumber: string;
  supersedesEstimateId: string | null;
  status: EstimateStatus;
  title: string | null;
  notes: string | null;
  currency: string;
  pricingStrategy: PricingStrategy;
  markupPercentage: number;
  marginPercentage: number;
  fixedMarkupAmount: number;
  overheadPercentage: number;
  discountType: DiscountType;
  discountValue: number;
  taxRatePercentage: number;
  taxBasis: TaxBasis;
  discountBeforeTax: boolean;
  materialCost: number;
  laborCost: number;
  otherCost: number;
  overheadAmount: number;
  totalCost: number;
  markupAmount: number;
  discountAmount: number;
  taxAmount: number;
  subtotal: number;
  grandTotal: number;
  grossProfit: number;
  grossMarginPercentage: number;
  isEditable: boolean;
  finalizedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  /** Concurrency token. Echoed back on pricing updates so a stale edit is refused. */
  rowVersion: string;
  lines: EstimateLine[];
}

export interface EstimateLine {
  id: string;
  roomId: string | null;
  roomName: string | null;
  surfaceId: string | null;
  category: string;
  description: string;
  quantity: number;
  purchaseQuantity: number;
  unit: string;
  unitCost: number;
  unitPrice: number;
  wastePercentage: number;
  totalCost: number;
  totalPrice: number;
  calculation: string | null;
  isPriceOverridden: boolean;
  originalUnitPrice: number | null;
  overrideReason: string | null;
  notes: string | null;
  sortOrder: number;
}

export interface EstimateSummary {
  id: string;
  estimateNumber: string;
  version: number;
  displayNumber: string;
  status: EstimateStatus;
  projectId: string;
  projectName: string;
  customerName: string;
  grandTotal: number;
  currency: string;
  createdAt: string;
  finalizedAt: string | null;
}

export interface UpdateEstimatePricingRequest {
  pricingStrategy: PricingStrategy;
  markupPercentage: number;
  marginPercentage: number;
  fixedMarkupAmount: number;
  overheadPercentage: number;
  discountType: DiscountType;
  discountValue: number;
  taxRatePercentage: number;
  taxBasis: TaxBasis;
  discountBeforeTax: boolean;
  rowVersion?: string | null;
}

export interface SaveEstimateLineRequest {
  category: string;
  description: string;
  quantity: number;
  unit: string;
  unitCost: number;
  unitPrice: number;
  roomId?: string | null;
  notes?: string | null;
}

// --- Quotes -----------------------------------------------------------------------------------

export type QuoteStatus = 'Draft' | 'Sent' | 'Viewed' | 'Accepted' | 'Rejected' | 'Expired' | 'Cancelled';

export interface Quote {
  id: string;
  quoteNumber: string;
  version: number;
  status: QuoteStatus;
  projectId: string;
  projectName: string;
  estimateId: string;
  estimateNumber: string;
  customerId: string;
  customerDisplayName: string;
  customerEmail: string | null;
  title: string | null;
  scopeOfWork: string | null;
  termsAndConditions: string | null;
  currency: string;
  quoteDate: string;
  expiresAt: string;
  materialTotal: number;
  laborTotal: number;
  otherTotal: number;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  grandTotal: number;
  sentAt: string | null;
  firstViewedAt: string | null;
  respondedAt: string | null;
  hasPdf: boolean;
  lines: QuoteLine[];
  recipients: QuoteRecipient[];
  approvals: QuoteApproval[];
}

export interface QuoteLine {
  id: string;
  roomName: string | null;
  category: string;
  description: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  totalPrice: number;
  isVisibleToCustomer: boolean;
  sortOrder: number;
}

export interface QuoteRecipient {
  id: string;
  email: string;
  name: string | null;
  sentAt: string | null;
  deliveryError: string | null;
}

export interface QuoteApproval {
  id: string;
  decision: string;
  customerName: string;
  customerEmail: string | null;
  comments: string | null;
  decidedAt: string;
}

export interface QuoteSummary {
  id: string;
  quoteNumber: string;
  status: QuoteStatus;
  projectId: string;
  projectName: string;
  customerDisplayName: string;
  grandTotal: number;
  currency: string;
  quoteDate: string;
  expiresAt: string;
  sentAt: string | null;
  respondedAt: string | null;
}

export interface PublicQuote {
  quoteNumber: string;
  status: QuoteStatus;
  title: string | null;
  scopeOfWork: string | null;
  termsAndConditions: string | null;
  footerNote: string | null;
  companyName: string;
  companyEmail: string | null;
  companyPhone: string | null;
  companyAddressLine: string | null;
  companyLicenseNumber: string | null;
  customerName: string;
  projectName: string | null;
  currency: string;
  quoteDate: string;
  expiresAt: string;
  isExpired: boolean;
  canRespond: boolean;
  materialTotal: number;
  laborTotal: number;
  otherTotal: number;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  grandTotal: number;
  lines: PublicQuoteLine[];
  existingDecision: PublicQuoteDecision | null;
}

export interface PublicQuoteLine {
  roomName: string | null;
  description: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  totalPrice: number;
}

export interface PublicQuoteDecision {
  decision: string;
  customerName: string;
  decidedAt: string;
}

export interface PublicQuoteDecisionRequest {
  decision: 'Accepted' | 'Rejected' | 'ChangesRequested';
  customerName: string;
  customerEmail?: string | null;
  comments?: string | null;
}

// --- Dashboard, settings and audit --------------------------------------------------------------

export interface Dashboard {
  kpis: DashboardKpis;
  quotePipeline: PipelineStage[];
  recentProjects: RecentProject[];
  recentEstimates: RecentEstimate[];
  recentQuotes: RecentQuote[];
}

export interface DashboardKpis {
  totalProjects: number;
  activeProjects: number;
  draftEstimates: number;
  quotesSent: number;
  quotesAccepted: number;
  quotesRejected: number;
  totalQuotedValue: number;
  acceptedQuoteValue: number;
  averageEstimateValue: number;
  winRatePercentage: number;
  currency: string;
}

export interface PipelineStage {
  status: string;
  count: number;
  value: number;
}

export interface RecentProject {
  id: string;
  projectNumber: string;
  name: string;
  customerName: string;
  status: string;
  createdAt: string;
}

export interface RecentEstimate {
  id: string;
  displayNumber: string;
  projectName: string;
  status: string;
  grandTotal: number;
  createdAt: string;
}

export interface RecentQuote {
  id: string;
  quoteNumber: string;
  customerName: string;
  status: string;
  grandTotal: number;
  quoteDate: string;
}

export interface OrganizationSettings {
  organizationId: string;
  organizationName: string;
  legalName: string | null;
  email: string | null;
  phone: string | null;
  website: string | null;
  licenseNumber: string | null;
  address: Address | null;
  currency: string;
  defaultTaxRatePercentage: number;
  taxBasis: TaxBasis;
  defaultOverheadPercentage: number;
  defaultPricingStrategy: PricingStrategy;
  defaultMarkupPercentage: number;
  defaultMarginPercentage: number;
  discountBeforeTax: boolean;
  quoteValidityDays: number;
  customerNumberPrefix: string;
  projectNumberPrefix: string;
  estimateNumberPrefix: string;
  quoteNumberPrefix: string;
  logoPath: string | null;
  quoteTermsAndConditions: string | null;
  quoteFooterNote: string | null;
}

export interface AuditLogEntry {
  id: string;
  userId: string | null;
  userEmail: string | null;
  entityName: string;
  entityId: string | null;
  action: string;
  oldValues: string | null;
  newValues: string | null;
  summary: string | null;
  ipAddress: string | null;
  correlationId: string | null;
  createdAt: string;
}

/** RFC 7807 problem response. Every API error arrives in this shape. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
