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
    /// enforces the content contract at the boundary: all six source slot kinds construct, while a null
    /// source and an installed Dependency the effect interpreter cannot honor are rejected rather than
    /// silently mis-executed. Interpretation order is pragmas, then installed Dependencies, then the
    /// arrangement's attached Patches in position order, with an uninterpretable Patch failing
    /// construction like any other installed content.
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
            3,
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
        public void Constructor_StructureBearingArrangement_Constructs()
        {
            CompiledSource source = CompiledFrom(StructureBearingArrangement());

            ExecutionRequest request = new(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies());

            Assert.AreSame(source, request.Source);
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
        public void Constructor_PragmaDirectives_InterpretAheadOfDependencies()
        {
            CompiledSource source = new CompiledSource(
                CoreAndInstructionArrangement(),
                new List<DirectiveInstance> { new DirectiveInstance(new InstanceID(31), OverclockDirective("WB-DIR-921")) },
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                new DependencyInstance(new InstanceID(12), ReactionDependency("WB-DEP-922"))
            };

            ExecutionRequest request = new(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), installed);

            Assert.AreEqual(2, request.InterpretedEffects.Count);
            Assert.AreEqual(new InstanceID(31), request.InterpretedEffects[0].Origin);
            Assert.AreEqual(ActiveEffectKind.AddedExecution, request.InterpretedEffects[0].Kind);
            Assert.AreEqual(new InstanceID(12), request.InterpretedEffects[1].Origin);
            Assert.AreEqual(ActiveEffectKind.Reaction, request.InterpretedEffects[1].Kind);
        }

        [Test]
        public void Constructor_UninterpretablePragma_Throws()
        {
            CompiledSource source = new CompiledSource(
                CoreAndInstructionArrangement(),
                new List<DirectiveInstance> { new DirectiveInstance(new InstanceID(32), PreFixAlignDirective()) },
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies()));
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

        [Test]
        public void Constructor_PatchedHosts_InterpretAfterDependenciesInArrangementOrder()
        {
            CompiledSource source = new CompiledSource(
                PatchedArrangement(),
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                new DependencyInstance(new InstanceID(12), ReactionDependency("WB-DEP-926"))
            };

            ExecutionRequest request = new(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), installed);

            Assert.AreEqual(3, request.InterpretedEffects.Count);
            Assert.AreEqual(new InstanceID(12), request.InterpretedEffects[0].Origin);
            Assert.AreEqual(new InstanceID(61), request.InterpretedEffects[1].Origin);
            Assert.AreEqual(new InstanceID(51), request.InterpretedEffects[1].HostInstance);
            Assert.AreEqual(new InstanceID(63), request.InterpretedEffects[2].Origin);
            Assert.AreEqual(new InstanceID(53), request.InterpretedEffects[2].HostInstance);
        }

        [Test]
        public void Constructor_ContainedPatchedInstruction_IsInterpreted()
        {
            CompiledSource source = new CompiledSource(
                ContainedPatchedArrangement(),
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            ExecutionRequest request = new(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies());

            Assert.AreEqual(1, request.InterpretedEffects.Count);
            Assert.AreEqual(new InstanceID(65), request.InterpretedEffects[0].Origin);
            Assert.AreEqual(new InstanceID(5), request.InterpretedEffects[0].HostInstance);
        }

        [Test]
        public void Constructor_UnpatchedArrangement_YieldsNoPatchEffects()
        {
            ExecutionRequest request = new(
                CompiledFrom(CoreAndInstructionArrangement()), ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies());

            Assert.AreEqual(0, request.InterpretedEffects.Count);
        }

        [Test]
        public void Constructor_UninterpretablePatch_Throws()
        {
            CompiledSource source = new CompiledSource(
                UninterpretablePatchArrangement(),
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            Assert.Throws<ArgumentException>(() => _ = new ExecutionRequest(
                source, ValidConfiguration(), ValidStamps(), ZeroInitialState(), NoDependencies()));
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
            InstructionInstance contained = new(new InstanceID(5), _instructionDefinition, null);

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), structure, contained),
                SourceSlot.ForContainedEmpty(new SourcePosition(4), structure)
            });
        }

        /// <summary>
        /// A Core line, an unpatched Instruction, then two patched Instructions at ascending
        /// positions, so arrangement order is observable in the interpreted list.
        /// </summary>
        /// <returns>The arrangement.</returns>
        private static SourceArrangement PatchedArrangement()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(new InstanceID(50), _instructionDefinition, null)),
                SourceSlot.ForInstruction(new SourcePosition(3), PatchedHost(51, 61)),
                SourceSlot.ForInstruction(new SourcePosition(4), PatchedHost(53, 63))
            });
        }

        /// <summary>
        /// A Structure whose contained Instruction carries a Patch, so contained hosts are shown to
        /// participate.
        /// </summary>
        /// <returns>The arrangement.</returns>
        private static SourceArrangement ContainedPatchedArrangement()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            StructureInstance structure = new(new InstanceID(4), _repeatDefinition);

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), core),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), structure, PatchedHost(5, 65)),
                SourceSlot.ForContainedEmpty(new SourcePosition(4), structure)
            });
        }

        /// <summary>
        /// An arrangement whose patched host carries a Patch declaring a socketed shape with no
        /// host-referential declaration, which the interpreter refuses.
        /// </summary>
        /// <returns>The arrangement.</returns>
        private static SourceArrangement UninterpretablePatchArrangement()
        {
            EffectDefinition effect = new(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Lifecycle,
                    "RUNTIME_UNIT_COMPLETED",
                    new List<TriggerQualifier> { new TriggerQualifier("POSITIONAL", "EVEN_NUMBERED_LINE") },
                    new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE")),
                new AddedExecutionRequestOperation(new TargetingRule("OWN_HOST", string.Empty), false),
                new TargetingRule("NO_TARGET", string.Empty),
                new EffectTiming(TimingKind.Band, "ADDED_EXECUTION_HANDLING"),
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "SOURCE_EXECUTION"));

            InstructionInstance host = new(
                new InstanceID(55),
                _instructionDefinition,
                new PatchInstance(new InstanceID(67), PatchWith("WB-PAT-908", effect)));

            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), host)
            });
        }

        /// <summary>
        /// Builds an Instruction instance carrying an ECHO-PATCH-shaped socketed Patch.
        /// </summary>
        /// <param name="hostInstanceID">The host's instance identity value.</param>
        /// <param name="patchInstanceID">The Patch's instance identity value.</param>
        /// <returns>The patched Instruction instance.</returns>
        private static InstructionInstance PatchedHost(int hostInstanceID, int patchInstanceID)
        {
            EffectDefinition effect = new(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Lifecycle,
                    "RUNTIME_UNIT_COMPLETED",
                    new List<TriggerQualifier> { new TriggerQualifier("POSITIONAL", "EVEN_NUMBERED_LINE") },
                    new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE")),
                new AddedExecutionRequestOperation(new TargetingRule("OWN_HOST", string.Empty), false),
                new TargetingRule("OWN_HOST", string.Empty),
                new EffectTiming(TimingKind.Band, "ADDED_EXECUTION_HANDLING"),
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "SOURCE_EXECUTION"));

            return new InstructionInstance(
                new InstanceID(hostInstanceID),
                _instructionDefinition,
                new PatchInstance(new InstanceID(patchInstanceID), PatchWith("WB-PAT-002", effect)));
        }

        /// <summary>
        /// Builds a frozen Patch definition carrying one effect.
        /// </summary>
        /// <param name="id">The definition's surrogate-key identity.</param>
        /// <param name="effect">The declarative effect.</param>
        /// <returns>The frozen definition.</returns>
        private static PatchDefinition PatchWith(string id, EffectDefinition effect)
        {
            return new PatchDefinition(
                new PatchID(id),
                "rules",
                "TEST PATCH",
                ContentCategory.Patch,
                Rarity.Common,
                Array.Empty<string>(),
                new PatchHostEligibility("ORDINARY_INSTRUCTION_HOSTS"),
                new List<EffectDefinition> { effect });
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

        private static DirectiveDefinition OverclockDirective(string id)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "VALUE"),
                    new TriggerQualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION")
                },
                timing);

            return new DirectiveDefinition(
                new DirectiveID(id),
                "Test rules.",
                "TEST DIRECTIVE",
                ContentCategory.Directive,
                Rarity.Uncommon,
                new List<string>(),
                new List<EffectDefinition>
                {
                    new EffectDefinition(
                        PhaseDomain.Execution,
                        trigger,
                        new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                        new TargetingRule("TRIGGERING_UNIT", string.Empty),
                        timing,
                        StackingMode.IndependentResolution,
                        new EffectFrequency("ONCE", "EXECUTION"))
                });
        }

        private static DirectiveDefinition PreFixAlignDirective()
        {
            EffectTiming timing = new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("PARITY", "ODD"),
                    new TriggerQualifier("REGISTER", "VALUE")
                },
                timing);

            return new DirectiveDefinition(
                new DirectiveID("WB-DIR-922"),
                "Test rules.",
                "TEST DIRECTIVE",
                ContentCategory.Directive,
                Rarity.Uncommon,
                new List<string>(),
                new List<EffectDefinition>
                {
                    new EffectDefinition(
                        PhaseDomain.Execution,
                        trigger,
                        new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                        new TargetingRule("NO_TARGET", string.Empty),
                        timing,
                        StackingMode.IndependentResolution,
                        new EffectFrequency("ONCE", "EXECUTION"))
                });
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
