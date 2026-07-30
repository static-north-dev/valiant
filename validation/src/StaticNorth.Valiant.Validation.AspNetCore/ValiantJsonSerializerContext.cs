using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace StaticNorth.Valiant.Validation.AspNetCore;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class ValiantJsonSerializerContext : JsonSerializerContext;
