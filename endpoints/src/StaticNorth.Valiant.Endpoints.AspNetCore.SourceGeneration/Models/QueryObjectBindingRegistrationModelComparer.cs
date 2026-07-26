namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal sealed class QueryObjectBindingRegistrationModelComparer : IEqualityComparer<QueryObjectBindingRegistrationModel>
{
    public static readonly QueryObjectBindingRegistrationModelComparer Instance = new();

    private QueryObjectBindingRegistrationModelComparer()
    {
    }

    public bool Equals(QueryObjectBindingRegistrationModel x, QueryObjectBindingRegistrationModel y)
    {
        return x.Equals(y);
    }

    public int GetHashCode(QueryObjectBindingRegistrationModel obj)
    {
        return obj.GetHashCode();
    }
}
