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
    /// Tests depth-first descendant closure and the declared cross-batch offer order: SIGNAL ROUTER
    /// fires off the resolved operation (delta-insensitive), its Signal gain fires PARALLEL CHANNEL
    /// as a strictly deeper descendant resolved before the unit completes, the whole resolved-
    /// operation cascade precedes the primary-quantity batch's evidence in event order, and the
    /// all-five-installed fixture double-runs structurally equal.
    /// </summary>
    public sealed class ExecutionSchedulerDescendantTests
    {
        [Test]
        public void SignalRouter_FiresOffTheResolvedOperation_DespiteZeroDelta()
        {
            ExecutionRecord record = Execute(DescendantRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            QuantityChangePayload primary = (QuantityChangePayload)events[3].Payload;
            Assert.AreEqual(0, primary.FinalDelta);

            Assert.AreEqual(ExecutionEventSubtypes.EffectQualified, events[5].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectCommitted, events[6].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ImmediateReactionResolved, events[7].Subtype);
            for (int i = 5; i <= 7; i++)
            {
                Assert.AreEqual(unit.ChildEvents[2], events[i].CausingEvent);
                Assert.AreEqual(1, events[i].CausalDepth);
                Assert.AreEqual(new InstanceID(600), events[i].EffectOriginInstance);
            }

            QuantityChangePayload signalGain = (QuantityChangePayload)events[8].Payload;
            Assert.AreEqual("Signal", signalGain.QuantityIdentity);
            Assert.AreEqual(0, signalGain.PriorValue);
            Assert.AreEqual(1, signalGain.FinalValue);
            Assert.AreEqual(2, events[8].CausalDepth);
            Assert.AreEqual(unit.ChildEvents[7], events[8].CausingEvent);
        }

        [Test]
        public void SignalRouterQuantity_FiresParallelChannelAsDescendant()
        {
            ExecutionRecord record = Execute(DescendantRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(ExecutionEventSubtypes.EffectQualified, events[9].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectCommitted, events[10].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ImmediateReactionResolved, events[11].Subtype);
            for (int i = 9; i <= 11; i++)
            {
                Assert.AreEqual(unit.ChildEvents[8], events[i].CausingEvent);
                Assert.AreEqual(3, events[i].CausalDepth);
                Assert.AreEqual(new InstanceID(601), events[i].EffectOriginInstance);
            }

            QuantityChangePayload valueGain = (QuantityChangePayload)events[12].Payload;
            Assert.AreEqual("Value", valueGain.QuantityIdentity);
            Assert.AreEqual(0, valueGain.PriorValue);
            Assert.AreEqual(1, valueGain.FinalValue);
            Assert.AreEqual(4, events[12].CausalDepth);
            Assert.AreEqual(new InstanceID(601), events[12].EffectOriginInstance);

            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionCompleted, events[events.Count - 1].Subtype);
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(unit.Identity, events[i].ContainingUnit);
            }
        }

        [Test]
        public void CrossBatchOrder_ResolvedCascadePrecedesPrimaryQuantityNearMiss()
        {
            ExecutionRecord record = Execute(DescendantRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            int cascadeEnd = -1;
            int primaryNearMiss = -1;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == ExecutionEventSubtypes.QuantityChanged && events[i].CausalDepth == 4)
                    cascadeEnd = i;

                if (events[i].Subtype == ExecutionEventSubtypes.EffectFailedToQualify
                    && events[i].DispositionReason == "ACTUAL_DELTA_SIGN:POSITIVE")
                    primaryNearMiss = i;
            }

            Assert.GreaterOrEqual(cascadeEnd, 0);
            Assert.GreaterOrEqual(primaryNearMiss, 0);
            Assert.Less(cascadeEnd, primaryNearMiss);
            Assert.AreEqual(unit.ChildEvents[3], events[primaryNearMiss].CausingEvent);
            Assert.AreEqual(1, events[primaryNearMiss].CausalDepth);
        }

        [Test]
        public void Registers_AndSafety_ReflectTheCascade()
        {
            ExecutionRecord record = Execute(DescendantRequest());

            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
            Assert.AreEqual(new SignalValue(1), record.FinalState.FinalSignal);
            Assert.AreEqual(new ScoreValue(0), record.FinalState.FinalScore);
            Assert.AreEqual(new SafetyCounts(0, 0, 1, 2, 0), record.SafetyCounts);

            for (int i = 0; i < record.Events.Count; i++)
                Assert.AreEqual(SafetyStatus.Normal, record.Events[i].Evidence.SafetyStatus);
        }

        [Test]
        public void AllFiveInstalled_StrongOrder_SixteenAndDeterministic()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = AllFiveRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(new ScoreValue(16), first.FinalState.FinalOutput);
            Assert.AreEqual(2, first.SafetyCounts.EffectReactions);
            Assert.AreEqual(1, first.SafetyCounts.OperationTransformations);
            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// SIGNAL ROUTER (#600) and PARALLEL CHANNEL (#601) installed with one player
        /// <c>Value += Signal</c> Instruction and Signal starting at zero.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest DescendantRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), new InstructionInstance(ids.Next(), ValueAddSignalInstruction(), null))
            });

            return Request(arrangement, new List<DependencyInstance>
            {
                SignalRouterInstance(600),
                ParallelChannelInstance(601)
            });
        }

        /// <summary>
        /// All five Dependencies installed on the §28.3 execution-1 strong arrangement: STANDARD
        /// LIBRARY (#700), PARALLEL CHANNEL (#701), OUTPUT CACHE (#702), SIGNAL ROUTER (#703),
        /// OUTPUT PIPELINE (#704).
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest AllFiveRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)))),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(ids.Next(), AddValueInstruction(2), null)),
                SourceSlot.ForInstruction(new SourcePosition(3), new InstructionInstance(ids.Next(), AddValueInstruction(2), null)),
                SourceSlot.ForInstruction(new SourcePosition(4), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForCore(new SourcePosition(7), new CoreLine("core-07", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value))))
            });

            return Request(arrangement, new List<DependencyInstance>
            {
                StandardLibraryInstance(700),
                ParallelChannelInstance(701),
                OutputCacheInstance(702),
                SignalRouterInstance(703),
                OutputPipelineInstance(704)
            });
        }

        /// <summary>
        /// Executes a request on a fresh scheduler and builder.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Assembles a request over the arrangement with the given installed Dependencies.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest Request(SourceArrangement arrangement, List<DependencyInstance> installed)
        {
            CompiledSource source = new CompiledSource(
                arrangement,
                new List<DirectiveInstance>(),
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            return new ExecutionRequest(
                source,
                SchedulerFixtures.StandardConfiguration(),
                SchedulerFixtures.StandardStamps(),
                SchedulerFixtures.ZeroState(),
                installed);
        }

        /// <summary>
        /// The <c>Value += Signal</c> Instruction definition.
        /// </summary>
        /// <returns>The definition.</returns>
        private static InstructionDefinition ValueAddSignalInstruction()
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Signal)));
        }

        /// <summary>
        /// A fixed <c>Value += constant</c> Instruction definition.
        /// </summary>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The definition.</returns>
        private static InstructionDefinition AddValueInstruction(int constant)
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(constant)));
        }

        /// <summary>
        /// The <c>Score += Value</c> Instruction definition.
        /// </summary>
        /// <returns>The definition.</returns>
        private static InstructionDefinition ScoreAddValueInstruction()
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value)));
        }

        /// <summary>
        /// Wraps a primary operation in a minimal Instruction definition.
        /// </summary>
        /// <param name="operation">The primary operation.</param>
        /// <returns>The definition.</returns>
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

        /// <summary>
        /// A STANDARD-LIBRARY-shaped instance matching the fixed catalog record (WB-DEP-001).
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance StandardLibraryInstance(int instance)
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

            return Dependency(instance, "WB-DEP-001", Effect(trigger, CoreRegister.Value, 1, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A SIGNAL-ROUTER-shaped instance matching the shipped catalog record (WB-DEP-006).
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance SignalRouterInstance(int instance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_RESOLVED",
                new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", "VALUE_ADD_SIGNAL") },
                timing);

            return Dependency(instance, "WB-DEP-006", Effect(trigger, CoreRegister.Signal, 1, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A PARALLEL-CHANNEL-shaped instance matching the shipped catalog record (WB-DEP-004).
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance ParallelChannelInstance(int instance)
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

            return Dependency(instance, "WB-DEP-004", new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE")));
        }

        /// <summary>
        /// An OUTPUT-CACHE-shaped instance matching the shipped catalog record (WB-DEP-005).
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance OutputCacheInstance(int instance)
        {
            return ScoreReaction(instance, "WB-DEP-005", CoreRegister.Score, 2);
        }

        /// <summary>
        /// An OUTPUT-PIPELINE-shaped instance matching the shipped catalog record (WB-DEP-011).
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance OutputPipelineInstance(int instance)
        {
            return ScoreReaction(instance, "WB-DEP-011", CoreRegister.Value, 2);
        }

        /// <summary>
        /// A first-qualifying positive player-Instruction Score reaction shaped like the shipped
        /// records.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <param name="register">The register the reaction writes.</param>
        /// <param name="constant">The constant amount.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance ScoreReaction(
            int instance,
            string definitionID,
            CoreRegister register,
            int constant)
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

            return Dependency(instance, definitionID, Effect(trigger, register, constant, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// Builds an EXECUTION-domain constant-operand quantity-change effect.
        /// </summary>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="register">The register the operation writes.</param>
        /// <param name="constant">The constant operand.</param>
        /// <param name="allowance">The frequency-allowance token.</param>
        /// <param name="scope">The frequency-scope token.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition Effect(
            TriggerDescriptor trigger,
            CoreRegister register,
            int constant,
            string allowance,
            string scope)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(register, QuantityOperator.Add, OperandSpec.FromConstant(constant)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency(allowance, scope));
        }

        /// <summary>
        /// Wraps one effect in a Dependency definition and instance.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <param name="effect">The declared effect.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance Dependency(int instance, string definitionID, EffectDefinition effect)
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
        /// Resolves a unit's child events to their evidence in stream order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit whose events to resolve.</param>
        /// <returns>The unit's event evidence in order.</returns>
        private static List<EventEvidence> UnitEvents(ExecutionRecord record, RuntimeUnitRecord unit)
        {
            List<EventEvidence> events = new List<EventEvidence>(unit.ChildEvents.Count);
            for (int i = 0; i < unit.ChildEvents.Count; i++)
            {
                for (int j = 0; j < record.Events.Count; j++)
                {
                    if (record.Events[j].Identity == unit.ChildEvents[i])
                    {
                        events.Add(record.Events[j].Evidence);
                        break;
                    }
                }
            }

            Assert.AreEqual(unit.ChildEvents.Count, events.Count);
            return events;
        }
    }
}
