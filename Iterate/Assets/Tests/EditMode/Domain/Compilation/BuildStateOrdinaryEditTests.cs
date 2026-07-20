using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the four ordinary Command-typed edits (install, move, swap, remove) applied through
    /// <see cref="BuildState"/> as invoker, over the Design §5 standard Core shape. Each operation is
    /// tested on its success path and on every rejection reachable for it, with rejections asserting the
    /// arrangement reference is unchanged and the buffer was not touched.
    /// </summary>
    public sealed class BuildStateOrdinaryEditTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", "Value = 1");
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", "Signal = 0");
        private static readonly CoreLine _coreNine = new CoreLine("core-09", "Score += Value");

        private static readonly InstructionDefinition _instructionDefinition = new InstructionDefinition(
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

        private static readonly StructureDefinition _repeatTwo = new StructureDefinition(
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

        private static readonly StructureDefinition _repeatThree = new StructureDefinition(
            new StructureID("WB-STR-002"),
            "Repeat 3",
            "Repeat 3",
            ContentCategory.Structure,
            Rarity.Common,
            Array.Empty<string>(),
            3,
            StructureKind.Repeat,
            3,
            null);

        // ----- Install -----

        [Test]
        public void Install_InstructionIntoEmpty_Succeeds()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(instruction);
            BuildState state = new BuildState(StandardCore(), buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(10), new SourcePosition(3));

            Assert.IsTrue(result.Succeeded);
            SourceSlot slot = state.CurrentArrangement.SlotAt(new SourcePosition(3));
            Assert.AreEqual(SourceSlotKind.Instruction, slot.Kind);
            Assert.AreEqual(instruction, slot.Instruction);
            CollectionAssert.AreEqual(new[] { new InstanceID(10) }, buffer.TakenIDs);
        }

        [Test]
        public void Install_InstructionIntoContainedEmpty_Succeeds()
        {
            StructureInstance structure = NewStructure(20, _repeatTwo);
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(instruction);
            BuildState state = new BuildState(
                Player(Header(3, structure), Contained(4, structure), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(10), new SourcePosition(4));

            Assert.IsTrue(result.Succeeded);
            SourceSlot slot = state.CurrentArrangement.SlotAt(new SourcePosition(4));
            Assert.AreEqual(SourceSlotKind.ContainedInstruction, slot.Kind);
            Assert.AreEqual(instruction, slot.Instruction);
            Assert.AreEqual(structure, slot.Structure);
        }

        [Test]
        public void Install_StructureSpanningThree_ConvertsToHeaderAndContained()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddStructure(structure);
            BuildState state = new BuildState(StandardCore(), buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(20), new SourcePosition(4));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.StructureHeader, state.CurrentArrangement.SlotAt(new SourcePosition(4)).Kind);
            Assert.AreEqual(SourceSlotKind.ContainedEmpty, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Kind);
            Assert.AreEqual(SourceSlotKind.ContainedEmpty, state.CurrentArrangement.SlotAt(new SourcePosition(6)).Kind);
            Assert.AreEqual(structure, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Structure);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Kind);
        }

        [Test]
        public void Install_StructureIntoContainedSlot_NestingNotSupported()
        {
            StructureInstance host = NewStructure(20, _repeatTwo);
            StructureInstance nested = NewStructure(21, _repeatTwo);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddStructure(nested);
            SourceArrangement arrangement = Player(
                Header(3, host), Contained(4, host), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(21), new SourcePosition(4));

            AssertRejected(result, SourceEditRejection.NestingNotSupported, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Install_OntoOccupied_DestinationNotEmpty()
        {
            InstructionInstance seated = NewInstruction(11);
            InstructionInstance incoming = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(incoming);
            SourceArrangement arrangement = Player(
                Instr(3, seated), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(10), new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.DestinationNotEmpty, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Install_OntoCore_CoreImmutable()
        {
            InstructionInstance incoming = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(incoming);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(10), new SourcePosition(1));

            AssertRejected(result, SourceEditRejection.CoreImmutable, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Install_ItemNotInBuffer_Rejected()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(99), new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.ItemNotInBuffer, state, arrangement);
        }

        [Test]
        public void Install_StructureWithoutContiguousEmpties_InsufficientFootprint()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddStructure(structure);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(20), new SourcePosition(8));

            AssertRejected(result, SourceEditRejection.InsufficientFootprint, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Install_PositionOutOfRange_PositionNotFound()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(instruction);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryInstall(new InstanceID(10), new SourcePosition(99));

            AssertRejected(result, SourceEditRejection.PositionNotFound, state, arrangement);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        // ----- Move -----

        [Test]
        public void Move_InstructionToEmpty_Succeeds()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Instr(3, instruction), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(3), new SourcePosition(5));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Kind);
            Assert.AreEqual(SourceSlotKind.Instruction, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Kind);
            Assert.AreEqual(instruction, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Instruction);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void Move_TopLevelToContainedEmpty_Succeeds()
        {
            InstructionInstance instruction = NewInstruction(10);
            StructureInstance structure = NewStructure(20, _repeatTwo);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Instr(3, instruction), Empty(4), Header(5, structure), Contained(6, structure), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(3), new SourcePosition(6));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Kind);
            SourceSlot moved = state.CurrentArrangement.SlotAt(new SourcePosition(6));
            Assert.AreEqual(SourceSlotKind.ContainedInstruction, moved.Kind);
            Assert.AreEqual(instruction, moved.Instruction);
            Assert.AreEqual(structure, moved.Structure);
        }

        [Test]
        public void Move_ContainedToTopLevel_Succeeds()
        {
            InstructionInstance instruction = NewInstruction(10);
            StructureInstance structure = NewStructure(20, _repeatTwo);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Header(3, structure), ContainedInstr(4, structure, instruction), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(4), new SourcePosition(7));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.ContainedEmpty, state.CurrentArrangement.SlotAt(new SourcePosition(4)).Kind);
            SourceSlot moved = state.CurrentArrangement.SlotAt(new SourcePosition(7));
            Assert.AreEqual(SourceSlotKind.Instruction, moved.Kind);
            Assert.AreEqual(instruction, moved.Instruction);
        }

        [Test]
        public void Move_WholeStructure_PreservesContainedInstruction()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            InstructionInstance child = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Header(3, structure), ContainedInstr(4, structure, child), Contained(5, structure), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(3), new SourcePosition(6));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Kind);
            Assert.AreEqual(SourceSlotKind.StructureHeader, state.CurrentArrangement.SlotAt(new SourcePosition(6)).Kind);
            SourceSlot movedChild = state.CurrentArrangement.SlotAt(new SourcePosition(7));
            Assert.AreEqual(SourceSlotKind.ContainedInstruction, movedChild.Kind);
            Assert.AreEqual(child, movedChild.Instruction);
            Assert.AreEqual(structure, movedChild.Structure);
            Assert.AreEqual(SourceSlotKind.ContainedEmpty, state.CurrentArrangement.SlotAt(new SourcePosition(8)).Kind);
        }

        [Test]
        public void Move_EmptyOrigin_OriginEmpty()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(3), new SourcePosition(5));

            AssertRejected(result, SourceEditRejection.OriginEmpty, state, arrangement);
        }

        [Test]
        public void Move_ToOccupied_DestinationNotEmpty()
        {
            InstructionInstance first = NewInstruction(10);
            InstructionInstance second = NewInstruction(11);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Instr(3, first), Empty(4), Instr(5, second), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(3), new SourcePosition(5));

            AssertRejected(result, SourceEditRejection.DestinationNotEmpty, state, arrangement);
        }

        [Test]
        public void Move_CoreOrigin_CoreImmutable()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(1), new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.CoreImmutable, state, arrangement);
        }

        [Test]
        public void Move_WholeStructureWithoutContiguousEmpties_InsufficientFootprint()
        {
            StructureInstance structure = NewStructure(20, _repeatThree);
            InstructionInstance blocker = NewInstruction(11);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Header(3, structure), Contained(4, structure), Contained(5, structure), Empty(6), Instr(7, blocker), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryMove(new SourcePosition(3), new SourcePosition(6));

            AssertRejected(result, SourceEditRejection.InsufficientFootprint, state, arrangement);
        }

        // ----- Swap -----

        [Test]
        public void Swap_InstructionInstruction_Succeeds()
        {
            InstructionInstance first = NewInstruction(10);
            InstructionInstance second = NewInstruction(11);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Instr(3, first), Empty(4), Instr(5, second), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(5));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(second, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Instruction);
            Assert.AreEqual(first, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Instruction);
        }

        [Test]
        public void Swap_TopLevelAndContainedInstruction_Succeeds()
        {
            InstructionInstance top = NewInstruction(10);
            InstructionInstance child = NewInstruction(11);
            StructureInstance structure = NewStructure(20, _repeatTwo);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Instr(3, top), Empty(4), Header(5, structure), ContainedInstr(6, structure, child), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(6));

            Assert.IsTrue(result.Succeeded);
            SourceSlot topSlot = state.CurrentArrangement.SlotAt(new SourcePosition(3));
            Assert.AreEqual(SourceSlotKind.Instruction, topSlot.Kind);
            Assert.AreEqual(child, topSlot.Instruction);
            SourceSlot childSlot = state.CurrentArrangement.SlotAt(new SourcePosition(6));
            Assert.AreEqual(SourceSlotKind.ContainedInstruction, childSlot.Kind);
            Assert.AreEqual(top, childSlot.Instruction);
            Assert.AreEqual(structure, childSlot.Structure);
        }

        [Test]
        public void Swap_StructureStructureEqualFootprint_Succeeds()
        {
            StructureInstance first = NewStructure(20, _repeatTwo);
            StructureInstance second = NewStructure(21, _repeatTwo);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(
                Player(Header(3, first), Contained(4, first), Header(5, second), Contained(6, second), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(5));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(second, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Structure);
            Assert.AreEqual(first, state.CurrentArrangement.SlotAt(new SourcePosition(5)).Structure);
        }

        [Test]
        public void Swap_StructureAndSingleInstruction_IncompatibleSwap()
        {
            StructureInstance structure = NewStructure(20, _repeatTwo);
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Header(3, structure), Contained(4, structure), Instr(5, instruction), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(5));

            AssertRejected(result, SourceEditRejection.IncompatibleSwap, state, arrangement);
        }

        [Test]
        public void Swap_StructuresUnequalFootprint_IncompatibleSwap()
        {
            StructureInstance small = NewStructure(20, _repeatTwo);
            StructureInstance large = NewStructure(21, _repeatThree);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Header(3, small), Contained(4, small), Header(5, large), Contained(6, large), Contained(7, large), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(5));

            AssertRejected(result, SourceEditRejection.IncompatibleSwap, state, arrangement);
        }

        [Test]
        public void Swap_WithEmpty_IncompatibleSwap()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Instr(3, instruction), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(5));

            AssertRejected(result, SourceEditRejection.IncompatibleSwap, state, arrangement);
        }

        [Test]
        public void Swap_WithCore_CoreImmutable()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = Player(
                Instr(3, instruction), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TrySwap(new SourcePosition(3), new SourcePosition(1));

            AssertRejected(result, SourceEditRejection.CoreImmutable, state, arrangement);
        }

        // ----- Remove -----

        [Test]
        public void Remove_Instruction_Succeeds()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(5);
            BuildState state = new BuildState(
                Player(Instr(3, instruction), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(3));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Kind);
            CollectionAssert.AreEqual(new[] { instruction }, buffer.AcceptedInstructions);
        }

        [Test]
        public void Remove_ContainedInstruction_Succeeds()
        {
            StructureInstance structure = NewStructure(20, _repeatTwo);
            InstructionInstance child = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(5);
            BuildState state = new BuildState(
                Player(Header(3, structure), ContainedInstr(4, structure, child), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(4));

            Assert.IsTrue(result.Succeeded);
            SourceSlot vacated = state.CurrentArrangement.SlotAt(new SourcePosition(4));
            Assert.AreEqual(SourceSlotKind.ContainedEmpty, vacated.Kind);
            Assert.AreEqual(structure, vacated.Structure);
            CollectionAssert.AreEqual(new[] { child }, buffer.AcceptedInstructions);
        }

        [Test]
        public void Remove_EmptyStructure_Succeeds()
        {
            StructureInstance structure = NewStructure(20, _repeatTwo);
            FakeBuildBuffer buffer = new FakeBuildBuffer(5);
            BuildState state = new BuildState(
                Player(Header(3, structure), Contained(4, structure), Empty(5), Empty(6), Empty(7), Empty(8)),
                buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(3));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(3)).Kind);
            Assert.AreEqual(SourceSlotKind.Empty, state.CurrentArrangement.SlotAt(new SourcePosition(4)).Kind);
            CollectionAssert.AreEqual(new[] { structure }, buffer.AcceptedStructures);
        }

        [Test]
        public void Remove_FullBuffer_BufferFull()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(0);
            SourceArrangement arrangement = Player(
                Instr(3, instruction), Empty(4), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.BufferFull, state, arrangement);
            CollectionAssert.IsEmpty(buffer.AcceptedInstructions);
        }

        [Test]
        public void Remove_NonEmptyStructure_InvalidContainment()
        {
            StructureInstance structure = NewStructure(20, _repeatTwo);
            InstructionInstance child = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(5);
            SourceArrangement arrangement = Player(
                Header(3, structure), ContainedInstr(4, structure, child), Empty(5), Empty(6), Empty(7), Empty(8));
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.InvalidContainment, state, arrangement);
            CollectionAssert.IsEmpty(buffer.AcceptedStructures);
        }

        [Test]
        public void Remove_EmptyOrigin_OriginEmpty()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(5);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(3));

            AssertRejected(result, SourceEditRejection.OriginEmpty, state, arrangement);
        }

        [Test]
        public void Remove_Core_CoreImmutable()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(5);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.TryRemove(new SourcePosition(1));

            AssertRejected(result, SourceEditRejection.CoreImmutable, state, arrangement);
        }

        // ----- BuildState invoker -----

        [Test]
        public void Apply_RejectedEdit_LeavesCurrentArrangementReferenceIdentical()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.Apply(new MoveEdit(new SourcePosition(3), new SourcePosition(5)));

            Assert.IsFalse(result.Succeeded);
            Assert.AreSame(arrangement, state.CurrentArrangement);
        }

        [Test]
        public void Apply_SuccessfulEdit_ReplacesCurrentArrangement()
        {
            InstructionInstance instruction = NewInstruction(10);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(instruction);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            BuildOperationResult result = state.Apply(new InstallEdit(new InstanceID(10), new SourcePosition(3)));

            Assert.IsTrue(result.Succeeded);
            Assert.AreNotSame(arrangement, state.CurrentArrangement);
        }

        // ----- Fixtures -----

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

        private static InstructionInstance NewInstruction(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _instructionDefinition, null);
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
