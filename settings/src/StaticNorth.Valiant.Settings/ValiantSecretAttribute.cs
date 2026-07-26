namespace StaticNorth.Valiant.Settings;

/// <summary>
/// Marks a settings property as sensitive so schema consumers can hide its runtime value.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ValiantSecretAttribute : Attribute;
