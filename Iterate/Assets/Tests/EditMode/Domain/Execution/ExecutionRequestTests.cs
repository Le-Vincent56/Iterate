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
    /// Tests that <see cref="ExecutionRequest"/> validates its five components fully at construction and
    /// enforces the content contract at the boundary: an arrangement carrying any Structure-bearing slot
    /// and an installed Dependency the effect interpreter cannot honor are both rejected rather than
    /// silently mis-executed.
    /// </summary>
    public sealed class ExecutionRequestTests
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

        [Test]
        public void Constructor_ValidRequest_RoundTrips()
        {
            CompiledSource source = CompiledFrom(CoreAndInstructionArrangement());
            ProcessExecutionConfiguration configuration = ValidConfiguration();
            List<RevisionStamp> stamps = ValidStamps();
            InitialExecutionState initialState = ZeroInitialState();

            List<DependencyInstance> installed = new List<DependencyInstance>();
            ExecutionRequest request = new(source, configuration, stamps, initialState, installed);

            Assert.AreSame(source, request.Source);
            Assert.AreSame(configuration, request.Configuration);
            Assert.AreEqual(stamps, request.RevisionStamps);
            Assert.AreSame(initialState, request.InitialState);
            Assert.AreSame(installed, request.InstalledDependencies);
            Assert.AreEqual(0, request.InterpretedEffects.Count);
        }

        [Test]
        public void Constructor_NullSource_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                null, ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies()));
        }

        [Test]
        public void Constructor_NullConfiguration_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), null, ValidStamps(), ZeroInitialState(), NoDependencies()));
        }

        [Test]
        public void Constructor_NullInitialState_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), ValidStamps(), null, NoDependencies()));
        }

        [Test]
        public void Constructor_NullStamps_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), null, ZeroInitialState(), NoDependencies()));
        }

        [Test]
        public void Constructor_EmptyStamps_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), new List<RevisionStamp>(), ZeroInitialState(), NoDependencies()));
        }

        [Test]
        public void Constructor_StructureBearingArrangement_Throws()
        {
            CompiledSource source = CompiledFrom(StructureBearingArrangement());

            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies()));
        }

        [Test]
        public void Constructor_NullInstalledDependencies_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), ValidStamps(), ZeroInitialState(), null));
        }

        [Test]
        public void Constructor_UninterpretableDependency_Throws()
        {
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                new DependencyInstance(new InstanceID(10), RescueDependency())
            };

            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), ValidStamps(), ZeroInitialState(), installed));
        }

        [Test]
        public void Constructor_FiveDependencies_CarriesInterpretedEffects()
        {
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                new DependencyInstance(new InstanceID(11), ModificationDependency("WB-DEP-921")),
                new DependencyInstance(new InstanceID(12), ReactionDependency("WB-DEP-922")),
                new DependencyInstance(new InstanceID(13), ReactionDependency("WB-DEP-923")),
                new DependencyInstance(new InstanceID(14), ReactionDependency("WB-DEP-924")),
                new DependencyInstance(new InstanceID(15), ReactionDependency("WB-DEP-925"))
            };

            ExecutionRequest request = new(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), ValidStamps(), ZeroInitialState(), installed);

            Assert.AreEqual(5, request.InterpretedEffects.Count);
            Assert.AreEqual(new InstanceID(11), request.InterpretedEffects[0].Origin);
            Assert.IsTrue(request.InterpretedEffects[0].IsModification);
            Assert.AreEqual(new InstanceID(15), request.InterpretedEffects[4].Origin);
            Assert.IsFalse(request.InterpretedEffects[4].IsModification);
        }

        private static ProcessThresholds ValidThresholds()
        {
            return new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));
        }

        private static ProcessExecutionConfiguration ValidConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                ValidThresholds());
        }

        private static List<RevisionStamp> ValidStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        private static InitialExecutionState ZeroInitialState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        private static CompiledSource CompiledFrom(SourceArrangement arrangement)
        {
            return new CompiledSource(
                arrangement,
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));
        }

        private static SourceArrangement CoreAndInstructionArrangement()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            InstructionInstance instruction = new(new InstanceID(1), _instructionDefinition, null);

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForInstruction(new SourcePosition(2), instruction),
                SourceSlot.ForEmpty(new SourcePosition(3))
            });
        }

        private static SourceArrangement StructureBearingArrangement()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            StructureInstance structure = new(new InstanceID(4), _repeatDefinition);

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(3), structure)
            });
        }

        private static List<DependencyInstance> NoDependencies()
        {
            return new List<DependencyInstance>();
        }

        private static DependencyDefinition Dependency(string id, EffectDefinition effect)
        {
            return new DependencyDefinition(
                new DependencyID(id),
                "Test rules.",
                "TEST DEPENDENCY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new List<string>(),
                0,
                new List<EffectDefinition> { effect });
        }

        private static DependencyDefinition ReactionDependency(string id)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "SIGNAL")
                },
                timing);

            return Dependency(id, new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE")));
        }

        private static DependencyDefinition ModificationDependency(string id)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("OPERATION_CLASS", "FIXED_ADDITION"),
                    new TriggerQualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"),
                    new TriggerQualifier("REGISTER", "VALUE")
                },
                timing);

            return Dependency(id, new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION")));
        }

        private static DependencyDefinition RescueDependency()
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier> { new TriggerQualifier("REGISTER", "SCORE") },
                timing);

            return Dependency("WB-DEP-930", new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new RescueOperation("RESCUED"),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION")));
        }
    }
}
