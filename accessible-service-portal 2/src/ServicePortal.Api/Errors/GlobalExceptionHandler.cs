using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ServicePortal.Api.Errors;

/// <summary>
/// Last line of defence. Turns unexpected exceptions into a safe Problem Details response:
/// full detail goes to the logs, never to the client.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // The client went away; there is nobody to answer and nothing went wrong on our side.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        ProblemDetails problem;
        if (exception is BadHttpRequestException badRequest)
        {
            LogBadRequest(httpContext.Request.Method, httpContext.Request.Path, badRequest.Message);
            problem = new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "The request could not be read.",
                Detail = "Check that the body is valid JSON and every value has the expected type.",
                Extensions = { ["code"] = "request.malformed" }
            };
        }
        else
        {
            LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong on our side.",
                Detail = "Please try again. If it keeps happening, contact support and quote the trace id.",
                Extensions = { ["code"] = "server.unexpected" }
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Bad request {Method} {Path}: {Reason}")]
    private partial void LogBadRequest(string method, string path, string reason);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, string path);
}
