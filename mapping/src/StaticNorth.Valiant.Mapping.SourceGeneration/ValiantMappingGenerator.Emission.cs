using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using static Valiant.SourceGeneration.ValiantSourceBuilder;

namespace Valiant.Mapping.SourceGeneration;

/// <summary>
/// Generates Valiant mapper implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantMappingGenerator
{
    private const int DynamicLookupDictionaryThreshold = 8;

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
            "Valiant.Mapping.DependencyInjection.g.cs",
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
            "Valiant.Mapping.MapperRegistrations.g.cs",
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
            builder.Append("[assembly: global::Valiant.Mapping.ValiantMapperRegistrationAttribute(typeof(")
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
            .Append(" : global::Valiant.Mapping.IMapper<")
            .Append(mapping.SourceType)
            .Append(", ")
            .Append(mapping.TargetType)
            .AppendLine(">");
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("{");

        AppendMappingOptionsMembers(builder, namespaceName, declaration, mapping);
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
        builder.AppendLine("namespace Valiant.Mapping");
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
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantMappers(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::System.Action<global::Valiant.Mapping.ValiantMappingOptions>? configure = null)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (services is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var valiantMappingOptions = new global::Valiant.Mapping.ValiantMappingOptions();");
        builder.AppendLine("            configure?.Invoke(valiantMappingOptions);");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::Valiant.Mapping.ValiantMappingOptions), valiantMappingOptions));");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::Valiant.Mapping.IMapper), typeof(global::Valiant.Mapping.ValiantGeneratedMapper)));");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::Valiant.Mapping.IMapperFactory), typeof(global::Valiant.Mapping.ValiantGeneratedMapperFactory)));");
        builder.AppendLine();

        foreach (var mapper in mappers)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(")
                .Append(mapper.MapperType)
                .Append("), typeof(")
                .Append(mapper.MapperType)
                .AppendLine(")));");
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .Append(">), typeof(")
                .Append(mapper.MapperType)
                .AppendLine(")));");
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        AppendGeneratedMapper(builder, mappers);
        builder.AppendLine();
        AppendGeneratedMapperFactory(builder, mappers);
        if (mappers.Length > DynamicLookupDictionaryThreshold)
        {
            builder.AppendLine();
            AppendGeneratedMapperLookup(builder, mappers);
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendGeneratedMapper(StringBuilder builder, ImmutableArray<DependencyInjectionMapperModel> mappers)
    {
        builder.AppendLine("    file sealed class ValiantGeneratedMapper : global::Valiant.Mapping.IMapper");
        builder.AppendLine("    {");
        builder.AppendLine("        private readonly global::System.IServiceProvider _services;");
        builder.AppendLine();
        builder.AppendLine("        public ValiantGeneratedMapper(global::System.IServiceProvider services)");
        builder.AppendLine("        {");
        builder.AppendLine("            _services = services ?? throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public TDestination Map<TDestination>(object source)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (TryMap<TDestination>(source, out var destination))");
        builder.AppendLine("            {");
        builder.AppendLine("                return destination!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var sourceType = source?.GetType() ?? throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            throw CreateMissingMapperException(sourceType, typeof(TDestination));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public object Map(object source, global::System.Type destinationType)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (TryMap(source, destinationType, out var destination))");
        builder.AppendLine("            {");
        builder.AppendLine("                return destination!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var sourceType = source?.GetType() ?? throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            throw CreateMissingMapperException(sourceType, destinationType);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public bool TryMap<TDestination>(object source, out TDestination? destination)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (source is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var sourceType = source.GetType();");
        foreach (var mapper in mappers)
        {
            builder.Append("            if (sourceType == typeof(")
                .Append(mapper.SourceType)
                .Append(") && typeof(TDestination) == typeof(")
                .Append(mapper.TargetType)
                .AppendLine("))");
            builder.AppendLine("            {");
            builder.Append("                if (_services.GetService(typeof(global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .Append(">)) is global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .AppendLine("> resolvedMapper)");
            builder.AppendLine("                {");
            builder.Append("                    destination = (TDestination)(object)resolvedMapper.Map((")
                .Append(mapper.SourceType)
                .AppendLine(")source);");
            builder.AppendLine("                    return true;");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                destination = default;");
            builder.AppendLine("                return false;");
            builder.AppendLine("            }");
        }

        builder.AppendLine();
        builder.AppendLine("            destination = default;");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public bool TryMap(object source, global::System.Type destinationType, out object? destination)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (source is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            if (destinationType is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(destinationType));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var sourceType = source.GetType();");
        if (mappers.Length > DynamicLookupDictionaryThreshold)
        {
            builder.AppendLine("            return ValiantGeneratedMapperLookup.TryMap(_services, source, sourceType, destinationType, out destination);");
        }
        else
        {
            foreach (var mapper in mappers)
            {
                builder.Append("            if (sourceType == typeof(")
                    .Append(mapper.SourceType)
                    .Append(") && destinationType == typeof(")
                    .Append(mapper.TargetType)
                    .AppendLine("))");
                builder.AppendLine("            {");
                builder.Append("                if (_services.GetService(typeof(global::Valiant.Mapping.IMapper<")
                    .Append(mapper.SourceType)
                    .Append(", ")
                    .Append(mapper.TargetType)
                    .Append(">)) is global::Valiant.Mapping.IMapper<")
                    .Append(mapper.SourceType)
                    .Append(", ")
                    .Append(mapper.TargetType)
                    .AppendLine("> resolvedMapper)");
                builder.AppendLine("                {");
                builder.Append("                    destination = resolvedMapper.Map((")
                    .Append(mapper.SourceType)
                    .AppendLine(")source);");
                builder.AppendLine("                    return true;");
                builder.AppendLine("                }");
                builder.AppendLine();
                builder.AppendLine("                destination = null;");
                builder.AppendLine("                return false;");
                builder.AppendLine("            }");
            }

            builder.AppendLine();
            builder.AppendLine("            destination = null;");
            builder.AppendLine("            return false;");
        }
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private static global::System.InvalidOperationException CreateMissingMapperException(global::System.Type sourceType, global::System.Type destinationType)");
        builder.AppendLine("        {");
        builder.AppendLine("            return new global::System.InvalidOperationException(\"No Valiant mapper is registered for source type '\" + sourceType.FullName + \"' and target type '\" + destinationType.FullName + \"'. Register mappers with AddValiantMappers.\");");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void AppendGeneratedMapperFactory(StringBuilder builder, ImmutableArray<DependencyInjectionMapperModel> mappers)
    {
        builder.AppendLine("    file sealed class ValiantGeneratedMapperFactory : global::Valiant.Mapping.IMapperFactory");
        builder.AppendLine("    {");
        builder.AppendLine("        private readonly global::System.IServiceProvider _services;");
        builder.AppendLine();
        builder.AppendLine("        public ValiantGeneratedMapperFactory(global::System.IServiceProvider services)");
        builder.AppendLine("        {");
        builder.AppendLine("            _services = services ?? throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public global::Valiant.Mapping.IMapper<TSource, TTarget> CreateMapper<TSource, TTarget>()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (TryCreateMapper<TSource, TTarget>(out var mapper))");
        builder.AppendLine("            {");
        builder.AppendLine("                return mapper!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            throw CreateMissingMapperException(typeof(TSource), typeof(TTarget));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public object CreateMapper(global::System.Type sourceType, global::System.Type targetType)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (TryCreateMapper(sourceType, targetType, out var mapper))");
        builder.AppendLine("            {");
        builder.AppendLine("                return mapper!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            throw CreateMissingMapperException(sourceType, targetType);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public bool TryCreateMapper<TSource, TTarget>(out global::Valiant.Mapping.IMapper<TSource, TTarget>? mapper)");
        builder.AppendLine("        {");
        foreach (var mapper in mappers)
        {
            builder.Append("            if (typeof(TSource) == typeof(")
                .Append(mapper.SourceType)
                .Append(") && typeof(TTarget) == typeof(")
                .Append(mapper.TargetType)
                .AppendLine("))");
            builder.AppendLine("            {");
            builder.Append("                if (_services.GetService(typeof(global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .Append(">)) is global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .AppendLine("> resolvedMapper)");
            builder.AppendLine("                {");
            builder.AppendLine("                    mapper = (global::Valiant.Mapping.IMapper<TSource, TTarget>)(object)resolvedMapper;");
            builder.AppendLine("                    return true;");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                mapper = null;");
            builder.AppendLine("                return false;");
            builder.AppendLine("            }");
        }

        builder.AppendLine();
        builder.AppendLine("            mapper = null;");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public bool TryCreateMapper(global::System.Type sourceType, global::System.Type targetType, out object? mapper)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (sourceType is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(sourceType));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            if (targetType is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(targetType));");
        builder.AppendLine("            }");
        builder.AppendLine();
        if (mappers.Length > DynamicLookupDictionaryThreshold)
        {
            builder.AppendLine("            return ValiantGeneratedMapperLookup.TryResolve(_services, sourceType, targetType, out mapper);");
        }
        else
        {
            foreach (var mapper in mappers)
            {
                builder.Append("            if (sourceType == typeof(")
                    .Append(mapper.SourceType)
                    .Append(") && targetType == typeof(")
                    .Append(mapper.TargetType)
                    .AppendLine("))");
                builder.AppendLine("            {");
                builder.Append("                mapper = _services.GetService(typeof(global::Valiant.Mapping.IMapper<")
                    .Append(mapper.SourceType)
                    .Append(", ")
                    .Append(mapper.TargetType)
                    .AppendLine(">));");
                builder.AppendLine("                return mapper is not null;");
                builder.AppendLine("            }");
            }

            builder.AppendLine();
            builder.AppendLine("            mapper = null;");
            builder.AppendLine("            return false;");
        }
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private static global::System.InvalidOperationException CreateMissingMapperException(global::System.Type sourceType, global::System.Type targetType)");
        builder.AppendLine("        {");
        builder.AppendLine("            return new global::System.InvalidOperationException(\"No Valiant mapper is registered for source type '\" + sourceType.FullName + \"' and target type '\" + targetType.FullName + \"'. Register mappers with AddValiantMappers.\");");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void AppendGeneratedMapperLookup(
        StringBuilder builder,
        ImmutableArray<DependencyInjectionMapperModel> mappers)
    {
        builder.AppendLine("    file static class ValiantGeneratedMapperLookup");
        builder.AppendLine("    {");
        builder.AppendLine("        private delegate object? ResolveMapper(global::System.IServiceProvider services);");
        builder.AppendLine("        private delegate MappingResult MapValue(global::System.IServiceProvider services, object source);");
        builder.AppendLine();
        builder.AppendLine("        private readonly struct MappingResult");
        builder.AppendLine("        {");
        builder.AppendLine("            public MappingResult(bool found, object? value)");
        builder.AppendLine("            {");
        builder.AppendLine("                Found = found;");
        builder.AppendLine("                Value = value;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public bool Found { get; }");
        builder.AppendLine("            public object? Value { get; }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private readonly struct Entry");
        builder.AppendLine("        {");
        builder.AppendLine("            public Entry(ResolveMapper resolve, MapValue map)");
        builder.AppendLine("            {");
        builder.AppendLine("                Resolve = resolve;");
        builder.AppendLine("                Map = map;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public ResolveMapper Resolve { get; }");
        builder.AppendLine("            public MapValue Map { get; }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private static readonly global::System.Collections.Generic.IReadOnlyDictionary<(global::System.Type Source, global::System.Type Target), Entry> Entries =");
        builder.AppendLine("            new global::System.Collections.Generic.Dictionary<(global::System.Type Source, global::System.Type Target), Entry>");
        builder.AppendLine("            {");
        foreach (var mapper in mappers)
        {
            builder.Append("                [(typeof(")
                .Append(mapper.SourceType)
                .Append("), typeof(")
                .Append(mapper.TargetType)
                .Append("))] = new Entry(")
                .AppendLine();
            builder.Append("                    static services => services.GetService(typeof(global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .AppendLine(">)),");
            builder.Append("                    static (services, source) => services.GetService(typeof(global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .Append(">)) is global::Valiant.Mapping.IMapper<")
                .Append(mapper.SourceType)
                .Append(", ")
                .Append(mapper.TargetType)
                .Append("> mapper ? new MappingResult(true, mapper.Map((")
                .Append(mapper.SourceType)
                .AppendLine(")source)) : default),");
        }

        builder.AppendLine("            };");
        builder.AppendLine();
        builder.AppendLine("        public static bool TryResolve(global::System.IServiceProvider services, global::System.Type sourceType, global::System.Type targetType, out object? mapper)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (Entries.TryGetValue((sourceType, targetType), out var entry))");
        builder.AppendLine("            {");
        builder.AppendLine("                mapper = entry.Resolve(services);");
        builder.AppendLine("                return mapper is not null;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            mapper = null;");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public static bool TryMap(global::System.IServiceProvider services, object source, global::System.Type sourceType, global::System.Type targetType, out object? destination)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (Entries.TryGetValue((sourceType, targetType), out var entry))");
        builder.AppendLine("            {");
        builder.AppendLine("                var result = entry.Map(services, source);");
        builder.AppendLine("                destination = result.Value;");
        builder.AppendLine("                return result.Found;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            destination = null;");
        builder.AppendLine("            return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
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
            builder.AppendLine("if (GetEffectiveMappingMode() == global::Valiant.Mapping.ValiantMappingMode.Debuggable)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("return MapRuntime(source);");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
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
            builder.Append(argument.ParameterName).Append(": ").Append(argument.SourceExpression);
        }

        builder.Append(')');

        var initializerAssignments = mapping.Assignments
            .Where(static assignment => assignment.ConditionExpression is null)
            .ToImmutableArray();
        var conditionalAssignments = mapping.Assignments
            .Where(static assignment => assignment.ConditionExpression is not null)
            .ToImmutableArray();

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

        foreach (var assignment in conditionalAssignments)
        {
            builder.AppendLine();
            AppendIndent(builder, 2, namespaceName);
            builder.Append("if (").Append(assignment.ConditionExpression).AppendLine(")");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.Append("target.")
                .Append(assignment.TargetMemberName)
                .Append(" = ")
                .Append(assignment.SourceExpression)
                .AppendLine(";");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
        }

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return target;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private global::Valiant.Mapping.ValiantNullHandling GetEffectiveNullHandling()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return _valiantMappingOptions?.NullHandling ?? global::Valiant.Mapping.ValiantNullHandling.MapNulls;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        if (mapping.UsesServiceProviderMapper)
        {
            AppendServiceProviderMappingMembers(builder, namespaceName);
        }
    }

    private static void AppendMappingOptionsMembers(StringBuilder builder, string? namespaceName, MapperDeclarationModel declaration, MappingModel mapping)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private readonly global::Valiant.Mapping.ValiantMappingOptions? _valiantMappingOptions = null;");
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
        builder.Append("public ").Append(declaration.Name).AppendLine("(global::Valiant.Mapping.ValiantMappingOptions valiantMappingOptions)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantMappingOptions = valiantMappingOptions ?? throw new global::System.ArgumentNullException(nameof(valiantMappingOptions));");
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
        builder.Append("public ").Append(declaration.Name).AppendLine("(global::Valiant.Mapping.ValiantMappingOptions valiantMappingOptions, global::System.IServiceProvider valiantServiceProvider)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantMappingOptions = valiantMappingOptions ?? throw new global::System.ArgumentNullException(nameof(valiantMappingOptions));");
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
        builder.AppendLine("var mapperType = typeof(global::Valiant.Mapping.IMapper<TSourceMember, TTargetMember>);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("global::Valiant.Mapping.IMapper<TSourceMember, TTargetMember>? mapper = null;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("lock (_valiantMapperCache)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("if (_valiantMapperCache.TryGetValue(mapperType, out var cachedMapper))");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("mapper = (global::Valiant.Mapping.IMapper<TSourceMember, TTargetMember>)cachedMapper;");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("else if (_valiantServiceProvider?.GetService(mapperType) is global::Valiant.Mapping.IMapper<TSourceMember, TTargetMember> resolvedMapper)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("_valiantMapperCache.Add(mapperType, resolvedMapper);");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("mapper = resolvedMapper;");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (mapper is not null)");
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
        builder.AppendLine("private static TTargetMember MapWithDepthGuard<TSourceMember, TTargetMember>(global::Valiant.Mapping.IMapper<TSourceMember, TTargetMember> mapper, TSourceMember sourceMember)");
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
            if (!HasParameter(constructor, "global::Valiant.Mapping.ValiantMappingOptions"))
            {
                AppendUserConstructorOverload(builder, namespaceName, declaration, constructor, includeOptions: true, includeServiceProvider: false, constructorSignatures);
            }

            if (includeServiceProvider &&
                !HasParameter(constructor, "global::System.IServiceProvider") &&
                !HasParameter(constructor, "global::Valiant.Mapping.ValiantMappingOptions"))
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
            builder.Append("global::Valiant.Mapping.ValiantMappingOptions __valiantMappingOptions");
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
            builder.AppendLine("_valiantMappingOptions = __valiantMappingOptions ?? throw new global::System.ArgumentNullException(nameof(__valiantMappingOptions));");
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
            parameterTypes.Add("global::Valiant.Mapping.ValiantMappingOptions");
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

    private static void AppendRuntimeMappingMembers(StringBuilder builder, string? namespaceName, MappingModel mapping)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private global::Valiant.Mapping.ValiantMappingMode GetEffectiveMappingMode()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var mode = _valiantMappingOptions?.Mode ?? global::Valiant.Mapping.ValiantMappingMode.Auto;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (mode != global::Valiant.Mapping.ValiantMappingMode.Auto)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return mode;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine("#if DEBUG");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return global::Valiant.Mapping.ValiantMappingMode.Debuggable;");
        builder.AppendLine("#else");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return global::Valiant.Mapping.ValiantMappingMode.Fast;");
        builder.AppendLine("#endif");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("private ").Append(mapping.TargetType).Append(" MapRuntime(").Append(mapping.SourceType).AppendLine(" source)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var context = new global::Valiant.Mapping.MappingContext<")
            .Append(mapping.SourceType)
            .Append(", ")
            .Append(mapping.TargetType)
            .AppendLine(">(source, GetEffectiveNullHandling());");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("Configure(context);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return context.MapRuntime();");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

}
