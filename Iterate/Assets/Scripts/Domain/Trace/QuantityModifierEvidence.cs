using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// One modifier applied during a quantity change: the modifier's identity, the declared stage it
    /// applied at, and the signed amount it contributed. The stage vocabulary binds at the producing
    /// children; this leaf records the declared token.
    /// </summary>
    public sealed record QuantityModifierEvidence
    {
        /// <summary>
        /// The identity of the modifier that applied; never empty.
        /// </summary>
        public string ModifierIdentity { get; }

        /// <summary>
        /// The declared stage the modifier applied at; never empty.
        /// </summary>
        public string Stage { get; }

        /// <summary>
        /// The signed amount the modifier contributed to the change.
        /// </summary>
        public int Amount { get; }

        public QuantityModifierEvidence(string modifierIdentity, string stage, int amount)
        {
            if (string.IsNullOrEmpty(modifierIdentity))
                throw new ArgumentException("A modifier evidence requires a modifier identity.", nameof(modifierIdentity));
            
            if (string.IsNullOrEmpty(stage))
                throw new ArgumentException("A modifier evidence requires a stage token.", nameof(stage));

            ModifierIdentity = modifierIdentity;
            Stage = stage;
            Amount = amount;
        }
    }
}