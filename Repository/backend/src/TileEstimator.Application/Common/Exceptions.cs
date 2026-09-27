namespace TileEstimator.Application.Common;

/// <summary>
/// The requested row does not exist, or belongs to another tenant. Deliberately the same
/// exception in both cases: telling a caller "this exists but is not yours" leaks data.
/// </summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}

/// <summary>The caller is authenticated but lacks the permission this action needs.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>The request conflicts with the current state, e.g. a duplicate SKU.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>
/// A cross-tenant access attempt. Thrown by the SaveChanges interceptor and by lookups that
/// resolve a parent row, and always logged.
/// </summary>
public sealed class TenantViolationException(string message) : Exception(message);

/// <summary>Input failed validation. Carries the per-field errors for the ProblemDetails response.</summary>
public sealed class ValidationFailedException : Exception
{
    public ValidationFailedException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") => Errors = errors;

    public ValidationFailedException(string field, string error)
        : base("One or more validation errors occurred.") =>
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [error] };

    public IDictionary<string, string[]> Errors { get; }
}
