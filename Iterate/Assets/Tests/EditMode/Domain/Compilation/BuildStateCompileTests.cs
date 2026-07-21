using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the atomic commit: the immutable CompiledSource snapshot, the blocked-commit contract (nothing
    /// mutates), the post-commit classification transitions, edited-compilation progression, pragma
    /// consumption into the CompiledSource, and first-qualifying ledger consumption. Fixtures mirror the
    /// shipped WB-DEP-002 (CLEAN BUILD) and WB-DIR-004 (COMPILE AHEAD) shapes.
    /// </summary>
    public sealed class BuildStateCompileTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Signal, OperandSpec.FromConstant(0)));
        private static readonly CoreLine _coreNine = new CoreLine("core-09", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

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

        [Test]
        public void Compile_InitialFreeBuild_CommitsAtZeroCost()
        {
            BuildState state = NewState(new FakeBuildBuffer(10));

            CompilationAttempt attempt = state.Compile(Bytes(10), NoEffects());

            Assert.IsTrue(attempt.Committed);
            Assert.AreEqual(CompilationClassification.Initial, attempt.Breakdown.Classification);
            Assert.AreEqual(0, attempt.Breakdown.FinalCost);
            Assert.AreSame(state.CurrentArrangement, attempt.Source.Arrangement);
            Assert.AreEqual(0, state.EditedCompilationCount);
        }

        [Test]
        public void Compile_UnchangedRecompile_CommitsFree()
        {
            BuildState state = NewState(new FakeBuildBuffer(10));
            state.Compile(Bytes(10), NoEffects());

            CompilationAttempt attempt = state.Compile(Bytes(10), NoEffects());

            Assert.IsTrue(attempt.Committed);
            Assert.AreEqual(CompilationClassification.Unchanged, attempt.Breakdown.Classification);
            Assert.AreEqual(0, attempt.Breakdown.FinalCost);
            Assert.AreEqual(0, state.EditedCompilationCount);
        }

        [Test]
        public void Compile_OrdinaryEdits_CostOneTwoThreeAndAdvance()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewInstruction(11));
            buffer.AddInstruction(NewInstruction(12));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());

            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            CompilationAttempt first = state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(11), new SourcePosition(4));
            CompilationAttempt second = state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(12), new SourcePosition(5));
            CompilationAttempt third = state.Compile(Bytes(10), NoEffects());

            Assert.AreEqual(1, first.Breakdown.FinalCost);
            Assert.AreEqual(2, second.Breakdown.FinalCost);
            Assert.AreEqual(3, third.Breakdown.FinalCost);
            Assert.AreEqual(3, state.EditedCompilationCount);
        }

        [Test]
        public void Classify_AfterCommitNoEdits_Unchanged()
        {
            BuildState state = NewState(new FakeBuildBuffer(10));
            state.Compile(Bytes(10), NoEffects());

            Assert.AreEqual(CompilationClassification.Unchanged, state.Classify());
        }

        [Test]
        public void Classify_ManualReversalAfterCommit_ClearsToUnchanged()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            BuildState state = NewState(buffer);
            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            state.Compile(Bytes(10), NoEffects());

            state.TryMove(new SourcePosition(3), new SourcePosition(5));
            state.TryMove(new SourcePosition(5), new SourcePosition(3));

            Assert.AreEqual(CompilationClassification.Unchanged, state.Classify());
        }

        [Test]
        public void Compile_OverwriteAfterCommit_FreeOnlyChangedDoesNotAdvance()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewOverwrite(30));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            state.Compile(Bytes(10), NoEffects());

            state.TryOverwrite(new InstanceID(30), new SourcePosition(3));
            Assert.AreEqual(CompilationClassification.FreeOnlyChanged, state.Classify());

            CompilationAttempt attempt = state.Compile(Bytes(10), NoEffects());

            Assert.IsTrue(attempt.Committed);
            Assert.IsFalse(attempt.Breakdown.AdvancesProgression);
            Assert.AreEqual(1, state.EditedCompilationCount);
        }

        [Test]
        public void Compile_OverwriteThenOrdinary_OrdinaryEditedAdvancesOnce()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewInstruction(11));
            buffer.AddInstruction(NewOverwrite(30));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            state.Compile(Bytes(10), NoEffects());

            state.TryOverwrite(new InstanceID(30), new SourcePosition(3));
            state.TryInstall(new InstanceID(11), new SourcePosition(4));
            Assert.AreEqual(CompilationClassification.OrdinaryEdited, state.Classify());

            CompilationAttempt attempt = state.Compile(Bytes(10), NoEffects());

            Assert.IsTrue(attempt.Breakdown.AdvancesProgression);
            Assert.AreEqual(2, state.EditedCompilationCount);
        }

        [Test]
        public void Compile_InsufficientBytes_BlocksAndMutatesNothing()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            SourceArrangement before = state.CurrentArrangement;

            CompilationAttempt blocked = state.Compile(Bytes(0), NoEffects());

            Assert.IsFalse(blocked.Committed);
            Assert.AreEqual(CompilationBlockReason.InsufficientBytes, blocked.BlockReason);
            AssertNothingMutated(state, before, 0, 0);

            CompilationAttempt later = state.Compile(Bytes(10), NoEffects());
            Assert.IsTrue(later.Committed);
            Assert.AreEqual(1, state.EditedCompilationCount);
        }

        [Test]
        public void Compile_FourthOrdinary_BlocksBaseCostUndefinedAndMutatesNothing()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewInstruction(11));
            buffer.AddInstruction(NewInstruction(12));
            buffer.AddInstruction(NewInstruction(13));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            CommitOrdinary(state, 10, 3);
            CommitOrdinary(state, 11, 4);
            CommitOrdinary(state, 12, 5);

            state.TryInstall(new InstanceID(13), new SourcePosition(6));
            SourceArrangement before = state.CurrentArrangement;

            CompilationAttempt blocked = state.Compile(Bytes(10), NoEffects());

            Assert.IsFalse(blocked.Committed);
            Assert.AreEqual(CompilationBlockReason.BaseCostUndefined, blocked.BlockReason);
            AssertNothingMutated(state, before, 3, 0);
        }

        [Test]
        public void Compile_CleanBuildZeroesFirstEdited_StillAdvances_ThenConsumed()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewInstruction(11));
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            ActiveCompilationEffect cleanBuild = CleanBuild(100);

            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            CompilationAttempt firstEdited = state.Compile(Bytes(10), new[] { cleanBuild });

            Assert.AreEqual(0, firstEdited.Breakdown.FinalCost);
            Assert.IsTrue(firstEdited.Breakdown.AdvancesProgression);
            Assert.AreEqual(1, state.EditedCompilationCount);

            state.TryInstall(new InstanceID(11), new SourcePosition(4));
            CompilationAttempt secondEdited = state.Compile(Bytes(10), new[] { cleanBuild });

            Assert.AreEqual(2, secondEdited.Breakdown.FinalCost);
            Assert.AreEqual(2, state.EditedCompilationCount);
        }

        [Test]
        public void Compile_PragmaEntersCompiledSourceAndClears()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            DirectiveInstance compileAhead = NewCompileAhead(50);
            buffer.AddDirective(compileAhead);
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());

            state.TryInstall(new InstanceID(10), new SourcePosition(3));
            state.TryActivateDirective(new InstanceID(50));
            CompilationAttempt attempt = state.Compile(Bytes(10), NoEffects());

            Assert.IsTrue(attempt.Committed);
            Assert.AreEqual(0, attempt.Breakdown.FinalCost);
            CollectionAssert.AreEqual(new[] { compileAhead }, attempt.Source.Pragmas);
            CollectionAssert.IsEmpty(state.PendingPragmas);
        }

        [Test]
        public void Compile_BlockedCommit_LeavesPragmasPending()
        {
            FakeBuildBuffer buffer = new FakeBuildBuffer(10);
            buffer.AddInstruction(NewInstruction(10));
            buffer.AddInstruction(NewInstruction(11));
            buffer.AddInstruction(NewInstruction(12));
            buffer.AddInstruction(NewInstruction(13));
            DirectiveInstance compileAhead = NewCompileAhead(50);
            buffer.AddDirective(compileAhead);
            BuildState state = NewState(buffer);
            state.Compile(Bytes(10), NoEffects());
            CommitOrdinary(state, 10, 3);
            CommitOrdinary(state, 11, 4);
            CommitOrdinary(state, 12, 5);

            state.TryInstall(new InstanceID(13), new SourcePosition(6));
            state.TryActivateDirective(new InstanceID(50));
            CompilationAttempt blocked = state.Compile(Bytes(10), NoEffects());

            Assert.IsFalse(blocked.Committed);
            Assert.AreEqual(CompilationBlockReason.BaseCostUndefined, blocked.BlockReason);
            CollectionAssert.AreEqual(new[] { compileAhead }, state.PendingPragmas);
        }

        private static void AssertNothingMutated(
            BuildState state,
            SourceArrangement expectedArrangement,
            int expectedCount,
            int expectedPragmas)
        {
            Assert.AreSame(expectedArrangement, state.CurrentArrangement);
            Assert.AreEqual(expectedCount, state.EditedCompilationCount);
            Assert.AreEqual(expectedPragmas, state.PendingPragmas.Count);
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

        private static ActiveCompilationEffect CleanBuild(int instanceIDValue)
        {
            return ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(instanceIDValue), "CLEAN BUILD", CleanBuildEffect());
        }

        private static InstructionInstance NewInstruction(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _ordinaryDefinition, null);
        }

        private static InstructionInstance NewOverwrite(int idValue)
        {
            return new InstructionInstance(new InstanceID(idValue), _overwriteDefinition, null);
        }

        private static DirectiveInstance NewCompileAhead(int idValue)
        {
            DirectiveDefinition definition = new DirectiveDefinition(
                new DirectiveID("WB-DIR-004"),
                "Compile ahead",
                "COMPILE AHEAD",
                ContentCategory.Directive,
                Rarity.Common,
                Array.Empty<string>(),
                new[] { CompileAheadEffect() });
            return new DirectiveInstance(new InstanceID(idValue), definition);
        }

        private static EffectDefinition CleanBuildEffect()
        {
            return new EffectDefinition(
                PhaseDomain.Compilation,
                new TriggerDescriptor(EventFamily.Lifecycle, "COMPILATION_COMMITTED", new[] { new TriggerQualifier("OPERATION_CLASS", "EDITED_COMPILATION") }, null),
                new CostModificationOperation("COMPILATION", true, 0, 0, true),
                null,
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS"));
        }

        private static EffectDefinition CompileAheadEffect()
        {
            return new EffectDefinition(
                PhaseDomain.Compilation,
                new TriggerDescriptor(EventFamily.Lifecycle, "COMPILATION_COMMITTED", Array.Empty<TriggerQualifier>(), null),
                new CostModificationOperation("COMPILATION", false, -1, 0, true),
                null,
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "COMPILATION"));
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
