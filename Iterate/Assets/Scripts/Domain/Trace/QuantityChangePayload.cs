using System;
using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The grounded evidence of a single quantity change: the quantity's identity and category, the
    /// declared operation, the requested amount, the prior value, an optional link to a replacing
    /// operation, the modifiers and bounds applied while resolving, and the final delta and value. The
    /// final delta is preserved distinctly from the final value and must be consistent with the prior
    /// value, so a resolved zero-delta change is a representable, distinct fact. Equality is structural
    /// over the applied-modifier list.
    /// </summary>
    public sealed record QuantityChangePayload : EventPayload
    {
        /// <summary>
        /// The identity of the quantity that changed; never empty.
        /// </summary>
        public string QuantityIdentity { get; }

        /// <summary>
        /// The controlled category of the quantity.
        /// </summary>
        public QuantityCategory Category { get; }

        /// <summary>
        /// The controlled operation type the change declared.
        /// </summary>
        public QuantityOperationType Operation { get; }

        /// <summary>
        /// The requested delta or proposed value before modification.
        /// </summary>
        public int RequestedAmount { get; }

        /// <summary>
        /// The quantity's value before the change.
        /// </summary>
        public int PriorValue { get; }

        /// <summary>
        /// The event identity of the operation that replaced this one, or null when not replaced.
        /// </summary>
        public TraceEventID? ReplacementLink { get; }

        /// <summary>
        /// The modifiers applied while resolving, in declared order; never null, possibly empty.
        /// </summary>
        public IReadOnlyList<QuantityModifierEvidence> AppliedModifiers { get; }

        /// <summary>
        /// The bound applied while resolving, or null when no bound applied.
        /// </summary>
        public QuantityBoundsEvidence AppliedBounds { get; }

        /// <summary>
        /// The signed change actually applied; equal to the final value minus the prior value.
        /// </summary>
        public int FinalDelta { get; }

        /// <summary>
        /// The quantity's value after the change.
        /// </summary>
        public int FinalValue { get; }

        public QuantityChangePayload(
            string quantityIdentity,
            QuantityCategory category,
            QuantityOperationType operation,
            int requestedAmount,
            int priorValue,
            TraceEventID? replacementLink,
            IReadOnlyList<QuantityModifierEvidence> appliedModifiers,
            QuantityBoundsEvidence appliedBounds,
            int finalDelta,
            int finalValue
        )
        {
            if (string.IsNullOrEmpty(quantityIdentity))
                throw new ArgumentException("A quantity change requires a quantity identity.", nameof(quantityIdentity));
            
            if (finalDelta != finalValue - priorValue)
                throw new ArgumentException("The final delta must equal the final value minus the prior value.", nameof(finalDelta));

            QuantityIdentity = quantityIdentity;
            Category = category;
            Operation = operation;
            RequestedAmount = requestedAmount;
            PriorValue = priorValue;
            ReplacementLink = replacementLink;
            AppliedModifiers = appliedModifiers ?? throw new ArgumentException("A quantity change requires an applied-modifiers list.", nameof(appliedModifiers));
            AppliedBounds = appliedBounds;
            FinalDelta = finalDelta;
            FinalValue = finalValue;
        }

        /// <summary>
        /// Structural value equality: every scalar field plus element-wise modifier comparison.
        /// </summary>
        /// <param name="other">The payload to compare against.</param>
        /// <returns>True when every field and the modifiers are equal.</returns>
        public bool Equals(QuantityChangePayload other)
        {
            return base.Equals(other)
                && string.Equals(QuantityIdentity, other?.QuantityIdentity, StringComparison.Ordinal)
                && Category == other?.Category
                && Operation == other.Operation
                && RequestedAmount == other.RequestedAmount
                && PriorValue == other.PriorValue
                && ReplacementLink == other.ReplacementLink
                && AppliedBounds == other.AppliedBounds
                && FinalDelta == other.FinalDelta
                && FinalValue == other.FinalValue
                && TraceEquality.ListEquals(AppliedModifiers, other.AppliedModifiers);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the identity, category, operation, final
        /// value, and modifier count.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + QuantityIdentity.GetHashCode();
                hash = hash * 31 + (int)Category;
                hash = hash * 31 + (int)Operation;
                hash = hash * 31 + FinalValue;
                hash = hash * 31 + AppliedModifiers.Count;
                return hash;
            }
        }
    }
}