using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the one constructible CAB-EVT-430 host-validity check: a lock whose locked position still
    /// holds the same instance validates, a lock whose position holds a different instance or whose
    /// host has been removed or moved fails <c>HOST_MISSING</c>, and the validator owns the
    /// cancellation reason composition. The clauses the domain model guarantees structurally (a
    /// non-null executable definition, arrangement immutability during execution) are recorded in the
    /// validator's own XML docs rather than exercised as dead branches, so this suite pins only the
    /// live check and the reason vocabulary.
    /// </summary>
    public sealed class LockedHostValidatorTests
    {
        [Test]
        public void Validate_LockedPositionHoldsSameInstance_ReturnsNull()
        {
            SourceSlot host = Host(10, 1);
            SourceArrangement arrangement = ArrangementOf(host);

            Assert.IsNull(LockedHostValidator.Validate(LockFor(host), arrangement));
        }

        [Test]
        public void Validate_PositionHoldsDifferentInstance_ReturnsHostMissing()
        {
            SourceSlot locked = Host(10, 1);
            SourceArrangement arrangement = ArrangementOf(Host(11, 1));

            Assert.AreEqual("HOST_MISSING", LockedHostValidator.Validate(LockFor(locked), arrangement));
        }

        [Test]
        public void Validate_HostAbsentFromArrangement_ReturnsHostMissing()
        {
            SourceSlot locked = Host(10, 1);
            SourceArrangement arrangement = ArrangementOf(Empty(1));

            Assert.AreEqual("HOST_MISSING", LockedHostValidator.Validate(LockFor(locked), arrangement));
        }

        [Test]
        public void Validate_HostMovedToADifferentPosition_ReturnsHostMissing()
        {
            SourceSlot locked = Host(10, 1);
            SourceArrangement arrangement = ArrangementOf(Empty(1), Host(10, 2));

            Assert.AreEqual("HOST_MISSING", LockedHostValidator.Validate(LockFor(locked), arrangement));
        }

        [Test]
        public void ComposeReason_PrependsTheLockedHostInvalidToken()
        {
            Assert.AreEqual("LOCKED_HOST_INVALID:HOST_MISSING", LockedHostValidator.ComposeReason("HOST_MISSING"));
        }

        [Test]
        public void Validate_NullLock_Throws()
        {
            SourceArrangement arrangement = ArrangementOf(Host(10, 1));

            Assert.Throws<ArgumentException>(() => LockedHostValidator.Validate(null, arrangement));
        }

        [Test]
        public void Validate_NullArrangement_Throws()
        {
            SourceSlot host = Host(10, 1);

            Assert.Throws<ArgumentException>(() => LockedHostValidator.Validate(LockFor(host), null));
        }

        /// <summary>
        /// A player Instruction slot at a position, carrying the instance whose identity value equals
        /// the slot's host identity.
        /// </summary>
        /// <param name="instanceID">The host instance identity value.</param>
        /// <param name="position">The one-based source position.</param>
        /// <returns>The Instruction slot.</returns>
        private static SourceSlot Host(int instanceID, int position)
        {
            InstructionInstance instance = new InstructionInstance(new InstanceID(instanceID), ValueAdd(), null);
            return SourceSlot.ForInstruction(new SourcePosition(position), instance);
        }

        /// <summary>
        /// An empty slot at a position.
        /// </summary>
        /// <param name="position">The one-based source position.</param>
        /// <returns>The empty slot.</returns>
        private static SourceSlot Empty(int position)
        {
            return SourceSlot.ForEmpty(new SourcePosition(position));
        }

        /// <summary>
        /// A target lock over an Instruction slot, keyed to that slot's occupant instance and position.
        /// </summary>
        /// <param name="slot">The locked slot.</param>
        /// <returns>The target lock.</returns>
        private static TargetLock LockFor(SourceSlot slot)
        {
            return new TargetLock(
                slot,
                slot.Instruction.InstanceID,
                null,
                new TraceEventID(14),
                new RuntimeUnitID(1),
                null);
        }

        /// <summary>
        /// A validated arrangement over the given slots.
        /// </summary>
        /// <param name="slots">The arrangement's slots in ascending position order.</param>
        /// <returns>The arrangement.</returns>
        private static SourceArrangement ArrangementOf(params SourceSlot[] slots)
        {
            return new SourceArrangement(new List<SourceSlot>(slots));
        }

        /// <summary>
        /// A fixture <c>Value += 1</c> Instruction definition.
        /// </summary>
        /// <returns>The definition.</returns>
        private static InstructionDefinition ValueAdd()
        {
            return new InstructionDefinition(
                new InstructionID("WB-INS-FIXTURE"),
                "rules",
                "instruction",
                ContentCategory.Instruction,
                Rarity.Common,
                new List<string>(),
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                new List<string>());
        }
    }
}
