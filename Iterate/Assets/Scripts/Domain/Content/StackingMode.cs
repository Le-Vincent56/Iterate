namespace Iterate.Domain.Content
{
    /// <summary>
    /// The controlled stacking mode a repeatable effect declares for coexisting instances. Binds the
    /// nine named controlled modes of the CAB stacking registry; content-specific custom formulas are
    /// a governed addition, not a member. Serialized in JSON as the uppercase mode tokens.
    /// </summary>
    public enum StackingMode
    {
        IndependentResolution,
        AdditiveParameter,
        MultiplicativeParameter,
        SetReplacement,
        FirstQualifyingSelection,
        CappedIndependent,
        ExclusiveGroup,
        SharedFrequency,
        StrongestOnly
    }
}