using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the read-only <see cref="BuildStatus"/> projection: the six independent UX-RUN-001 states and
    /// the UX-RUN-017 next-edited base-cost transparency (1/2/3 then undefined after the third), each derived
    /// from a pure preview.
    /// </summary>
    public sealed class BuildStatusTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", "Value = 1");
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", "Signal = 0");
        private static readonly CoreLine _coreNine = new CoreLine("core-09", "Score += Value");

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

        private static readonly DirectiveDefinition _directiveDefinition = new DirectiveDefinition(
            new DirectiveID("WB-DIR-004"),
            "Compile ahead",
            "COMPILE AHEAD",
            ContentCategory.Directive,
            Rarity.Common,
            Array.Empty<string>(),
            Array.Empty<EffectDefinition>());

        [Test]
        public void GetStatus_FreshState_NotCompiledNotChanged()
        {
            BuildState state = NewState(new FakeBuildBuffer(10));

            BuildStatus status = state.GetStatus(Bytes(10), NoEffects());

            Assert.IsFalse(status.SourceCompiled);
            Assert.IsFalse(status.SourceChanged);
            Assert.AreEqual(CompilationClassification.Initial, status.Classification);
        }

        [Test]
        public void GetStatus_AfterCommitNoEdits_SourceCompiled()
        {
            BuildState state = NewState(new FakeBuildBuffer(10));
            state.Compile(Bytes(10), NoEffects());

            BuildStatus status = state.GetStatus(Bytes(10), NoEffects());

            Assert.IsTrue(status.SourceCompiled);
            Assert.IsFalse(status.SourceChanged);
        }

        [Test]
        public void GetStatus_OrdinaryEdit_OrdinaryEditsPresentAndChanged()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(10), new SourcePosition(3));

            BuildStatus status = state.GetStatus(Bytes(10), NoEffects());

            Assert.IsTrue(status.SourceChanged);
            Assert.IsTrue(status.OrdinaryEditsPresent);
            Assert.IsFalse(status.FreeOnlyChangesPresent);
        }

        [Test]
        public void GetStatus_Overwrite_FreeOnlyChangesPresent()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewOverwrite(30));
            BuildState state = NewState(buffer);
            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            state.Compile(Bytes(10), NoEffects());
            state.TryOverwrite(new InstanceID(30), new SourcePosition(3));

            BuildStatus status = state.GetStatus(Bytes(10), NoEffects());

            Assert.IsTrue(status.FreeOnlyChangesPresent);
            Assert.IsFalse(status.OrdinaryEditsPresent);
            Assert.IsTrue(status.SourceChanged);
        }

        [Test]
        public void GetStatus_PendingPragma_True()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddDirective(new DirectiveInstance(new InstanceID(50), _directiveDefinition));
            BuildState state = NewState(buffer);
            state.TryActivateDirective(new InstanceID(50));

            BuildStatus status = state.GetStatus(Bytes(10), NoEffects());

            Assert.IsTrue(status.PendingDirectivePragma);
        }

        [Test]
        public void GetStatus_UnaffordableEdit_CompilationBlocked()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(10), new SourcePosition(3));

            BuildStatus status = state.GetStatus(Bytes(0), NoEffects());

            Assert.IsTrue(status.CompilationBlocked);
        }

        [Test]
        public void GetStatus_NextEditedBaseCost_OneTwoThreeThenUndefined()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewInstruction(11));
            buffer.AddInstruction(NewInstruction(12));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());

            BuildStatus atZero = state.GetStatus(Bytes(10), NoEffects());
            Assert.IsTrue(atZero.NextEditedBaseCostDefined);
            Assert.AreEqual(1, atZero.NextEditedBaseCost);

            CommitOrdinary(state, 10, 3);
            Assert.AreEqual(2, state.GetStatus(Bytes(10), NoEffects()).NextEditedBaseCost);

            CommitOrdinary(state, 11, 4);
            Assert.AreEqual(3, state.GetStatus(Bytes(10), NoEffects()).NextEditedBaseCost);

            CommitOrdinary(state, 12, 5);
            Assert.IsFalse(state.GetStatus(Bytes(10), NoEffects()).NextEditedBaseCostDefined);
        }

        private static void CommitOrdinary(BuildState state, int instructionIDValue, int line)
        {
            state.TryInstall(new InstanceID(instructionIDValue), new SourcePosition(line));
            state.Compile(Bytes(10), NoEffects());
        }

        private static BuildState NewState(FakeBuildBuffer buffer)
        {
            return new BuildState(StandardCore(), buffer, BuildParameters());
        }

        private static ByteAmount Bytes(int value)
        {
            return new ByteAmount(value);
        }

        private static IReadOnlyList<ActiveCompilationEffect> NoEffects()
        {
            return Array.Empty<ActiveCompilationEffect>();
        }

        private static InstructionInstance NewInstruction(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _ordinaryDefinition, null);
        }

        private static InstructionInstance NewOverwrite(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _overwriteDefinition, null);
        }

        private static SourceSlot Empty(int line)
        {
            return SourceSlot.ForEmpty(new SourcePosition(line));
        }

        private static SourceArrangement StandardCore()
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                Empty(3),
                Empty(4),
                Empty(5),
                Empty(6),
                Empty(7),
                Empty(8),
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }

        private static ParameterSet BuildParameters()
        {
            string[] ids =
            {
                "WB-PAR-001", "WB-PAR-002", "WB-PAR-003", "WB-PAR-004", "WB-PAR-005",
                "WB-PAR-006", "WB-PAR-007", "WB-PAR-008", "WB-PAR-009", "WB-PAR-010",
                "WB-PAR-011", "WB-PAR-012", "WB-PAR-013", "WB-PAR-014", "WB-PAR-015",
                "WB-PAR-016", "WB-PAR-017", "WB-PAR-018", "WB-PAR-019", "WB-PAR-020",
                "WB-PAR-021", "WB-PAR-022", "WB-PAR-023", "WB-PAR-024", "WB-PAR-026",
                "WB-PAR-028", "WB-PAR-029", "WB-PAR-030", "WB-PAR-035", "WB-PAR-036"
            };

            Dictionary<string, double> values = new Dictionary<string, double>();
            for (int i = 0; i < ids.Length; i++)
                values[ids[i]] = 1.0;

            values["WB-PAR-017"] = 0.0;
            values["WB-PAR-018"] = 0.0;
            values["WB-PAR-019"] = 1.0;
            values["WB-PAR-020"] = 2.0;
            values["WB-PAR-021"] = 3.0;
            return new ParameterSet(values);
        }
    }
}
