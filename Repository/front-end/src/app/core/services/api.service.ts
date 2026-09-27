import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import type * as Api from '../models/api.models';

/** Builds query parameters, dropping anything null, undefined or blank. */
function toParams(query?: object): HttpParams {
  let params = new HttpParams();
  if (!query) return params;

  for (const [key, value] of Object.entries(query as Record<string, unknown>)) {
    if (value === null || value === undefined || value === '') continue;
    params = params.set(key, String(value));
  }
  return params;
}

@Injectable({ providedIn: 'root' })
export class CustomerApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/customers`;

  list(query?: Api.PagedQuery & { status?: string; type?: string }): Observable<Api.PagedResult<Api.Customer>> {
    return this.http.get<Api.PagedResult<Api.Customer>>(this.url, { params: toParams(query) });
  }

  get(id: string): Observable<Api.Customer> {
    return this.http.get<Api.Customer>(`${this.url}/${id}`);
  }

  create(request: Api.SaveCustomerRequest): Observable<Api.Customer> {
    return this.http.post<Api.Customer>(this.url, request);
  }

  update(id: string, request: Api.SaveCustomerRequest): Observable<Api.Customer> {
    return this.http.put<Api.Customer>(`${this.url}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class ProjectApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/projects`;

  list(query?: Api.PagedQuery & { status?: string; customerId?: string }): Observable<Api.PagedResult<Api.Project>> {
    return this.http.get<Api.PagedResult<Api.Project>>(this.url, { params: toParams(query) });
  }

  get(id: string): Observable<Api.Project> {
    return this.http.get<Api.Project>(`${this.url}/${id}`);
  }

  create(request: Api.SaveProjectRequest): Observable<Api.Project> {
    return this.http.post<Api.Project>(this.url, request);
  }

  update(id: string, request: Api.SaveProjectRequest): Observable<Api.Project> {
    return this.http.put<Api.Project>(`${this.url}/${id}`, request);
  }

  updateStatus(id: string, status: string): Observable<Api.Project> {
    return this.http.put<Api.Project>(`${this.url}/${id}/status`, { status });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  // --- Rooms, surfaces and openings ---

  listRooms(projectId: string): Observable<Api.Room[]> {
    return this.http.get<Api.Room[]>(`${this.url}/${projectId}/rooms`);
  }

  createRoom(projectId: string, request: Api.SaveRoomRequest): Observable<Api.Room> {
    return this.http.post<Api.Room>(`${this.url}/${projectId}/rooms`, request);
  }

  updateRoom(projectId: string, roomId: string, request: Api.SaveRoomRequest): Observable<Api.Room> {
    return this.http.put<Api.Room>(`${this.url}/${projectId}/rooms/${roomId}`, request);
  }

  deleteRoom(projectId: string, roomId: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${projectId}/rooms/${roomId}`);
  }

  createSurface(projectId: string, roomId: string, request: Api.SaveSurfaceRequest): Observable<Api.Surface> {
    return this.http.post<Api.Surface>(`${this.url}/${projectId}/rooms/${roomId}/surfaces`, request);
  }

  updateSurface(
    projectId: string,
    roomId: string,
    surfaceId: string,
    request: Api.SaveSurfaceRequest,
  ): Observable<Api.Surface> {
    return this.http.put<Api.Surface>(
      `${this.url}/${projectId}/rooms/${roomId}/surfaces/${surfaceId}`,
      request,
    );
  }

  deleteSurface(projectId: string, roomId: string, surfaceId: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${projectId}/rooms/${roomId}/surfaces/${surfaceId}`);
  }

  createOpening(projectId: string, surfaceId: string, request: Api.SaveOpeningRequest): Observable<Api.Opening> {
    return this.http.post<Api.Opening>(`${this.url}/${projectId}/surfaces/${surfaceId}/openings`, request);
  }

  updateOpening(
    projectId: string,
    surfaceId: string,
    openingId: string,
    request: Api.SaveOpeningRequest,
  ): Observable<Api.Opening> {
    return this.http.put<Api.Opening>(
      `${this.url}/${projectId}/surfaces/${surfaceId}/openings/${openingId}`,
      request,
    );
  }

  deleteOpening(projectId: string, surfaceId: string, openingId: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${projectId}/surfaces/${surfaceId}/openings/${openingId}`);
  }

  listNotes(projectId: string): Observable<Api.ProjectNote[]> {
    return this.http.get<Api.ProjectNote[]>(`${this.url}/${projectId}/notes`);
  }

  addNote(projectId: string, body: string): Observable<Api.ProjectNote> {
    return this.http.post<Api.ProjectNote>(`${this.url}/${projectId}/notes`, { body });
  }
}

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/catalog`;

  listTiles(query?: Api.PagedQuery & { active?: boolean; materialType?: string }): Observable<Api.PagedResult<Api.Tile>> {
    return this.http.get<Api.PagedResult<Api.Tile>>(`${this.url}/tiles`, { params: toParams(query) });
  }

  getTile(id: string): Observable<Api.Tile> {
    return this.http.get<Api.Tile>(`${this.url}/tiles/${id}`);
  }

  createTile(request: Api.SaveTileRequest): Observable<Api.Tile> {
    return this.http.post<Api.Tile>(`${this.url}/tiles`, request);
  }

  updateTile(id: string, request: Api.SaveTileRequest): Observable<Api.Tile> {
    return this.http.put<Api.Tile>(`${this.url}/tiles/${id}`, request);
  }

  deleteTile(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/tiles/${id}`);
  }

  listMaterials(
    query?: Api.PagedQuery & { active?: boolean; category?: string },
  ): Observable<Api.PagedResult<Api.Material>> {
    return this.http.get<Api.PagedResult<Api.Material>>(`${this.url}/materials`, { params: toParams(query) });
  }

  createMaterial(request: Api.SaveMaterialRequest): Observable<Api.Material> {
    return this.http.post<Api.Material>(`${this.url}/materials`, request);
  }

  updateMaterial(id: string, request: Api.SaveMaterialRequest): Observable<Api.Material> {
    return this.http.put<Api.Material>(`${this.url}/materials/${id}`, request);
  }

  deleteMaterial(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/materials/${id}`);
  }

  listPatterns(): Observable<Api.Pattern[]> {
    return this.http.get<Api.Pattern[]>(`${this.url}/patterns`);
  }

  createPattern(request: Api.SavePatternRequest): Observable<Api.Pattern> {
    return this.http.post<Api.Pattern>(`${this.url}/patterns`, request);
  }

  updatePattern(id: string, request: Api.SavePatternRequest): Observable<Api.Pattern> {
    return this.http.put<Api.Pattern>(`${this.url}/patterns/${id}`, request);
  }

  deletePattern(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/patterns/${id}`);
  }

  listWasteRules(): Observable<Api.WasteRule[]> {
    return this.http.get<Api.WasteRule[]>(`${this.url}/waste-rules`);
  }

  createWasteRule(request: Api.SaveWasteRuleRequest): Observable<Api.WasteRule> {
    return this.http.post<Api.WasteRule>(`${this.url}/waste-rules`, request);
  }

  updateWasteRule(id: string, request: Api.SaveWasteRuleRequest): Observable<Api.WasteRule> {
    return this.http.put<Api.WasteRule>(`${this.url}/waste-rules/${id}`, request);
  }

  deleteWasteRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/waste-rules/${id}`);
  }

  listLaborRates(): Observable<Api.LaborRate[]> {
    return this.http.get<Api.LaborRate[]>(`${this.url}/labor-rates`);
  }

  createLaborRate(request: Api.SaveLaborRateRequest): Observable<Api.LaborRate> {
    return this.http.post<Api.LaborRate>(`${this.url}/labor-rates`, request);
  }

  updateLaborRate(id: string, request: Api.SaveLaborRateRequest): Observable<Api.LaborRate> {
    return this.http.put<Api.LaborRate>(`${this.url}/labor-rates/${id}`, request);
  }

  deleteLaborRate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/labor-rates/${id}`);
  }

  listAssemblies(): Observable<Api.Assembly[]> {
    return this.http.get<Api.Assembly[]>(`${this.url}/assemblies`);
  }

  getAssembly(id: string): Observable<Api.Assembly> {
    return this.http.get<Api.Assembly>(`${this.url}/assemblies/${id}`);
  }

  createAssembly(request: Api.SaveAssemblyRequest): Observable<Api.Assembly> {
    return this.http.post<Api.Assembly>(`${this.url}/assemblies`, request);
  }

  updateAssembly(id: string, request: Api.SaveAssemblyRequest): Observable<Api.Assembly> {
    return this.http.put<Api.Assembly>(`${this.url}/assemblies/${id}`, request);
  }

  deleteAssembly(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/assemblies/${id}`);
  }

  listSuppliers(): Observable<Api.Supplier[]> {
    return this.http.get<Api.Supplier[]>(`${this.url}/suppliers`);
  }
}

@Injectable({ providedIn: 'root' })
export class TakeoffApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/takeoffs`;

  calculateProject(projectId: string): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/calculate`, null, {
      params: toParams({ projectId }),
    });
  }

  calculateSurface(surfaceId: string): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/calculate-surface`, null, {
      params: toParams({ surfaceId }),
    });
  }

  floor(request: Api.FloorCalculatorRequest): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/quick/floor`, request);
  }

  wall(request: Api.WallCalculatorRequest): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/quick/wall`, request);
  }

  backsplash(request: Api.BacksplashCalculatorRequest): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/quick/backsplash`, request);
  }

  shower(request: Api.ShowerCalculatorRequest): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/quick/shower`, request);
  }

  bathroom(request: Api.BathroomCalculatorRequest): Observable<Api.TakeoffResult> {
    return this.http.post<Api.TakeoffResult>(`${this.url}/quick/bathroom`, request);
  }
}

