namespace ServicePortal.Domain.ServiceRequests;

/// <summary>Value object: who raised the request and how to reach them.</summary>
public sealed record ContactDetails
{
    public ContactDetails(string name, string email, string? phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Name = name.Trim();
        Email = email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    public string Name { get; }

    public string Email { get; }

    public string? Phone { get; }
}
