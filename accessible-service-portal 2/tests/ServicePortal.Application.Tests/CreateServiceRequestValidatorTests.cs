using ServicePortal.Application.ServiceRequests;
using ServicePortal.Application.ServiceRequests.Validation;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.Tests;

public sealed class CreateServiceRequestValidatorTests
{
    internal static CreateServiceRequestRequest Valid() => new(
        Subject: "Signing link has expired",
        Category: "Document signing",
        Priority: "High",
        Description: "The link in my envelope says my session expired before the page loads.",
        RequesterName: "Alex Morgan",
        RequesterEmail: "alex.morgan@example.com",
        RequesterPhone: "803 555 0142");

    [Fact]
    public void ValidRequest_ProducesParsedSubmission()
    {
        var result = CreateServiceRequestValidator.Validate(Valid());

        Assert.True(result.IsSuccess);
        Assert.Equal(Priority.High, result.Value.Priority);
        Assert.Equal("803 555 0142", result.Value.Requester.Phone);
    }

    [Fact]
    public void EmptyRequest_ReportsOneMessagePerRequiredField_InFormOrder()
    {
        var result = CreateServiceRequestValidator.Validate(new CreateServiceRequestRequest(null, null, null, null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(
            new[] { "subject", "category", "priority", "description", "requesterName", "requesterEmail" },
            result.Error!.FieldErrors.Keys.ToArray());
        Assert.All(result.Error.FieldErrors.Values, messages => Assert.Single(messages));
        Assert.Equal("Enter a subject.", result.Error.FieldErrors["subject"][0]);
    }

    [Theory]
    [InlineData("Urgent!!")]
    [InlineData("2")]
    [InlineData("99")]
    public void UnknownPriority_IsAFriendlyFieldError_NotAnException(string priority)
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { Priority = priority });

        Assert.Equal("Choose Low, Medium or High.", result.Error!.FieldErrors["priority"][0]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("name@localhost")]
    [InlineData("Alex <alex@example.com>")]
    public void InvalidEmail_ExplainsTheExpectedFormat(string email)
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { RequesterEmail = email });

        Assert.Contains("like name@example.com", result.Error!.FieldErrors["requesterEmail"][0]);
    }

    [Fact]
    public void ShortDescription_SaysWhatToInclude()
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { Description = "Broken" });

        Assert.Contains("at least 20 characters", result.Error!.FieldErrors["description"][0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPhone_IsOptional(string phone)
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { RequesterPhone = phone });

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Requester.Phone);
    }

    [Fact]
    public void MissingPhone_IsOptional()
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { RequesterPhone = null });

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Requester.Phone);
    }

    [Fact]
    public void InvalidPhone_IsRejected()
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { RequesterPhone = "call me maybe" });

        Assert.True(result.Error!.FieldErrors.ContainsKey("requesterPhone"));
    }

    [Fact]
    public void UnknownCategory_IsRejected()
    {
        var result = CreateServiceRequestValidator.Validate(Valid() with { Category = "Made up" });

        Assert.Equal("Choose a category from the list.", result.Error!.FieldErrors["category"][0]);
    }
}
