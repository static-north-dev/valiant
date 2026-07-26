using Samples = StaticNorth.Valiant.Mapping.Samples;

Action sample;

sample = Samples.Basic.Sample.Run;
//sample = Samples.ExplicitMapping.Sample.Run;
//sample = Samples.ConditionalMapping.Sample.Run;
//sample = Samples.NullHandling.Sample.Run;
//sample = Samples.FlatteningAndEnums.Sample.Run;
//sample = Samples.Composition.Sample.Run;
//sample = Samples.DependencyInjection.Sample.Run;

sample();
