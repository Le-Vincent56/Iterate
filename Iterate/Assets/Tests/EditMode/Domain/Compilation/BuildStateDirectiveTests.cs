using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests irreversible Directive activation on <see cref="BuildState"/>: a peeked Directive is taken
    /// from the buffer and appended to the activation-ordered pending-pragma list; a non-Directive gives
    /// NotADirective and an absent id gives ItemNotInBuffer; activation is not a source edit, so it leaves
    /// classification and the current arrangement untouched and pragmas persist across ordinary edits.
    /// </summary>
    public sealed class BuildStateDirectiveTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Signal, OperandSpec.FromConstant(0)));
        private static readonly CoreLine _coreNine = new CoreLine("core-09", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

        private static readonly DirectiveDefinition _directiveDefinition = new DirectiveDefinition(
            new DirectiveID("WB-DIR-004"),
            "Compile ahead",
            "Compile Ahead",
            ContentCategory.Directive,
            Rarity.Common,
            Array.Empty<string>(),
            Array.Empty<EffectDefinition>());

        private static readonly InstructionDefinition _instructionDefinition = new InstructionDefinition(
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

        [Test]
        public void TryActivateDirective_ValidDirective_AppendedAndTaken()
        {
            DirectiveInstance directive = NewDirective(50);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddDirective(directive);
            BuildState state = new BuildState(StandardCore(), buffer);

            BuildOperationResult result = state.TryActivateDirective(new InstanceID(50));

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.AreEqual(new[] { directive }, state.PendingPragmas);
            CollectionAssert.AreEqual(new[] { new InstanceID(50) }, buffer.TakenIDs);
        }

        [Test]
        public void TryActivateDirective_TwoActivations_PreserveOrder()
        {
            DirectiveInstance first = NewDirective(50);
            DirectiveInstance second = NewDirective(51);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddDirective(first);
            buffer.AddDirective(second);
            BuildState state = new BuildState(StandardCore(), buffer);

            state.TryActivateDirective(new InstanceID(50));
            state.TryActivateDirective(new InstanceID(51));

            CollectionAssert.AreEqual(new[] { first, second }, state.PendingPragmas);
        }

        [Test]
        public void TryActivateDirective_NonDirective_NotADirective()
        {
            InstructionInstance instruction = new InstructionInstance(new InstanceID(10), _instructionDefinition, null);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(instruction);
            BuildState state = new BuildState(StandardCore(), buffer);

            BuildOperationResult result = state.TryActivateDirective(new InstanceID(10));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(SourceEditRejection.NotADirective, result.Rejection);
            CollectionAssert.IsEmpty(state.PendingPragmas);
            CollectionAssert.IsEmpty(buffer.TakenIDs);
        }

        [Test]
        public void TryActivateDirective_AbsentItem_ItemNotInBuffer()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            BuildState state = new BuildState(StandardCore(), buffer);

            BuildOperationResult result = state.TryActivateDirective(new InstanceID(99));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(SourceEditRejection.ItemNotInBuffer, result.Rejection);
            CollectionAssert.IsEmpty(state.PendingPragmas);
        }

        [Test]
        public void TryActivateDirective_LeavesClassificationAndArrangementUnchanged()
        {
            DirectiveInstance directive = NewDirective(50);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddDirective(directive);
            SourceArrangement arrangement = StandardCore();
            BuildState state = new BuildState(arrangement, buffer);

            state.TryActivateDirective(new InstanceID(50));

            Assert.AreEqual(CompilationClassification.Initial, state.Classify());
            Assert.AreSame(arrangement, state.CurrentArrangement);
        }

        [Test]
        public void PendingPragmas_PersistAcrossOrdinaryEdit()
        {
            DirectiveInstance directive = NewDirective(50);
            InstructionInstance instruction = new InstructionInstance(new InstanceID(10), _instructionDefinition, null);
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddDirective(directive);
            buffer.AddInstruction(instruction);
            BuildState state = new BuildState(StandardCore(), buffer);

            state.TryActivateDirective(new InstanceID(50));
            state.TryInstall(new InstanceID(10), new SourcePosition(3));

            CollectionAssert.AreEqual(new[] { directive }, state.PendingPragmas);
        }

        private static DirectiveInstance NewDirective(int idValue)
        {
            return new DirectiveInstance(new InstanceID(idValue), _directiveDefinition);
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
    }
}
