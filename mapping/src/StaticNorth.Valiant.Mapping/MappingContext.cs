using System.Linq.Expressions;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Defines mapping declarations from <typeparamref name="TSource" /> to <typeparamref name="TTarget" />.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public sealed class MappingContext<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>
{
    private readonly List<MappingRuleDeclaration<TSource>> _maps = [];
    private readonly List<MappingIgnoreDeclaration<TSource>> _ignores = [];
    private readonly List<object> _mappers = [];
    private readonly ValiantNullHandling _nullHandling;

    /// <summary>
    /// Initializes a new mapping declaration context.
    /// </summary>
    public MappingContext()
        : this(default!, false, ValiantNullHandling.MapNulls)
    {
    }

    /// <summary>
    /// Initializes a runtime mapping context for a source instance.
    /// </summary>
    /// <param name="source">The source instance being mapped.</param>
    public MappingContext(TSource source)
        : this(source, true, ValiantNullHandling.MapNulls)
    {
    }

    /// <summary>
    /// Initializes a runtime mapping context for a source instance and null handling policy.
    /// </summary>
    /// <param name="source">The source instance being mapped.</param>
    /// <param name="nullHandling">The null handling policy applied to mapping rules.</param>
    public MappingContext(TSource source, ValiantNullHandling nullHandling)
        : this(source, true, nullHandling)
    {
    }

    private MappingContext(TSource source, bool hasSource, ValiantNullHandling nullHandling)
    {
        Source = source;
        HasSource = hasSource;
        _nullHandling = nullHandling;
    }

    /// <summary>
    /// Gets the source instance for debuggable runtime mapping.
    /// </summary>
    public TSource Source { get; }

    internal bool HasSource { get; }

    /// <summary>
    /// Declares an explicit source-to-target member mapping.
    /// </summary>
    /// <param name="source">The source member selector.</param>
    /// <param name="target">The target member selector.</param>
    /// <returns>A mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> Map<TSourceMember, TTargetMember>(
        Expression<Func<TSource, TSourceMember>> source,
        Expression<Func<TTarget, TTargetMember>> target)
    {
        return Map(source, target, new MappingChainDeclaration<TSource>());
    }

    internal MappingRule<TSource, TTarget, TSourceMember, TTargetMember> Map<TSourceMember, TTargetMember>(
        Expression<Func<TSource, TSourceMember>> source,
        Expression<Func<TTarget, TTargetMember>> target,
        MappingChainDeclaration<TSource> chain)
    {
        _ = source ?? throw new ArgumentNullException(nameof(source));
        _ = target ?? throw new ArgumentNullException(nameof(target));
        var compiledSource = source.Compile();
        var declaration = new MappingRuleDeclaration<TSource>(
            value => compiledSource(value),
            GetSelectedMember(target),
            typeof(TSourceMember),
            typeof(TTargetMember),
            chain,
            _nullHandling);
        _maps.Add(declaration);
        return new MappingRule<TSource, TTarget, TSourceMember, TTargetMember>(this, declaration);
    }

    /// <summary>
    /// Declares that a target member is intentionally not mapped.
    /// </summary>
    /// <param name="target">The target member selector.</param>
    /// <returns>A mapping ignore rule builder.</returns>
    public MappingIgnoreRule<TSource, TTarget, TTargetMember> Ignore<TTargetMember>(
        Expression<Func<TTarget, TTargetMember>> target)
    {
        return Ignore(target, new MappingChainDeclaration<TSource>());
    }

    internal MappingIgnoreRule<TSource, TTarget, TTargetMember> Ignore<TTargetMember>(
        Expression<Func<TTarget, TTargetMember>> target,
        MappingChainDeclaration<TSource> chain)
    {
        _ = target ?? throw new ArgumentNullException(nameof(target));
        var declaration = new MappingIgnoreDeclaration<TSource>(GetSelectedMember(target), chain);
        _ignores.Add(declaration);
        return new MappingIgnoreRule<TSource, TTarget, TTargetMember>(this, declaration);
    }

    /// <summary>
    /// Declares a mapper dependency that can be reused by member mappings.
    /// </summary>
    /// <param name="mapper">The mapper dependency.</param>
    /// <typeparam name="TMapper">The mapper dependency type.</typeparam>
    /// <returns>The current mapping context.</returns>
    public MappingContext<TSource, TTarget> UseMapper<TMapper>(TMapper mapper)
    {
        _ = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _mappers.Add(mapper);
        return this;
    }

    /// <summary>
    /// Executes the configured runtime mapping rules.
    /// </summary>
    /// <returns>The mapped target instance.</returns>
    public TTarget MapRuntime()
    {
        if (!HasSource)
        {
            throw new InvalidOperationException("Runtime mapping requires a source instance.");
        }

        var target = CreateTarget();
        ApplyAssignments(target);
        return target;
    }

    private TTarget CreateTarget()
    {
        var targetType = typeof(TTarget);
        if (targetType.IsValueType)
        {
            return default!;
        }

        var constructors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(static constructor => constructor.GetParameters().Length);
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var arguments = new object?[parameters.Length];
            var canUseConstructor = true;
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (TryGetIgnore(parameter.Name!, out var ignore))
                {
                    if (ignore.Condition is not null && ignore.Condition(Source))
                    {
                        canUseConstructor = false;
                        break;
                    }

                    arguments[index] = GetDefault(parameter.ParameterType);
                    continue;
                }

                if (TryGetMap(parameter.Name!, out var map))
                {
                    arguments[index] = ShouldRun(map) ? ConvertValueOrFallback(map, parameter.ParameterType) : GetFallback(map, parameter.ParameterType);
                    continue;
                }

                arguments[index] = GetDefault(parameter.ParameterType);
            }

            if (canUseConstructor)
            {
                return (TTarget)constructor.Invoke(arguments);
            }
        }

        throw new InvalidOperationException("Target type '" + targetType.FullName + "' does not have a public constructor.");
    }

    private void ApplyAssignments(TTarget target)
    {
        foreach (var map in _maps)
        {
            if (HasActiveIgnore(map.TargetMember.Name) ||
                !CanAssign(map.TargetMember))
            {
                continue;
            }

            var targetType = GetMemberType(map.TargetMember);
            if (!ShouldRun(map))
            {
                ApplyAssignmentFallback(target, map, targetType);
                continue;
            }

            var value = map.Source(Source);
            if (ShouldIgnoreNullSource(map, value))
            {
                ApplyAssignmentFallback(target, map, targetType);
                continue;
            }

            SetValue(target!, map.TargetMember, ConvertValue(value, targetType));
        }
    }

    private bool ShouldRun(MappingRuleDeclaration<TSource> map)
    {
        return map.Condition is null || map.Condition(Source);
    }

    private object? ConvertValueOrFallback(MappingRuleDeclaration<TSource> map, Type targetType)
    {
        var value = map.Source(Source);
        return ShouldIgnoreNullSource(map, value)
            ? GetFallback(map, targetType)
            : ConvertValue(value, targetType);
    }

    private static bool ShouldIgnoreNullSource(MappingRuleDeclaration<TSource> map, object? value)
    {
        return value is null && map.NullHandling == ValiantNullHandling.IgnoreSourceNulls;
    }

    private object? GetFallback(MappingRuleDeclaration<TSource> map, Type targetType)
    {
        return map.Fallback is null ? GetDefault(targetType) : ConvertValue(map.Fallback(Source), targetType);
    }

    private void ApplyAssignmentFallback(
        TTarget target,
        MappingRuleDeclaration<TSource> map,
        Type targetType)
    {
        if (map.Fallback is not null)
        {
            SetValue(target!, map.TargetMember, ConvertValue(map.Fallback(Source), targetType));
        }
    }

    private bool HasActiveIgnore(string targetName)
    {
        foreach (var ignore in _ignores)
        {
            if (string.Equals(ignore.TargetMember.Name, targetName, StringComparison.OrdinalIgnoreCase) &&
                (ignore.Condition is null || ignore.Condition(Source)))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetIgnore(string targetName, out MappingIgnoreDeclaration<TSource> ignore)
    {
        foreach (var candidate in _ignores)
        {
            if (string.Equals(candidate.TargetMember.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                ignore = candidate;
                return true;
            }
        }

        ignore = null!;
        return false;
    }

    private bool TryGetMap(string targetName, out MappingRuleDeclaration<TSource> map)
    {
        foreach (var candidate in _maps)
        {
            if (string.Equals(candidate.TargetMember.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                map = candidate;
                return true;
            }
        }

        map = null!;
        return false;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reflection mapper invocation is used only by the debuggable runtime mapping backend.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reflection mapper invocation is used only by the debuggable runtime mapping backend.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection collection mapping is used only by the debuggable runtime mapping backend.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection collection mapping is used only by the debuggable runtime mapping backend.")]
    private object? ConvertValue(object? value, Type targetType)
    {
        if (value is null || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var valueType = value.GetType();
        if (valueType.IsEnum && targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        foreach (var mapper in _mappers)
        {
            var mapperInterface = mapper.GetType()
                .GetInterfaces()
                .FirstOrDefault(type => type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(IMapper<,>) &&
                    type.GetGenericArguments()[0].IsInstanceOfType(value) &&
                    type.GetGenericArguments()[1] == targetType);
            if (mapperInterface is null)
            {
                continue;
            }

            return mapperInterface.GetMethod(nameof(IMapper<object, object>.Map))!.Invoke(mapper, [value]);
        }

        if (value is IEnumerable enumerable &&
            value is not string &&
            TryGetEnumerableElementType(valueType, out var sourceElementType) &&
            TryGetEnumerableElementType(targetType, out var targetElementType))
        {
            var listType = typeof(List<>).MakeGenericType(targetElementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in enumerable)
            {
                list.Add(ConvertValue(item, targetElementType));
            }

            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(targetElementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        return value;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection collection mapping is used only by the debuggable runtime mapping backend.")]
    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        var enumerableType = type.GetInterfaces()
            .FirstOrDefault(static candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableType is not null)
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private static MemberInfo GetSelectedMember(LambdaExpression expression)
    {
        return expression.Body is MemberExpression memberExpression
            ? memberExpression.Member
            : throw new ArgumentException("Selector must be a member expression.", nameof(expression));
    }

    private static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new InvalidOperationException("Unsupported target member '" + member.Name + "'.")
        };
    }

    private static bool CanAssign(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => property.SetMethod is { IsPublic: true } &&
                !property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(static modifier => modifier.Name == "IsExternalInit"),
            FieldInfo field => !field.IsInitOnly,
            _ => false
        };
    }

    private static void SetValue(object target, MemberInfo member, object? value)
    {
        switch (member)
        {
            case PropertyInfo property:
                property.SetValue(target, value);
                break;
            case FieldInfo field:
                field.SetValue(target, value);
                break;
        }
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? MappingDefaultValueCache.GetDefault(type) : null;
    }
}

/// <summary>
/// Fluent mapping rule builder.
/// </summary>
public readonly struct MappingRule<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TSourceMember, TTargetMember>
{
    private readonly MappingContext<TSource, TTarget> _context;
    private readonly MappingRuleDeclaration<TSource> _declaration;

    internal MappingRule(MappingContext<TSource, TTarget> context, MappingRuleDeclaration<TSource> declaration)
    {
        _context = context;
        _declaration = declaration;
    }

    /// <summary>
    /// Runs every mapping declaration in the current fluent expression only when <paramref name="predicate" /> returns true for the source instance.
    /// </summary>
    /// <param name="predicate">The source predicate.</param>
    /// <returns>The current mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> When(Expression<Func<TSource, bool>> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _declaration.Chain.AddCondition(predicate.Compile());
        return this;
    }

    /// <summary>
    /// Uses <paramref name="value" /> when this mapping is skipped or ignores a null source value.
    /// </summary>
    /// <param name="value">The fallback target value.</param>
    /// <returns>The current mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> Default(TTargetMember value)
    {
        _declaration.Fallback = _ => value;
        return this;
    }

    /// <summary>
    /// Uses <paramref name="value" /> when this mapping is skipped or ignores a null source value.
    /// </summary>
    /// <param name="value">The fallback target value.</param>
    /// <returns>The current mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> Fallback(TTargetMember value)
    {
        return Default(value);
    }

    /// <summary>
    /// Leaves the target value unchanged when this mapping source expression returns null.
    /// </summary>
    /// <returns>The current mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> IgnoreNull()
    {
        _declaration.NullHandling = ValiantNullHandling.IgnoreSourceNulls;
        return this;
    }

    /// <summary>
    /// Maps a null source value even when the mapper options ignore source nulls by default.
    /// </summary>
    /// <returns>The current mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> MapNull()
    {
        _declaration.NullHandling = ValiantNullHandling.MapNulls;
        return this;
    }

    /// <summary>
    /// Maps a null source value even when the mapper options ignore source nulls by default.
    /// </summary>
    /// <returns>The current mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TTargetMember> AllowNull()
    {
        return MapNull();
    }

    /// <summary>
    /// Declares another explicit source-to-target member mapping.
    /// </summary>
    /// <param name="source">The source member selector.</param>
    /// <param name="target">The target member selector.</param>
    /// <returns>A mapping rule builder for the declared mapping.</returns>
    public MappingRule<TSource, TTarget, TNextSourceMember, TNextTargetMember> Map<TNextSourceMember, TNextTargetMember>(
        Expression<Func<TSource, TNextSourceMember>> source,
        Expression<Func<TTarget, TNextTargetMember>> target)
    {
        return _context.Map(source, target, _declaration.Chain);
    }

    /// <summary>
    /// Declares that a target member is intentionally not mapped.
    /// </summary>
    /// <param name="target">The target member selector.</param>
    /// <returns>A mapping ignore rule builder.</returns>
    public MappingIgnoreRule<TSource, TTarget, TNextTargetMember> Ignore<TNextTargetMember>(
        Expression<Func<TTarget, TNextTargetMember>> target)
    {
        return _context.Ignore(target, _declaration.Chain);
    }

    /// <summary>
    /// Declares a mapper dependency that can be reused by member mappings.
    /// </summary>
    /// <param name="mapper">The mapper dependency.</param>
    /// <typeparam name="TMapper">The mapper dependency type.</typeparam>
    /// <returns>The current mapping context.</returns>
    public MappingContext<TSource, TTarget> UseMapper<TMapper>(TMapper mapper)
    {
        return _context.UseMapper(mapper);
    }
}

/// <summary>
/// Fluent mapping ignore rule builder.
/// </summary>
public readonly struct MappingIgnoreRule<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TTargetMember>
{
    private readonly MappingContext<TSource, TTarget> _context;
    private readonly MappingIgnoreDeclaration<TSource> _declaration;

    internal MappingIgnoreRule(MappingContext<TSource, TTarget> context, MappingIgnoreDeclaration<TSource> declaration)
    {
        _context = context;
        _declaration = declaration;
    }

    /// <summary>
    /// Applies every mapping declaration in the current fluent expression only when <paramref name="predicate" /> returns true for the source instance.
    /// </summary>
    /// <param name="predicate">The source predicate.</param>
    /// <returns>The current mapping ignore rule builder.</returns>
    public MappingIgnoreRule<TSource, TTarget, TTargetMember> When(Expression<Func<TSource, bool>> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _declaration.Chain.AddCondition(predicate.Compile());
        return this;
    }

    /// <summary>
    /// Declares an explicit source-to-target member mapping.
    /// </summary>
    /// <param name="source">The source member selector.</param>
    /// <param name="target">The target member selector.</param>
    /// <returns>A mapping rule builder.</returns>
    public MappingRule<TSource, TTarget, TSourceMember, TNextTargetMember> Map<TSourceMember, TNextTargetMember>(
        Expression<Func<TSource, TSourceMember>> source,
        Expression<Func<TTarget, TNextTargetMember>> target)
    {
        return _context.Map(source, target, _declaration.Chain);
    }

    /// <summary>
    /// Declares another target member as intentionally not mapped.
    /// </summary>
    /// <param name="target">The target member selector.</param>
    /// <returns>A mapping ignore rule builder.</returns>
    public MappingIgnoreRule<TSource, TTarget, TNextTargetMember> Ignore<TNextTargetMember>(
        Expression<Func<TTarget, TNextTargetMember>> target)
    {
        return _context.Ignore(target, _declaration.Chain);
    }

    /// <summary>
    /// Declares a mapper dependency that can be reused by member mappings.
    /// </summary>
    /// <param name="mapper">The mapper dependency.</param>
    /// <typeparam name="TMapper">The mapper dependency type.</typeparam>
    /// <returns>The current mapping context.</returns>
    public MappingContext<TSource, TTarget> UseMapper<TMapper>(TMapper mapper)
    {
        return _context.UseMapper(mapper);
    }
}

internal sealed class MappingRuleDeclaration<TSource>
{
    public MappingRuleDeclaration(
        Func<TSource, object?> source,
        MemberInfo targetMember,
        Type sourceType,
        Type targetType,
        MappingChainDeclaration<TSource> chain,
        ValiantNullHandling nullHandling)
    {
        Source = source;
        TargetMember = targetMember;
        SourceType = sourceType;
        TargetType = targetType;
        Chain = chain;
        NullHandling = nullHandling;
    }

    public Func<TSource, object?> Source { get; }

    public MemberInfo TargetMember { get; }

    public Type SourceType { get; }

    public Type TargetType { get; }

    public MappingChainDeclaration<TSource> Chain { get; }

    public Func<TSource, bool>? Condition => Chain.Condition;

    public Func<TSource, object?>? Fallback { get; set; }

    public ValiantNullHandling NullHandling { get; set; }
}

internal sealed class MappingIgnoreDeclaration<TSource>
{
    public MappingIgnoreDeclaration(MemberInfo targetMember, MappingChainDeclaration<TSource> chain)
    {
        TargetMember = targetMember;
        Chain = chain;
    }

    public MemberInfo TargetMember { get; }

    public MappingChainDeclaration<TSource> Chain { get; }

    public Func<TSource, bool>? Condition => Chain.Condition;
}

internal sealed class MappingChainDeclaration<TSource>
{
    public Func<TSource, bool>? Condition { get; private set; }

    public void AddCondition(Func<TSource, bool> condition)
    {
        var previous = Condition;
        Condition = previous is null
            ? condition
            : source => previous(source) && condition(source);
    }
}

internal static class MappingDefaultValueCache
{
    private static readonly Dictionary<Type, Func<object?>> Factories = [];

    public static object? GetDefault(Type type)
    {
        lock (Factories)
        {
            if (!Factories.TryGetValue(type, out var factory))
            {
                factory = Expression.Lambda<Func<object?>>(
                    Expression.Convert(Expression.Default(type), typeof(object))).Compile();
                Factories.Add(type, factory);
            }

            return factory();
        }
    }
}
