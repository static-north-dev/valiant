namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal sealed class EndpointRegistrationModelComparer : IEqualityComparer<EndpointRegistrationModel>
{
    public static readonly EndpointRegistrationModelComparer Instance = new();

    private EndpointRegistrationModelComparer()
    {
    }

    public bool Equals(EndpointRegistrationModel x, EndpointRegistrationModel y)
    {
        return x.Equals(y);
    }

    public int GetHashCode(EndpointRegistrationModel obj)
    {
        return obj.GetHashCode();
    }
}
