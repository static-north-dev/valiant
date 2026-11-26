namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator]
public sealed partial class SetCountValidator : ValidatorDefinition<CollectionModel>
{
    public override void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(model => model.UniqueValues).Count(2);
    }
}
