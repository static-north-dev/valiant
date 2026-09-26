namespace StaticNorth.Valiant.Validation;

internal sealed class SingleValidationFailureList(ValidationFailure failure) : IReadOnlyList<ValidationFailure>
{
    public int Count => 1;

    public ValidationFailure this[int index] => index == 0
        ? failure
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<ValidationFailure> GetEnumerator()
    {
        yield return failure;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
