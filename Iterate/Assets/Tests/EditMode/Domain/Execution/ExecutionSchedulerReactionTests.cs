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
    /// Tests immediate reactions inside unit closure: OUTPUT CACHE's full chain and consumption,
    /// PARALLEL CHANNEL's every-qualifying reactions with the zero-delta near-miss, the CAB-pinned
    /// OUTPUT CACHE → OUTPUT PIPELINE sibling order over instance allocation, threshold crossings
    /// caused by a reaction's own quantity event, and the reaction safety tally.
    /// </summary>
    public sealed class ExecutionSchedulerReactionTests
    {
        [Test]
        public void OutputCache_PlayerScoreGain_FiresTheFullChainInsideTheUnit()
        {
            ExecutionRecord record = Execute(OutputCacheRequest());

            RuntimeUnitRecord unit = record.Units[2];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(12, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationResultFinalized, events[5].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectQualified, events[6].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectCommitted, events[7].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ImmediateReactionResolved, events[8].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, events[9].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionCompleted, events[11].Subtype);
            Assert.AreEqual(EventFamilies.Reaction, events[8].Family);

            TraceEventID primaryQuantity = unit.ChildEvents[4];
            for (int i = 6; i <= 8; i++)
            {
                Assert.AreEqual(primaryQuantity, events[i].CausingEvent);
                Assert.AreEqual(1, events[i].CausalDepth);
                Assert.AreEqual(new InstanceID(300), events[i].EffectOriginInstance);
                Assert.AreEqual(unit.Identity, events[i].ContainingUnit);
            }

            EventEvidence reactionQuantity = events[9];
            Assert.AreEqual(unit.ChildEvents[8], reactionQuantity.CausingEvent);
            Assert.AreEqual(2, reactionQuantity.CausalDepth);
            Assert.AreEqual(new InstanceID(300), reactionQuantity.EffectOriginInstance);
            Assert.IsNull(reactionQuantity.Ownership);
            Assert.IsNull(reactionQuantity.HostInstance);
            Assert.IsNull(reactionQuantity.Position);

            QuantityChangePayload payload = (QuantityChangePayload)reactionQuantity.Payload;
            Assert.AreEqual("Score", payload.QuantityIdentity);
            Assert.AreEqual(2, payload.PriorValue);
            Assert.AreEqual(4, payload.FinalValue);
        }

        [Test]
        public void OutputCache_RegistersReflectBothWrites()
        {
            ExecutionRecord record = Execute(OutputCacheRequest());

            Assert.AreEqual(new ScoreValue(5), record.FinalState.FinalOutput);
            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
        }

        [Test]
        public void OutputCache_CoreOwnedScoreGain_NearMisses()
        {
            ExecutionRecord record = Execute(OutputCacheRequest());

            RuntimeUnitRecord unit = record.Units[1];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(9, events.Count);
            EventEvidence nearMiss = events[6];
            Assert.AreEqual(ExecutionEventSubtypes.EffectFailedToQualify, nearMiss.Subtype);
            Assert.AreEqual(EventDisposition.FailedToQualify, nearMiss.Disposition);
            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", nearMiss.DispositionReason);
            Assert.AreEqual(unit.ChildEvents[4], nearMiss.CausingEvent);
            Assert.AreEqual(1, nearMiss.CausalDepth);
        }

        [Test]
        public void OutputCache_SecondQualifyingGain_IsSilent()
        {
            ExecutionRecord record = Execute(OutputCacheRequest());

            List<EventEvidence> events = UnitEvents(record, record.Units[3]);

            Assert.AreEqual(8, events.Count);
        }

        [Test]
        public void OutputCache_SecondExecute_FiresAgain()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = OutputCacheRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(new ScoreValue(5), second.FinalState.FinalOutput);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void ParallelChannel_EveryPositiveSignalDelta_Reacts()
        {
            ExecutionRecord record = Execute(ParallelChannelRequest());

            List<EventEvidence> first = UnitEvents(record, record.Units[0]);
            List<EventEvidence> second = UnitEvents(record, record.Units[1]);

            Assert.AreEqual(ExecutionEventSubtypes.ImmediateReactionResolved, first[8].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ImmediateReactionResolved, second[8].Subtype);
            Assert.AreEqual(new SignalValue(2), record.FinalState.FinalSignal);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            Assert.AreEqual(2, record.SafetyCounts.EffectReactions);
        }

        [Test]
        public void ParallelChannel_ZeroSignalDelta_NearMisses()
        {
            ExecutionRecord record = Execute(ParallelChannelRequest());

            List<EventEvidence> events = UnitEvents(record, record.Units[2]);

            Assert.AreEqual(9, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.EffectFailedToQualify, events[6].Subtype);
            Assert.AreEqual("ACTUAL_DELTA_SIGN:POSITIVE", events[6].DispositionReason);
        }

        [Test]
        public void SiblingOrder_CacheChainPrecedesPipeline_DespiteInstanceOrder()
        {
            ExecutionRecord record = Execute(SiblingRequest());

            RuntimeUnitRecord unit = record.Units[1];
            List<EventEvidence> events = UnitEvents(record, unit);

            int cacheResolved = IndexOfReaction(events, new InstanceID(2));
            int pipelineQualified = -1;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == ExecutionEventSubtypes.EffectQualified
                    && events[i].EffectOriginInstance == new InstanceID(1))
                {
                    pipelineQualified = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(cacheResolved, 0);
            Assert.GreaterOrEqual(pipelineQualified, 0);
            Assert.Less(cacheResolved, pipelineQualified);

            int quantityEvents = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == ExecutionEventSubtypes.QuantityChanged && events[i].CausalDepth == 2)
                    quantityEvents++;
            }

            Assert.AreEqual(2, quantityEvents);
            Assert.AreEqual(new ScoreValue(3), record.FinalState.FinalOutput);
            Assert.AreEqual(new ValueAmount(3), record.FinalState.FinalValue);
        }

        [Test]
        public void ThresholdCrossing_CausedByTheReactionQuantityEvent()
        {
            ExecutionRecord record = Execute(ThresholdRequest());

            Assert.AreEqual(1, record.ThresholdHistory.Count);
            EventEvidence crossing = Find(record, record.ThresholdHistory[0]);
            RuntimeUnitRecord unit = record.Units[1];

            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossing.Subtype);
            Assert.AreEqual(unit.ChildEvents[9], crossing.CausingEvent);
            Assert.AreEqual(3, crossing.CausalDepth);

            EventEvidence causing = Find(record, unit.ChildEvents[9]);
            Assert.AreEqual(2, causing.CausalDepth);
            Assert.AreEqual(new InstanceID(500), causing.EffectOriginInstance);
        }

        [Test]
        public void EveryEvent_CarriesNormalSafety()
        {
            ExecutionRecord record = Execute(SiblingRequest());

            for (int i = 0; i < record.Events.Count; i++)
                Assert.AreEqual(SafetyStatus.Normal, record.Events[i].Evidence.SafetyStatus);

            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
        }

        /// <summary>
        /// Finds the IMMEDIATE_REACTION_RESOLVED index for the given effect origin.
        /// </summary>
        /// <param name="events">The unit's events.</param>
        /// <param name="origin">The effect origin to find.</param>
        /// <returns>The index, or -1.</returns>
        private static int IndexOfReaction(List<EventEvidence> events, InstanceID origin)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == ExecutionEventSubtypes.ImmediateReactionResolved
                    && events[i].EffectOriginInstance == origin)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// OUTPUT CACHE (#300) installed: Core Value = 1, Core Score += Value (near-miss), player
        /// Score += Value (fires), player Score += Value (consumed, silent).
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest OutputCacheRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)))),
                SourceSlot.ForCore(new SourcePosition(2), new CoreLine("core-02", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)))),
                SourceSlot.ForInstruction(new SourcePosition(3), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null)),
                SourceSlot.ForInstruction(new SourcePosition(4), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null))
            });

            return Request(arrangement, new List<DependencyInstance> { OutputCacheInstance(300) });
        }

        /// <summary>
        /// PARALLEL CHANNEL (#400) installed: two player Signal += 1 Instructions and one
        /// player Signal += 0.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ParallelChannelRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), new InstructionInstance(ids.Next(), SignalAddInstruction(1), null)),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(ids.Next(), SignalAddInstruction(1), null)),
                SourceSlot.ForInstruction(new SourcePosition(3), new InstructionInstance(ids.Next(), SignalAddInstruction(0), null))
            });

            return Request(arrangement, new List<DependencyInstance> { ParallelChannelInstance(400) });
        }

        /// <summary>
        /// OUTPUT PIPELINE allocated first (#1) and OUTPUT CACHE second (#2): Core Value = 1 then one
        /// player Score += Value both react to.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest SiblingRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)))),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null))
            });

            return Request(arrangement, new List<DependencyInstance>
            {
                OutputPipelineInstance(1),
                OutputCacheInstance(2)
            });
        }

        /// <summary>
        /// OUTPUT CACHE (#500) installed with Core Value = 19 then player Score += Value, so the
        /// reaction's +2 crosses Pass (19 → 21).
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ThresholdRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(19)))),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null))
            });

            return Request(arrangement, new List<DependencyInstance> { OutputCacheInstance(500) });
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
        /// The <c>Score += Value</c> Instruction definition.
        /// </summary>
        /// <returns>The definition.</returns>
        private static InstructionDefinition ScoreAddValueInstruction()
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value)));
        }

        /// <summary>
        /// A fixed <c>Signal += constant</c> Instruction definition.
        /// </summary>
        /// <param name="constant">The fixed operand.</param>
        /// <returns>The definition.</returns>
        private static InstructionDefinition SignalAddInstruction(int constant)
        {
            return InstructionWith(new QuantityChangeOperation(CoreRegister.Signal, QuantityOperator.Add, OperandSpec.FromConstant(constant)));
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

            return Dependency(instance, definitionID, new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(register, QuantityOperator.Add, OperandSpec.FromConstant(constant)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION")));
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
                events.Add(Find(record, unit.ChildEvents[i]));
            }

            return events;
        }

        /// <summary>
        /// Finds one event's evidence by identity.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="identity">The event identity.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence Find(ExecutionRecord record, TraceEventID identity)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Identity == identity)
                    return record.Events[i].Evidence;
            }

            Assert.Fail($"Event {identity} not found in the record.");
            return null;
        }
    }
}
