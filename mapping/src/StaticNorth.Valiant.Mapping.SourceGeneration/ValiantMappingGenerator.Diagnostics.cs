using System.Collections.Immutable;
using System.Text;
using static StaticNorth.Valiant.SourceGeneration.ValiantFingerprint;

namespace StaticNorth.Valiant.Mapping.SourceGeneration;

/// <summary>
/// Generates Valiant mapper implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantMappingGenerator
{
    private static string CreateMapperFingerprint(
        MapperDeclarationModel declaration,
        MappingModel? mapping,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        var builder = new StringBuilder();
        AppendFingerprintValue(builder, declaration.HintName);
        AppendFingerprintValue(builder, declaration.Name);
        AppendFingerprintValue(builder, declaration.NamespaceName);
        AppendFingerprintValue(builder, declaration.Modifier);
        AppendFingerprintValue(builder, declaration.Keyword);
        AppendFingerprintValue(builder, declaration.Location.ToString());
        AppendFingerprintValue(builder, declaration.HasInstanceConstructor ? "constructors" : "no-constructors");

        foreach (var constructor in declaration.UserConstructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                AppendFingerprintValue(builder, parameter.TypeName);
                AppendFingerprintValue(builder, parameter.Name);
            }
        }

        foreach (var signature in declaration.ConstructorSignatures)
        {
            AppendFingerprintValue(builder, signature);
        }

        foreach (var containingType in declaration.ContainingTypes)
        {
            AppendFingerprintValue(builder, containingType.Modifier);
            AppendFingerprintValue(builder, containingType.Keyword);
            AppendFingerprintValue(builder, containingType.Name);
        }

        if (mapping is not null)
        {
            AppendFingerprintValue(builder, mapping.SourceType);
            AppendFingerprintValue(builder, mapping.TargetType);
            AppendFingerprintValue(builder, mapping.IsSourceValueType ? "source-value" : "source-reference");
            AppendFingerprintValue(builder, mapping.InheritsMapperBase ? "base" : "no-base");
            AppendFingerprintValue(builder, mapping.HasConfigureMethod ? "configure" : "no-configure");
            AppendFingerprintValue(builder, mapping.UsesServiceProviderMapper ? "service-provider-mappers" : "no-service-provider-mappers");

            foreach (var local in mapping.ConfigureLocals)
            {
                AppendFingerprintValue(builder, local);
            }

            foreach (var argument in mapping.Constructor.Arguments)
            {
                AppendFingerprintValue(builder, argument.ParameterName);
                AppendFingerprintValue(builder, argument.ParameterType);
                AppendFingerprintValue(builder, argument.RuntimeParameterType);
                AppendFingerprintValue(builder, argument.SourceExpression);
            }

            foreach (var assignment in mapping.Assignments)
            {
                AppendFingerprintValue(builder, assignment.TargetMemberName);
                AppendFingerprintValue(builder, assignment.SourceExpression);
                AppendFingerprintValue(builder, assignment.ConditionExpression);
                AppendFingerprintValue(builder, assignment.UseInitializer ? "initializer" : "assignment");
                AppendFingerprintValue(builder, assignment.InitPropertyType);
            }

            foreach (var collectionMapping in mapping.CollectionMappings)
            {
                AppendFingerprintValue(builder, collectionMapping.MethodName);
                AppendFingerprintValue(builder, collectionMapping.Kind.ToString());
                AppendFingerprintValue(builder, collectionMapping.SourceKind.ToString());
                AppendFingerprintValue(builder, collectionMapping.SourceType);
                AppendFingerprintValue(builder, collectionMapping.SourceElementType);
                AppendFingerprintValue(builder, collectionMapping.TargetElementType);
                AppendFingerprintValue(builder, collectionMapping.ArrayAllocationFormat);
                AppendFingerprintValue(builder, collectionMapping.MappedElementExpression);
                AppendFingerprintValue(builder, collectionMapping.RequiresElementMapper ? "captured" : "inline");
                AppendFingerprintValue(builder, collectionMapping.CapacityExpression);
            }
        }

        foreach (var diagnostic in diagnostics)
        {
            AppendFingerprintValue(builder, diagnostic.Descriptor.Id);
            AppendFingerprintValue(builder, diagnostic.Location.ToString());
            foreach (var messageArg in diagnostic.MessageArgs)
            {
                AppendFingerprintValue(builder, messageArg?.ToString());
            }
        }

        return builder.ToString();
    }

}
