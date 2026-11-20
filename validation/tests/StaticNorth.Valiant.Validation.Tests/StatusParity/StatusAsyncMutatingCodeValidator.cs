namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusAsyncMutatingCodeValidator : ValidatorDefinition<StatusParityModel>
{
    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.Property(x => x.Missing).Required(code: context.Instance.EnableRule(), severity: ValiantSeverity.Warning);
        context.Property(x => x.Present).When(static x => x.Enabled).SatisfiesAsync(CheckTailAsync);
    }

    private static async ValueTask<bool> CheckTailAsync(StatusParityModel model, string value, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return model.RunTail(value.Length == 0);
    }
}
