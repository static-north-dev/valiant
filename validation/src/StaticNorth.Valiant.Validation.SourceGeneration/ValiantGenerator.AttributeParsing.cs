using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantGenerator
{
    private const string AttributeSuffix = "Attribute";
    private const string ValiantValidationNamespace = "Valiant.Validation";
    private const string ValiantSeverityMetadataName = "Valiant.Validation.ValiantSeverity";
    private const string RuleInstanceExpression = "instance";
    private const string AttributeCodePropertyName = "Code";
    private const string AttributeMessagePropertyName = "Message";
    private const string AttributeSeverityPropertyName = "Severity";
    private const string AttributeConditionMethodPropertyName = "ConditionMethod";
    private const string AttributeConditionTypePropertyName = "ConditionType";
    private const string SystemCollectionsICollectionMetadataName = "System.Collections.ICollection";
    private const string SystemCollectionsIEnumerableMetadataName = "System.Collections.IEnumerable";
    private const string SystemCollectionsIDictionaryMetadataName = "System.Collections.IDictionary";
    private const string ValiantPropertyValidatorDefinitionMetadataName = "Valiant.Validation.PropertyValidatorDefinition`1";
    private const string ExpectedReferenceOrNullableValueType = "a reference type or nullable value type";
    private const string ExpectedEnumOrNullableEnum = "an enum or nullable enum";
    private const string ExpectedString = "string";
    private const string ExpectedIntegerType = "an integer type";
    private const string ExpectedStringArrayOrCollection = "string, array, or collection";
    private const string ExpectedCollection = "a collection";
    private const string ExpectedDictionary = "a dictionary";
    private const string ExpectedCompatibleParameterlessPropertyValidator = "a property validator type that inherits PropertyValidatorDefinition<TProperty> and has a public parameterless constructor";

    private static void AddAttributeValidationNodes(
        ITypeSymbol modelType,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var member in EnumerateAttributeValidationMembers(modelType))
        {
            AddAttributeValidationNodes(modelType, member, nodes, diagnostics);
        }
    }

    private static bool HasSupportedValidationAttributes(ITypeSymbol modelType)
    {
        foreach (var member in EnumerateAttributeValidationMembers(modelType))
        {
            foreach (var attribute in member.GetAttributes())
            {
                if (IsSupportedValidationAttribute(attribute))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<ISymbol> EnumerateAttributeValidationMembers(ITypeSymbol modelType)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = modelType; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                switch (member)
                {
                    case IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property:
                        if (seen.Add(property.Name))
                        {
                            yield return property;
                        }

                        break;
                    case IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } field:
                        if (seen.Add(field.Name))
                        {
                            yield return field;
                        }

                        break;
                }
            }
        }
    }

    private static void AddAttributeValidationNodes(
        ITypeSymbol modelType,
        ISymbol member,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var memberType = GetAttributeValidationMemberType(member);
        if (memberType is null)
        {
            return;
        }

        foreach (var attribute in member.GetAttributes())
        {
            if (TryCreateAttributeRule(modelType, member, memberType, attribute, out var rule, out var diagnostic))
            {
                nodes.Add(new RuleNode(rule));
            }
            else if (diagnostic is not null)
            {
                diagnostics.Add(diagnostic);
            }
        }
    }

    private static bool TryCreateAttributeRule(
        ITypeSymbol modelType,
        ISymbol member,
        ITypeSymbol memberType,
        AttributeData attribute,
        out RuleModel rule,
        out DiagnosticInfo? diagnostic)
    {
        rule = null!;
        if (!TryCreateAttributeRuleCheck(attribute, modelType, memberType, member, out var check, out var conditionExpression, out var conditionDelegateType, out diagnostic))
        {
            return false;
        }

        var propertyType = memberType.ToDisplayString(TypeDisplayFormat);
        rule = new RuleModel(
            member.Name,
            ToLiteral(member.Name),
            member.Name,
            ImmutableArray<string>.Empty,
            RuleInstanceExpression,
            propertyType,
            memberType.ToDisplayString(MetadataTypeDisplayFormat),
            memberType.SpecialType == SpecialType.System_String,
            IsIntegerType(memberType),
            IsNullableValueType(memberType),
            null,
            0,
            conditionExpression,
            conditionDelegateType,
            member.Locations.FirstOrDefault() ?? Location.None,
            ImmutableArray.Create(check));
        return true;
    }

    private static ITypeSymbol? GetAttributeValidationMemberType(ISymbol member)
    {
        return member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };
    }

    private static bool TryCreateAttributeRuleCheck(
        AttributeData attribute,
        ITypeSymbol modelType,
        ITypeSymbol memberType,
        ISymbol member,
        out RuleCheck check,
        out string? conditionExpression,
        out string? conditionDelegateType,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        conditionExpression = null;
        conditionDelegateType = null;
        diagnostic = null;
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is null ||
            attributeClass.ContainingNamespace.ToDisplayString() != ValiantValidationNamespace)
        {
            return false;
        }

        var kind = GetValidationAttributeKind(attributeClass);
        if (!TryGetAttributeRuleArgumentCount(kind, out var ruleArgumentCount) ||
            attribute.ConstructorArguments.Length < ruleArgumentCount)
        {
            if (kind != AttributeKinds.UseValidator)
            {
                return false;
            }
        }

        if (!IsAttributeCompatibleWithMemberType(kind, memberType, out var expectedType))
        {
            diagnostic = CreateValidationAttributeTypeMismatchDiagnostic(kind, member, memberType, attribute, expectedType);
            return false;
        }

        if (kind == AttributeKinds.UseValidator)
        {
            if (!TryCreateAttributeValidatorReuseCheck(attribute, member, memberType, out check, out diagnostic))
            {
                return false;
            }

            AddAttributeCondition(attribute, modelType, out conditionExpression, out conditionDelegateType);
            return true;
        }

        var arguments = ImmutableArray.CreateBuilder<string>(ruleArgumentCount);
        for (var index = 0; index < ruleArgumentCount; index++)
        {
            if (!TryGetAttributeArgumentExpression(attribute.ConstructorArguments[index], out var argumentExpression))
            {
                return false;
            }

            arguments.Add(argumentExpression);
        }

        var code = GetAttributeStringMetadata(attribute, AttributeCodePropertyName, ruleArgumentCount);
        var message = GetAttributeStringMetadata(attribute, AttributeMessagePropertyName, ruleArgumentCount + (code is null ? 0 : 1));
        var severity = GetAttributeSeverityExpression(attribute, ruleArgumentCount + (code is null ? 0 : 1) + (message is null ? 0 : 1));
        AddAttributeCondition(attribute, modelType, out conditionExpression, out conditionDelegateType);

        check = CreateAttributeRuleCheck(kind, arguments.ToImmutable(), code, message, severity, member);
        return true;
    }

    private static bool TryCreateAttributeValidatorReuseCheck(
        AttributeData attribute,
        ISymbol member,
        ITypeSymbol memberType,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;
        if (attribute.AttributeClass is not { TypeArguments.Length: 1 } attributeClass ||
            attributeClass.TypeArguments[0] is not INamedTypeSymbol validatorType ||
            !IsParameterlessValidatorCompatibleWithMemberType(validatorType, memberType))
        {
            diagnostic = CreateValidationAttributeTypeMismatchDiagnostic(
                AttributeKinds.UseValidator,
                member,
                memberType,
                attribute,
                ExpectedCompatibleParameterlessPropertyValidator);
            return false;
        }

        var propertyType = memberType.ToDisplayString(MetadataTypeDisplayFormat);
        var validatorExpression = "((global::Valiant.Validation.IPropertyValidator<" +
            propertyType +
            ">)(new " +
            validatorType.ToDisplayString(TypeDisplayFormat) +
            "()))";

        check = new RuleCheck(
            AttributeKinds.UseValidator,
            ImmutableArray.Create(validatorExpression),
            ImmutableArray<string>.Empty,
            false,
            null,
            0,
            null,
            null,
            null,
            GetAttributeDiagnosticLocation(attribute, member));
        return true;
    }

    private static void AddAttributeCondition(
        AttributeData attribute,
        ITypeSymbol modelType,
        out string? conditionExpression,
        out string? conditionDelegateType)
    {
        var conditionMethod = GetAttributeStringMetadata(attribute, AttributeConditionMethodPropertyName, -1);
        if (conditionMethod is null)
        {
            conditionExpression = null;
            conditionDelegateType = null;
            return;
        }

        conditionExpression = GetAttributeConditionExpression(attribute, modelType, conditionMethod);
        conditionDelegateType = "global::System.Func<" + modelType.ToDisplayString(TypeDisplayFormat) + ", bool>";
    }

    private static DiagnosticInfo CreateValidationAttributeTypeMismatchDiagnostic(
        string kind,
        ISymbol member,
        ITypeSymbol memberType,
        AttributeData attribute,
        string expectedType)
    {
        return new DiagnosticInfo(
            ValidationAttributeTypeMismatch,
            GetAttributeDiagnosticLocation(attribute, member),
            kind,
            member.Name,
            memberType.ToDisplayString(TypeDisplayFormat),
            expectedType);
    }

    private static Location GetAttributeDiagnosticLocation(AttributeData attribute, ISymbol member)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
            member.Locations.FirstOrDefault() ??
            Location.None;
    }

    private static bool IsAttributeCompatibleWithMemberType(string kind, ITypeSymbol memberType, out string expectedType)
    {
        switch (kind)
        {
            case AttributeKinds.Required when memberType.IsValueType && !IsNullableValueType(memberType):
                expectedType = ExpectedReferenceOrNullableValueType;
                return false;
            case AttributeKinds.UseValidator:
                expectedType = string.Empty;
                return true;
            case AttributeKinds.DefinedEnum when !IsEnumCompatibleType(memberType):
                expectedType = ExpectedEnumOrNullableEnum;
                return false;
            case AttributeKinds.EnumName when memberType.SpecialType != SpecialType.System_String:
                expectedType = ExpectedString;
                return false;
            case AttributeKinds.EnumValue when !IsIntegerType(memberType):
                expectedType = ExpectedIntegerType;
                return false;
            case AttributeKinds.MinLength or AttributeKinds.MaxLength or AttributeKinds.LengthBetween when !IsLengthCompatibleType(memberType):
                expectedType = ExpectedStringArrayOrCollection;
                return false;
            case AttributeKinds.Count or AttributeKinds.CountBetween or AttributeKinds.MinCount or AttributeKinds.MaxCount when !IsCountCompatibleType(memberType):
                expectedType = ExpectedStringArrayOrCollection;
                return false;
            case AttributeKinds.Contains or AttributeKinds.Excludes when !IsCollectionCompatibleType(memberType):
                expectedType = ExpectedCollection;
                return false;
            case AttributeKinds.HasKey or AttributeKinds.HasValue when !IsDictionaryCompatibleType(memberType):
                expectedType = ExpectedDictionary;
                return false;
            case AttributeKinds.GreaterThan or AttributeKinds.GreaterThanOrEqualTo or AttributeKinds.LessThan or AttributeKinds.LessThanOrEqualTo or AttributeKinds.InclusiveBetween or AttributeKinds.ExclusiveBetween when !IsIntegerType(memberType):
                expectedType = ExpectedIntegerType;
                return false;
            case AttributeKinds.Equal or AttributeKinds.NotEqual when memberType.SpecialType != SpecialType.System_String:
                expectedType = ExpectedString;
                return false;
            default:
                expectedType = string.Empty;
                return true;
        }
    }

    private static bool IsSupportedValidationAttribute(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        return attributeClass is not null &&
            attributeClass.ContainingNamespace.ToDisplayString() == ValiantValidationNamespace &&
            (GetValidationAttributeKind(attributeClass) == AttributeKinds.UseValidator ||
                TryGetAttributeRuleArgumentCount(GetValidationAttributeKind(attributeClass), out _));
    }

    private static RuleCheck CreateAttributeRuleCheck(
        string kind,
        ImmutableArray<string> arguments,
        string? code,
        string? message,
        string? severity,
        ISymbol member)
    {
        return new RuleCheck(
            kind,
            arguments,
            ImmutableArray<string>.Empty,
            false,
            null,
            0,
            code,
            message,
            severity,
            member.Locations.FirstOrDefault() ?? Location.None);
    }

    private static bool TryGetAttributeRuleArgumentCount(string kind, out int count)
    {
        switch (kind)
        {
            case AttributeKinds.NotNull:
            case AttributeKinds.Null:
            case AttributeKinds.Required:
            case AttributeKinds.NotEmpty:
            case AttributeKinds.Empty:
            case AttributeKinds.DefinedEnum:
                count = 0;
                return true;
            case AttributeKinds.EnumName:
            case AttributeKinds.EnumValue:
            case AttributeKinds.MinLength:
            case AttributeKinds.MaxLength:
            case AttributeKinds.MinCount:
            case AttributeKinds.MaxCount:
            case AttributeKinds.Count:
            case AttributeKinds.GreaterThan:
            case AttributeKinds.GreaterThanOrEqualTo:
            case AttributeKinds.LessThan:
            case AttributeKinds.LessThanOrEqualTo:
            case AttributeKinds.Equal:
            case AttributeKinds.NotEqual:
            case AttributeKinds.Contains:
            case AttributeKinds.Excludes:
            case AttributeKinds.HasKey:
            case AttributeKinds.HasValue:
                count = 1;
                return true;
            case AttributeKinds.LengthBetween:
            case AttributeKinds.InclusiveBetween:
            case AttributeKinds.ExclusiveBetween:
            case AttributeKinds.CountBetween:
                count = 2;
                return true;
            default:
                count = -1;
                return false;
        }
    }

    private static bool TryGetAttributeArgumentExpression(TypedConstant constant, out string expression)
    {
        if (constant.Value is ITypeSymbol type)
        {
            expression = "typeof(" + type.ToDisplayString(TypeDisplayFormat) + ")";
            return true;
        }

        switch (constant.Value)
        {
            case string text:
                expression = ToLiteral(text);
                return true;
            case char value:
                expression = "'" + value.ToString().Replace("\\", "\\\\").Replace("'", "\\'") + "'";
                return true;
            case bool value:
                expression = value ? "true" : "false";
                return true;
            case byte value:
                expression = value.ToString(CultureInfo.InvariantCulture);
                return true;
            case sbyte value:
                expression = value.ToString(CultureInfo.InvariantCulture);
                return true;
            case short value:
                expression = value.ToString(CultureInfo.InvariantCulture);
                return true;
            case ushort value:
                expression = value.ToString(CultureInfo.InvariantCulture);
                return true;
            case int value:
                expression = value.ToString(CultureInfo.InvariantCulture);
                return true;
            case uint value:
                expression = value.ToString(CultureInfo.InvariantCulture) + "u";
                return true;
            case long value:
                expression = value.ToString(CultureInfo.InvariantCulture) + "L";
                return true;
            case ulong value:
                expression = value.ToString(CultureInfo.InvariantCulture) + "UL";
                return true;
            case float value:
                expression = value.ToString("R", CultureInfo.InvariantCulture) + "f";
                return true;
            case double value:
                expression = value.ToString("R", CultureInfo.InvariantCulture) + "d";
                return true;
            default:
                expression = string.Empty;
                return false;
        }
    }

    private static string? GetAttributeStringMetadata(AttributeData attribute, string propertyName, int constructorArgumentIndex)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument is { Key: var key, Value.Value: string value } &&
                key == propertyName &&
                !string.IsNullOrEmpty(value))
            {
                return ToLiteral(value);
            }
        }

        if (constructorArgumentIndex >= 0 &&
            attribute.ConstructorArguments.Length > constructorArgumentIndex &&
            attribute.ConstructorArguments[constructorArgumentIndex].Value is string constructorValue &&
            !string.IsNullOrEmpty(constructorValue))
        {
            return ToLiteral(constructorValue);
        }

        return null;
    }

    private static string? GetAttributeSeverityExpression(AttributeData attribute, int constructorArgumentIndex)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == AttributeSeverityPropertyName &&
                TryGetSeverityExpression(namedArgument.Value, out var severityExpression))
            {
                return severityExpression;
            }
        }

        if (constructorArgumentIndex >= 0 &&
            attribute.ConstructorArguments.Length > constructorArgumentIndex &&
            TryGetSeverityExpression(attribute.ConstructorArguments[constructorArgumentIndex], out var constructorSeverityExpression))
        {
            return constructorSeverityExpression;
        }

        return null;
    }

    private static bool TryGetSeverityExpression(TypedConstant constant, out string expression)
    {
        if (constant.Type?.ToDisplayString() == ValiantSeverityMetadataName &&
            constant.Value is int value &&
            value is >= 0 and <= 2)
        {
            switch (value)
            {
                case 0:
                    expression = "global::Valiant.Validation.ValiantSeverity.Error";
                    return true;
                case 1:
                    expression = "global::Valiant.Validation.ValiantSeverity.Warning";
                    return true;
                case 2:
                    expression = "global::Valiant.Validation.ValiantSeverity.Info";
                    return true;
            }
        }

        expression = string.Empty;
        return false;
    }

    private static string GetAttributeConditionExpression(AttributeData attribute, ITypeSymbol modelType, string conditionMethodExpression)
    {
        var conditionType = GetAttributeConditionType(attribute);
        var methodName = conditionMethodExpression.Substring(1, conditionMethodExpression.Length - 2);
        return conditionType is null
            ? modelType.ToDisplayString(TypeDisplayFormat) + "." + methodName
            : conditionType + "." + methodName;
    }

    private static string? GetAttributeConditionType(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == AttributeConditionTypePropertyName &&
                namedArgument.Value.Value is ITypeSymbol conditionType)
            {
                return conditionType.ToDisplayString(TypeDisplayFormat);
            }
        }

        return null;
    }

    private static bool IsLengthCompatibleType(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_String ||
            type.TypeKind == TypeKind.Array ||
            ImplementsInterface(type, SystemCollectionsICollectionMetadataName);
    }

    private static bool IsEnumCompatibleType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } namedType)
        {
            type = namedType.TypeArguments[0];
        }

        return type.TypeKind == TypeKind.Enum;
    }

    private static bool IsCountCompatibleType(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_String ||
            type.TypeKind == TypeKind.Array ||
            ImplementsInterface(type, SystemCollectionsICollectionMetadataName);
    }

    private static bool IsCollectionCompatibleType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Array ||
            type.SpecialType != SpecialType.System_String &&
            ImplementsInterface(type, SystemCollectionsIEnumerableMetadataName);
    }

    private static bool IsDictionaryCompatibleType(ITypeSymbol type)
    {
        return ImplementsInterface(type, SystemCollectionsIDictionaryMetadataName);
    }

    private static bool ImplementsInterface(ITypeSymbol type, string metadataName)
    {
        if (type is INamedTypeSymbol namedType &&
            GetInterfaceMetadataName(namedType) == metadataName)
        {
            return true;
        }

        foreach (var interfaceType in type.AllInterfaces)
        {
            if (GetInterfaceMetadataName(interfaceType) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetInterfaceMetadataName(INamedTypeSymbol interfaceType)
    {
        var namespaceName = interfaceType.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : interfaceType.ContainingNamespace.ToDisplayString();
        var typeName = interfaceType.OriginalDefinition.MetadataName;
        return string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;
    }

    private static string GetValidationAttributeKind(INamedTypeSymbol attributeClass)
    {
        var metadataName = attributeClass.MetadataName;
        var genericArityIndex = metadataName.IndexOf('`');
        if (genericArityIndex >= 0)
        {
            metadataName = metadataName.Substring(0, genericArityIndex);
        }

        return metadataName.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? metadataName.Substring(0, metadataName.Length - AttributeSuffix.Length)
            : metadataName;
    }

    private static bool IsParameterlessValidatorCompatibleWithMemberType(INamedTypeSymbol validatorType, ITypeSymbol memberType)
    {
        if (validatorType.IsAbstract ||
            !HasPublicParameterlessConstructor(validatorType))
        {
            return false;
        }

        return TryGetPropertyValidatorDefinitionArgument(validatorType, out var validatorArgumentType) &&
            IsValidatorArgumentCompatibleWithMemberType(validatorArgumentType, memberType);
    }

    private static bool TryGetPropertyValidatorDefinitionArgument(INamedTypeSymbol validatorType, out ITypeSymbol propertyType)
    {
        for (var baseType = validatorType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (GetInterfaceMetadataName(baseType) == ValiantPropertyValidatorDefinitionMetadataName &&
                baseType.TypeArguments.Length == 1)
            {
                propertyType = baseType.TypeArguments[0];
                return true;
            }
        }

        propertyType = null!;
        return false;
    }

    private static bool IsValidatorArgumentCompatibleWithMemberType(ITypeSymbol validatorArgumentType, ITypeSymbol memberType)
    {
        return string.Equals(
            validatorArgumentType.ToDisplayString(MetadataTypeDisplayFormat),
            memberType.ToDisplayString(MetadataTypeDisplayFormat),
            StringComparison.Ordinal);
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility == Accessibility.Public &&
                constructor.Parameters.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static class AttributeKinds
    {
        public const string NotNull = "NotNull";
        public const string Null = "Null";
        public const string Required = "Required";
        public const string NotEmpty = "NotEmpty";
        public const string Empty = "Empty";
        public const string DefinedEnum = "DefinedEnum";
        public const string UseValidator = "UseValidator";
        public const string EnumName = "EnumName";
        public const string EnumValue = "EnumValue";
        public const string LengthBetween = "LengthBetween";
        public const string MinLength = "MinLength";
        public const string MaxLength = "MaxLength";
        public const string Count = "Count";
        public const string CountBetween = "CountBetween";
        public const string MinCount = "MinCount";
        public const string MaxCount = "MaxCount";
        public const string Contains = "Contains";
        public const string Excludes = "Excludes";
        public const string HasKey = "HasKey";
        public const string HasValue = "HasValue";
        public const string GreaterThan = "GreaterThan";
        public const string GreaterThanOrEqualTo = "GreaterThanOrEqualTo";
        public const string LessThan = "LessThan";
        public const string LessThanOrEqualTo = "LessThanOrEqualTo";
        public const string Equal = "Equal";
        public const string NotEqual = "NotEqual";
        public const string InclusiveBetween = "InclusiveBetween";
        public const string ExclusiveBetween = "ExclusiveBetween";
    }
}
