using Microsoft.Extensions.Options;

namespace Valiant.Settings.AspNetCore.Samples.Validation;

internal static class ValidationEndpoints
{
    public static RouteGroupBuilder MapValidationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/validation");
        group.MapGet("/valid", (IOptions<ValidatedSettings> options) => Read(options));
        group.MapGet("/invalid", (IOptions<ValidatedSettings> options) => Read(options));
        group.MapGet("/missing-validator", (IOptions<MissingValidatorSettings> options) => Read(options));
        return group;
    }

    private static IResult Read<TSettings>(IOptions<TSettings> options)
        where TSettings : class
    {
        try
        {
            return Results.Ok(options.Value);
        }
        catch (OptionsValidationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["settings"] = exception.Failures.ToArray()
            });
        }
    }
}
