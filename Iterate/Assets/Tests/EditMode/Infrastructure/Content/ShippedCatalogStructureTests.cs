using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The child's feature-wrap conformance evidence over the real frozen catalog: shipped WB-STR-*
    /// Structures executed with real WB-DEP-001 and WB-DEP-007 installed — the selected-host
    /// observable on shipped content, both Condition results, the rescue path end-to-end, Repeat
    /// compounding, the empty-Structure lifecycle with past-footprint traversal, and double-run
    /// determinism over the richest arrangement.
    /// </summary>
    public sealed class ShippedCatalogStructureTests
    {
        private ContentCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            _catalog = loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void RepeatTwoWithStandardLibrary_ModifiesBothIterations_OneCommitment()
        {
            // WB-STR-001 Repeat 2 over WB-INS-002 (Value += 2) with WB-DEP-001:
            // iteration 1: 0 + 2 + 1 = 3 (commitment); iteration 2 re-applies: 3 + 2 + 1 = 6.
            StructureInstance structure = Structure("WB-STR-001", 10);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-001", 100)));

            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);
            Assert.AreEqual(2, record.Units.Count);

            List<QuantityChangePayload> payloads = InUnitQuantityPayloads(record);
            Assert.AreEqual(2, payloads.Count);
            Assert.AreEqual(1, payloads[0].AppliedModifiers.Count);
            Assert.AreEqual(3, payloads[0].FinalValue);
            Assert.AreEqual(1, payloads[1].AppliedModifiers.Count);
            Assert.AreEqual(6, payloads[1].FinalValue);

            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.EffectCommitted));
            AssertHealthyRun(record);
        }

        [Test]
        public void ConditionValueIsEven_TrueRun_ExecutesTheChild()
        {
            // WB-STR-002 If Value is even: Value 0 at entry (even) -> TRUE; child WB-INS-002: 0 + 2 = 2.
            StructureInstance structure = Structure("WB-STR-002", 10);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies()));

            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.ConditionTrue));
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ConditionFalse));
            Assert.AreEqual(1, record.Units.Count);
            Assert.AreEqual(EventDisposition.Resolved, record.Units[0].Closure.FinalDisposition);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            AssertHealthyRun(record);
        }

        [Test]
        public void ConditionValueIsEven_FalseRun_SkipsTheChild()
        {
            // Top-level WB-INS-001 makes Value 1 (odd) before entry -> FALSE; the child never adds:
            // final Value 1.
            StructureInstance structure = Structure("WB-STR-002", 10);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-001", ids)),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), structure, Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies()));

            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.ConditionTrue));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.ConditionFalse));
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[1].Closure.FinalDisposition);
            Assert.IsNull(record.Units[1].Closure.PrimaryOperationEvent);
            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
            AssertHealthyRun(record);
        }

        [Test]
        public void ConditionSignalAtLeastThree_FalseWithSafeMode_RescuesTheFirstSkipOnly()
        {
            // Two WB-STR-003 instances, Signal 0 at both entries -> both FALSE. SAFE MODE rescues the
            // first skipped child (WB-INS-002: 0 + 2 = 2) and is consumed; the second child
            // (WB-INS-003) stays skipped: final Value 2.
            StructureInstance first = Structure("WB-STR-003", 10);
            StructureInstance second = Structure("WB-STR-003", 11);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), first),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), first, Instruction("WB-INS-002", ids)),
                SourceSlot.ForStructureHeader(new SourcePosition(3), second),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), second, Instruction("WB-INS-003", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, Installed("WB-DEP-007", 200)));

            Assert.AreEqual(2, record.Units.Count);

            RuntimeUnitRecord rescued = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, rescued);
            int skip = IndexOf(events, ExecutionEventSubtypes.SourceExecutionSkipped);
            int qualified = IndexOf(events, ExecutionEventSubtypes.EffectQualified);
            int committed = IndexOf(events, ExecutionEventSubtypes.EffectCommitted);
            int rescuedEvent = IndexOf(events, ExecutionEventSubtypes.SourceExecutionRescued);
            int resolved = IndexOf(events, ExecutionEventSubtypes.PrimaryOperationResolved);
            Assert.Less(skip, qualified);
            Assert.Less(qualified, committed);
            Assert.Less(committed, rescuedEvent);
            Assert.Less(rescuedEvent, resolved);
            Assert.AreEqual(new InstanceID(200), events[qualified].EffectOriginInstance);
            Assert.AreEqual(EventDisposition.Rescued, rescued.Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, rescued.Closure.OriginalDisposition);
            Assert.IsNotNull(rescued.Closure.PrimaryOperationEvent);

            Assert.AreEqual(EventDisposition.Skipped, record.Units[1].Closure.FinalDisposition);
            Assert.IsNull(record.Units[1].Closure.OriginalDisposition);
            Assert.AreEqual(0, CountSubtype(record, ExecutionEventSubtypes.EffectFailedToQualify));
            Assert.AreEqual(2, CountSubtype(record, ExecutionEventSubtypes.ConditionFalse));
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            AssertHealthyRun(record);
        }

        [Test]
        public void RepeatThree_CompoundsPerIterationState()
        {
            // Top-level WB-INS-001: Value 1. WB-STR-006 Repeat 3 over WB-INS-005 (Value x2):
            // 1 -> 2 -> 4 -> 8.
            StructureInstance structure = Structure("WB-STR-006", 10);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Instruction("WB-INS-001", ids)),
                SourceSlot.ForStructureHeader(new SourcePosition(2), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), structure, Instruction("WB-INS-005", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies()));

            Assert.AreEqual(4, record.Units.Count);
            Assert.AreEqual(3, CountSubtype(record, ExecutionEventSubtypes.RepeatIterationStarted));
            Assert.AreEqual(3, CountSubtype(record, ExecutionEventSubtypes.RepeatIterationCompleted));

            List<QuantityChangePayload> payloads = InUnitQuantityPayloads(record);
            Assert.AreEqual(4, payloads.Count);
            Assert.AreEqual(1, payloads[0].FinalValue);
            Assert.AreEqual(2, payloads[1].FinalValue);
            Assert.AreEqual(4, payloads[2].FinalValue);
            Assert.AreEqual(8, payloads[3].FinalValue);
            Assert.AreEqual(new ValueAmount(8), record.FinalState.FinalValue);
            AssertHealthyRun(record);
        }

        [Test]
        public void EmptyShippedRepeat_FullLifecycle_TraversalResumesPastTheFootprint()
        {
            // WB-STR-001 Repeat 2 with its contained position empty, then a top-level WB-INS-002:
            // the full structural lifecycle, no child units, and the trailing Instruction runs: Value 2.
            StructureInstance structure = Structure("WB-STR-001", 10);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(2), structure),
                SourceSlot.ForInstruction(new SourcePosition(3), Instruction("WB-INS-002", ids))
            });

            ExecutionRecord record = Execute(Request(arrangement, NoDependencies()));

            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.StructureActivated));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.StructureEntered));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.RepeatCountCaptured));
            Assert.AreEqual(2, CountSubtype(record, ExecutionEventSubtypes.RepeatIterationStarted));
            Assert.AreEqual(2, CountSubtype(record, ExecutionEventSubtypes.RepeatIterationCompleted));
            Assert.AreEqual(1, CountSubtype(record, ExecutionEventSubtypes.StructureExited));

            Assert.AreEqual(1, record.Units.Count);
            Assert.IsNull(record.Units[0].Opening.StructureContext);
            Assert.AreEqual(3, record.Units[0].Opening.Position.Value.LineNumber);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
            AssertHealthyRun(record);
        }

        [Test]
        public void RichestArrangement_DoubleRun_IsStructurallyEqual()
        {
            // Repeat 2 over WB-INS-002 with WB-DEP-001: 0 -> 3 -> 6 (one commitment, one
            // re-application). WB-STR-003 FALSE over WB-INS-003 with WB-DEP-007: rescued, and the
            // selected host never migrates to the rescued Instruction: 6 + 3 = 9.
            ExecutionRequest request = RichestRequest();
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());

            ExecutionRecord firstRun = scheduler.Execute(request);
            ExecutionRecord secondRun = scheduler.Execute(request);

            Assert.AreEqual(new ValueAmount(9), firstRun.FinalState.FinalValue);
            Assert.AreEqual(EventDisposition.Rescued, firstRun.Units[2].Closure.FinalDisposition);
            Assert.AreEqual(2, CountSubtype(firstRun, ExecutionEventSubtypes.PrimaryOperationModified));
            Assert.AreEqual(2, CountSubtype(firstRun, ExecutionEventSubtypes.EffectCommitted));
            Assert.AreEqual(firstRun, secondRun);
        }

        /// <summary>
        /// The richest arrangement: WB-STR-001 (Repeat 2) over WB-INS-002, then WB-STR-003 over
        /// WB-INS-003, with real WB-DEP-001 (#100) and WB-DEP-007 (#200) installed.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest RichestRequest()
        {
            StructureInstance repeat = Structure("WB-STR-001", 10);
            StructureInstance condition = Structure("WB-STR-003", 11);
            InstanceIDSource ids = new InstanceIDSource();
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), repeat),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), repeat, Instruction("WB-INS-002", ids)),
                SourceSlot.ForStructureHeader(new SourcePosition(3), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), condition, Instruction("WB-INS-003", ids))
            });

            List<DependencyInstance> installed = new List<DependencyInstance>();
            installed.AddRange(Installed("WB-DEP-001", 100));
            installed.AddRange(Installed("WB-DEP-007", 200));
            return Request(arrangement, installed);
        }

        /// <summary>
        /// Wraps a real frozen Structure definition in an instance.
        /// </summary>
        /// <param name="id">The Structure's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Structure instance.</returns>
        private StructureInstance Structure(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetStructure(new StructureID(id), out StructureDefinition definition), id);
            return new StructureInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// Wraps a real frozen Instruction definition in an instance with the next identity.
        /// </summary>
        /// <param name="id">The Instruction's surrogate-key identity.</param>
        /// <param name="ids">The instance-identity source.</param>
        /// <returns>The Instruction instance.</returns>
        private InstructionInstance Instruction(string id, InstanceIDSource ids)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition), id);
            return new InstructionInstance(ids.Next(), definition, null);
        }

        /// <summary>
        /// One real frozen Dependency wrapped as a single-entry installed list.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The installed list.</returns>
        private List<DependencyInstance> Installed(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetDependency(new DependencyID(id), out DependencyDefinition definition), id);
            return new List<DependencyInstance> { new DependencyInstance(new InstanceID(instance), definition) };
        }

        /// <summary>
        /// The empty installed-Dependency list.
        /// </summary>
        /// <returns>The empty list.</returns>
        private static List<DependencyInstance> NoDependencies()
        {
            return new List<DependencyInstance>();
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

            return new ExecutionRequest(source, StandardConfiguration(), StandardStamps(), ZeroState(), installed);
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
        /// Counts the events carrying the subtype across the record.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token to count.</param>
        /// <returns>The number of matching events.</returns>
        private static int CountSubtype(ExecutionRecord record, string subtype)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Collects the in-unit QUANTITY payloads in record order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The quantity payloads in order.</returns>
        private static List<QuantityChangePayload> InUnitQuantityPayloads(ExecutionRecord record)
        {
            List<QuantityChangePayload> payloads = new List<QuantityChangePayload>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Family == EventFamilies.Quantity && evidence.ContainingUnit != null)
                    payloads.Add((QuantityChangePayload)evidence.Payload);
            }

            return payloads;
        }

        /// <summary>
        /// Returns the index of the first event carrying the subtype, failing when absent.
        /// </summary>
        /// <param name="events">The unit's events in stream order.</param>
        /// <param name="subtype">The subtype token to find.</param>
        /// <returns>The index of the first match.</returns>
        private static int IndexOf(List<EventEvidence> events, string subtype)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == subtype)
                    return i;
            }

            Assert.Fail($"No event with subtype {subtype} found in the unit stream.");
            return -1;
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
                events.Add(FindEvent(record, unit.ChildEvents[i]));
            }

            return events;
        }

        /// <summary>
        /// Finds one event's evidence by identity.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="identity">The event identity.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence FindEvent(ExecutionRecord record, TraceEventID identity)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Identity == identity)
                    return record.Events[i].Evidence;
            }

            throw new InvalidOperationException("The referenced event was not found in the record.");
        }

        /// <summary>
        /// Asserts the standing healthy-run contract: completed, valid, eligible, normal safety.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        private static void AssertHealthyRun(ExecutionRecord record)
        {
            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Eligible, record.HandoffStatus);
            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
        }

        /// <summary>
        /// The standard §28.3 configuration with thresholds 20 / 30 / 36.
        /// </summary>
        /// <returns>The configuration.</returns>
        private static ProcessExecutionConfiguration StandardConfiguration()
        {
            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36)));
        }

        /// <summary>
        /// The standard two-stamp revision list.
        /// </summary>
        /// <returns>The revision stamps.</returns>
        private static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        /// <summary>
        /// An initial register state with all three registers zero.
        /// </summary>
        /// <returns>The zero initial state.</returns>
        private static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }
    }
}
