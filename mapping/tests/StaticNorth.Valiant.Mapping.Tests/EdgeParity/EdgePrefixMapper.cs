namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

public readonly struct EdgePrefixMapper(string prefix) : IMapper<EdgeInner, EdgeInnerDto>
{
    public EdgeInnerDto Map(EdgeInner value) => new(prefix + value.Name);
}
