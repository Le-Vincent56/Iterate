using System;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One finalized quantity change offered to the effect engine: register, actual delta, and origin —
    /// a primary-operation change carries its source ownership, a reaction-caused change carries its
    /// effect origin, so an observing effect can never mistake one for the other.
    /// </summary>
    /// <param name="Unit">The runtime unit containing the change.</param>
    /// <param name="Event">The finalized quantity event this occurrence mirrors.</param>
    /// <param name="CausalDepth">The candidate event's causal depth; never negative.</param>
    /// <param name="Register">The register that changed.</param>
    /// <param name="ActualDelta">The finalized actual delta.</param>
    /// <param name="Ownership">The source ownership; required for a primary-operation change.</param>
    /// <param name="EffectOrigin">The causing effect's instance; non-null exactly when the change is reaction-caused.</param>
    /// <param name="FromPrimaryOperation">Whether the change was produced by the unit's primary operation.</param>
    public sealed record QuantityOccurrence(
        RuntimeUnitID Unit,
        TraceEventID Event,
        int CausalDepth,
        CoreRegister Register,
        int ActualDelta,
        OwnershipClassification? Ownership,
        InstanceID? EffectOrigin,
        bool FromPrimaryOperation
    )
    {
        /// <summary>
        /// The candidate event's causal depth. Validated non-negative at construction.
        /// </summary>
        public int CausalDepth { get; } = RequireDepth(CausalDepth);

        /// <summary>
        /// The source ownership. Validated at construction: required for a primary-operation change.
        /// </summary>
        public OwnershipClassification? Ownership { get; } = RequireOwnership(FromPrimaryOperation, Ownership);

        /// <summary>
        /// The causing effect's instance. Validated at construction: non-null exactly when the change
        /// is reaction-caused.
        /// </summary>
        public InstanceID? EffectOrigin { get; } = RequireEffectOrigin(FromPrimaryOperation, EffectOrigin);

        /// <summary>
        /// Validates that the causal depth is non-negative.
        /// </summary>
        /// <param name="causalDepth">The candidate depth.</param>
        /// <returns>The depth unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the depth is negative.</exception>
        private static int RequireDepth(int causalDepth)
        {
            if (causalDepth < 0)
                throw new ArgumentException("An occurrence requires a non-negative causal depth.", nameof(causalDepth));

            return causalDepth;
        }

        /// <summary>
        /// Validates that a primary-operation change carries its source ownership.
        /// </summary>
        /// <param name="fromPrimaryOperation">Whether the change is primary-operation-caused.</param>
        /// <param name="ownership">The candidate ownership.</param>
        /// <returns>The ownership unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when a primary change carries no ownership.</exception>
        private static OwnershipClassification? RequireOwnership(
            bool fromPrimaryOperation,
            OwnershipClassification? ownership
        )
        {
            if (fromPrimaryOperation && ownership == null)
                throw new ArgumentException("A primary-operation change requires its source ownership.", nameof(ownership));

            return ownership;
        }

        /// <summary>
        /// Validates that the effect origin is present exactly when the change is reaction-caused.
        /// </summary>
        /// <param name="fromPrimaryOperation">Whether the change is primary-operation-caused.</param>
        /// <param name="effectOrigin">The candidate effect origin.</param>
        /// <returns>The effect origin unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and origin kind disagree.</exception>
        private static InstanceID? RequireEffectOrigin(
            bool fromPrimaryOperation,
            InstanceID? effectOrigin
        )
        {
            if (!fromPrimaryOperation && effectOrigin == null)
                throw new ArgumentException("A reaction-caused change requires its effect origin.", nameof(effectOrigin));

            if (fromPrimaryOperation && effectOrigin != null)
                throw new ArgumentException("A primary-operation change carries no effect origin.", nameof(effectOrigin));

            return effectOrigin;
        }
    }
}