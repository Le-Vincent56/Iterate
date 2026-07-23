using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One primary operation observed at the pending or resolved boundary: the structural facts of the
    /// operation — target register, operator, operand shape, ownership with the owning host instance —
    /// offered to the effect engine for trigger matching as the complete boundary snapshot, never
    /// partially calculated state.
    /// </summary>
    /// <param name="Unit">The runtime unit executing the operation.</param>
    /// <param name="Event">The trace event this occurrence mirrors.</param>
    /// <param name="CausalDepth">The candidate event's causal depth; never negative.</param>
    /// <param name="HostInstance">The owning host instance; non-null exactly when player-owned.</param>
    /// <param name="Register">The register the operation targets.</param>
    /// <param name="Operator">The operator the operation applies.</param>
    /// <param name="OperandSource">The operand's declared source.</param>
    /// <param name="OperandRegister">The operand register; non-null exactly when the operand source is a register.</param>
    /// <param name="Ownership">The executing unit's ownership classification.</param>
    public sealed record OperationOccurrence(
        RuntimeUnitID Unit,
        TraceEventID Event,
        int CausalDepth,
        InstanceID? HostInstance,
        CoreRegister Register,
        CoreLineOperator Operator,
        OperandSource OperandSource,
        CoreRegister? OperandRegister,
        OwnershipClassification Ownership
    )
    {
        /// <summary>
        /// The candidate event's causal depth. Validated non-negative at construction.
        /// </summary>
        public int CausalDepth { get; } = RequireDepth(CausalDepth);

        /// <summary>
        /// The owning host instance. Validated at construction: non-null exactly when the ownership
        /// is player-owned.
        /// </summary>
        public InstanceID? HostInstance { get; } = RequireHostPairing(Ownership, HostInstance);

        /// <summary>
        /// The operand register. Validated at construction: non-null exactly when
        /// <see cref="OperandSource"/> is <see cref="OperandSource.Register"/>.
        /// </summary>
        public CoreRegister? OperandRegister { get; } = RequireOperandPairing(OperandSource, OperandRegister);

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
        /// Validates that the host instance is present exactly when the unit is player-owned.
        /// </summary>
        /// <param name="ownership">The unit's ownership classification.</param>
        /// <param name="hostInstance">The candidate host instance.</param>
        /// <returns>The host instance unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and ownership disagree.</exception>
        private static InstanceID? RequireHostPairing(
            OwnershipClassification ownership,
            InstanceID? hostInstance)
        {
            if (ownership == OwnershipClassification.PlayerOwned && hostInstance == null)
                throw new ArgumentException("A player-owned occurrence requires a host instance.", nameof(hostInstance));

            if (ownership == OwnershipClassification.CoreOwned && hostInstance != null)
                throw new ArgumentException("A Core-owned occurrence carries no host instance.", nameof(hostInstance));

            return hostInstance;
        }

        /// <summary>
        /// Validates that the operand register is present exactly when the operand source is a register.
        /// </summary>
        /// <param name="operandSource">The operand's declared source.</param>
        /// <param name="operandRegister">The candidate operand register.</param>
        /// <returns>The operand register unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and source disagree.</exception>
        private static CoreRegister? RequireOperandPairing(
            OperandSource operandSource,
            CoreRegister? operandRegister
        )
        {
            if (operandSource == OperandSource.Register && operandRegister == null)
                throw new ArgumentException("A register operand requires an operand register.", nameof(operandRegister));

            if (operandSource != OperandSource.Register && operandRegister != null)
                throw new ArgumentException("A non-register operand carries no operand register.", nameof(operandRegister));

            return operandRegister;
        }
    }
}