using System.IO.Compression;
using System.Xml.Linq;

if (args.Length != 2)
{
    throw new ArgumentException("Usage: Valiant.PackageVerifier <package-directory> <package-version>");
}

var packageDirectory = Path.GetFullPath(args[0]);
var version = args[1];
var packages = new Dictionary<string, PackageExpectation>(StringComparer.Ordinal)
{
    ["Valiant.Endpoints.AspNetCore"] = new(
        ["lib/net8.0/Valiant.Endpoints.AspNetCore.dll", "lib/net8.0/Valiant.Endpoints.dll", "analyzers/dotnet/cs/Valiant.Endpoints.AspNetCore.SourceGeneration.dll"],
        []),
    ["Valiant.Mapping"] = new(
        ["lib/net8.0/Valiant.Mapping.dll", "lib/netstandard2.0/Valiant.Mapping.dll", "analyzers/dotnet/cs/Valiant.Mapping.SourceGeneration.dll", "buildTransitive/Valiant.Mapping.props"],
        []),
    ["Valiant.Settings"] = new(
        ["lib/net8.0/Valiant.Settings.dll", "lib/netstandard2.0/Valiant.Settings.dll", "analyzers/dotnet/cs/Valiant.Settings.SourceGeneration.dll", "buildTransitive/Valiant.Settings.props"],
        []),
    ["Valiant.Settings.AspNetCore"] = new(
        ["lib/net8.0/Valiant.Settings.AspNetCore.dll"],
        ["Valiant.Settings"]),
    ["Valiant.Settings.Validation"] = new(
        ["lib/net8.0/Valiant.Settings.Validation.dll"],
        ["Valiant.Settings", "Valiant.Validation"]),
    ["Valiant.Validation"] = new(
        ["lib/net8.0/Valiant.Validation.dll", "lib/netstandard2.0/Valiant.Validation.dll", "analyzers/dotnet/cs/Valiant.Validation.SourceGeneration.dll", "buildTransitive/Valiant.Validation.props"],
        []),
    ["Valiant.Validation.AspNetCore"] = new(
        ["lib/net8.0/Valiant.Validation.AspNetCore.dll"],
        ["Valiant.Validation"]),
    ["Valiant.Validation.AspNetCore.OpenApi"] = new(
        ["lib/net10.0/Valiant.Validation.AspNetCore.OpenApi.dll"],
        ["Valiant.Validation"]),
    ["Valiant.Validation.MediatR"] = new(
        ["lib/net8.0/Valiant.Validation.MediatR.dll", "buildTransitive/Valiant.Validation.MediatR.props", "contentFiles/cs/any/ValiantMediatRExtensions.cs"],
        ["MediatR", "Valiant.Validation"]),
    ["Valiant.Validation.Mediator"] = new(
        ["lib/net8.0/Valiant.Validation.Mediator.dll", "buildTransitive/Valiant.Validation.Mediator.props", "contentFiles/cs/any/ValiantMediatorServiceCollectionExtensions.cs"],
        ["Mediator.Abstractions", "Valiant.Validation"]),
};

