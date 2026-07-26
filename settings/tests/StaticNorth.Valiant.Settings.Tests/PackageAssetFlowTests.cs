namespace StaticNorth.Valiant.Settings.Tests;

public sealed class PackageAssetFlowTests
{
    [Test]
    public async Task Settings_Integration_Packages_Forward_Parent_Build_And_Analyzer_Assets()
    {
        var aspNetCore = ReadRepositoryFile("settings/src/StaticNorth.Valiant.Settings.AspNetCore/StaticNorth.Valiant.Settings.AspNetCore.csproj");
        var validation = ReadRepositoryFile("settings/src/StaticNorth.Valiant.Settings.Validation/StaticNorth.Valiant.Settings.Validation.csproj");

        await Assert.That(aspNetCore).Contains("StaticNorth.Valiant.Settings.csproj\" PrivateAssets=\"contentfiles\"");
        await Assert.That(validation).Contains("StaticNorth.Valiant.Settings.csproj\" PrivateAssets=\"contentfiles\"");
        await Assert.That(validation).Contains("StaticNorth.Valiant.Validation.csproj\" PrivateAssets=\"contentfiles\"");
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StaticNorth.Valiant.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate repository root.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
