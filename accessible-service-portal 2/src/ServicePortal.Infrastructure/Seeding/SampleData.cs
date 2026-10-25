using System.Linq;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Infrastructure.Seeding;

/// <summary>Realistic sample data: enough rows to exercise search, sorting and pagination on first run.</summary>
internal static class SampleData
{
    private sealed record Seed(
        string Subject, string Category, Priority Priority, string Name, string Email,
        double HoursAgo, RequestStatus[] Workflow, string Description);

    private static readonly Seed[] Seeds =
    [
        new("Signing link shows an expired-session message", "Document signing", Priority.High, "Jordan Lee",
            "jordan.lee@example.com", 2, [],
            "I opened the envelope from my advisor and the signing link says my session expired before the page loads."),
        new("Screen reader skips the signature field", "Accessibility barrier", Priority.High, "Priya Shah",
            "priya.shah@example.com", 5, [RequestStatus.InProgress],
            "Using NVDA with Chrome, the signature field is not announced and I cannot reach it with the Tab key."),
        new("Reset my multi-factor authentication device", "Account access", Priority.Medium, "Sam Carter",
            "sam.carter@example.com", 9, [RequestStatus.InProgress, RequestStatus.Resolved],
            "I replaced my phone and can no longer receive the verification code. Please reset my registered device."),
        new("Duplicate charge on September statement", "Billing", Priority.Medium, "Maria Gonzalez",
            "maria.gonzalez@example.com", 20, [],
            "My September statement shows the same advisory fee twice on the 14th. I'd like one of them refunded."),
        new("Focus is lost after closing the upload dialog", "Accessibility barrier", Priority.Medium, "Daniel Kim",
            "daniel.kim@example.com", 27, [RequestStatus.InProgress],
            "After I close the document upload dialog with Escape, keyboard focus jumps to the top of the page."),
        new("Cannot download completed envelope as PDF", "Document signing", Priority.Low, "Aisha Patel",
            "aisha.patel@example.com", 34, [RequestStatus.InProgress, RequestStatus.Resolved],
            "The Download PDF button for my completed envelope does nothing in Safari 17 on macOS."),
        new("Update the email address on my account", "Account access", Priority.Low, "Chris Morgan",
            "chris.morgan@example.com", 50, [RequestStatus.Cancelled],
            "Please change my contact email; I no longer have access to the old mailbox."),
        new("Low contrast on the status badges", "Accessibility barrier", Priority.Low, "Elena Rossi",
            "elena.rossi@example.com", 61, [],
            "The light grey Pending badge is very hard to read for me. Could the contrast be increased?"),
        new("Envelope stuck in Sent after all parties signed", "Document signing", Priority.High, "Marcus Brown",
            "marcus.brown@example.com", 73, [RequestStatus.InProgress],
            "All three signers completed the envelope yesterday, but its status still shows Sent instead of Completed."),
        new("Question about paperless statement preferences", "Other", Priority.Low, "Grace Liu",
            "grace.liu@example.com", 96, [RequestStatus.InProgress, RequestStatus.Resolved],
            "Where can I switch quarterly statements to paperless delivery for my joint account?"),
        new("Locked out after password reset", "Account access", Priority.High, "Omar Haddad",
            "omar.haddad@example.com", 120, [RequestStatus.InProgress, RequestStatus.Resolved],
            "I reset my password and the account was locked after the first sign-in attempt with the new one."),
        new("Timeout warning gives too little time", "Accessibility barrier", Priority.Medium, "Hannah Fischer",
            "hannah.fischer@example.com", 150, [],
            "The session timeout warning appears with only 30 seconds left, which is not enough time with my switch device.")
    ];

    public static IEnumerable<ServiceRequest> Create(DateTimeOffset now)
    {
        var id = 1000;
        for (var i = Seeds.Length - 1; i >= 0; i--)
        {
            var seed = Seeds[i];
            var submitted = now.AddHours(-seed.HoursAgo);
            var request = ServiceRequest.Submit(
                ++id,
                seed.Subject,
                seed.Description,
                new ContactDetails(seed.Name, seed.Email, null),
                seed.Category,
                seed.Priority,
                submitted);

            var changedAt = submitted;
            foreach (var status in seed.Workflow)
            {
                changedAt = changedAt.AddMinutes(45);
                request.ChangeStatus(status, changedAt);
            }

            yield return request;
        }
    }
}
