using System.Collections.Immutable;

namespace Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct QueryObjectBindingRegistrationModel(
    ImmutableArray<QueryObjectBindingModel> bindings,
    ImmutableArray<QueryObjectBindingConflictModel> conflicts) : IEquatable<QueryObjectBindingRegistrationModel>
{
    public QueryObjectBindingRegistrationModel(ImmutableArray<QueryObjectBindingModel> bindings)
        : this(bindings, ImmutableArray<QueryObjectBindingConflictModel>.Empty)
    {
    }

    public ImmutableArray<QueryObjectBindingModel> Bindings { get; } = bindings;

    public ImmutableArray<QueryObjectBindingConflictModel> Conflicts { get; } = conflicts;

    public bool IsEmpty => Bindings.IsDefaultOrEmpty && Conflicts.IsDefaultOrEmpty;

    public bool Equals(QueryObjectBindingRegistrationModel other)
    {
        return Bindings.SequenceEqual(other.Bindings) &&
            Conflicts.SequenceEqual(other.Conflicts);
    }

    public override bool Equals(object? obj)
    {
        return obj is QueryObjectBindingRegistrationModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = 17;
            foreach (var binding in Bindings)
            {
                hashCode = (hashCode * 397) ^ binding.GetHashCode();
            }

            foreach (var conflict in Conflicts)
            {
                hashCode = (hashCode * 397) ^ conflict.GetHashCode();
            }

            return hashCode;
        }
    }
}
