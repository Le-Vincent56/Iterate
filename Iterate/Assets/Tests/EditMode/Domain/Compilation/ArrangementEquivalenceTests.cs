using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the single UX-SRC-016 mechanical-equivalence comparator
    /// <see cref="ArrangementEquivalence.AreEquivalent"/>: per-position comparison over kind, Core-line
    /// value, occupant <see cref="InstanceID"/>, attached-Patch <see cref="InstanceID"/>, and owning
    /// Structure identity. Child order falls out of the per-position comparison.
    /// </summary>
    public sealed class ArrangementEquivalenceTests
    {
        private static readonly CoreLine _coreOne = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
        private static readonly CoreLine _coreTwo = new("core-02", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Signal, OperandSpec.FromConstant(0)));
        private static readonly CoreLine _coreNine = new("core-09", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

        private static readonly InstructionDefinition _instructionDefinition = new(
            new InstructionID("WB-INS-002"),
            "rules",
            "Add 1 to Value",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        private static readonly StructureDefinition _repeatDefinition = new(
            new StructureID("WB-STR-001"),
            "Repeat 2",
            "Repeat 2",
            ContentCategory.Structure,
            Rarity.Common,
            Array.Empty<string>(),
            2,
            StructureKind.Repeat,
            2,
            null);

        private static readonly PatchDefinition _patchDefinition = new(
            new PatchID("WB-PAT-001"),
            "Patch",
            "Patch",
            ContentCategory.Patch,
            Rarity.Common,
            Array.Empty<string>(),
            null,
            Array.Empty<EffectDefinition>());

        [Test]
        public void AreEquivalent_IdenticalArrangements_True()
        {
            InstructionInstance instruction = new(new InstanceID(7), _instructionDefinition, null);

            Assert.IsTrue(ArrangementEquivalence.AreEquivalent(
                WithInstructionAt3(instruction),
                WithInstructionAt3(instruction)));
        }

        [Test]
        public void AreEquivalent_DifferentSlotCount_False()
        {
            SourceArrangement full = StandardCore();
            SourceArrangement shorter = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForEmpty(new SourcePosition(2))
            });

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(full, shorter));
        }

        [Test]
        public void AreEquivalent_SameDefinitionDifferentInstanceID_False()
        {
            InstructionInstance left = new(new InstanceID(7), _instructionDefinition, null);
            InstructionInstance right = new(new InstanceID(8), _instructionDefinition, null);

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(
                WithInstructionAt3(left),
                WithInstructionAt3(right)));
        }

        [Test]
        public void AreEquivalent_SameInstanceMovedToDifferentPosition_False()
        {
            InstructionInstance instruction = new(new InstanceID(7), _instructionDefinition, null);

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(
                WithInstructionAt3(instruction),
                WithInstructionAt4(instruction)));
        }

        [Test]
        public void AreEquivalent_OneOccupantPatched_False()
        {
            InstructionInstance unpatched = new(new InstanceID(7), _instructionDefinition, null);
            InstructionInstance patched = unpatched with
            {
                AttachedPatch = new PatchInstance(new InstanceID(2), _patchDefinition)
            };

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(
                WithInstructionAt3(unpatched),
                WithInstructionAt3(patched)));
        }

        [Test]
        public void AreEquivalent_BothPatchedSamePatchInstanceID_True()
        {
            InstructionInstance left = new InstructionInstance(new InstanceID(7), _instructionDefinition, null) with
            {
                AttachedPatch = new PatchInstance(new InstanceID(2), _patchDefinition)
            };
            InstructionInstance right = new InstructionInstance(new InstanceID(7), _instructionDefinition, null) with
            {
                AttachedPatch = new PatchInstance(new InstanceID(2), _patchDefinition)
            };

            Assert.IsTrue(ArrangementEquivalence.AreEquivalent(
                WithInstructionAt3(left),
                WithInstructionAt3(right)));
        }

        [Test]
        public void AreEquivalent_BothPatchedDifferentPatchInstanceID_False()
        {
            InstructionInstance left = new InstructionInstance(new InstanceID(7), _instructionDefinition, null) with
            {
                AttachedPatch = new PatchInstance(new InstanceID(2), _patchDefinition)
            };
            InstructionInstance right = new InstructionInstance(new InstanceID(7), _instructionDefinition, null) with
            {
                AttachedPatch = new PatchInstance(new InstanceID(3), _patchDefinition)
            };

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(
                WithInstructionAt3(left),
                WithInstructionAt3(right)));
        }

        [Test]
        public void AreEquivalent_DifferentCoreLine_False()
        {
            SourceArrangement left = StandardCore();
            SourceArrangement right = left.WithSlots(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(2))))
            });

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(left, right));
        }

        [Test]
        public void AreEquivalent_DifferentOwningStructureIdentity_False()
        {
            StructureInstance first = new(new InstanceID(4), _repeatDefinition);
            StructureInstance second = new(new InstanceID(5), _repeatDefinition);

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(
                WithStructureAt3(first),
                WithStructureAt3(second)));
        }

        [Test]
        public void AreEquivalent_DifferentKind_False()
        {
            InstructionInstance instruction = new(new InstanceID(7), _instructionDefinition, null);

            Assert.IsFalse(ArrangementEquivalence.AreEquivalent(
                StandardCore(),
                WithInstructionAt3(instruction)));
        }

        private static SourceArrangement StandardCore()
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                SourceSlot.ForEmpty(new SourcePosition(3)),
                SourceSlot.ForEmpty(new SourcePosition(4)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForEmpty(new SourcePosition(8)),
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }

        private static SourceArrangement WithInstructionAt3(InstructionInstance instruction)
        {
            return StandardCore().WithSlots(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(3), instruction)
            });
        }

        private static SourceArrangement WithInstructionAt4(InstructionInstance instruction)
        {
            return StandardCore().WithSlots(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(4), instruction)
            });
        }

        private static SourceArrangement WithStructureAt3(StructureInstance structure)
        {
            return StandardCore().WithSlots(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(3), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(4), structure)
            });
        }
    }
}
