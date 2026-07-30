using Microsoft.AspNetCore.Http;

namespace StaticNorth.Valiant.Validation.AspNetCore;

internal sealed class ValiantValidationProblemResult(
    IEnumerable<ValidationFailure> failures) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        _ = httpContext ?? throw new ArgumentNullException(nameof(httpContext));

        var problemDetails = ValiantProblemDetails.ToHttpValidationProblemDetails(failures);
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            ValiantJsonSerializerContext.Default.HttpValidationProblemDetails,
            contentType: "application/problem+json",
            cancellationToken: httpContext.RequestAborted);
    }
}
