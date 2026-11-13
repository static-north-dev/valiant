using System.Linq.Expressions;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Defines mapping declarations from <typeparamref name="TSource" /> to <typeparamref name="TTarget" />.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public sealed class MappingContext<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>
{
    [ThreadStatic]
    private static int _nestedMappingDepth;

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
        // Configure runs for every source and may capture values from that invocation.
        var compiledSource = source.Compile(preferInterpretation: true);
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
        return MapRuntimeCore(null, null, null);
    }

    /// <summary>
    /// Executes the runtime rules using the constructor selected by the source generator.
    /// </summary>
    /// <param name="constructorParameterTypes">The parameter types of the selected constructor.</param>
    /// <param name="resolveMapper">The optional resolver for nested mappers.</param>
    /// <returns>The mapped target instance.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TTarget MapRuntime(Type[] constructorParameterTypes, Func<Type, object?>? resolveMapper = null)
    {
        return MapRuntime(constructorParameterTypes, resolveMapper, null);
    }

    /// <summary>
    /// Executes runtime rules using the constructor and initializer order selected by the generator.
    /// </summary>
    /// <param name="constructorParameterTypes">The parameter types of the selected constructor.</param>
    /// <param name="resolveMapper">The optional resolver for nested mappers.</param>
    /// <param name="initializerMembers">The members assigned in the object initializer.</param>
    /// <returns>The mapped target instance.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TTarget MapRuntime(Type[] constructorParameterTypes, Func<Type, object?>? resolveMapper, string[]? initializerMembers)
    {
        _ = constructorParameterTypes ?? throw new ArgumentNullException(nameof(constructorParameterTypes));
        return MapRuntimeCore(constructorParameterTypes, initializerMembers, resolveMapper);
    }

    private TTarget MapRuntimeCore(Type[]? constructorParameterTypes, string[]? initializerMembers, Func<Type, object?>? resolveMapper)
    {
        if (!HasSource)
        {
            throw new InvalidOperationException("Runtime mapping requires a source instance.");
        }

        // Keep one boxed target so reflection writes to the same struct we return.
        object target = CreateTarget(constructorParameterTypes, resolveMapper, out var parameters)!;
        ApplyAssignments(target, parameters, initializerMembers, resolveMapper);
        return (TTarget)target;
    }

    private TTarget CreateTarget(Type[]? constructorParameterTypes, Func<Type, object?>? resolveMapper, out ParameterInfo[] boundParameters)
    {
        var targetType = typeof(TTarget);
        boundParameters = [];
        if (targetType.IsValueType)
        {
            return Activator.CreateInstance<TTarget>();
        }

        var constructors = constructorParameterTypes is null
            ? targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(static constructor => constructor.GetParameters().Length)
            : new[] { targetType.GetConstructor(constructorParameterTypes)! }.AsEnumerable();
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            if (constructorParameterTypes is null && parameters.Any(parameter => _ignores.Any(ignore =>
                ignore.Condition is not null && string.Equals(ignore.TargetMember.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                ignore.Condition(Source))))
            {
                continue;
            }

            var arguments = new object?[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                arguments[index] = GetDefault(parameter.ParameterType);
                if (HasActiveIgnore(parameter.Name!))
                {
                    continue;
                }

                foreach (var map in _maps)
                {
                    if (string.Equals(map.TargetMember.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                        TryGetMappedValue(map, parameter.ParameterType, resolveMapper, out var value))
                    {
                        arguments[index] = value;
                        break;
                    }
                }
            }

            boundParameters = parameters;
            return (TTarget)InvokeUnwrapped(constructor, null, arguments)!;
        }

        throw new InvalidOperationException("Target type '" + targetType.FullName + "' does not have a public constructor.");
    }

    private void ApplyAssignments(object target, ParameterInfo[] parameters, string[]? initializerMembers, Func<Type, object?>? resolveMapper)
    {
        IEnumerable<MappingRuleDeclaration<TSource>> maps = _maps;
        if (initializerMembers is { Length: > 0 })
        {
            maps = _maps.OrderBy(map =>
            {
                var index = Array.IndexOf(initializerMembers, map.TargetMember.Name);
                return index < 0 ? int.MaxValue : index;
            });
        }

        foreach (var map in maps)
        {
            if (HasActiveIgnore(map.TargetMember.Name) ||
                parameters.Any(parameter => string.Equals(parameter.Name, map.TargetMember.Name, StringComparison.OrdinalIgnoreCase)) ||
                !CanAssign(map.TargetMember))
            {
                continue;
            }

            var targetType = GetMemberType(map.TargetMember);
            if (TryGetMappedValue(map, targetType, resolveMapper, out var value))
            {
                SetValue(target, map.TargetMember, value);
            }
            else if (map.TargetMember is PropertyInfo { SetMethod: { } setter } && IsInitOnly(setter) &&
                (initializerMembers is null || Array.IndexOf(initializerMembers, map.TargetMember.Name) >= 0))
            {
                SetValue(target, map.TargetMember, GetDefault(targetType));
            }
        }
    }

    private bool TryGetMappedValue(MappingRuleDeclaration<TSource> map, Type targetType, Func<Type, object?>? resolveMapper, out object? result)
    {
        if (map.Condition is null || map.Condition(Source))
        {
            var value = map.Source(Source);
            if (value is not null || map.NullHandling != ValiantNullHandling.IgnoreSourceNulls)
            {
                result = ConvertValue(value, map.SourceType, targetType, resolveMapper);
                return true;
            }
        }

        result = map.Fallback?.Invoke(Source);
        return map.Fallback is not null;
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

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reflection mapper invocation is used only by the debuggable runtime mapping backend.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reflection mapper invocation is used only by the debuggable runtime mapping backend.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection collection mapping is used only by the debuggable runtime mapping backend.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection collection mapping is used only by the debuggable runtime mapping backend.")]
    private object? ConvertValue(object? value, Type sourceType, Type targetType, Func<Type, object?>? resolveMapper)
    {
        if (targetType.IsAssignableFrom(sourceType))
        {
            return value;
        }

        if (MappingValueConversion.TryConvert(value, sourceType, targetType, out var converted))
        {
            return converted;
        }

        var mapperInterface = typeof(IMapper<,>).MakeGenericType(sourceType, targetType);
        foreach (var mapper in _mappers)
        {
            if (mapper.GetType().GetInterfaces().Contains(mapperInterface))
            {
                return InvokeUnwrapped(mapperInterface.GetMethod(nameof(IMapper<object, object>.Map))!, mapper, [value]);
            }
        }

        if (value is null)
        {
            return null;
        }

        if ((Nullable.GetUnderlyingType(sourceType) ?? sourceType).IsEnum &&
            (Nullable.GetUnderlyingType(targetType) ?? targetType) is { IsEnum: true } targetEnum)
        {
            return Enum.ToObject(targetEnum, value);
        }

        if (value is IEnumerable enumerable &&
            value is not string &&
            TryGetEnumerableElementType(sourceType, out var sourceElementType) &&
            TryGetEnumerableElementType(targetType, out var targetElementType))
        {
            var listType = typeof(List<>).MakeGenericType(targetElementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in enumerable)
            {
                list.Add(ConvertValue(item, sourceElementType, targetElementType, resolveMapper));
            }

            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(targetElementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            if (targetType.IsAssignableFrom(listType))
            {
                return list;
            }

            var setType = typeof(HashSet<>).MakeGenericType(targetElementType);
            return targetType.IsAssignableFrom(setType)
                ? Activator.CreateInstance(setType, [list])
                : list;
        }

        var resolvedMapper = resolveMapper?.Invoke(mapperInterface);
        if (resolvedMapper is not null)
        {
            if (_nestedMappingDepth >= 64)
            {
                throw new InvalidOperationException($"Valiant mapper recursion depth exceeded 64 while mapping source type '{sourceType.FullName}' to target type '{targetType.FullName}'. Check for cyclic object graphs or configure this member explicitly.");
            }

            _nestedMappingDepth++;
            try
            {
                return InvokeUnwrapped(mapperInterface.GetMethod(nameof(IMapper<object, object>.Map))!, resolvedMapper, [value]);
            }
            finally
            {
                _nestedMappingDepth--;
            }
        }

        throw new InvalidOperationException($"No Valiant mapper is registered for source type '{sourceType.FullName}' and target type '{targetType.FullName}'. Register mappers with AddValiantMappers or configure this member with UseMapper.");
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
            PropertyInfo property => property.SetMethod is { IsPublic: true },
            FieldInfo field => field.IsPublic && !field.IsInitOnly,
            _ => false
        };
    }

    private static bool IsInitOnly(MethodInfo setter)
    {
        return setter.ReturnParameter.GetRequiredCustomModifiers().Any(static modifier => modifier.Name == "IsExternalInit");
    }

    private static void SetValue(object target, MemberInfo member, object? value)
    {
        switch (member)
        {
            case PropertyInfo property:
                InvokeUnwrapped(property.SetMethod!, target, [value]);
                break;
            case FieldInfo field:
                field.SetValue(target, value);
                break;
        }
    }

    private static object? InvokeUnwrapped(MethodBase method, object? target, object?[] arguments)
    {
        try
        {
            return method is ConstructorInfo constructor
                ? constructor.Invoke(arguments)
                : method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
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
        _declaration.Chain.AddCondition(predicate.Compile(preferInterpretation: true));
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
        _declaration.Chain.AddCondition(predicate.Compile(preferInterpretation: true));
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
