using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests Core materialisation: an authored Core becomes an arrangement of Core lines and empty
    /// player positions and nothing else. Progression never builds an Instruction or Structure slot —
    /// installing is Compilation's job — and a Core-owned Structure is refused outright until the Core
    /// source objects child lands.
    /// </summary>
    public sealed class CoreMaterializerTests
    {
        [Test]
        public void Materialize_TheTutorialOneCoreShape_ProducesSevenSlotsWithFiveEmpty()
        {
            CoreMaterialization result = CoreMaterializer.Materialize(TutorialOneCore());

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(7, result.Arrangement.Slots.Count);
            Assert.AreEqual(7, result.FinalOutput.LineNumber);

            int empty = 0;
            foreach (SourceSlot slot in result.Arrangement.Slots)
            {
                if (slot.Kind == SourceSlotKind.Empty)
                    empty++;
            }

            Assert.AreEqual(5, empty);
        }

        [Test]
        public void Materialize_EmitsCoreSlotsForFixedInstructions()
        {
            CoreMaterialization result = CoreMaterializer.Materialize(TutorialOneCore());

            Assert.AreEqual(SourceSlotKind.Core, result.Arrangement.Slots[0].Kind);
            Assert.AreEqual(SourceSlotKind.Core, result.Arrangement.Slots[6].Kind);
        }

        [Test]
        public void Materialize_EmitsNothingButCoreAndEmptySlots()
        {
            CoreMaterialization result = CoreMaterializer.Materialize(TutorialOneCore());

            foreach (SourceSlot slot in result.Arrangement.Slots)
            {
                Assert.IsTrue(
                    slot.Kind == SourceSlotKind.Core || slot.Kind == SourceSlotKind.Empty,
                    "Progression never builds an Instruction or Structure slot; " + slot.Kind + " appeared."
                );
            }
        }

        [Test]
        public void Materialize_CarriesTheTypedOperationOntoTheCoreLine()
        {
            CoreMaterialization result = CoreMaterializer.Materialize(TutorialOneCore());

            CoreLine line = result.Arrangement.Slots[0].Core;
            Assert.AreEqual(CoreLineOperator.Assign, line.Operation.Operator);
            Assert.AreEqual(CoreRegister.Value, line.Operation.Target);
        }

        [Test]
        public void Materialize_GivesEachCoreLineAStableIdentityFromItsCoreAndPosition()
        {
            CoreMaterialization result = CoreMaterializer.Materialize(TutorialOneCore());

            Assert.AreEqual("WB-CORE-001:L01", result.Arrangement.Slots[0].Core.Identity);
            Assert.AreEqual("WB-CORE-001:L07", result.Arrangement.Slots[6].Core.Identity);
        }

        [Test]
        public void Materialize_ACoreCarryingAFixedStructure_IsRejected()
        {
            CoreMaterialization result = CoreMaterializer.Materialize(ParityCheckCore());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ProcessCreationRejection.CoreStructureUnsupported, result.Rejection);
            Assert.IsNull(result.Arrangement);
        }

        [Test]
        public void Materialize_APositionsOnlyCore_StillDesignatesItsFinalOutput()
        {
            CoreDefinition core = new(
                new CoreID("WB-CORE-002"),
                "Two-line Core",
                new CoreLineSpec[] { Open(1), Fixed(2, CoreLineOperator.Add, CoreRegister.Score) },
                new SourcePosition(2)
            );

            CoreMaterialization result = CoreMaterializer.Materialize(core);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(2, result.FinalOutput.LineNumber);
            Assert.AreEqual(SourceSlotKind.Empty, result.Arrangement.Slots[0].Kind);
        }

        /// <summary>
        /// Builds the Tutorial Process 1 Core shape: an assignment, five player positions, and a
        /// designated scoring output.
        /// </summary>
        /// <returns>The Core definition.</returns>
        private static CoreDefinition TutorialOneCore()
        {
            List<CoreLineSpec> lines = new()
            {
                Fixed(1, CoreLineOperator.Assign, CoreRegister.Value),
                Open(2),
                Open(3),
                Open(4),
                Open(5),
                Open(6),
                Fixed(7, CoreLineOperator.Add, CoreRegister.Score)
            };

            return new CoreDefinition(new CoreID("WB-CORE-001"), "Tutorial Process 1 Core", lines, new SourcePosition(7));
        }

        /// <summary>
        /// Builds a Core carrying a fixed Structure, which this slice cannot materialise.
        /// </summary>
        /// <returns>The Core definition.</returns>
        private static CoreDefinition ParityCheckCore()
        {
            CoreLineSpec contained = new(
                0,
                CoreLineKind.FixedInstruction,
                new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)),
                null,
                null
            );

            List<CoreLineSpec> lines = new()
            {
                Open(1),
                new CoreLineSpec(
                    2,
                    CoreLineKind.FixedStructure,
                    null,
                    new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0),
                    contained
                ),
                Fixed(3, CoreLineOperator.Add, CoreRegister.Score)
            };

            return new CoreDefinition(new CoreID("WB-CORE-003"), "PARITY CHECK Core", lines, new SourcePosition(3));
        }

        /// <summary>
        /// Builds a fixed-instruction Core line.
        /// </summary>
        /// <param name="position">The one-based Core position.</param>
        /// <param name="lineOperator">The operator the line applies.</param>
        /// <param name="target">The register the line writes.</param>
        /// <returns>The line spec.</returns>
        private static CoreLineSpec Fixed(int position, CoreLineOperator lineOperator, CoreRegister target)
        {
            return new CoreLineSpec(
                position,
                CoreLineKind.FixedInstruction,
                new CoreLineOperation(lineOperator, target, OperandSpec.FromConstant(1)),
                null,
                null
            );
        }

        /// <summary>
        /// Builds an open Core line.
        /// </summary>
        /// <param name="position">The one-based Core position.</param>
        /// <returns>The line spec.</returns>
        private static CoreLineSpec Open(int position)
        {
            return new CoreLineSpec(position, CoreLineKind.Open, null, null, null);
        }
    }
}