@Injectable({ providedIn: 'root' })
export class EstimateApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/estimates`;

  list(query?: Api.PagedQuery & { projectId?: string; status?: string }): Observable<Api.PagedResult<Api.EstimateSummary>> {
    return this.http.get<Api.PagedResult<Api.EstimateSummary>>(this.url, { params: toParams(query) });
  }

  get(id: string): Observable<Api.Estimate> {
    return this.http.get<Api.Estimate>(`${this.url}/${id}`);
  }

  versions(id: string): Observable<Api.EstimateSummary[]> {
    return this.http.get<Api.EstimateSummary[]>(`${this.url}/${id}/versions`);
  }

  create(projectId: string, title?: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(this.url, { projectId, title });
  }

  calculate(id: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/calculate`, null);
  }

  rebuild(id: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/rebuild`, null);
  }

  updatePricing(id: string, request: Api.UpdateEstimatePricingRequest): Observable<Api.Estimate> {
    return this.http.put<Api.Estimate>(`${this.url}/${id}/pricing`, request);
  }

  updateDetails(id: string, title: string | null, notes: string | null): Observable<Api.Estimate> {
    return this.http.put<Api.Estimate>(`${this.url}/${id}/details`, { title, notes });
  }

  addLine(id: string, request: Api.SaveEstimateLineRequest): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/lines`, request);
  }

  updateLine(id: string, lineId: string, request: Api.SaveEstimateLineRequest): Observable<Api.Estimate> {
    return this.http.put<Api.Estimate>(`${this.url}/${id}/lines/${lineId}`, request);
  }

  duplicateLine(id: string, lineId: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/lines/${lineId}/duplicate`, null);
  }

  overridePrice(id: string, lineId: string, unitPrice: number, reason?: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/lines/${lineId}/override-price`, {
      unitPrice,
      reason,
    });
  }

  clearOverride(id: string, lineId: string): Observable<Api.Estimate> {
    return this.http.delete<Api.Estimate>(`${this.url}/${id}/lines/${lineId}/override-price`);
  }

  deleteLine(id: string, lineId: string): Observable<Api.Estimate> {
    return this.http.delete<Api.Estimate>(`${this.url}/${id}/lines/${lineId}`);
  }

  finalize(id: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/finalize`, null);
  }

  newVersion(id: string): Observable<Api.Estimate> {
    return this.http.post<Api.Estimate>(`${this.url}/${id}/new-version`, null);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class QuoteApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/quotes`;

  list(query?: Api.PagedQuery & { status?: string; projectId?: string }): Observable<Api.PagedResult<Api.QuoteSummary>> {
    return this.http.get<Api.PagedResult<Api.QuoteSummary>>(this.url, { params: toParams(query) });
  }

  get(id: string): Observable<Api.Quote> {
    return this.http.get<Api.Quote>(`${this.url}/${id}`);
  }

  create(estimateId: string, title?: string, scopeOfWork?: string): Observable<Api.Quote> {
    return this.http.post<Api.Quote>(this.url, { estimateId, title, scopeOfWork });
  }

  updateContent(
    id: string,
    content: { title?: string | null; scopeOfWork?: string | null; termsAndConditions?: string | null; footerNote?: string | null },
  ): Observable<Api.Quote> {
    return this.http.put<Api.Quote>(`${this.url}/${id}/content`, content);
  }

  /** Downloads the proposal PDF as a blob for preview or save. */
  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  send(id: string, recipientEmails: string[], message?: string): Observable<Api.Quote> {
    return this.http.post<Api.Quote>(`${this.url}/${id}/send`, { recipientEmails, message });
  }

  recordDecision(id: string, request: Api.PublicQuoteDecisionRequest): Observable<Api.Quote> {
    return this.http.post<Api.Quote>(`${this.url}/${id}/record-decision`, request);
  }

  cancel(id: string): Observable<Api.Quote> {
    return this.http.post<Api.Quote>(`${this.url}/${id}/cancel`, null);
  }
}

