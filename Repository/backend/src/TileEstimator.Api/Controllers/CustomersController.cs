using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TileEstimator.Api.Authorization;
using TileEstimator.Api.Mapping;
using TileEstimator.Application.Abstractions;
using TileEstimator.Application.Authorization;
using TileEstimator.Application.Common;
using TileEstimator.Application.Services;
using TileEstimator.Contracts.Common;
using TileEstimator.Contracts.Projects;
using TileEstimator.Domain.Customers;
using TileEstimator.Domain.Enums;

namespace TileEstimator.Api.Controllers;

/// <summary>
/// SPEC 8 customers. Every query runs through the tenant-filtered context, so no handler here
/// needs its own organization check.
/// </summary>
[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController(
    IApplicationDbContext db,
    INumberSequenceService numbers,
    ICurrentOrganizationService currentOrganization,
    IAuditService audit)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CustomerRead)]
    [ProducesResponseType(typeof(PagedResult<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> List(
        [FromQuery] CustomerQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var customers = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            customers = customers.Where(c =>
                c.CustomerNumber.Contains(term) ||
                (c.FirstName != null && c.FirstName.Contains(term)) ||
                (c.LastName != null && c.LastName.Contains(term)) ||
                (c.CompanyName != null && c.CompanyName.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)));
        }

        if (Enum.TryParse<CustomerStatus>(query.Status, true, out var status))
        {
            customers = customers.Where(c => c.Status == status);
        }

        if (Enum.TryParse<CustomerType>(query.Type, true, out var type))
        {
            customers = customers.Where(c => c.Type == type);
        }

        var total = await customers.CountAsync(ct);

        var page = await customers
            .OrderByDescending(c => c.CreatedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        // One grouped query for the counts rather than one per customer.
        var ids = page.Select(c => c.Id).ToList();
        var projectCounts = await db.Projects
            .Where(p => ids.Contains(p.CustomerId))
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);

        var items = page
            .Select(c => c.ToResponse(projectCounts.GetValueOrDefault(c.Id)))
            .ToList();

        return Ok(new PagedResult<CustomerResponse>(items, query.Page, query.PageSize, total));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.CustomerRead)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
                           .FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Customer), id);

        var projectCount = await db.Projects.CountAsync(p => p.CustomerId == id, ct);
        return Ok(customer.ToResponse(projectCount));
    }

    [HttpPost]
    [HasPermission(Permissions.CustomerManage)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> Create(SaveCustomerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = currentOrganization.RequireOrganizationId();
        var type = ParseEnum<CustomerType>(request.Type, nameof(request.Type));

        var settings = await db.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

        var number = await numbers.NextAsync(organizationId, "Customer",
            settings?.CustomerNumberPrefix ?? "CUST", ct);

        var customer = Customer.Create(organizationId, number, type, request.FirstName,
            request.LastName, request.CompanyName, request.Email, request.Phone);

        customer.Update(type, request.FirstName, request.LastName, request.CompanyName,
            request.Email, request.Phone, request.Notes,
            ParseEnum<CustomerStatus>(request.Status, nameof(request.Status)),
            request.BillingAddress.ToDomain(), request.ServiceAddress.ToDomain());

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(organizationId, AuditAction.Create, nameof(Customer),
            customer.Id.ToString(), $"Customer {customer.CustomerNumber} created.", ct);

        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer.ToResponse());
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CustomerManage)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, SaveCustomerRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Customer), id);

        customer.Update(
            ParseEnum<CustomerType>(request.Type, nameof(request.Type)),
            request.FirstName, request.LastName, request.CompanyName, request.Email, request.Phone,
            request.Notes, ParseEnum<CustomerStatus>(request.Status, nameof(request.Status)),
            request.BillingAddress.ToDomain(), request.ServiceAddress.ToDomain());

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(customer.OrganizationId, AuditAction.Update, nameof(Customer),
            customer.Id.ToString(), $"Customer {customer.CustomerNumber} updated.", ct);

        return Ok(customer.ToResponse());
    }

    /// <summary>
    /// Soft-deletes a customer. Refused while projects still reference them, so a delete can
    /// never orphan a project or its estimates.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.CustomerManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Customer), id);

        if (await db.Projects.AnyAsync(p => p.CustomerId == id, ct))
        {
            throw new ConflictException(
                "This customer still has projects. Remove or reassign them before deleting the customer.");
        }

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(customer.OrganizationId, AuditAction.Delete, nameof(Customer),
            id.ToString(), $"Customer {customer.CustomerNumber} deleted.", ct);

        return NoContent();
    }

    internal static TEnum ParseEnum<TEnum>(string? value, string field) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed)
            ? parsed
            : throw new ValidationFailedException(field,
                $"'{value}' is not valid. Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
}
