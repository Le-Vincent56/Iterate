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
    /// The Epic's cross-child conformance milestone: the real frozen catalog (post-fix) executed over
    /// the Design §28.3 Core with the shipped STANDARD LIBRARY installed, asserting the canon-verbatim
    /// representative outputs 12 / 7 / 24 / 14 / 26 / 36 — including Benchmark 36's upward crossing,
    /// closing child iii's deferral — plus the all-five-Dependency scenario and determinism.
    /// </summary>
    public sealed class ShippedCatalogEffectSystemTests
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
        public void Execution1Strong_ScoresTwelve()
        {
            // Value 1 → 4 (STANDARD LIBRARY +1 on the first +2) → 6; player Score 6; Core 6 + 6 = 12.
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-012", null, null));

            Assert.AreEqual(new ScoreValue(12), record.FinalState.FinalOutput);
            Assert.AreEqual(0, record.ThresholdHistory.Count);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution1Weak_ScoresSeven()
        {
            // Score += Value first: Score 1; Value 1 → 4 → 6; Core 1 + 6 = 7.
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-012", "WB-INS-002", "WB-INS-002", null, null));

            Assert.AreEqual(new ScoreValue(7), record.FinalState.FinalOutput);
            Assert.AreEqual(0, record.ThresholdHistory.Count);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution2Strong_ScoresTwentyFour_CrossesPassOnly()
        {
            // Value 1 → 4 → 6 → 12; player Score 12; Core 12 + 12 = 24 crossing Pass upward.
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-005", "WB-INS-012", null));

            Assert.AreEqual(new ScoreValue(24), record.FinalState.FinalOutput);
            Assert.AreEqual(1, record.ThresholdHistory.Count);
            EventEvidence crossing = FindEvent(record, record.ThresholdHistory[0]);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossing.Subtype);
            Assert.AreEqual(ProcessThresholds.PassName, crossing.TargetIdentity);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution2Weak_ScoresFourteen()
        {
            // ×2 first: Value 1 → 2 → 5 (STANDARD LIBRARY on the first fixed add) → 7; Score 7; Core 14.
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-005", "WB-INS-002", "WB-INS-002", "WB-INS-012", null));

            Assert.AreEqual(new ScoreValue(14), record.FinalState.FinalOutput);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution3Average_ScoresTwentySix()
        {
            // Value 1 → 4 → 8 → 10 → 13; player Score 13; Core 13 + 13 = 26.
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-005", "WB-INS-002", "WB-INS-003", "WB-INS-012"));

            Assert.AreEqual(new ScoreValue(26), record.FinalState.FinalOutput);
            AssertHealthyRun(record);
        }

        [Test]
        public void Execution3Maximum_ScoresThirtySix_CrossesBenchmarkUpward()
        {
            // Value 1 → 4 → 6 → 9 → 18; player Score 18; Core 18 + 18 = 36 crossing Pass, Optimize, Benchmark.
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-003", "WB-INS-005", "WB-INS-012"));

            Assert.AreEqual(new ScoreValue(36), record.FinalState.FinalOutput);
            Assert.AreEqual(3, record.ThresholdHistory.Count);

            RuntimeUnitRecord coreUnit = record.Units[record.Units.Count - 1];
            string[] expectedBands = { ProcessThresholds.PassName, ProcessThresholds.OptimizeName, ProcessThresholds.BenchmarkName };
            for (int i = 0; i < 3; i++)
            {
                EventEvidence crossing = FindEvent(record, record.ThresholdHistory[i]);
                Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossing.Subtype);
                Assert.AreEqual(expectedBands[i], crossing.TargetIdentity);
                Assert.AreEqual(coreUnit.Identity, crossing.ContainingUnit);
            }

            AssertHealthyRun(record);
        }

        [Test]
        public void Execution1Strong_ModifiedUnit_CarriesTheRealChainAndModifier()
        {
            ExecutionRecord record = Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-012", null, null));

            RuntimeUnitRecord unit = record.Units[1];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(9, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.EffectQualified, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectCommitted, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationModified, events[4].Subtype);
            for (int i = 2; i <= 4; i++)
            {
                Assert.AreEqual(new InstanceID(100), events[i].EffectOriginInstance);
            }

            QuantityChangePayload payload = (QuantityChangePayload)events[6].Payload;
            Assert.AreEqual(2, payload.RequestedAmount);
            Assert.AreEqual(1, payload.AppliedModifiers.Count);
            Assert.AreEqual(1, payload.AppliedModifiers[0].Amount);
            Assert.AreEqual(new InstanceID(100).ToString(), payload.AppliedModifiers[0].ModifierIdentity);
            Assert.AreEqual(3, payload.FinalDelta);
        }

        [Test]
        public void StandardLibrary_FiresExactlyOncePerExecution_AcrossAllSixRuns()
        {
            ExecutionRecord[] records =
            {
                Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-012", null, null)),
                Execute(StandardLibraryRequest("WB-INS-012", "WB-INS-002", "WB-INS-002", null, null)),
                Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-005", "WB-INS-012", null)),
                Execute(StandardLibraryRequest("WB-INS-005", "WB-INS-002", "WB-INS-002", "WB-INS-012", null)),
                Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-005", "WB-INS-002", "WB-INS-003", "WB-INS-012")),
                Execute(StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-003", "WB-INS-005", "WB-INS-012"))
            };

            for (int i = 0; i < records.Length; i++)
            {
                int modified = 0;
                for (int j = 0; j < records[i].Events.Count; j++)
                {
                    if (records[i].Events[j].Evidence.Subtype == ExecutionEventSubtypes.PrimaryOperationModified)
                        modified++;
                }

                Assert.AreEqual(1, modified, $"run {i}");
            }
        }

        [Test]
        public void AllFiveInstalled_Execution1Strong_ScoresSixteen()
        {
            // Value 1 → 4 → 6; player Score 6; OUTPUT CACHE Score 6 → 8; OUTPUT PIPELINE Value 6 → 8;
            // Core Score += Value 8 + 8 = 16. No Signal content — PARALLEL CHANNEL and SIGNAL ROUTER silent.
            ExecutionRecord record = Execute(AllFiveRequest());

            Assert.AreEqual(new ScoreValue(16), record.FinalState.FinalOutput);
            Assert.AreEqual(2, record.SafetyCounts.EffectReactions);

            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.EffectQualified
                    && evidence.Subtype != ExecutionEventSubtypes.EffectCommitted
                    && evidence.Subtype != ExecutionEventSubtypes.ImmediateReactionResolved)
                    continue;

                Assert.AreNotEqual(new InstanceID(101), evidence.EffectOriginInstance, "PARALLEL CHANNEL must never fire");
                Assert.AreNotEqual(new InstanceID(103), evidence.EffectOriginInstance, "SIGNAL ROUTER must never fire");
            }

            RuntimeUnitRecord scoreUnit = record.Units[3];
            List<EventEvidence> events = UnitEvents(record, scoreUnit);
            int cacheReaction = IndexOfReaction(events, new InstanceID(102));
            int pipelineReaction = IndexOfReaction(events, new InstanceID(104));
            Assert.GreaterOrEqual(cacheReaction, 0);
            Assert.GreaterOrEqual(pipelineReaction, 0);
            Assert.Less(cacheReaction, pipelineReaction);

            AssertHealthyRun(record);
        }

        [Test]
        public void Execution3Maximum_DoubleRun_IsStructurallyEqual()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = StandardLibraryRequest("WB-INS-002", "WB-INS-002", "WB-INS-003", "WB-INS-005", "WB-INS-012");

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(new ScoreValue(36), first.FinalState.FinalOutput);
            Assert.AreEqual(first, second);
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
        /// The §28.3 request with the real STANDARD LIBRARY installed as instance #100.
        /// </summary>
        /// <param name="players">The five player-slot Instruction identities; null for empty.</param>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest StandardLibraryRequest(params string[] players)
        {
            return BuildRequest(players, new List<DependencyInstance> { Dependency("WB-DEP-001", 100) });
        }

        /// <summary>
        /// The §28.3 execution-1 strong request with all five shipped Dependencies installed
        /// (#100–#104 in catalog order).
        /// </summary>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest AllFiveRequest()
        {
            List<DependencyInstance> installed = new List<DependencyInstance>
            {
                Dependency("WB-DEP-001", 100),
                Dependency("WB-DEP-004", 101),
                Dependency("WB-DEP-005", 102),
                Dependency("WB-DEP-006", 103),
                Dependency("WB-DEP-011", 104)
            };

            return BuildRequest(new[] { "WB-INS-002", "WB-INS-002", "WB-INS-012", null, null }, installed);
        }

        /// <summary>
        /// Wraps a real frozen Dependency definition in an instance.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private DependencyInstance Dependency(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetDependency(new DependencyID(id), out DependencyDefinition definition), id);
            return new DependencyInstance(new InstanceID(instance), definition);
        }

        /// <summary>
        /// Assembles the §28.3 Core — <c>01 Value = 1</c>, five player slots, <c>07 Score += Value</c> —
        /// over real shipped Instructions with the given installed Dependencies.
        /// </summary>
        /// <param name="players">The five player-slot Instruction identities; null for empty.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <returns>The assembled request.</returns>
        private ExecutionRequest BuildRequest(string[] players, List<DependencyInstance> installed)
        {
            CoreLine coreOne = new CoreLine("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine coreSeven = new CoreLine("core-07", new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)));

            InstanceIDSource ids = new InstanceIDSource();
            List<SourceSlot> slots = new List<SourceSlot>();
            slots.Add(SourceSlot.ForCore(new SourcePosition(1), coreOne));
            for (int i = 0; i < players.Length; i++)
            {
                int position = i + 2;
                if (players[i] == null)
                {
                    slots.Add(SourceSlot.ForEmpty(new SourcePosition(position)));
                }
                else
                {
                    Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(players[i]), out InstructionDefinition definition), players[i]);
                    slots.Add(SourceSlot.ForInstruction(new SourcePosition(position), new InstructionInstance(ids.Next(), definition, null)));
                }
            }

            slots.Add(SourceSlot.ForCore(new SourcePosition(7), coreSeven));

            SourceArrangement arrangement = new SourceArrangement(slots);
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
        /// Asserts the standing healthy-run contract: completed, valid, eligible, normal safety.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        private static void AssertHealthyRun(ExecutionRecord record)
        {
            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Eligible, record.HandoffStatus);
            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
            Assert.LessOrEqual(record.SafetyCounts.SourceExecutionUnits, 7);
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
