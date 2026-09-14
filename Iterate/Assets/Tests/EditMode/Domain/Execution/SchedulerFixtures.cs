using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Shared hand-built fixtures for the <see cref="ExecutionScheduler"/> tests: the Tutorial-Process-1
    /// shape (Core <c>Value = 1</c>, player Instructions, Core <c>Score += Value</c>) and a Pass-crossing
    /// arrangement, assembled with real <see cref="ExecutionRequest"/>/<see cref="CompiledSource"/> types.
    /// </summary>
    public static class SchedulerFixtures
    {
        /// <summary>
        /// The §28.3 Score bands: Pass 20, Optimize 30, Benchmark 36.
        /// </summary>
        /// <returns>The standard thresholds.</returns>
        public static ProcessThresholds StandardThresholds()
        {
            return new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));
        }

        /// <summary>
        /// The standard two-stamp revision list: the content catalog and the determinism revision.
        /// </summary>
        /// <returns>The standard revision stamps.</returns>
        public static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        /// <summary>
        /// The standard Process execution configuration carrying the seven header identities and the
        /// standard thresholds.
        /// </summary>
        /// <returns>The standard configuration.</returns>
        public static ProcessExecutionConfiguration StandardConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                StandardThresholds());
        }

        /// <summary>
        /// An initial register state with all three registers zero.
        /// </summary>
        /// <returns>The zero initial state.</returns>
        public static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        /// <summary>
        /// An initial register state seeded with the given register values.
        /// </summary>
        /// <param name="value">The initial Value register.</param>
        /// <param name="signal">The initial Signal register.</param>
        /// <param name="score">The initial Score register.</param>
        /// <returns>The seeded initial state.</returns>
        public static InitialExecutionState State(int value, int signal, int score)
        {
            return new InitialExecutionState(new ValueAmount(value), new SignalValue(signal), new ScoreValue(score));
        }

        /// <summary>
        /// The strong-order Tutorial-Process-1 request: Core <c>Value = 1</c> at 1, player <c>Value += 2</c>
        /// at 2 and 3, player <c>Score += Value</c> at 4, empties at 5 and 6, Core <c>Score += Value</c> at
        /// 7. Traverses to a final Score of 10 from a zero start.
        /// </summary>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="ids">The instance-identity source.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest StrongOrderRequest(InitialExecutionState initialState, InstanceIDSource ids)
        {
            CoreLine coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine coreSeven = new CoreLine("core-07", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

            InstructionInstance addA = new InstructionInstance(ids.Next(), AddValueInstruction(2), Array.Empty<PatchAttachment>());
            InstructionInstance addB = new InstructionInstance(ids.Next(), AddValueInstruction(2), Array.Empty<PatchAttachment>());
            InstructionInstance scoreAdd = new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), Array.Empty<PatchAttachment>());

            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), coreOne),
                SourceSlot.ForInstruction(new SourcePosition(2), addA),
                SourceSlot.ForInstruction(new SourcePosition(3), addB),
                SourceSlot.ForInstruction(new SourcePosition(4), scoreAdd),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForCore(new SourcePosition(7), coreSeven)
            });

            return BuildRequest(arrangement, initialState, ids);
        }

        /// <summary>
        /// A minimal request whose Score crosses Pass upward exactly once: Core <c>Value = 1</c> at 1,
        /// player <c>Value += 20</c> at 2, Core <c>Score += Value</c> at 3. From a zero start the Score
        /// event reads 0 → 21.
        /// </summary>
        /// <param name="ids">The instance-identity source.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest PassCrossingRequest(InstanceIDSource ids)
        {
            CoreLine coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine coreThree = new CoreLine("core-03", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

            InstructionInstance boost = new InstructionInstance(ids.Next(), AddValueInstruction(20), Array.Empty<PatchAttachment>());

            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), coreOne),
                SourceSlot.ForInstruction(new SourcePosition(2), boost),
                SourceSlot.ForCore(new SourcePosition(3), coreThree)
            });

            return BuildRequest(arrangement, ZeroState(), ids);
        }

        /// <summary>
        /// Assembles a request over an arbitrary hand-built arrangement with no installed
        /// Dependencies — the Structure-bearing fixture entry point.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="ids">The instance-identity source for the pragma.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest RequestOver(
            SourceArrangement arrangement,
            InitialExecutionState initialState,
            InstanceIDSource ids)
        {
            return RequestOver(arrangement, initialState, ids, new List<DependencyInstance>());
        }

        /// <summary>
        /// Assembles a request over an arbitrary hand-built arrangement with the given installed
        /// Dependency instances.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="ids">The instance-identity source for the pragma.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest RequestOver(
            SourceArrangement arrangement,
            InitialExecutionState initialState,
            InstanceIDSource ids,
            List<DependencyInstance> installed)
        {
            DirectiveInstance pragma = new DirectiveInstance(ids.Next(), StandardDirective());
            List<DirectiveInstance> pragmas = new List<DirectiveInstance> { pragma };
            CompiledSource source = new CompiledSource(arrangement, pragmas, StandardCost());

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), initialState, installed);
        }

        /// <summary>
        /// The standard configuration carrying a Process rule and an optional designated final Core
        /// output position. Built explicitly rather than with a <c>with</c> expression: every property
        /// on the configuration is redeclared get-only behind a validation initializer, so the record
        /// has no non-destructive mutation surface.
        /// </summary>
        /// <param name="rule">The Process-rule instance the execution is governed by.</param>
        /// <param name="designatedFinalCoreOutputPosition">The position whose activation closes the cooling window, or null.</param>
        /// <returns>The configuration.</returns>
        public static ProcessExecutionConfiguration ConfigurationWithRule(
            ProcessRuleInstance rule,
            SourcePosition? designatedFinalCoreOutputPosition)
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                StandardThresholds(),
                rule,
                designatedFinalCoreOutputPosition);
        }

        /// <summary>
        /// Assembles a request over an arbitrary arrangement under a caller-supplied configuration —
        /// the shape a Process rule needs, since the rule is carried on the configuration rather than
        /// installed like a Dependency.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="configuration">The Process configuration.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest RequestOver(
            SourceArrangement arrangement,
            InitialExecutionState initialState,
            ProcessExecutionConfiguration configuration)
        {
            CompiledSource source = new CompiledSource(arrangement, new List<DirectiveInstance>(), StandardCost());

            return new ExecutionRequest(source, configuration, StandardStamps(), initialState, new List<DependencyInstance>());
        }

        /// <summary>
        /// Assembles a request over an arbitrary arrangement under a caller-supplied configuration
        /// with installed Dependency instances and active Directive pragmas — the shape a Process
        /// rule needs alongside SAFE MODE, a creator, or a scoring reaction.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="configuration">The Process configuration.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <param name="pragmas">The active Directive pragmas.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest RequestOver(
            SourceArrangement arrangement,
            InitialExecutionState initialState,
            ProcessExecutionConfiguration configuration,
            List<DependencyInstance> installed,
            List<DirectiveInstance> pragmas)
        {
            CompiledSource source = new CompiledSource(arrangement, pragmas, StandardCost());

            return new ExecutionRequest(source, configuration, StandardStamps(), initialState, installed);
        }

        /// <summary>
        /// A THERMAL-THROTTLE-shaped Process-rule instance (WB-PRC-001): the pre-operation Heat gain
        /// on every qualifying multiplication, clamped to zero through three, and the cooling request
        /// on every positive Score change, clamped at zero.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Process-rule instance.</returns>
        public static ProcessRuleInstance ThermalThrottleRule(int instance)
        {
            EffectTiming preOperation = new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION");
            EffectDefinition gain = new EffectDefinition(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Operation,
                    "PRIMARY_OPERATION_PENDING",
                    new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", "MULTIPLY") },
                    preOperation),
                new CounterRequestOperation("HEAT", 1, 0, 3, true, true),
                new TargetingRule("NO_TARGET", string.Empty),
                preOperation,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            EffectTiming reaction = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            EffectDefinition cooling = new EffectDefinition(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Quantity,
                    "QUANTITY_CHANGED",
                    new List<TriggerQualifier>
                    {
                        new TriggerQualifier("REGISTER", "SCORE"),
                        new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE")
                    },
                    reaction),
                new CounterRequestOperation("HEAT", -1, 0, 0, true, false),
                new TargetingRule("NO_TARGET", string.Empty),
                reaction,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            ProcessRuleDefinition definition = new ProcessRuleDefinition(
                new ProcessRuleID("WB-PRC-001"),
                "Multiplication raises Heat; Score increases cool it.",
                "THERMAL THROTTLE",
                ContentCategory.ProcessRule,
                Rarity.Starter,
                new List<string> { "ProcessRule", "Heat" },
                new List<EffectDefinition> { gain, cooling });

            return new ProcessRuleInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// A player Instruction instance with a fixed <c>Value ×= constant</c> primary operation —
        /// the multiplication the Heat pre-check observes.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The Instruction instance.</returns>
        public static InstructionInstance ValueMultiplyInstance(int instance, int constant)
        {
            return new InstructionInstance(
                new InstanceID(instance),
                InstructionWith(new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Multiply, OperandSpec.FromConstant(constant))),
                Array.Empty<PatchAttachment>());
        }

        /// <summary>
        /// A player Instruction instance with a <c>Score += Value</c> primary operation — the scoring
        /// source a cooling request observes.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Instruction instance.</returns>
        public static InstructionInstance ScoreAddValueInstance(int instance)
        {
            return new InstructionInstance(new InstanceID(instance), ScoreAddValueInstruction(), Array.Empty<PatchAttachment>());
        }

        /// <summary>
        /// A Core line applying the given operator to Value with a constant operand.
        /// </summary>
        /// <param name="identity">The stable Core-line identity.</param>
        /// <param name="op">The operator the line applies.</param>
        /// <param name="constant">The constant operand.</param>
        /// <returns>The Core line.</returns>
        public static CoreLine ValueCoreLine(string identity, CoreLineOperator op, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(op, CoreRegister.Value, OperandSpec.FromConstant(constant)));
        }

        /// <summary>
        /// A Core line adding Value to Score — the intermediate or designated final Core output.
        /// </summary>
        /// <param name="identity">The stable Core-line identity.</param>
        /// <returns>The Core line.</returns>
        public static CoreLine ScoreOutputCoreLine(string identity)
        {
            return new CoreLine(
                identity,
                new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));
        }

        /// <summary>
        /// An OUTPUT-CACHE-shaped Directive instance: an every-qualifying reaction observing positive
        /// player-Instruction Score gains and adding the constant to Score. The operation-class
        /// qualifier admits only primary operations, so the reaction's own Score change cannot
        /// re-qualify it — one separate scoring reaction per player scoring unit.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="constant">The fixed operand the reaction adds to Score.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance ScoreGainReactionPragma(int instance, int constant)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "SCORE"),
                    new TriggerQualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION")
                },
                timing);

            return DirectiveOver(instance, "WB-DIR-904", ReactionEffect(trigger, CoreRegister.Score, constant));
        }

        /// <summary>
        /// A SAFE-MODE-shaped Dependency instance (WB-DEP-007): the first skipped source execution
        /// each execution is rescued to resolve normally — the skipped-execution disposition trigger
        /// at the pre-operation band, no qualifiers, resolving to RESCUED once per execution.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        public static DependencyInstance SafeModeInstance(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Disposition,
                "SOURCE_EXECUTION_SKIPPED",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"));

            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new RescueOperation("RESCUED"),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));

            return DependencyOver(instance, "WB-DEP-007", effect);
        }

        /// <summary>
        /// A STANDARD-LIBRARY-shaped Dependency instance (WB-DEP-001): the first player fixed Value
        /// addition gains +1 at the modification band.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        public static DependencyInstance StandardLibraryInstance(int instance)
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

            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));

            return DependencyOver(instance, "WB-DEP-001", effect);
        }

        /// <summary>
        /// A PARALLEL-CHANNEL-shaped Dependency instance (WB-DEP-004): every positive Signal delta
        /// adds 1 to Value at the immediate-reaction band.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        public static DependencyInstance ParallelChannelInstance(int instance)
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

            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            return DependencyOver(instance, "WB-DEP-004", effect);
        }

        /// <summary>
        /// Wraps one effect in a Dependency definition and instance.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <param name="effect">The declared effect.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance DependencyOver(int instance, string definitionID, EffectDefinition effect)
        {
            DependencyDefinition definition = new DependencyDefinition(
                new DependencyID(definitionID),
                "Test rules.",
                "TEST DEPENDENCY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new List<string>(),
                0,
                new List<EffectDefinition> { effect });

            return new DependencyInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// A frozen Repeat Structure instance with the given identity, count, and footprint.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="repeatCount">The authored iteration count.</param>
        /// <param name="sourceFootprint">The reserved source footprint including the header.</param>
        /// <returns>The Structure instance.</returns>
        public static StructureInstance RepeatStructure(int instance, int repeatCount, int sourceFootprint)
        {
            StructureDefinition definition = new StructureDefinition(
                new StructureID("WB-STR-FIXTURE-R"),
                "rules",
                "structure",
                ContentCategory.Structure,
                Rarity.Common,
                Array.Empty<string>(),
                sourceFootprint,
                StructureKind.Repeat,
                repeatCount,
                null);

            return new StructureInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// A frozen Condition Structure instance with the given identity, predicate, and footprint.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="register">The register the predicate reads.</param>
        /// <param name="comparison">The predicate comparison.</param>
        /// <param name="operand">The comparison operand; zero for parity.</param>
        /// <param name="sourceFootprint">The reserved source footprint including the header.</param>
        /// <returns>The Structure instance.</returns>
        public static StructureInstance ConditionStructure(
            int instance,
            CoreRegister register,
            PredicateComparison comparison,
            int operand,
            int sourceFootprint)
        {
            StructureDefinition definition = new StructureDefinition(
                new StructureID("WB-STR-FIXTURE-C"),
                "rules",
                "structure",
                ContentCategory.Structure,
                Rarity.Common,
                Array.Empty<string>(),
                sourceFootprint,
                StructureKind.Condition,
                0,
                new StructurePredicate(register, comparison, operand));

            return new StructureInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// A player Instruction instance with a fixed <c>Signal += constant</c> primary operation.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The Instruction instance.</returns>
        public static InstructionInstance SignalAddInstance(int instance, int constant)
        {
            return new InstructionInstance(
                new InstanceID(instance),
                InstructionWith(new QuantityChangeOperation(CoreRegister.Signal, QuantityOperator.Add, OperandSpec.FromConstant(constant))),
                Array.Empty<PatchAttachment>());
        }

        /// <summary>
        /// A player Instruction instance with a fixed <c>Value += constant</c> primary operation.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The Instruction instance.</returns>
        public static InstructionInstance ValueAddInstance(int instance, int constant)
        {
            return new InstructionInstance(new InstanceID(instance), AddValueInstruction(constant), Array.Empty<PatchAttachment>());
        }

        /// <summary>
        /// Assembles a request over a hand-built arrangement with extra active Directive pragmas
        /// ahead of the standard effect-free fixture pragma, plus installed Dependency instances.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="initialState">The initial register state.</param>
        /// <param name="ids">The instance-identity source for the standard pragma.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <param name="extraPragmas">The active Directive pragmas to interpret first.</param>
        /// <returns>The assembled request.</returns>
        public static ExecutionRequest RequestOver(
            SourceArrangement arrangement,
            InitialExecutionState initialState,
            InstanceIDSource ids,
            List<DependencyInstance> installed,
            List<DirectiveInstance> extraPragmas)
        {
            List<DirectiveInstance> pragmas = new List<DirectiveInstance>();
            for (int i = 0; i < extraPragmas.Count; i++)
            {
                pragmas.Add(extraPragmas[i]);
            }

            pragmas.Add(new DirectiveInstance(ids.Next(), StandardDirective()));
            CompiledSource source = new CompiledSource(arrangement, pragmas, StandardCost());

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), initialState, installed);
        }

        /// <summary>
        /// An OVERCLOCK-shaped Directive instance (WB-DIR-001, post-fix): the first positive player
        /// Value gain each execution requests one added execution of the triggering unit.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance OverclockPragma(int instance)
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

            return DirectiveOver(instance, "WB-DIR-001", CreatorEffect(trigger, "TRIGGERING_UNIT", "ONCE", "EXECUTION"));
        }

        /// <summary>
        /// An every-qualifying creator Directive instance: the same positive player Value-gain shape
        /// without the once-per-execution allowance, so only the origin lock can stop it — the
        /// termination probe.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance EveryQualifyingCreatorPragma(int instance)
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

            return DirectiveOver(instance, "WB-DIR-901", CreatorEffect(trigger, "TRIGGERING_UNIT", "EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// An every-qualifying reaction Directive instance observing positive player Value gains and
        /// adding the constant to Value. The operation-class qualifier admits only a primary
        /// operation of a player-owned unit, so the reaction's own quantity change — which is not
        /// from a primary operation — can never re-qualify it or its siblings: the fan-out is flat,
        /// one resolution per reaction instance per player unit, with no cascade.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="constant">The fixed operand the reaction adds to Value.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance ValueGainReactionPragma(int instance, int constant)
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

            return DirectiveOver(instance, "WB-DIR-903", ReactionEffect(trigger, CoreRegister.Value, constant));
        }

        /// <summary>
        /// An every-qualifying reaction Directive instance shaped exactly like
        /// <see cref="ValueGainReactionPragma"/> but observing positive player <em>Signal</em> gains
        /// — the failed-qualification probe against a Value-only arrangement.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="constant">The fixed operand the reaction adds to Value.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance SignalGainReactionPragma(int instance, int constant)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                    new TriggerQualifier("REGISTER", "SIGNAL"),
                    new TriggerQualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION")
                },
                timing);

            return DirectiveOver(instance, "WB-DIR-904", ReactionEffect(trigger, CoreRegister.Value, constant));
        }

        /// <summary>
        /// A LOOP-UNROLLER-shaped Dependency instance (WB-DEP-009): the first successful
        /// Repeat-context unit closure each execution requests one added execution of that unit.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        public static DependencyInstance LoopUnrollerInstance(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier> { new TriggerQualifier("STRUCTURE_CONTEXT", "INSIDE_REPEAT") },
                new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));

            return DependencyOver(instance, "WB-DEP-009", CreatorEffect(trigger, "TRIGGERING_UNIT", "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A BRANCH-PREDICTOR-shaped Dependency instance (WB-DEP-010): the first successful Condition
        /// evaluation each execution establishes a pending request for its first contained
        /// Instruction.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        public static DependencyInstance BranchPredictorInstance(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Structure,
                "CONDITION_TRUE",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));

            return DependencyOver(instance, "WB-DEP-010", CreatorEffect(trigger, "FIRST_CONTAINED_INSTRUCTION", "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// An ALIGN-shaped Directive instance (WB-DIR-002, post-fix): at the end-of-player-traversal
        /// boundary, an odd Value gains 1, once per execution.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance AlignPragma(int instance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Reaction,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("PARITY", "ODD"),
                    new TriggerQualifier("REGISTER", "VALUE")
                },
                timing);

            return DirectiveOver(instance, "WB-DIR-002", BoundaryEffect(trigger, timing, CoreRegister.Value, 1));
        }

        /// <summary>
        /// A fixture boundary Directive that adds 1 to Signal at the end-of-player-traversal boundary
        /// with no parity gate — the probe for source-less reaction resolution.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Directive instance.</returns>
        public static DirectiveInstance SignalBoundaryPragma(int instance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Reaction,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier> { new TriggerQualifier("REGISTER", "SIGNAL") },
                timing);

            return DirectiveOver(instance, "WB-DIR-902", BoundaryEffect(trigger, timing, CoreRegister.Signal, 1));
        }

        /// <summary>
        /// Builds an EXECUTION-domain boundary quantity-change effect resolving once per execution.
        /// </summary>
        /// <param name="trigger">The boundary trigger descriptor.</param>
        /// <param name="timing">The named-boundary timing.</param>
        /// <param name="register">The register the operation writes.</param>
        /// <param name="constant">The constant operand.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition BoundaryEffect(
            TriggerDescriptor trigger,
            EffectTiming timing,
            CoreRegister register,
            int constant)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(register, QuantityOperator.Add, OperandSpec.FromConstant(constant)),
                new TargetingRule("NO_TARGET", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "EXECUTION"));
        }

        /// <summary>
        /// Builds an EXECUTION-domain reaction effect at the immediate-result-reaction band whose
        /// operation adds the constant to the register, resolving on every qualifying event.
        /// </summary>
        /// <param name="trigger">The quantity trigger descriptor.</param>
        /// <param name="register">The register the reaction's operation writes.</param>
        /// <param name="constant">The constant operand.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition ReactionEffect(
            TriggerDescriptor trigger,
            CoreRegister register,
            int constant)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(register, QuantityOperator.Add, OperandSpec.FromConstant(constant)),
                new TargetingRule("NO_TARGET", string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// Builds an EXECUTION-domain added-execution-request effect.
        /// </summary>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="targeting">The added-execution targeting token.</param>
        /// <param name="allowance">The frequency-allowance token.</param>
        /// <param name="scope">The frequency-scope token.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition CreatorEffect(
            TriggerDescriptor trigger,
            string targeting,
            string allowance,
            string scope)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new AddedExecutionRequestOperation(new TargetingRule(targeting, string.Empty), false),
                new TargetingRule(targeting, string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency(allowance, scope));
        }

        /// <summary>
        /// Wraps one effect in a Directive definition and instance.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <param name="effect">The declared effect.</param>
        /// <returns>The Directive instance.</returns>
        private static DirectiveInstance DirectiveOver(int instance, string definitionID, EffectDefinition effect)
        {
            DirectiveDefinition definition = new DirectiveDefinition(
                new DirectiveID(definitionID),
                "Test rules.",
                "TEST DIRECTIVE",
                ContentCategory.Directive,
                Rarity.Uncommon,
                new List<string>(),
                new List<EffectDefinition> { effect });

            return new DirectiveInstance(new InstanceID(instance), definition);
        }

        private static ExecutionRequest BuildRequest(SourceArrangement arrangement, InitialExecutionState initialState, InstanceIDSource ids)
        {
            DirectiveInstance pragma = new DirectiveInstance(ids.Next(), StandardDirective());
            List<DirectiveInstance> pragmas = new List<DirectiveInstance> { pragma };
            CompiledSource source = new CompiledSource(arrangement, pragmas, StandardCost());

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), initialState, new List<DependencyInstance>());
        }

        private static InstructionDefinition AddValueInstruction(int constant)
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(constant)));
        }

        private static InstructionDefinition ScoreAddValueInstruction()
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value)));
        }

        private static InstructionDefinition InstructionWith(QuantityChangeOperation operation)
        {
            return new InstructionDefinition(
                new InstructionID("WB-INS-FIXTURE"),
                "rules",
                "instruction",
                ContentCategory.Instruction,
                Rarity.Common,
                Array.Empty<string>(),
                1,
                operation,
                null,
                Array.Empty<string>());
        }

        private static DirectiveDefinition StandardDirective()
        {
            return new DirectiveDefinition(
                new DirectiveID("WB-DIR-FIXTURE"),
                "rules",
                "directive",
                ContentCategory.Directive,
                Rarity.Common,
                Array.Empty<string>(),
                Array.Empty<EffectDefinition>());
        }

        private static CompilationCostBreakdown StandardCost()
        {
            return new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false);
        }
    }
}
