using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Application.Validation;
using ServicePortal.Domain.Common;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests.Validation;

/// <summary>
/// Turns raw query-string values into a <see cref="ServiceRequestQuery"/>.
/// Sort uses the common "field" / "-field" convention: <c>?sort=-createdAt</c> is newest first.
/// </summary>
public static class QueryParser
{
    public const int SearchMaxLength = 100;

    private static readonly Dictionary<string, SortField> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdAt"] = SortField.CreatedAt,
        ["reference"] = SortField.Reference,
        ["priority"] = SortField.Priority,
        ["status"] = SortField.Status
    };

    public static Result<ServiceRequestQuery> Parse(ListServiceRequestsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();
        var defaults = ServiceRequestQuery.Default;

        RequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<RequestStatus>(request.Status, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed) && !int.TryParse(request.Status, out _))
            {
                status = parsed;
            }
            else
            {
                errors.Add("status", "Status must be Open, InProgress, Resolved or Cancelled.");
            }
        }

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        if (search is { Length: > SearchMaxLength })
        {
            errors.Add("search", $"Search must be {SearchMaxLength} characters or fewer.");
        }

        var sort = defaults.Sort;
        var direction = defaults.Direction;
        if (!string.IsNullOrWhiteSpace(request.Sort))
        {
            var descending = request.Sort.StartsWith('-');
            var key = request.Sort.TrimStart('-', '+');
            if (SortFields.TryGetValue(key, out var field))
            {
                sort = field;
                direction = descending ? SortDirection.Descending : SortDirection.Ascending;
            }
            else
            {
                errors.Add("sort", "Sort must be one of createdAt, reference, priority or status, optionally prefixed with '-'.");
            }
        }

        var page = request.Page ?? 1;
        if (page < 1)
        {
            errors.Add("page", "Page must be 1 or greater.");
        }

        var pageSize = request.PageSize ?? defaults.PageSize;
        if (pageSize is < 1 or > ServiceRequestQuery.MaxPageSize)
        {
            errors.Add("pageSize", $"Page size must be between 1 and {ServiceRequestQuery.MaxPageSize}.");
        }

        return errors.Any
            ? errors.ToError()
            : new ServiceRequestQuery(status, search, sort, direction, page, pageSize);
    }

    public static Result<RequestStatus> ParseStatus(ChangeStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new FieldErrors();
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors.Add("status", "Choose a status.");
        }
        else if (!Enum.TryParse<RequestStatus>(request.Status, ignoreCase: true, out var status)
                 || !Enum.IsDefined(status) || int.TryParse(request.Status, out _))
        {
            errors.Add("status", "Choose a status from the list.");
        }
        else
        {
            return status;
        }

        return errors.ToError();
    }
}
