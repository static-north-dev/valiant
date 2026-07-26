using Samples = StaticNorth.Valiant.Validation.Samples;

Func<Task> sample;

sample = Samples.Basic.Sample.RunAsync;
//sample = Samples.AttributeValidation.Sample.RunAsync;
//sample = Samples.DependencyInjection.Sample.RunAsync;
//sample = Samples.SyncAndAsyncValidation.Sample.RunAsync;
//sample = Samples.Severity.Sample.RunAsync;
//sample = Samples.CompositionAndPaths.Sample.RunAsync;
//sample = Samples.FailureHandling.Sample.RunAsync;
//sample = Samples.ControlFlowAndCollections.Sample.RunAsync;

await sample();
