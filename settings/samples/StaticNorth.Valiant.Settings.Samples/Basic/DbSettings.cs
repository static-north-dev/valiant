namespace StaticNorth.Valiant.Settings.Samples.Basic;

[ValiantSettings("Samples:Db", configure: true)]
public class DbSettings
{
    [ValiantSecret]
    public required string ConnectionString { get; init; }
}
