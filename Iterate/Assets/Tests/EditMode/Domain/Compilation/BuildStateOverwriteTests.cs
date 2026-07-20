using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the free-only <see cref="OverwriteEdit"/> applied through <see cref="BuildState"/>: the
    /// free-only flag, ordinary and contained Instruction targets archiving their occupant, the Design §17
    /// Structure-unwrap example verbatim, every reachable rejection, and the append-only archive.
    /// </summary>
    public sealed class BuildStateOverwriteTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", "Value = 1");
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", "Signal = 0");
        private static readonly CoreLine _coreNine = new CoreLine("core-09", "Score += Value");

        private static readonly InstructionDefinition _overwriteDefinition = new InstructionDefinition(
            new InstructionID("WB-INS-OVR"),
            "Value += 8",
            "Value += 8",
            ContentCategory.Instruction,
            Rarity.Common,
            new[] { "Overwrite" },
            1,
            null,
            null,
            Array.Empty<string>());

        private static readonly InstructionDefinition _ordinaryDefinition = new InstructionDefinition(
            new InstructionID("WB-INS-002"),
            "Signal += 2",
            "Signal += 2",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        private static readonly StructureDefinition _repeatThree = new StructureDefinition(
            new StructureID("WB-STR-001"),
            "Repeat 2",
            "Repeat 2",
            ContentCategory.Structure,
            Rarity.Common,
            Array.Empty<string>(),
            3,
            StructureKind.Repeat,
            2,
            null);

        private static readonly PatchDefinition _patchDefinition = new PatchDefinition(
            new PatchID("WB-PAT-001"),
            "Patch",
            "Patch",
            ContentCategory.Patch,
            Rarity.Common,
            Array.Empty<string>(),
            null,
            Array.Empty<EffectDefinition>());

        [Test]
        public void Overwrite_IsFreeOnly_True()
        {
            OverwriteEdit edit = new OverwriteEdit(new InstanceID(30), new SourcePosition(3));

            Assert.IsTrue(edit.IsFreeOnly);
        }

        [Test]
        public void Overwrite_InstructionTarget_ArchivesOccupantAndTakesSlot()
        {
            InstructionInstance seated = NewOrdinary(11);
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            BuildState state = new BuildState(
                Player(Instr(3, seated), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(3));

            Assert.IsTrue(result.Succeeded);
            SourceSlot slot = state.CurrentArrangement.SlotAt(new SourcePosition(3));
            Assert.AreEqual(SourceSlotKind.Instruction, slot.Kind);
            Assert.AreEqual(overwrite, slot.Instruction);
            CollectionAssert.AreEqual(new[] { new InstanceID(11) }, state.ArchivedInstances);
            CollectionAssert.AreEqual(new[] { new InstanceID(30) }, buffer.TakenIDs);
        }

        [Test]
        public void Overwrite_ContainedInstructionTarget_Succeeds()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            InstructionInstance child = NewOrdinary(11);
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            BuildState state = new BuildState(
                Player(Header(3, structure), ContainedInstr(4, structure, child), Contained(5, structure), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(4));

            Assert.IsTrue(result.Succeeded);
            SourceSlot slot = state.CurrentArrangement.SlotAt(new SourcePosition(4));
            Assert.AreEqual(SourceSlotKind.ContainedInstruction, slot.Kind);
            Assert.AreEqual(overwrite, slot.Instruction);
            Assert.AreEqual(structure, slot.Structure);
            CollectionAssert.AreEqual(new[] { new InstanceID(11) }, state.ArchivedInstances);
        }

        [Test]
        public void Overwrite_StructureHeaderTarget_UnwrapsPerDesign17()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            InstructionInstance firstChild = NewPatched(11, 40);
            InstructionInstance secondChild = NewOrdinary(12);
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            BuildState state = new BuildState(
                Player(Header(3, structure), ContainedInstr(4, structure, firstChild), ContainedInstr(5, structure, secondChild), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(3));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(9, state.CurrentArrangement.SlotCount);
            SourceSlot header = state.CurrentArrangement.SlotAt(new SourcePosition(3));
            Assert.AreEqual(SourceSlotKind.Instruction, header.Kind);
            Assert.AreEqual(overwrite, header.Instruction);
            SourceSlot unwrappedFirst = state.CurrentArrangement.SlotAt(new SourcePosition(4));
            Assert.AreEqual(SourceSlotKind.Instruction, unwrappedFirst.Kind);
            Assert.AreEqual(firstChild, unwrappedFirst.Instruction);
            Assert.AreEqual(new InstanceID(40), unwrappedFirst.Instruction.AttachedPatch.InstanceID);
            SourceSlot unwrappedSecond = state.CurrentArrangement.SlotAt(new SourcePosition(5));
            Assert.AreEqual(SourceSlotKind.Instruction, unwrappedSecond.Kind);
            Assert.AreEqual(secondChild, unwrappedSecond.Instruction);
            CollectionAssert.AreEqual(new[] { new InstanceID(20) }, state.ArchivedInstances);
        }

        [Test]
        public void Overwrite_ContainedEmptyUnwrapsToEmpty()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            InstructionInstance child = NewOrdinary(11);
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            BuildState state = new BuildState(
                Player(Header(3, structure), ContainedInstr(4, structure, child), Contained(5, structure), Empty(6), Empty(7), Empty(8)),
                buffer);

            state.TryOverwrite(new InstanceID(30), new SourcePosition(3));

            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Kind);
        }

        [Test]
        public void Overwrite_NotOverwriteInstruction_Rejected()
        {
            InstructionInstance seated = NewOrdinary(11);
            InstructionInstance ordinary = NewOrdinary(12);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(ordinary);
            SourceArrangement arrangement = Player(
                Instr(3, seated), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(12), new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.NotAnOverwriteInstruction, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Overwrite_AbsentItem_NotOverwriteInstruction()
        {
            InstructionInstance seated = NewOrdinary(11);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Instr(3, seated), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(99), new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.NotAnOverwriteInstruction, state, arrangement);
        }

        [Test]
        public void Overwrite_EmptyTarget_OverwriteTargetEmpty()
        {
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.OverwriteTargetEmpty, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Overwrite_ContainedEmptyTarget_OverwriteTargetEmpty()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            SourceArrangement arrangement = Player(
                Header(3, structure), Contained(4, structure), Contained(5, structure), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(4));

            AssertRejected(result, SourceEditRejection.OverwriteTargetEmpty, state, arrangement);
        }

        [Test]
        public void Overwrite_CoreTarget_CoreImmutable()
        {
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(1));

            AssertRejected(result, SourceEditRejection.CoreImmutable, state, arrangement);
        }

        [Test]
        public void Overwrite_PositionOutOfRange_PositionNotFound()
        {
            InstructionInstance overwrite = NewOverwrite(30);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(overwrite);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryOverwrite(new InstanceID(30), new SourcePosition(99));

            AssertRejected(result, SourceEditRejection.PositionNotFound, state, arrangement);
        }

        [Test]
        public void ArchivedInstances_AcrossTwoOverwrites_AppendedInOrder()
        {
            InstructionInstance firstSeated = NewOrdinary(11);
            InstructionInstance secondSeated = NewOrdinary(12);
            InstructionInstance firstOverwrite = NewOverwrite(30);
            InstructionInstance secondOverwrite = NewOverwrite(31);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(firstOverwrite);
            buffer.AddInstruction(secondOverwrite);
            BuildState state = new BuildState(
                Player(Instr(3, firstSeated), Empty(4), Instr(5, secondSeated), Empty(6), Empty(7), Empty(8)),
                buffer);

            state.TryOverwrite(new InstanceID(30), new SourcePosition(3));
            state.TryOverwrite(new InstanceID(31), new SourcePosition(5));

            CollectionAssert.AreEqual(new[] { new InstanceID(11), new InstanceID(12) }, state.ArchivedInstances);
        }

        private static void AssertRejected(
            BuildOperationResult result,
            SourceEditRejection expected,
            BuildState state,
            SourceArrangement before)
        {
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(expected, result.Rejection);
            Assert.AreSame(before, state.CurrentArrangement);
        }

        private static InstructionInstance NewOverwrite(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _overwriteDefinition, null);
        }

        private static InstructionInstance NewOrdinary(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _ordinaryDefinition, null);
        }

        private static InstructionInstance NewPatched(int idValue, int patchIDValue)
        {
            PatchInstance patch = new PatchInstance(new InstanceID(patchIDValue), _patchDefinition);
            return new InstructionInstance(new InstanceID(idValue), _ordinaryDefinition, patch);
        }

        private static StructureInstance NewStructure(int idValue, StructureDefinition definition)
        {
            return new StructureInstance(new InstanceID(idValue), definition);
        }

        private static SourceSlot Empty(int line)
        {
            return SourceSlot.ForEmpty(new SourcePosition(line));
        }

        private static SourceSlot Instr(int line, InstructionInstance instruction)
        {
            return SourceSlot.ForInstruction(new SourcePosition(line), instruction);
        }

        private static SourceSlot Header(int line, StructureInstance structure)
        {
            return SourceSlot.ForStructureHeader(new SourcePosition(line), structure);
        }

        private static SourceSlot Contained(int line, StructureInstance structure)
        {
            return SourceSlot.ForContainedEmpty(new SourcePosition(line), structure);
        }

        private static SourceSlot ContainedInstr(int line, StructureInstance structure, InstructionInstance instruction)
        {
            return SourceSlot.ForContainedInstruction(new SourcePosition(line), structure, instruction);
        }

        private static SourceArrangement StandardCore()
        {
            return Player(Empty(3), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
        }

        private static SourceArrangement Player(
            SourceSlot p3,
            SourceSlot p4,
            SourceSlot p5,
            SourceSlot p6,
            SourceSlot p7,
            SourceSlot p8)
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                p3,
                p4,
                p5,
                p6,
                p7,
                p8,
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }
    }
}
