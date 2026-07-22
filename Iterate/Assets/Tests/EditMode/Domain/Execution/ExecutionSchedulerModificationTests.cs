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
    /// Tests the modification band with STANDARD LIBRARY installed on the §28.3 execution-1
    /// arrangements: the first eligible player fixed Value addition gains +1 folded into its own
    /// pending operation (12 strong / 7 weak), the qualification chain and modifier evidence carry
    /// the effect origin, near-misses carry the failed-to-qualify disposition contract, and the
    /// staged reaction boundary fails fast on any qualified reaction batch until the next card
    /// resolves it.
    /// </summary>
    public sealed class ExecutionSchedulerModificationTests
    {
        [Test]
        public void StrongOrder_FirstEligibleUnit_CarriesTheQualificationChain()
        {
            ExecutionRecord record = Execute(StrongRequest());

            RuntimeUnitRecord unit = record.Units[1];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(9, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationPending, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectQualified, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectCommitted, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationModified, events[4].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationResolved, events[5].Subtype);
            Assert.AreEqual(EventFamilies.Qualification, events[2].Family);
            Assert.AreEqual(EventFamilies.Qualification, events[3].Family);
            Assert.AreEqual(EventFamilies.Operation, events[4].Family);

            TraceEventID pending = unit.ChildEvents[1];
            for (int i = 2; i <= 4; i++)
            {
                Assert.AreEqual(pending, events[i].CausingEvent);
                Assert.AreEqual(1, events[i].CausalDepth);
                Assert.AreEqual(new InstanceID(100), events[i].EffectOriginInstance);
                Assert.AreEqual(unit.Identity, events[i].ContainingUnit);
            }
        }

        [Test]
        public void StrongOrder_ModifiedQuantity_CarriesTheModifierEvidence()
        {
            ExecutionRecord record = Execute(StrongRequest());

            List<EventEvidence> events = UnitEvents(record, record.Units[1]);
            QuantityChangePayload payload = (QuantityChangePayload)events[6].Payload;

            Assert.AreEqual(2, payload.RequestedAmount);
            Assert.AreEqual(1, payload.AppliedModifiers.Count);
            Assert.AreEqual(1, payload.AppliedModifiers[0].Amount);
            Assert.AreEqual("#100", payload.AppliedModifiers[0].ModifierIdentity);
            Assert.AreEqual(1, payload.PriorValue);
            Assert.AreEqual(3, payload.FinalDelta);
            Assert.AreEqual(4, payload.FinalValue);
        }

        [Test]
        public void StrongOrder_FinalOutputIsTwelve()
        {
            ExecutionRecord record = Execute(StrongRequest());

            Assert.AreEqual(new ScoreValue(12), record.FinalState.FinalOutput);
        }

        [Test]
        public void WeakOrder_FinalOutputIsSeven()
        {
            ExecutionRecord record = Execute(WeakRequest());

            Assert.AreEqual(new ScoreValue(7), record.FinalState.FinalOutput);
        }

        [Test]
        public void CoreAssignUnit_NearMisses_WithTheDispositionContract()
        {
            ExecutionRecord record = Execute(StrongRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(7, events.Count);
            EventEvidence nearMiss = events[2];
            Assert.AreEqual(EventFamilies.Qualification, nearMiss.Family);
            Assert.AreEqual(ExecutionEventSubtypes.EffectFailedToQualify, nearMiss.Subtype);
            Assert.AreEqual(EventDisposition.FailedToQualify, nearMiss.Disposition);
            Assert.AreEqual("OPERATION_CLASS:FIXED_ADDITION", nearMiss.DispositionReason);
            Assert.AreEqual(0, nearMiss.Qualifiers.Count);
            Assert.AreEqual(unit.ChildEvents[1], nearMiss.CausingEvent);
            Assert.AreEqual(1, nearMiss.CausalDepth);
            Assert.AreEqual(new InstanceID(100), nearMiss.EffectOriginInstance);
        }

        [Test]
        public void SecondFixedAddition_IsSilentlyUnmodified()
        {
            ExecutionRecord record = Execute(StrongRequest());

            List<EventEvidence> events = UnitEvents(record, record.Units[2]);

            Assert.AreEqual(6, events.Count);
            QuantityChangePayload payload = (QuantityChangePayload)events[3].Payload;
            Assert.AreEqual(0, payload.AppliedModifiers.Count);
            Assert.AreEqual(4, payload.PriorValue);
            Assert.AreEqual(6, payload.FinalValue);
        }

        [Test]
        public void ExactlyOneModification_PerExecution()
        {
            ExecutionRecord record = Execute(StrongRequest());

            int modified = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == ExecutionEventSubtypes.PrimaryOperationModified)
                    modified++;
            }

            Assert.AreEqual(1, modified);
            Assert.AreEqual(new SafetyCounts(0, 0, 5, 0, 1), record.SafetyCounts);
        }

        [Test]
        public void SecondExecute_ModifiesAgain()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = StrongRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(new ScoreValue(12), second.FinalState.FinalOutput);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void Header_CarriesTheInstalledInstance()
        {
            ExecutionRecord record = Execute(StrongRequest());

            Assert.AreEqual(1, record.Header.InstalledDependencyInstances.Count);
            Assert.AreEqual(new InstanceID(100), record.Header.InstalledDependencyInstances[0]);
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
        /// The §28.3 execution-1 strong arrangement with STANDARD LIBRARY installed as instance #100.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest StrongRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), CoreAssignValueOne()),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(ids.Next(), AddValueInstruction(2), null)),
                SourceSlot.ForInstruction(new SourcePosition(3), new InstructionInstance(ids.Next(), AddValueInstruction(2), null)),
                SourceSlot.ForInstruction(new SourcePosition(4), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForCore(new SourcePosition(7), CoreScoreAddValue())
            });

            return Request(arrangement, new List<DependencyInstance> { StandardLibraryInstance(100) });
        }

        /// <summary>
        /// The §28.3 execution-1 weak arrangement (Score += Value first) with STANDARD LIBRARY
        /// installed as instance #100.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest WeakRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), CoreAssignValueOne()),
                SourceSlot.ForInstruction(new SourcePosition(2), new InstructionInstance(ids.Next(), ScoreAddValueInstruction(), null)),
                SourceSlot.ForInstruction(new SourcePosition(3), new InstructionInstance(ids.Next(), AddValueInstruction(2), null)),
                SourceSlot.ForInstruction(new SourcePosition(4), new InstructionInstance(ids.Next(), AddValueInstruction(2), null)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForCore(new SourcePosition(7), CoreScoreAddValue())
            });

            return Request(arrangement, new List<DependencyInstance> { StandardLibraryInstance(100) });
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
        /// The Core line <c>01 Value = 1</c>.
        /// </summary>
        /// <returns>The Core line.</returns>
        private static CoreLine CoreAssignValueOne()
        {
            return new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
        }

        /// <summary>
        /// The Core line <c>07 Score += Value</c>.
        /// </summary>
        /// <returns>The Core line.</returns>
        private static CoreLine CoreScoreAddValue()
        {
            return new CoreLine("core-07", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));
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

            return Dependency(instance, "WB-DEP-001", new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION")));
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
