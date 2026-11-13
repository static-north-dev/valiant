namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeCancelBetweenRulesValidator : ValidatorDefinition<EdgeCancelInput>
{
    public override void Configure(ValidationContext<EdgeCancelInput> context)
    {
        context.Property(x => x.Value).SatisfiesAsync(Cancel, code: "cancel");
        context.Property(x => x.Value).Required();
    }

    private static ValueTask<bool> Cancel(EdgeCancelInput model, string value, CancellationToken token)
    {
        model.Cancellation.Cancel();
        return ValueTask.FromResult(true);
    }
}
