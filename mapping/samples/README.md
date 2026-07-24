# Mapping samples

Select one sample in `Program.cs`, then run the project:

```bash
dotnet run --project mapping/samples/Valiant.Mapping.Samples
```

`Program.cs` is intentionally only a sample selector. The description on each
`Sample` class explains what that folder demonstrates:

- `Basic`: convention mapping into an immutable constructor target.
- `ExplicitMapping`: transforms, renamed members, and intentional ignores.
- `ConditionalMapping`: native `if/else`, chained `When`, `Fallback`, and `Default`.
- `NullHandling`: global null policy, per-rule overrides, and ignored-null fallback.
- `FlatteningAndEnums`: flattening, unflattening, and compatible enum conversion.
- `Composition`: nested and collection mapper reuse.
- `DependencyInjection`: typed, facade, factory, and successful/missing `Try` APIs.

Each folder is self-contained so its models, mapper, and invocation can be read
or copied without pulling in unrelated examples.
