using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.Organizations;

/// <summary>A US postal address. Owned value object, stored inline on its parent row.</summary>
public class Address
{
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = "US";

    private Address() { }

    public static Address Create(string line1, string? line2, string city, string state, string postalCode, string country = "US")
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(line1), "Address line 1 is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(city), "City is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(state), "State is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(postalCode), "Postal code is required.");

        return new Address
        {
            Line1 = line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            City = city.Trim(),
            State = state.Trim().ToUpperInvariant(),
            PostalCode = postalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(country) ? "US" : country.Trim().ToUpperInvariant()
        };
    }

    public string SingleLine
    {
        get
        {
            var parts = new List<string> { Line1 };
            if (!string.IsNullOrWhiteSpace(Line2)) parts.Add(Line2);
            parts.Add(City + ", " + State + " " + PostalCode);
            return string.Join(", ", parts);
        }
    }
}
