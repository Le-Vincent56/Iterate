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
    /// Tests the reshaped <see cref="ExecutionFrame"/>: the typed captured-request batch appends in
    /// order, the branch lineage, added-execution depth, and root activation round-trip, and the
    /// constructor validates its components.
    /// </summary>
    public sealed class ExecutionFrameTests
    {
        private static readonly InstructionDefinition _instructionDefinition = new(
            new InstructionID("WB-INS-002"),
            "rules",
            "Add 2 to Value",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        [Test]
        public void Constructor_RoundTrips()
        {
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(new InstanceID(9));

            ExecutionFrame frame = new ExecutionFrame(new RuntimeUnitID(5), lineage, 1, new RuntimeUnitID(2));

            Assert.AreEqual(new RuntimeUnitID(5), frame.Unit);
            Assert.AreSame(lineage, frame.Lineage);
            Assert.AreEqual(1, frame.AddedExecutionDepth);
            Assert.AreEqual(new RuntimeUnitID(2), frame.RootActivation);
        }

        [Test]
        public void Constructor_SelfRootedCanonicalShape_RoundTrips()
        {
            ExecutionFrame frame = new ExecutionFrame(new RuntimeUnitID(4), EffectOriginLineage.Empty, 0, new RuntimeUnitID(4));

            Assert.AreEqual(EffectOriginLineage.Empty, frame.Lineage);
            Assert.AreEqual(0, frame.AddedExecutionDepth);
            Assert.AreEqual(frame.Unit, frame.RootActivation);
        }

        [Test]
        public void Constructor_NullLineage_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionFrame(new RuntimeUnitID(1), null, 0, new RuntimeUnitID(1)));
        }

        [Test]
        public void Constructor_NegativeDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionFrame(new RuntimeUnitID(1), EffectOriginLineage.Empty, -1, new RuntimeUnitID(1)));
        }

        [Test]
        public void CapturedBatch_StartsEmpty()
        {
            ExecutionFrame frame = new ExecutionFrame(new RuntimeUnitID(1), EffectOriginLineage.Empty, 0, new RuntimeUnitID(1));

            Assert.AreEqual(0, frame.CapturedBatch.Count);
        }

        [Test]
        public void Capture_AppendsInOrder()
        {
            ExecutionFrame frame = new ExecutionFrame(new RuntimeUnitID(1), EffectOriginLineage.Empty, 0, new RuntimeUnitID(1));
            AddedExecutionRequest first = Request("WB-DIR-001:0#9@e7");
            AddedExecutionRequest second = Request("WB-DEP-009:0#8@e9");

            frame.Capture(first);
            frame.Capture(second);

            Assert.AreEqual(2, frame.CapturedBatch.Count);
            Assert.AreSame(first, frame.CapturedBatch[0]);
            Assert.AreSame(second, frame.CapturedBatch[1]);
        }

        [Test]
        public void Capture_Null_Throws()
        {
            ExecutionFrame frame = new ExecutionFrame(new RuntimeUnitID(1), EffectOriginLineage.Empty, 0, new RuntimeUnitID(1));

            Assert.Throws<ArgumentException>(() => frame.Capture(null));
        }

        /// <summary>
        /// Builds a valid captured request with the given identity.
        /// </summary>
        /// <param name="identity">The request identity.</param>
        /// <returns>The request.</returns>
        private static AddedExecutionRequest Request(string identity)
        {
            InstructionInstance instruction = new(new InstanceID(1), _instructionDefinition, null);
            SourceSlot slot = SourceSlot.ForInstruction(new SourcePosition(2), instruction);

            return new AddedExecutionRequest(
                identity,
                new InstanceID(9),
                "WB-DIR-001",
                new TraceEventID(7),
                null,
                slot,
                null,
                EffectOriginLineage.Empty.Append(new InstanceID(9)),
                1,
                new RuntimeUnitID(1));
        }
    }
}
