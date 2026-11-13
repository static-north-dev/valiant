namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

public sealed class EdgeMultiMapper : IMapper<EdgeInner, EdgeInnerDto>, IMapper<EdgeInner, string>
{
    EdgeInnerDto IMapper<EdgeInner, EdgeInnerDto>.Map(EdgeInner value) => new(value.Name);
    string IMapper<EdgeInner, string>.Map(EdgeInner value) => value.Name;
}
