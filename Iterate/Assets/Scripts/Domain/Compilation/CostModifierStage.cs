namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The cost-modifier stage at which a modifier resolves: set/replacement, additive, or the
    /// multiplicative slot present but unused in v0.1.
    /// </summary>
    public enum CostModifierStage
    {
        /// <summary>
        /// The set-or-replacement stage: sets the cost absolutely.
        /// </summary>
        SetOrReplacement,

        /// <summary>
        /// The additive stage: adds a signed amount to the cost.
        /// </summary>
        Additive,

        /// <summary>
        /// The multiplicative stage; present but unused in v0.1.
        /// </summary>
        Multiplicative
    }
}