using System.Net.Mail;
using System.Text.RegularExpressions;
using ServicePortal.Application.Validation;
using ServicePortal.Domain.Common;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests.Validation;

/// <summary>Input that has passed validation and been parsed into domain types.</summary>
public sealed record ValidatedSubmission(
    string Subject,
    string Category,
    Priority Priority,
    string Description,
    ContactDetails Requester);

/// <summary>
/// Validates a new submission. Messages follow WCAG 3.3.3 (Error Suggestion): each one says what is
/// wrong and how to fix it, and the UI shows it verbatim next to the field.
/// </summary>
public static partial class CreateServiceRequestValidator
{
    public static Result<ValidatedSubmission> Validate(CreateServiceRequestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();
        var subject = request.Subject?.Trim();
        var description = request.Description?.Trim();
        var name = request.RequesterName?.Trim();
        var email = request.RequesterEmail?.Trim();
        var phone = string.IsNullOrWhiteSpace(request.RequesterPhone) ? null : request.RequesterPhone.Trim();

        // Order matches the form, so the error summary lists problems top to bottom.
        if (string.IsNullOrEmpty(subject))
        {
            errors.Add("subject", "Enter a subject.");
        }
        else if (subject.Length > ServiceRequestRules.SubjectMaxLength)
        {
            errors.Add("subject", $"Subject must be {ServiceRequestRules.SubjectMaxLength} characters or fewer.");
        }

        if (string.IsNullOrEmpty(request.Category))
        {
            errors.Add("category", "Choose a category.");
        }
        else if (!ServiceRequestRules.IsKnownCategory(request.Category))
        {
            errors.Add("category", "Choose a category from the list.");
        }

        Priority priority = default;
        if (string.IsNullOrEmpty(request.Priority))
        {
            errors.Add("priority", "Choose a priority.");
        }
        else if (!Enum.TryParse(request.Priority, ignoreCase: true, out priority) || !Enum.IsDefined(priority)
                 || int.TryParse(request.Priority, out _))
        {
            errors.Add("priority", "Choose Low, Medium or High.");
        }

        if (string.IsNullOrEmpty(description))
        {
            errors.Add("description", "Describe your request.");
        }
        else if (description.Length < ServiceRequestRules.DescriptionMinLength)
        {
            errors.Add("description",
                $"Description must be at least {ServiceRequestRules.DescriptionMinLength} characters. Include what you were trying to do and what happened.");
        }
        else if (description.Length > ServiceRequestRules.DescriptionMaxLength)
        {
            errors.Add("description", $"Description must be {ServiceRequestRules.DescriptionMaxLength} characters or fewer.");
        }

        if (string.IsNullOrEmpty(name))
        {
            errors.Add("requesterName", "Enter your full name.");
        }
        else if (name.Length > ServiceRequestRules.NameMaxLength)
        {
            errors.Add("requesterName", $"Full name must be {ServiceRequestRules.NameMaxLength} characters or fewer.");
        }

        if (string.IsNullOrEmpty(email))
        {
            errors.Add("requesterEmail", "Enter your email address.");
        }
        else if (email.Length > ServiceRequestRules.EmailMaxLength || !IsEmail(email))
        {
            errors.Add("requesterEmail", "Enter an email address in the correct format, like name@example.com.");
        }

        if (phone is not null && (phone.Length > ServiceRequestRules.PhoneMaxLength || !PhonePattern().IsMatch(phone)))
        {
            errors.Add("requesterPhone", "Enter a phone number using digits, spaces, dashes or brackets, like 803 555 0142.");
        }

        if (errors.Any)
        {
            return errors.ToError();
        }

        return new ValidatedSubmission(
            subject!,
            request.Category!,
            priority,
            description!,
            new ContactDetails(name!, email!, phone));
    }

    private static bool IsEmail(string value) =>
        MailAddress.TryCreate(value, out var address)
        && string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)
        && address.Host.Contains('.', StringComparison.Ordinal);

    [GeneratedRegex(@"^\+?[0-9()\-.\s]{7,20}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PhonePattern();
}
