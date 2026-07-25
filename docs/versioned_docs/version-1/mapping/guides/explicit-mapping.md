---
title: Explicit mapping
description: Transform, condition, ignore, default, and control null behavior for mapped members.
---

# Explicit mapping

Override `Configure` when names differ or a target value needs business-specific shaping:

```csharp
[ValiantMapper]
public sealed partial class UserMapper : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context
            .Map(source => source.EmailAddress, target => target.Email)
            .Map(source => $"{source.FirstName} {source.LastName}", target => target.DisplayName)
            .Ignore(target => target.InternalNote);
    }
}
```

## Conditional chains

`When` applies to every declaration in the same fluent chain:

```csharp
context
    .Map(source => source.EmailAddress, target => target.Email)
    .Map(source => source.Phone, target => target.Phone)
    .When(source => source.IncludeContactDetails);
```

Start a new statement when rules need different conditions.

## Defaults and fallbacks

`Default` and `Fallback` are aliases. The value is used when a rule is skipped by its condition or ignores a null source value:

```csharp
context.Map(source => source.DisplayName, target => target.DisplayName)
    .When(source => source.IsVisible)
    .Fallback("Hidden user");
```

## Null handling

The mapper-wide default comes from `ValiantMappingOptions.NullHandling`:

```csharp
services.AddValiantMappers(options =>
    options.NullHandling = ValiantNullHandling.IgnoreSourceNulls);
```

Override it per rule:

```csharp
context.Map(source => source.Nickname, target => target.Nickname)
    .IgnoreNull();

context.Map(source => source.AvatarUrl, target => target.AvatarUrl)
    .MapNull(); // AllowNull() is an alias.
```

| API | Effect |
| --- | --- |
| `IgnoreNull()` | Leave the target value unchanged when the source expression returns `null` |
| `MapNull()` | Assign `null`, even when mapper options ignore source nulls |
| `AllowNull()` | Alias for `MapNull()` |
| `Default(value)` | Use a value when the rule is skipped or a null is ignored |
| `Fallback(value)` | Alias for `Default(value)` |

## Intentional omissions

Use `Ignore` to document that a writable or required target member is intentionally not populated:

```csharp
context.Ignore(target => target.AuditStamp);
```

This is preferable to suppressing an unmapped-member warning because the decision remains beside the rest of the mapping contract.

Run the conditional and null-policy variants from the
[Mapping sample catalog](../samples.md).
