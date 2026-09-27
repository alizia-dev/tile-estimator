using TileEstimator.Domain.Common;
using TileEstimator.Domain.Enums;
using TileEstimator.Domain.Organizations;

namespace TileEstimator.Domain.Customers;

/// <summary>SPEC 8 customer. Supports residential (person) and commercial (company) customers.</summary>
public class Customer : TenantEntity
{
    public string CustomerNumber { get; private set; } = string.Empty;
    public CustomerType Type { get; private set; } = CustomerType.Residential;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? CompanyName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;

    public Address? BillingAddress { get; private set; }
    public Address? ServiceAddress { get; private set; }

    public ICollection<CustomerContact> Contacts { get; private set; } = new List<CustomerContact>();

    private Customer() { }

    public static Customer Create(
        Guid organizationId,
        string customerNumber,
        CustomerType type,
        string? firstName,
        string? lastName,
        string? companyName,
        string? email,
        string? phone)
    {
        if (type == CustomerType.Commercial)
        {
            DomainException.Require(!string.IsNullOrWhiteSpace(companyName),
                "A commercial customer needs a company name.");
        }
        else
        {
            DomainException.Require(!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName),
                "A residential customer needs a first or last name.");
        }

        return new Customer
        {
            OrganizationId = organizationId,
            CustomerNumber = customerNumber,
            Type = type,
            FirstName = firstName?.Trim(),
            LastName = lastName?.Trim(),
            CompanyName = companyName?.Trim(),
            Email = email?.Trim(),
            Phone = phone?.Trim()
        };
    }

    /// <summary>What to print on a quote: the company for commercial, the person otherwise.</summary>
    public string DisplayName
    {
        get
        {
            if (Type == CustomerType.Commercial && !string.IsNullOrWhiteSpace(CompanyName))
            {
                return CompanyName;
            }
            var name = string.Join(" ", new[] { FirstName, LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(name) ? CompanyName ?? CustomerNumber : name;
        }
    }

    public void Update(
        CustomerType type,
        string? firstName,
        string? lastName,
        string? companyName,
        string? email,
        string? phone,
        string? notes,
        CustomerStatus status,
        Address? billingAddress,
        Address? serviceAddress)
    {
        Type = type;
        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
        CompanyName = companyName?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        Notes = notes;
        Status = status;
        BillingAddress = billingAddress;
        ServiceAddress = serviceAddress;
    }
}

/// <summary>An extra person to contact at a customer, typically for commercial accounts.</summary>
public class CustomerContact : TenantEntity
{
    public Guid CustomerId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Title { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsPrimary { get; private set; }

    public Customer? Customer { get; private set; }

    private CustomerContact() { }

    public static CustomerContact Create(Guid organizationId, Guid customerId, string firstName, string lastName,
        string? title, string? email, string? phone, bool isPrimary)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(firstName), "Contact first name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(lastName), "Contact last name is required.");

        return new CustomerContact
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Title = title?.Trim(),
            Email = email?.Trim(),
            Phone = phone?.Trim(),
            IsPrimary = isPrimary
        };
    }

    public void Update(string firstName, string lastName, string? title, string? email, string? phone, bool isPrimary)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(firstName), "Contact first name is required.");
        DomainException.Require(!string.IsNullOrWhiteSpace(lastName), "Contact last name is required.");
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Title = title?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        IsPrimary = isPrimary;
    }
}
