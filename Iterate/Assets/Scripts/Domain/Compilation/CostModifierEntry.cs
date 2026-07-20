namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// One recorded cost-modifier application: its stage, source, the before/after cost, and whether it was
    /// numerically redundant — the cost was unchanged.
    /// </summary>
    /// <param name="Stage">The staging bucket the modifier resolved in.</param>
    /// <param name="SourceDisplayName">The display name of the source that contributed the modifier.</param>
    /// <param name="SetsAbsolute">Whether the modifier set the cost absolutely.</param>
    /// <param name="Amount">The modifier's authored amount.</param>
    /// <param name="CostBefore">The cost before the modifier.</param>
    /// <param name="CostAfter">The cost after the modifier.</param>
    /// <param name="NumericallyRedundant">Whether the modifier left the cost unchanged.</param>
    public sealed record CostModifierEntry(
        CostModifierStage Stage,
        string SourceDisplayName,
        bool SetsAbsolute,
        int Amount,
        int CostBefore,
        int CostAfter,
        bool NumericallyRedundant
    );
}