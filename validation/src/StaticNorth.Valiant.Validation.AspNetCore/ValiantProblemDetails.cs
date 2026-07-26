using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace StaticNorth.Valiant.Validation.AspNetCore;

/// <summary>
/// Converts Valiant validation failures to ASP.NET Core validation problem details shapes.
/// </summary>
public static class ValiantProblemDetails
{
    /// <summary>
    /// Groups validation failures by property path for ASP.NET Core validation problem responses.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <returns>A dictionary keyed by property path.</returns>
    public static IDictionary<string, string[]> ToValidationProblemDictionary(this ValidationResult result)
    {
        _ = result ?? throw new ArgumentNullException(nameof(result));
        return ToValidationProblemDictionary(result.Errors);
    }

    /// <summary>
    /// Groups validation failures by property path for ASP.NET Core validation problem responses.
    /// </summary>
    /// <param name="failures">The validation failures.</param>
    /// <returns>A dictionary keyed by property path.</returns>
    public static IDictionary<string, string[]> ToValidationProblemDictionary(IEnumerable<ValidationFailure> failures)
    {
        _ = failures ?? throw new ArgumentNullException(nameof(failures));
        return failures
            .GroupBy(static failure => failure.PropertyName)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static failure => failure.ErrorMessage).ToArray());
    }

    /// <summary>
    /// Creates HTTP validation problem details from a Valiant result.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">The problem details title.</param>
    /// <returns>The HTTP validation problem details.</returns>
    public static HttpValidationProblemDetails ToHttpValidationProblemDetails(
        this ValidationResult result,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null)
    {
        _ = result ?? throw new ArgumentNullException(nameof(result));
        return ToHttpValidationProblemDetails(result.Errors, statusCode, title);
    }

    /// <summary>
    /// Creates HTTP validation problem details from Valiant failures.
    /// </summary>
    /// <param name="failures">The validation failures.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">The problem details title.</param>
    /// <returns>The HTTP validation problem details.</returns>
    public static HttpValidationProblemDetails ToHttpValidationProblemDetails(
        IEnumerable<ValidationFailure> failures,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null)
    {
        var problemDetails = new HttpValidationProblemDetails(ToValidationProblemDictionary(failures))
        {
            Status = statusCode,
            Title = title ?? "One or more validation errors occurred."
        };
        return problemDetails;
    }

    /// <summary>
    /// Creates MVC validation problem details from a Valiant result.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">The problem details title.</param>
    /// <returns>The MVC validation problem details.</returns>
    public static ValidationProblemDetails ToMvcValidationProblemDetails(
        this ValidationResult result,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null)
    {
        _ = result ?? throw new ArgumentNullException(nameof(result));
        return ToMvcValidationProblemDetails(result.Errors, statusCode, title);
    }

    /// <summary>
    /// Creates MVC validation problem details from Valiant failures.
    /// </summary>
    /// <param name="failures">The validation failures.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">The problem details title.</param>
    /// <returns>The MVC validation problem details.</returns>
    public static ValidationProblemDetails ToMvcValidationProblemDetails(
        IEnumerable<ValidationFailure> failures,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null)
    {
        var problemDetails = new ValidationProblemDetails(ToValidationProblemDictionary(failures))
        {
            Status = statusCode,
            Title = title ?? "One or more validation errors occurred."
        };
        return problemDetails;
    }
}
