using Microsoft.CodeAnalysis;

namespace StaticNorth.Valiant.SourceGeneration;

internal static class ValiantDependencyInjectionSymbols
{
    private const string ServiceCollectionMetadataName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    private const string ServiceDescriptorMetadataName = "Microsoft.Extensions.DependencyInjection.ServiceDescriptor";
    private const string ServiceCollectionDescriptorExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions";

    public static bool HasDependencyInjection(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName(ServiceCollectionMetadataName) is not null &&
            compilation.GetTypeByMetadataName(ServiceDescriptorMetadataName) is not null &&
            compilation.GetTypeByMetadataName(ServiceCollectionDescriptorExtensionsMetadataName) is not null;
    }
}
