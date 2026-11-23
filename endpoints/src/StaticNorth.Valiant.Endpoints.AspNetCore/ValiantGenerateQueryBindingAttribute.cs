namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Enables source generation for Valiant query binding in the assembly that declares the endpoints.
/// </summary>
/// <remarks>
/// Enables whole-request binding, <c>AsParameters</c> query processing, and direct <c>TryParse</c> generation.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ValiantGenerateQueryBindingAttribute : Attribute;
