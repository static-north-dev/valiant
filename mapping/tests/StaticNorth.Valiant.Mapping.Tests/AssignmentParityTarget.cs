namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class AssignmentParityTarget
{
    private string? _initial = "original";

    public string? First { get; set; }

    public string? Second { get; set; }

    public string? Initial
    {
        get => _initial;
        init
        {
            InitWrites++;
            _initial = value;
        }
    }

    public int InitWrites { get; private set; }
}
