namespace TileEstimator.Domain.Common;

/// <summary>Thrown when a domain invariant or business rule is violated.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new DomainException(message);
    }
}
