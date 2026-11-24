using StaticNorth.Valiant.Mapping;
using StaticNorth.Valiant.Settings;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.NetStandard.PackageConsumer
{
    [ValiantSettings("Consumer")]
    public sealed class ConsumerSettings
    {
        public int Count { get; set; }
    }

    public sealed class ConsumerInput
    {
        public int Count { get; set; }
    }

    [ValiantMapper]
    public sealed partial class ConsumerMapper : MapperDefinition<ConsumerInput, ConsumerSettings>
    {
    }

    [ValiantValidator]
    public sealed partial class ConsumerValidator : ValidatorDefinition<ConsumerSettings>
    {
        public override void Configure(ValidationContext<ConsumerSettings> context)
        {
            context.Property(settings => settings.Count).GreaterThan(0);
        }
    }
}
