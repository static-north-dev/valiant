using Microsoft.AspNetCore.Http;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure;

internal sealed class ValiantQueryBindingException(IReadOnlyList<QueryError> errors) :
    BadHttpRequestException(
        errors.Count > 0 ? errors[0].Message : "The query string could not be bound to this request.",
        StatusCodes.Status400BadRequest)
{
    public IReadOnlyList<QueryError> Errors { get; } = errors;
}
