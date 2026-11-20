using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using StaticNorth.Valiant.SourceGeneration;
using static StaticNorth.Valiant.SourceGeneration.ValiantSourceBuilder;

namespace StaticNorth.Valiant.Mapping.SourceGeneration;

/// <summary>
/// Generates Valiant mapper implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantMappingGenerator
{

    private static void ExecuteMapperDiagnostics(SourceProductionContext context, MapperModel mapper)
    {
        foreach (var diagnostic in mapper.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.Create());
        }
    }

    private static void ExecuteMapperSource(SourceProductionContext context, MapperModel mapper)
    {
        if (!IsValidMapper(mapper))
        {
            return;
        }

        context.AddSource(mapper.Declaration.HintName, SourceText.From(GenerateMapper(mapper), Encoding.UTF8));
    }

    private static void ExecuteDependencyInjection(
        SourceProductionContext context,
        bool hasDependencyInjection,
        ImmutableArray<DependencyInjectionMapperModel> mappers)
    {
        if (!hasDependencyInjection)
        {
            return;
        }

        context.AddSource(
            "StaticNorth.Valiant.Mapping.DependencyInjection.g.cs",
            SourceText.From(GenerateDependencyInjection([..mappers.Distinct().OrderBy(static mapper => mapper.MapperType, StringComparer.Ordinal)]), Encoding.UTF8));
    }

    private static void ExecuteMapperPairRegistrationSource(
        SourceProductionContext context,
        ImmutableArray<MapperPairRegistrationModel> mapperPairs)
    {
        if (mapperPairs.IsDefaultOrEmpty)
        {
            return;
        }

        context.AddSource(
            "StaticNorth.Valiant.Mapping.MapperRegistrations.g.cs",
            SourceText.From(GenerateMapperPairRegistrations([..mapperPairs.Distinct().OrderBy(static mapper => mapper.SourceType, StringComparer.Ordinal).ThenBy(static mapper => mapper.TargetType, StringComparer.Ordinal)]), Encoding.UTF8));
    }

    private static bool IsValidMapper(MapperModel mapper)
    {
        return mapper.Mapping is not null &&
            !mapper.Diagnostics.Any(static diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);
    }

    private static void ExecuteDuplicateMapperPairDiagnostics(
        SourceProductionContext context,
        ImmutableArray<MapperModel> mappers)
    {
        var validMappers = mappers
            .Where(static mapper => IsValidMapper(mapper))
            .GroupBy(static mapper => mapper.Mapping!.SourceType + "\u001f" + mapper.Mapping.TargetType)
            .Where(static group => group.Count() > 1);

        foreach (var group in validMappers)
        {
            var orderedMappers = group
                .OrderBy(static mapper => mapper.Declaration.Name, StringComparer.Ordinal)
                .ThenBy(static mapper => mapper.Declaration.HintName, StringComparer.Ordinal)
                .ToImmutableArray();
            var primaryMapper = orderedMappers[0];
            for (var index = 1; index < orderedMappers.Length; index++)
            {
                var duplicateMapper = orderedMappers[index];
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMapperPair,
                    duplicateMapper.Declaration.Location.ToLocation(),
                    duplicateMapper.Declaration.Name,
                    duplicateMapper.Mapping!.SourceType,
                    duplicateMapper.Mapping.TargetType,
                    primaryMapper.Declaration.Name));
            }
        }
    }

    private static string GenerateMapperPairRegistrations(ImmutableArray<MapperPairRegistrationModel> mapperPairs)
    {
        var builder = new StringBuilder();
        AppendGeneratedFileHeader(builder);

        foreach (var mapperPair in mapperPairs)
        {
            builder.Append("[assembly: global::StaticNorth.Valiant.Mapping.ValiantMapperRegistrationAttribute(typeof(")
                .Append(mapperPair.SourceType)
                .Append("), typeof(")
                .Append(mapperPair.TargetType)
                .AppendLine("))]");
        }

        return builder.ToString();
    }

    private static string GenerateMapper(MapperModel mapper)
    {
        var builder = new StringBuilder();
        var declaration = mapper.Declaration;
        var mapping = mapper.Mapping!;
        var namespaceName = declaration.NamespaceName;

        AppendGeneratedFileHeader(builder);

        if (namespaceName is not null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypeCount = AppendContainingTypeDeclarations(
            builder,
            namespaceName,
            declaration.ContainingTypes,
            static containingType => containingType.Modifier,
            static containingType => containingType.Keyword,
            static containingType => containingType.Name);

        AppendXmlDocumentation(builder, namespaceName, 0, "Maps instances using generated direct assignment code.");
        AppendIndent(builder, 0, namespaceName);
        builder.Append(declaration.Modifier)
            .Append("partial ")
            .Append(declaration.Keyword)
            .Append(' ')
            .Append(declaration.Name)
            .Append(" : global::StaticNorth.Valiant.Mapping.IMapper<")
            .Append(mapping.SourceType)
            .Append(", ")
            .Append(mapping.TargetType)
            .AppendLine(">");
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("{");

        AppendMappingOptionsMembers(builder, namespaceName, declaration, mapping);
        AppendResolveMappingModeMember(builder, namespaceName);
        builder.AppendLine();
        AppendMapMethod(builder, namespaceName, mapping);
        if (mapping.HasConfigureMethod)
        {
            AppendRuntimeMappingMembers(builder, namespaceName, mapping);
        }

        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("}");
        AppendContainingTypeClosures(builder, namespaceName, containingTypeCount);

        if (namespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string GenerateDependencyInjection(ImmutableArray<DependencyInjectionMapperModel> mappers)
    {
        var builder = new StringBuilder();
        AppendGeneratedFileHeader(builder);
        builder.AppendLine();
        builder.AppendLine("namespace StaticNorth.Valiant.Mapping");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Provides dependency injection registration for Valiant mappers.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static class ValiantMappingServiceCollectionExtensions");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Registers source-generated Valiant mappers.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        /// <param name=\"services\">The service collection.</param>");
        builder.AppendLine("        /// <returns>The service collection.</returns>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantMappers(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::System.Action<global::StaticNorth.Valiant.Mapping.ValiantMappingOptions>? configure = null)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (services is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var valiantMappingOptions = new global::StaticNorth.Valiant.Mapping.ValiantMappingOptions();");
        builder.AppendLine("            configure?.Invoke(valiantMappingOptions);");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::StaticNorth.Valiant.Mapping.ValiantMappingOptions), valiantMappingOptions));");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::StaticNorth.Valiant.Mapping.IMapper), typeof(global::StaticNorth.Valiant.Mapping.Infrastructure.ValiantMapperLookup)));");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::StaticNorth.Valiant.Mapping.IMapperFactory), typeof(global::StaticNorth.Valiant.Mapping.Infrastructure.ValiantMapperLookup)));");
        builder.AppendLine();

        foreach (var mapper in mappers)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(")
                .Append(mapper.MapperType)
                .Append("), typeof(")
                .Append(mapper.MapperType)
                .AppendLine(")));");
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(global::StaticNorth.Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .Append(">), typeof(")
                .Append(mapper.MapperType)
                .AppendLine(")));");
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::StaticNorth.Valiant.Mapping.Infrastructure.ValiantMapperRegistration), typeof(global::StaticNorth.Valiant.Mapping.Infrastructure.ValiantMapperRegistration<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .AppendLine(">)));");
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendMapMethod(StringBuilder builder, string? namespaceName, MappingModel mapping)
    {
        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Maps the specified source instance to a target instance.",
            "The mapped target instance.",
            ("source", "The source instance."));
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ")
            .Append(mapping.TargetType)
            .Append(" Map(")
            .Append(mapping.SourceType)
            .AppendLine(" source)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");

        if (!mapping.IsSourceValueType)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("if (source is null)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("throw new global::System.ArgumentNullException(nameof(source));");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
        }

        if (mapping.HasConfigureMethod)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("if (GetEffectiveMappingMode() == global::StaticNorth.Valiant.Mapping.ValiantMappingMode.Debuggable)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("return MapRuntime(source);");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
        }

        foreach (var declaration in mapping.ConfigureLocals)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine(declaration);
        }

        AppendIndent(builder, 2, namespaceName);
        builder.Append("var target = new ").Append(mapping.TargetType).Append('(');
        for (var index = 0; index < mapping.Constructor.Arguments.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            var argument = mapping.Constructor.Arguments[index];
            builder.Append(argument.ParameterName).Append(": ")
                .Append("ConvertConstructorArgument<").Append(argument.ParameterType).Append(">(")
                .Append(argument.SourceExpression).Append(')');
        }

        builder.Append(')');

        var initializerAssignments = mapping.Assignments
            .Where(static assignment => assignment.UseInitializer && assignment.ConditionExpression is null)
            .ToImmutableArray();
        var remainingAssignments = mapping.Assignments
            .Where(static assignment => !assignment.UseInitializer || assignment.ConditionExpression is not null);

        if (initializerAssignments.Length == 0)
        {
            builder.AppendLine(";");
        }
        else
        {
            builder.AppendLine();
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            for (var index = 0; index < initializerAssignments.Length; index++)
            {
                var assignment = initializerAssignments[index];
                AppendIndent(builder, 3, namespaceName);
                builder.Append(assignment.TargetMemberName)
                    .Append(" = ")
                    .Append(assignment.SourceExpression);
                builder.AppendLine(index == initializerAssignments.Length - 1 ? string.Empty : ",");
            }

            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("};");
        }

        foreach (var assignment in remainingAssignments)
        {
            builder.AppendLine();
            var indent = 2;
            if (assignment.ConditionExpression is not null)
            {
                AppendIndent(builder, indent, namespaceName);
                builder.Append("if (").Append(assignment.ConditionExpression).AppendLine(")");
                AppendIndent(builder, indent, namespaceName);
                builder.AppendLine("{");
                indent++;
            }

            AppendIndent(builder, indent, namespaceName);
            if (assignment.InitPropertyType is not null)
            {
                builder.Append("global::StaticNorth.Valiant.Mapping.Infrastructure.ValiantInitProperty.Set<")
                    .Append(mapping.TargetType).Append(", ").Append(assignment.InitPropertyType)
                    .Append(">(ref target, nameof(target.")
                    .Append(assignment.TargetMemberName).Append("), ").Append(assignment.SourceExpression).AppendLine(");");
            }
            else
            {
                builder.Append("target.").Append(assignment.TargetMemberName).Append(" = ")
                    .Append(assignment.SourceExpression).AppendLine(";");
            }

            if (assignment.ConditionExpression is not null)
            {
                AppendIndent(builder, indent - 1, namespaceName);
                builder.AppendLine("}");
            }
        }

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return target;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        if (!mapping.Constructor.Arguments.IsEmpty)
        {
            builder.AppendLine();
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("private static T ConvertConstructorArgument<T>(T value) => value;");
        }

        if (mapping.ConfigureLocals.Any(static local => local.StartsWith("var __valiantMapper", StringComparison.Ordinal)))
        {
            builder.AppendLine();
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("private static TDestination MapWithConfiguredMapper<TSourceMember, TDestination>(TSourceMember value, global::System.Func<TSourceMember, TDestination> map) => map(value);");
        }

        foreach (var collectionMapping in mapping.CollectionMappings)
        {
            AppendCollectionMappingMethod(builder, namespaceName, collectionMapping);
        }

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private global::StaticNorth.Valiant.Mapping.ValiantNullHandling GetEffectiveNullHandling()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return _valiantNullHandling;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        if (mapping.UsesServiceProviderMapper)
        {
            AppendServiceProviderMappingMembers(builder, namespaceName);
        }
    }

    private static void AppendCollectionMappingMethod(
        StringBuilder builder,
        string? namespaceName,
        CollectionMappingModel collectionMapping)
    {
        var returnType = collectionMapping.Kind switch
        {
            CollectionMappingKind.Array => collectionMapping.TargetElementType + "[]",
            CollectionMappingKind.HashSet => "global::System.Collections.Generic.HashSet<" + collectionMapping.TargetElementType + ">",
            _ => "global::System.Collections.Generic.List<" + collectionMapping.TargetElementType + ">"
        };

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        AppendIndent(builder, 1, namespaceName);
        builder.Append("private ")
            .Append(returnType)
            .Append(' ')
            .Append(collectionMapping.MethodName)
            .Append('(')
            .Append(collectionMapping.SourceType)
            .Append(" source");
        if (collectionMapping.RequiresElementMapper)
        {
            builder.Append(", global::System.Func<").Append(collectionMapping.SourceElementType).Append(", ")
                .Append(collectionMapping.TargetElementType).Append("> mapItem");
        }

        builder.AppendLine(")");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (source is null)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return null!;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();

        if (collectionMapping.Kind == CollectionMappingKind.Array)
        {
            AppendArrayCollectionMappingBody(builder, namespaceName, collectionMapping);
        }
        else
        {
            AppendAddCollectionMappingBody(builder, namespaceName, collectionMapping);
        }

        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendAddCollectionMappingBody(
        StringBuilder builder,
        string? namespaceName,
        CollectionMappingModel collectionMapping)
    {
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var target = new global::System.Collections.Generic.")
            .Append(collectionMapping.Kind == CollectionMappingKind.HashSet ? "HashSet" : "List")
            .Append('<')
            .Append(collectionMapping.TargetElementType)
            .Append('>');
        if (collectionMapping.CapacityExpression is null)
        {
            builder.AppendLine("();");
        }
        else
        {
            builder.Append('(')
                .Append(collectionMapping.CapacityExpression)
                .AppendLine(");");
        }

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("foreach (var item in source)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("target.Add(")
            .Append(collectionMapping.ElementMapping)
            .AppendLine(");");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return target;");
    }

    private static void AppendArrayCollectionMappingBody(
        StringBuilder builder,
        string? namespaceName,
        CollectionMappingModel collectionMapping)
    {
        if (collectionMapping.SourceKind == CollectionSourceKind.Array)
        {
            AppendIndexedArrayCollectionMappingBody(builder, namespaceName, collectionMapping);
            return;
        }

        if (collectionMapping.CapacityExpression is null)
        {
            AppendRuntimeSizedArrayCollectionMappingBody(builder, namespaceName, collectionMapping);

            AppendIndent(builder, 2, namespaceName);
            builder.Append("var target = new global::System.Collections.Generic.List<")
                .Append(collectionMapping.TargetElementType)
                .AppendLine(">();");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("foreach (var item in source)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.Append("target.Add(")
                .Append(collectionMapping.ElementMapping)
                .AppendLine(");");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("return target.ToArray();");
            return;
        }

        AppendIndent(builder, 2, namespaceName);
        builder.Append("var target = new ")
            .AppendFormat(CultureInfo.InvariantCulture, collectionMapping.ArrayAllocationFormat!, collectionMapping.CapacityExpression)
            .AppendLine(";");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var targetIndex = 0;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("foreach (var item in source)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("if (targetIndex == target.Length)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("global::System.Array.Resize(ref target, target.Length == 0 ? 4 : checked(target.Length * 2));");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 3, namespaceName);
        builder.Append("target[targetIndex++] = ")
            .Append(collectionMapping.ElementMapping)
            .AppendLine(";");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (targetIndex != target.Length)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("global::System.Array.Resize(ref target, targetIndex);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return target;");
    }

    private static void AppendIndexedArrayCollectionMappingBody(
        StringBuilder builder,
        string? namespaceName,
        CollectionMappingModel collectionMapping)
    {
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var target = new ")
            .AppendFormat(CultureInfo.InvariantCulture, collectionMapping.ArrayAllocationFormat!, "source.Length")
            .AppendLine(";");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("for (var sourceIndex = 0; sourceIndex < source.Length; sourceIndex++)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("var item = source[sourceIndex];");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("target[sourceIndex] = ")
            .Append(collectionMapping.ElementMapping)
            .AppendLine(";");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return target;");
    }

    private static void AppendRuntimeSizedArrayCollectionMappingBody(
        StringBuilder builder,
        string? namespaceName,
        CollectionMappingModel collectionMapping)
    {
        AppendIndent(builder, 2, namespaceName);
        builder.Append("if (source is ")
            .Append(collectionMapping.SourceElementType)
            .AppendLine("[] sourceArray)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("var arrayTarget = new ")
            .AppendFormat(CultureInfo.InvariantCulture, collectionMapping.ArrayAllocationFormat!, "sourceArray.Length")
            .AppendLine(";");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("for (var sourceIndex = 0; sourceIndex < sourceArray.Length; sourceIndex++)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("var item = sourceArray[sourceIndex];");
        AppendIndent(builder, 4, namespaceName);
        builder.Append("arrayTarget[sourceIndex] = ")
            .Append(collectionMapping.ElementMapping)
            .AppendLine(";");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return arrayTarget;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var sourceCount = -1;");
        AppendIndent(builder, 2, namespaceName);
        builder.Append("if (source is global::System.Collections.Generic.ICollection<")
            .Append(collectionMapping.SourceElementType)
            .AppendLine("> sourceCollection)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("sourceCount = sourceCollection.Count;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        builder.Append("else if (source is global::System.Collections.Generic.IReadOnlyCollection<")
            .Append(collectionMapping.SourceElementType)
            .AppendLine("> sourceReadOnlyCollection)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("sourceCount = sourceReadOnlyCollection.Count;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (sourceCount >= 0)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("var countedTarget = new ")
            .AppendFormat(CultureInfo.InvariantCulture, collectionMapping.ArrayAllocationFormat!, "sourceCount")
            .AppendLine(";");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("var targetIndex = 0;");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("foreach (var item in source)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("if (targetIndex == countedTarget.Length)");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 5, namespaceName);
        builder.AppendLine("global::System.Array.Resize(ref countedTarget, countedTarget.Length == 0 ? 4 : checked(countedTarget.Length * 2));");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 4, namespaceName);
        builder.Append("countedTarget[targetIndex++] = ")
            .Append(collectionMapping.ElementMapping)
            .AppendLine(";");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("if (targetIndex != countedTarget.Length)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("global::System.Array.Resize(ref countedTarget, targetIndex);");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return countedTarget;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendMappingOptionsMembers(StringBuilder builder, string? namespaceName, MapperDeclarationModel declaration, MappingModel mapping)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private readonly global::StaticNorth.Valiant.Mapping.ValiantMappingMode _valiantMappingMode = ResolveMappingMode(global::StaticNorth.Valiant.Mapping.ValiantMappingMode.Auto);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private readonly global::StaticNorth.Valiant.Mapping.ValiantNullHandling _valiantNullHandling = global::StaticNorth.Valiant.Mapping.ValiantNullHandling.MapNulls;");
        if (mapping.UsesServiceProviderMapper)
        {
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("private readonly global::System.IServiceProvider? _valiantServiceProvider = null;");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("[global::System.ThreadStatic]");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("private static int _valiantMappingDepth;");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("private readonly global::System.Collections.Generic.Dictionary<global::System.Type, object> _valiantMapperCache = new global::System.Collections.Generic.Dictionary<global::System.Type, object>();");
        }

        if (declaration.HasInstanceConstructor)
        {
            AppendUserConstructorOverloads(builder, namespaceName, declaration, mapping.UsesServiceProviderMapper);
            return;
        }

        builder.AppendLine();
        AppendXmlDocumentation(builder, namespaceName, 1, "Initializes a new mapper instance.");
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ").Append(declaration.Name).AppendLine("()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();

        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Initializes a new mapper instance with explicit mapping options.",
            null,
            ("valiantMappingOptions", "The mapping options to use for this mapper instance."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ").Append(declaration.Name).AppendLine("(global::StaticNorth.Valiant.Mapping.ValiantMappingOptions valiantMappingOptions)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_ = valiantMappingOptions ?? throw new global::System.ArgumentNullException(nameof(valiantMappingOptions));");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantMappingMode = ResolveMappingMode(valiantMappingOptions.Mode);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantNullHandling = valiantMappingOptions.NullHandling;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        if (!mapping.UsesServiceProviderMapper)
        {
            return;
        }

        builder.AppendLine();

        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Initializes a new mapper instance with service-provider mapper lookup.",
            null,
            ("valiantServiceProvider", "The service provider used to resolve child mappers."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ").Append(declaration.Name).AppendLine("(global::System.IServiceProvider valiantServiceProvider)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantServiceProvider = valiantServiceProvider ?? throw new global::System.ArgumentNullException(nameof(valiantServiceProvider));");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();

        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Initializes a new mapper instance with explicit mapping options and service-provider mapper lookup.",
            null,
            ("valiantMappingOptions", "The mapping options to use for this mapper instance."),
            ("valiantServiceProvider", "The service provider used to resolve child mappers."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ").Append(declaration.Name).AppendLine("(global::StaticNorth.Valiant.Mapping.ValiantMappingOptions valiantMappingOptions, global::System.IServiceProvider valiantServiceProvider)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_ = valiantMappingOptions ?? throw new global::System.ArgumentNullException(nameof(valiantMappingOptions));");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantMappingMode = ResolveMappingMode(valiantMappingOptions.Mode);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantNullHandling = valiantMappingOptions.NullHandling;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantServiceProvider = valiantServiceProvider ?? throw new global::System.ArgumentNullException(nameof(valiantServiceProvider));");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendServiceProviderMappingMembers(StringBuilder builder, string? namespaceName)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private TTargetMember MapWithServiceProvider<TSourceMember, TTargetMember>(TSourceMember sourceMember)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (sourceMember is null)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return default!;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (ResolveNestedMapper(typeof(global::StaticNorth.Valiant.Mapping.IMapper<TSourceMember, TTargetMember>)) is global::StaticNorth.Valiant.Mapping.IMapper<TSourceMember, TTargetMember> mapper)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return MapWithDepthGuard(mapper, sourceMember);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("throw new global::System.InvalidOperationException(\"No Valiant mapper is registered for source type '\" + typeof(TSourceMember).FullName + \"' and target type '\" + typeof(TTargetMember).FullName + \"'. Register mappers with AddValiantMappers or configure this member with UseMapper.\");");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private object? ResolveNestedMapper(global::System.Type mapperType)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("lock (_valiantMapperCache)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("if (_valiantMapperCache.TryGetValue(mapperType, out var cachedMapper))");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("return cachedMapper;");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("if (_valiantServiceProvider?.GetService(mapperType) is { } resolvedMapper && mapperType.IsInstanceOfType(resolvedMapper))");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("_valiantMapperCache.Add(mapperType, resolvedMapper);");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("return resolvedMapper;");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return null;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private static TTargetMember MapWithDepthGuard<TSourceMember, TTargetMember>(global::StaticNorth.Valiant.Mapping.IMapper<TSourceMember, TTargetMember> mapper, TSourceMember sourceMember)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (_valiantMappingDepth >= 64)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("throw new global::System.InvalidOperationException(\"Valiant mapper recursion depth exceeded 64 while mapping source type '\" + typeof(TSourceMember).FullName + \"' to target type '\" + typeof(TTargetMember).FullName + \"'. Check for cyclic object graphs or configure this member explicitly.\");");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantMappingDepth++;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("try");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return mapper.Map(sourceMember);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("finally");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("_valiantMappingDepth--;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendUserConstructorOverloads(StringBuilder builder, string? namespaceName, MapperDeclarationModel declaration, bool includeServiceProvider)
    {
        var constructorSignatures = new HashSet<string>(declaration.ConstructorSignatures, StringComparer.Ordinal);
        foreach (var constructor in declaration.UserConstructors)
        {
            if (!HasParameter(constructor, "global::StaticNorth.Valiant.Mapping.ValiantMappingOptions"))
            {
                AppendUserConstructorOverload(builder, namespaceName, declaration, constructor, includeOptions: true, includeServiceProvider: false, constructorSignatures);
            }

            if (includeServiceProvider &&
                !HasParameter(constructor, "global::System.IServiceProvider") &&
                !HasParameter(constructor, "global::StaticNorth.Valiant.Mapping.ValiantMappingOptions"))
            {
                AppendUserConstructorOverload(builder, namespaceName, declaration, constructor, includeOptions: false, includeServiceProvider: true, constructorSignatures);
                AppendUserConstructorOverload(builder, namespaceName, declaration, constructor, includeOptions: true, includeServiceProvider: true, constructorSignatures);
            }
        }
    }

    private static void AppendUserConstructorOverload(
        StringBuilder builder,
        string? namespaceName,
        MapperDeclarationModel declaration,
        MapperConstructorModel constructor,
        bool includeOptions,
        bool includeServiceProvider,
        HashSet<string> constructorSignatures)
    {
        var signature = CreateGeneratedConstructorSignature(constructor, includeOptions, includeServiceProvider);
        if (!constructorSignatures.Add(signature))
        {
            return;
        }

        builder.AppendLine();
        var summary = includeOptions && includeServiceProvider
            ? "Initializes a new mapper instance with user dependencies, explicit mapping options, and service-provider mapper lookup."
            : includeServiceProvider
                ? "Initializes a new mapper instance with user dependencies and service-provider mapper lookup."
                : "Initializes a new mapper instance with user dependencies and explicit mapping options.";
        if (includeOptions && includeServiceProvider)
        {
            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                summary,
                null,
                ("__valiantMappingOptions", "The mapping options to use for this mapper instance."),
                ("__valiantServiceProvider", "The service provider used to resolve child mappers."));
        }
        else if (includeOptions)
        {
            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                summary,
                null,
                ("__valiantMappingOptions", "The mapping options to use for this mapper instance."));
        }
        else
        {
            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                summary,
                null,
                ("__valiantServiceProvider", "The service provider used to resolve child mappers."));
        }

        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ").Append(declaration.Name).Append('(');
        AppendUserConstructorParameters(builder, constructor);
        if (constructor.Parameters.Length > 0)
        {
            builder.Append(", ");
        }

        if (includeOptions)
        {
            builder.Append("global::StaticNorth.Valiant.Mapping.ValiantMappingOptions __valiantMappingOptions");
            if (includeServiceProvider)
            {
                builder.Append(", ");
            }
        }

        if (includeServiceProvider)
        {
            builder.Append("global::System.IServiceProvider __valiantServiceProvider");
        }

        builder.Append(')');
        if (constructor.Parameters.Length > 0)
        {
            builder.Append(" : this(");
            AppendUserConstructorArguments(builder, constructor);
            builder.Append(')');
        }
        else
        {
            builder.Append(" : this()");
        }

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        if (includeOptions)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_ = __valiantMappingOptions ?? throw new global::System.ArgumentNullException(nameof(__valiantMappingOptions));");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_valiantMappingMode = ResolveMappingMode(__valiantMappingOptions.Mode);");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_valiantNullHandling = __valiantMappingOptions.NullHandling;");
        }

        if (includeServiceProvider)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_valiantServiceProvider = __valiantServiceProvider ?? throw new global::System.ArgumentNullException(nameof(__valiantServiceProvider));");
        }

        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendUserConstructorParameters(StringBuilder builder, MapperConstructorModel constructor)
    {
        for (var index = 0; index < constructor.Parameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            var parameter = constructor.Parameters[index];
            builder.Append(parameter.TypeName).Append(' ').Append(parameter.Name);
        }
    }

    private static void AppendUserConstructorArguments(StringBuilder builder, MapperConstructorModel constructor)
    {
        for (var index = 0; index < constructor.Parameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(constructor.Parameters[index].Name);
        }
    }

    private static string CreateGeneratedConstructorSignature(MapperConstructorModel constructor, bool includeOptions, bool includeServiceProvider)
    {
        var parameterTypes = new List<string>(constructor.Parameters.Length + (includeOptions ? 2 : 1));
        parameterTypes.AddRange(constructor.Parameters.Select(static parameter => parameter.TypeName));
        if (includeOptions)
        {
            parameterTypes.Add("global::StaticNorth.Valiant.Mapping.ValiantMappingOptions");
        }

        if (includeServiceProvider)
        {
            parameterTypes.Add("global::System.IServiceProvider");
        }

        return string.Join("|", parameterTypes);
    }

    private static bool HasParameter(MapperConstructorModel constructor, string typeName)
    {
        return constructor.Parameters.Any(parameter => string.Equals(parameter.TypeName, typeName, StringComparison.Ordinal));
    }

    private static void AppendResolveMappingModeMember(StringBuilder builder, string? namespaceName)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private static global::StaticNorth.Valiant.Mapping.ValiantMappingMode ResolveMappingMode(global::StaticNorth.Valiant.Mapping.ValiantMappingMode mode)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (mode != global::StaticNorth.Valiant.Mapping.ValiantMappingMode.Auto)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return mode;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return global::StaticNorth.Valiant.Mapping.ValiantMappingMode.Fast;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendRuntimeMappingMembers(StringBuilder builder, string? namespaceName, MappingModel mapping)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private global::StaticNorth.Valiant.Mapping.ValiantMappingMode GetEffectiveMappingMode()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return _valiantMappingMode;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("private ").Append(mapping.TargetType).Append(" MapRuntime(").Append(mapping.SourceType).AppendLine(" source)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var context = new global::StaticNorth.Valiant.Mapping.MappingContext<")
            .Append(mapping.SourceType)
            .Append(", ")
            .Append(mapping.TargetType)
            .AppendLine(">(source, GetEffectiveNullHandling());");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("Configure(context);");
        AppendIndent(builder, 2, namespaceName);
        builder.Append("return context.MapRuntime(new global::System.Type[] { ");
        for (var index = 0; index < mapping.Constructor.Arguments.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append("typeof(").Append(mapping.Constructor.Arguments[index].RuntimeParameterType).Append(')');
        }

        builder.Append(" }");
        var initializerMembers = mapping.Assignments
            .Where(static assignment => assignment.UseInitializer && assignment.ConditionExpression is null)
            .ToImmutableArray();
        if (!initializerMembers.IsEmpty || mapping.Assignments.Any(static assignment => assignment.InitPropertyType is not null))
        {
            builder.Append(mapping.UsesServiceProviderMapper ? ", ResolveNestedMapper" : ", null");
            builder.Append(", new string[] { ");
            for (var index = 0; index < initializerMembers.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(ValiantSourceLiterals.String(initializerMembers[index].TargetMemberName));
            }

            builder.Append(" }");
        }
        else if (mapping.UsesServiceProviderMapper)
        {
            builder.Append(", resolveMapper: ResolveNestedMapper");
        }

        builder.AppendLine(");");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

}
