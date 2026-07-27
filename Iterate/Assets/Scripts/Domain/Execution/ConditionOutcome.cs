namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The retained result of a Condition evaluation a unit executed inside: the two outcomes a
    /// System 1 Condition can hold. A unit not inside a Condition carries no outcome (a null
    /// <see cref="PostUnitOccurrence.ConditionResult"/>), and a host rescued from a false Condition
    /// carries <see cref="False"/>.
    /// </summary>
    public enum ConditionOutcome
    {
        /// <summary>
        /// The enclosing Condition evaluated true.
        /// </summary>
        True,

        /// <summary>
        /// The enclosing Condition evaluated false.
        /// </summary>
        False
    }
}