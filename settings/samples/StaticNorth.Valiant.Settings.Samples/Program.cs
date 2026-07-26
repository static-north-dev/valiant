using Samples = StaticNorth.Valiant.Settings.Samples;

Action sample;

// Basic: registers generated settings and consumes configured models through IOptions<T>.
//sample = Samples.Basic.Sample.Run;

// RegistrationOptions: compares normal schema-provider registration with binding-only registration.
//sample = Samples.RegistrationOptions.Sample.Run;

// SchemaRegistry: demonstrates All, Get<TSettings>, and successful/failed TryGet<TSettings> lookups.
//sample = Samples.SchemaRegistry.Sample.Run;

// SchemaMetadata: inspects generated property kinds, nesting, collections, nullability, and secrets.
sample = Samples.SchemaMetadata.Sample.Run;

sample();
