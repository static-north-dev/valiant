namespace Valiant.Endpoints.AspNetCore.SourceGeneration;

internal sealed class EndpointModelComparer : IEqualityComparer<EndpointModel>
{
    public static readonly EndpointModelComparer Instance = new();

    private EndpointModelComparer()
    {
    }

    public bool Equals(EndpointModel x, EndpointModel y)
    {
        return x.Equals(y);
    }

    public int GetHashCode(EndpointModel obj)
    {
        return obj.GetHashCode();
    }
}
