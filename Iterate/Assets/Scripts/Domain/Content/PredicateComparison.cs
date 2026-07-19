namespace Iterate.Domain.Content
{
    /// <summary>
    /// The comparison a Condition predicate applies to a register. Serialized in JSON as IS_EVEN,
    /// AT_LEAST.
    /// </summary>
    public enum PredicateComparison
    {
        IsEven,
        AtLeast
    }
}