var actualPackages = Directory
    .EnumerateFiles(packageDirectory, $"Valiant.*.{version}.nupkg", SearchOption.TopDirectoryOnly)
    .Where(static path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var actualSymbols = Directory
    .EnumerateFiles(packageDirectory, $"Valiant.*.{version}.snupkg", SearchOption.TopDirectoryOnly)
    .ToArray();

if (actualPackages.Length != packages.Count || actualSymbols.Length != packages.Count)
{
    throw new InvalidOperationException(
        $"Expected {packages.Count} nupkg and snupkg files for {version}, found {actualPackages.Length} and {actualSymbols.Length}.");
}

foreach (var (packageId, expectation) in packages)
{
    var packagePath = Path.Combine(packageDirectory, $"{packageId}.{version}.nupkg");
    var symbolPath = Path.Combine(packageDirectory, $"{packageId}.{version}.snupkg");
    VerifyPackage(packagePath, packageId, version, expectation);
    VerifySymbols(symbolPath, packageId);
}

if (File.Exists(Path.Combine(packageDirectory, $"Valiant.Endpoints.{version}.nupkg")))
{
    throw new InvalidOperationException("Valiant.Endpoints must remain bundled, not published as a standalone package.");
}

Console.WriteLine($"Verified {packages.Count} Valiant packages and symbol packages for {version}.");

static void VerifyPackage(string path, string packageId, string version, PackageExpectation expectation)
{
    using var archive = ZipFile.OpenRead(path);
    var entries = archive.Entries
        .Select(static entry => entry.FullName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var requiredEntry in expectation.RequiredEntries.Concat(["README.md", "logo.png"]))
    {
        if (!entries.Contains(requiredEntry))
        {
            throw new InvalidOperationException($"{packageId} is missing '{requiredEntry}'.");
        }
    }

    var nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
    using var nuspecStream = nuspecEntry.Open();
    var document = XDocument.Load(nuspecStream);
    var metadata = document.Root?.Elements().Single(static element => element.Name.LocalName == "metadata")
        ?? throw new InvalidOperationException($"{packageId} has no nuspec metadata.");

    RequireMetadata(metadata, "id", packageId);
    RequireMetadata(metadata, "version", version);
    RequireMetadata(metadata, "readme", "README.md");
    RequireMetadata(metadata, "icon", "logo.png");

    var license = metadata.Elements().SingleOrDefault(static element => element.Name.LocalName == "license");
    if (license?.Attribute("type")?.Value != "expression" || license.Value != "Apache-2.0")
    {
        throw new InvalidOperationException($"{packageId} does not carry the Apache-2.0 license expression.");
    }

    var repository = metadata.Elements().SingleOrDefault(static element => element.Name.LocalName == "repository");
    if (repository?.Attribute("type")?.Value != "git" ||
        repository.Attribute("url")?.Value != "https://github.com/static-north-dev/valiant.git")
    {
        throw new InvalidOperationException($"{packageId} has incorrect repository metadata.");
    }

    var dependencies = metadata
        .Descendants()
        .Where(static element => element.Name.LocalName == "dependency")
        .Select(static element => new
        {
            Id = element.Attribute("id")?.Value,
            Version = element.Attribute("version")?.Value,
        })
        .ToArray();

    foreach (var dependencyId in expectation.RequiredDependencies)
    {
        if (dependencies.All(dependency => dependency.Id != dependencyId))
        {
            throw new InvalidOperationException($"{packageId} is missing dependency '{dependencyId}'.");
        }
    }

    if (dependencies.Any(static dependency =>
            dependency.Id is "Microsoft.Net.Compilers.Toolset" ||
            dependency.Id?.Contains("SourceGeneration", StringComparison.Ordinal) == true))
    {
        throw new InvalidOperationException($"{packageId} leaks a compiler or source-generator implementation dependency.");
    }

    if (packageId == "Valiant.Validation.MediatR")
    {
        var mediatR = dependencies.Single(static dependency => dependency.Id == "MediatR");
        if (mediatR.Version != "[12.5.0]")
        {
            throw new InvalidOperationException($"MediatR must remain exactly pinned to [12.5.0], found '{mediatR.Version}'.");
        }
    }
}

static void VerifySymbols(string path, string packageId)
{
    using var archive = ZipFile.OpenRead(path);
    if (!archive.Entries.Any(static entry => entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException($"{packageId} symbol package contains no portable PDB.");
    }
}

static void RequireMetadata(XElement metadata, string name, string expected)
{
    var actual = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == name)?.Value;
    if (actual != expected)
    {
        throw new InvalidOperationException($"Expected nuspec {name} '{expected}', found '{actual}'.");
    }
}

internal sealed record PackageExpectation(
    IReadOnlyList<string> RequiredEntries,
    IReadOnlyList<string> RequiredDependencies);
