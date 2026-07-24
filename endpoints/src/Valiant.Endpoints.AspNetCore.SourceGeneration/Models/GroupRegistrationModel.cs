namespace Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct GroupRegistrationModel(string type, int order, int index)
{
    public string Type { get; } = type;

    public int Order { get; } = order;

    public int Index { get; } = index;
}
