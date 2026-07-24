---
title: Validation check catalog
description: Every built-in Valiant validation check, grouped by supported value shape.
---

# Validation check catalog

All checks accept optional `code`, `message`, and `severity`. Nullable value checks generally skip `null`; combine them with `Required()` or `NotNull()` when null is invalid.

## General values

| Check | Purpose |
| --- | --- |
| `NotNull()` / `Null()` | Require presence or absence |
| `Required()` | Require a non-null value and, for strings/collections, non-empty content |
| `NotEmpty()` / `Empty()` | Require content or emptiness |
| `Equal(value)` / `NotEqual(value)` | Compare with an expected value |
| `Satisfies(predicate)` | Apply a custom sync or async predicate |
| `UseValidator(validator)` | Delegate to a reusable property validator |

## Comparable values

`GreaterThan`, `GreaterThanOrEqualTo`, `LessThan`, `LessThanOrEqualTo`, `InclusiveBetween`, and `ExclusiveBetween` support compatible comparable values and nullable counterparts.

```csharp
context.Property(product => product.Price)
    .GreaterThan(0m)
    .LessThanOrEqualTo(10_000m);
```

## Strings and length-bearing values

| Check | Purpose |
| --- | --- |
| `LengthBetween(min, max)` | Inclusive length range |
| `MinLength(min)` | Minimum length |
| `MaxLength(max)` | Maximum length |

## Collections

| Check | Purpose |
| --- | --- |
| `Count(count)` | Exact item count |
| `CountBetween(min, max)` | Inclusive item-count range |
| `MinCount(min)` / `MaxCount(max)` | Count bounds |
| `Contains(item)` / `Excludes(item)` | Membership |
| `All(predicate)` / `Any(predicate)` / `None(predicate)` | Element predicates |
| `ForEach(configure)` | Validate every item with a nested context |

## Dictionaries

`HasKey(key)` and `HasValue(value)` validate dictionary membership.

## Enums

| Check | Input |
| --- | --- |
| `DefinedEnum()` | Enum or nullable enum value |
| `EnumName(enumType)` | String name |
| `EnumValue(enumType)` | Supported integral value or nullable counterpart |

## Attribute equivalents

Attribute-authored validators support the corresponding Valiant attributes for declarative checks, including required/nullability, equality, enum, length/count, collection membership, dictionary, and comparison rules. Rules that require executable delegates—custom predicates, native control flow, and inline `ForEach` contexts—belong in a fluent `ValidatorDefinition<T>`.