/** The unauthenticated customer-facing quote endpoints. No token is ever attached to these. */
@Injectable({ providedIn: 'root' })
export class PublicQuoteApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/public/quotes`;

  get(token: string): Observable<Api.PublicQuote> {
    return this.http.get<Api.PublicQuote>(`${this.url}/${encodeURIComponent(token)}`);
  }

  downloadPdf(token: string): Observable<Blob> {
    return this.http.get(`${this.url}/${encodeURIComponent(token)}/pdf`, { responseType: 'blob' });
  }

  respond(token: string, request: Api.PublicQuoteDecisionRequest): Observable<Api.PublicQuote> {
    return this.http.post<Api.PublicQuote>(`${this.url}/${encodeURIComponent(token)}/respond`, request);
  }
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get(): Observable<Api.Dashboard> {
    return this.http.get<Api.Dashboard>(`${environment.apiBaseUrl}/dashboard`);
  }
}

@Injectable({ providedIn: 'root' })
export class OrganizationApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/organization`;

  getSettings(): Observable<Api.OrganizationSettings> {
    return this.http.get<Api.OrganizationSettings>(`${this.url}/settings`);
  }

  updateProfile(request: Record<string, unknown>): Observable<Api.OrganizationSettings> {
    return this.http.put<Api.OrganizationSettings>(`${this.url}/profile`, request);
  }

  updateSettings(request: Record<string, unknown>): Observable<Api.OrganizationSettings> {
    return this.http.put<Api.OrganizationSettings>(`${this.url}/settings`, request);
  }

  listMembers(): Observable<Api.Member[]> {
    return this.http.get<Api.Member[]>(`${this.url}/members`);
  }

  updateMember(membershipId: string, roleName: string, isActive: boolean): Observable<Api.Member> {
    return this.http.put<Api.Member>(`${this.url}/members/${membershipId}`, { roleName, isActive });
  }

  listInvitations(): Observable<Api.Invitation[]> {
    return this.http.get<Api.Invitation[]>(`${this.url}/invitations`);
  }

  invite(email: string, roleName: string): Observable<Api.Invitation> {
    return this.http.post<Api.Invitation>(`${this.url}/invitations`, { email, roleName });
  }

  revokeInvitation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/invitations/${id}`);
  }

  auditLogs(query?: Api.PagedQuery & { entityName?: string; action?: string }): Observable<Api.PagedResult<Api.AuditLogEntry>> {
    return this.http.get<Api.PagedResult<Api.AuditLogEntry>>(`${environment.apiBaseUrl}/audit-logs`, {
      params: toParams(query),
    });
  }
}
