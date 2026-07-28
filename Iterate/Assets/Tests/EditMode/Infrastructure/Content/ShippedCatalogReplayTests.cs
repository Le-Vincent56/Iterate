using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The child's replay evidence over the real frozen catalog: five named fixtures are executed for a
    /// baseline record and then re-resolved through the replay comparer, which must report a match with
    /// a structurally equal recomputed record. This is the loop an interactive determinism harness would
    /// drive, run under CI-grade evidence on every test pass instead.
    /// The fixture register F1–F5 is duplicated by the Editor-side diagnostic fixture registry, because
    /// the Editor assembly cannot reference test assemblies. Both sides carry the same register and name
    /// their twin, so drift between them stays visible rather than silent.
    /// Nothing here is hand-authored content. F2–F5 reuse compositions already hand-traced by the
    /// standing Heat and Patch suites, and their traced registers are re-asserted here so a fixture
    /// cannot silently drift into a different composition while still replaying to a match.
    /// </summary>
    public sealed class ShippedCatalogReplayTests
    {
        /// <summary>
        /// The one-based position of the designated final Core output in the Design §28.9 Core.
        /// </summary>
        private const int DesignatedFinalOutput = 12;

        /// <summary>
        /// The instance identity the shipped Process rule is configured under.
        /// </summary>
        private const int RuleInstance = 900;

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
        public void F1_MinimalCore_ReplaysToMatch()
        {
            // F1 — the baseline sanity fixture: Core Value = 1, then Core Score += Value. No player
            // slots, no Dependencies, no pragmas. Value 1, Score 1.
            // Editor twin: DiagnosticFixtureRegistry "F1 Minimal Core".
            ExecutionRequest request = MinimalCoreRequest();
            ExecutionRecord baseline = Execute(request);

            Assert.AreEqual(new ValueAmount(1), baseline.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(1), baseline.FinalState.FinalScore);

            AssertReplaysToMatch(baseline, request);
        }

        [Test]
        public void F2_DesignCoreUnderThermalThrottle_ReplaysToMatch()
        {
            // F2 — the Design §28.9 Core under WB-PRC-001 THERMAL THROTTLE, six WB-INS-005
            // multiplications, no rescuer: the throttled run the Heat suite hand-traced. Lines 3/4/5
            // gain to Heat 3, lines 6/7/8 and Core line 9 skip, line 10 scores 8 and cools to 2, line 11
            // reaches Value 16, line 12 scores 24 without cooling. Value 16, Score 24.
            // Editor twin: DiagnosticFixtureRegistry "F2 Thermal Throttle".
            ExecutionRequest request = ThrottledCoreRequest();
            ExecutionRecord baseline = Execute(request);

            Assert.AreEqual(new ValueAmount(16), baseline.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(24), baseline.FinalState.FinalScore);

            AssertReplaysToMatch(baseline, request);
        }

        [Test]
        public void F3_BurstOutputBoundaryLock_ReplaysToMatch()
        {
            // F3 — the boundary-lock order signature the Patch suite hand-traced: Core Value = 3;
            // WB-INS-012 gains Score 0 → 3 and BURST OUTPUT locks it; the Burst branch re-reads the
            // pre-ALIGN Value 3 (Score 3 → 6); ALIGN then aligns Value 3 → 4; the trailing Core
            // Score += Value reads the aligned 4 (Score 6 → 10). Value 4, Score 10 — an ALIGN-first
            // ordering would land Score 11, so this register pair is the order evidence.
            // Editor twin: DiagnosticFixtureRegistry "F3 Burst Boundary Lock".
            ExecutionRequest request = BurstOrderSignatureRequest();
            ExecutionRecord baseline = Execute(request);

            Assert.AreEqual(new ValueAmount(4), baseline.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(10), baseline.FinalState.FinalScore);
            Assert.AreEqual(1, CountSubtype(baseline, ExecutionEventSubtypes.AddedExecutionTargetLocked));

            AssertReplaysToMatch(baseline, request);
        }

        [Test]
        public void F4_PatchDense_ReplaysToMatch()
        {
            // F4 — the Patch-dense fixture: all six frozen WB-PAT records across six distinct hosts,
            // every host-and-Patch pairing taken from a standing green single-Patch test, so each
            // pairing is individually known legal. Unlike F1–F3 and F5 its registers are deliberately
            // NOT hand-traced: no existing suite composes all six at once, and the replay assertion is
            // structural equality between a record and its own re-resolution, which no register value
            // participates in. Asserting an untraced number here would be a guess dressed as evidence.
            // Editor twin: DiagnosticFixtureRegistry "F4 Patch Dense".
            ExecutionRequest request = PatchDenseRequest();
            ExecutionRecord baseline = Execute(request);

            // The fixture really does carry Patch origins, read from event evidence — which is where a
            // Patch origin actually lands, as the Patch instance in EffectOriginInstance. The fixture
            // installs no Dependencies and activates no pragmas, so every effect origin in this record
            // is one of its own Patch instances.
            Assert.Greater(PatchOriginsPresent(baseline, PatchInstances()), 0, "the fixture must actually carry Patch origins");

            // The header's relevant-Patch list is empty, and that is not a property of this fixture:
            // the engine supplies an empty list for that field unconditionally, so it is empty for
            // every execution at current content however many Patches an arrangement carries. Pinned
            // here as absence rather than left unstated, so that wiring the field fails this line and
            // points at the comparer's RELEVANT_PATCHES correspondence component, which has no
            // request-reachable producer until that day.
            Assert.IsEmpty(baseline.Header.RelevantPatchInstances);

            AssertReplaysToMatch(baseline, request);
        }

        [Test]
        public void F5_AlignParityOddSeed_ReplaysToMatch()
        {
            // F5a — the odd arm of the ALIGN parity pair: the FEEDBACK-patched Score host gains
            // Score 0 → 1 and lifts Value 1 → 2; the Burst branch re-gains Score 1 → 3 and lifts
            // Value 2 → 3 (odd), so ALIGN's fresh offer qualifies → Value 4. Value 4, Score 3.
            // Editor twin: DiagnosticFixtureRegistry "F5 Align Parity (odd)".
            ExecutionRequest request = ParityFlipRequest(1);
            ExecutionRecord baseline = Execute(request);

            Assert.AreEqual(new ValueAmount(4), baseline.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(3), baseline.FinalState.FinalScore);

            AssertReplaysToMatch(baseline, request);
        }

        [Test]
        public void F5_AlignParityEvenSeed_ReplaysToMatch()
        {
            // F5b — the even arm: the Burst branch lifts Value 3 → 4 (even), so ALIGN's fresh offer
            // near-misses exactly once. Value 4, Score 5. The pair is the two-offer batch-discard
            // observable, which is why both seeds are replayed rather than one.
            // Editor twin: DiagnosticFixtureRegistry "F5 Align Parity (even)".
            ExecutionRequest request = ParityFlipRequest(2);
            ExecutionRecord baseline = Execute(request);

            Assert.AreEqual(new ValueAmount(4), baseline.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(5), baseline.FinalState.FinalScore);

            AssertReplaysToMatch(baseline, request);
        }

        [Test]
        public void PerturbedContentRevision_ReportsUnavailableAndWithholdsTheRecord()
        {
            // The CAB-EVT-770 arm on real content: the same composition re-resolved under a different
            // content-catalog revision is not a reproduction of the stored run, and the record computed
            // under it is withheld rather than offered as if it were.
            ExecutionRequest request = ThrottledCoreRequest();
            ExecutionRecord baseline = Execute(request);

            List<RevisionStamp> perturbed = new()
            {
                new RevisionStamp("Content Catalog", "0.2.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };

            ReplayComparisonResult result = new ReplayComparer().Compare(baseline, WithStamps(request, perturbed));

            Assert.AreEqual(ReplayComparisonStatus.ExactReproductionUnavailable, result.Status);
            Assert.AreEqual("REVISION_STAMP:Content Catalog", result.UnavailableComponent);
            Assert.IsNull(result.RecomputedRecord);
        }

        [Test]
        public void PerturbedSessionSeed_ReportsUnavailableAndWithholdsTheRecord()
        {
            ExecutionRequest request = ThrottledCoreRequest();
            ExecutionRecord baseline = Execute(request);

            ReplayComparisonResult result = new ReplayComparer().Compare(baseline, WithSessionSeed(request, "session-seed-B"));

            Assert.AreEqual(ReplayComparisonStatus.ExactReproductionUnavailable, result.Status);
            Assert.AreEqual("IDENTITY:SessionSeedIdentity", result.UnavailableComponent);
            Assert.IsNull(result.RecomputedRecord);
        }

        /// <summary>
        /// Replays the request through the comparer and asserts a match carrying a structurally equal
        /// recomputed record.
        /// </summary>
        /// <param name="baseline">The stored baseline record.</param>
        /// <param name="request">The request that produced it.</param>
        private static void AssertReplaysToMatch(ExecutionRecord baseline, ExecutionRequest request)
        {
            ReplayComparisonResult result = new ReplayComparer().Compare(baseline, request);

            Assert.AreEqual(ReplayComparisonStatus.Match, result.Status, DescribeDivergence(result));
            Assert.IsNotNull(result.RecomputedRecord);
            Assert.AreEqual(baseline, result.RecomputedRecord);
            Assert.IsNull(result.Divergence);
            Assert.IsNull(result.UnavailableComponent);
        }

        /// <summary>
        /// Renders a failed comparison's detail so a red reports its locus rather than only its status.
        /// </summary>
        /// <param name="result">The comparison result.</param>
        /// <returns>The failure message.</returns>
        private static string DescribeDivergence(ReplayComparisonResult result)
        {
            if (result.Divergence != null)
                return result.Divergence.Description;

            if (result.UnavailableComponent != null)
                return "exact reproduction unavailable: " + result.UnavailableComponent;

            return "matched";
        }

        private ExecutionRequest MinimalCoreRequest()
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AddRegister("core-02", CoreRegister.Score, CoreRegister.Value))
            });

            return Request(arrangement, NoDependencies(), NoPragmas(), StandardConfiguration());
        }

        private ExecutionRequest ThrottledCoreRequest()
        {
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 1)),
                SourceSlot.ForCore(new SourcePosition(2), AssignConstant("core-02", CoreRegister.Signal, 0))
            };

            for (int i = 0; i < 6; i++)
            {
                slots.Add(SourceSlot.ForInstruction(new SourcePosition(3 + i), Instruction("WB-INS-005", 10 + i)));
            }

            slots.Add(SourceSlot.ForCore(new SourcePosition(9), MultiplyConstant("core-09", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(10), AddRegister("core-10", CoreRegister.Score, CoreRegister.Value)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(11), MultiplyConstant("core-11", CoreRegister.Value, 2)));
            slots.Add(SourceSlot.ForCore(new SourcePosition(DesignatedFinalOutput), AddRegister("core-12", CoreRegister.Score, CoreRegister.Value)));

            return Request(new SourceArrangement(slots), NoDependencies(), NoPragmas(), ThrottleConfiguration());
        }

        private ExecutionRequest BurstOrderSignatureRequest()
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, 3)),
                SourceSlot.ForInstruction(new SourcePosition(2), Instruction("WB-INS-012", 20)),
                SourceSlot.ForCore(new SourcePosition(3), AddRegister("core-03", CoreRegister.Score, CoreRegister.Value))
            });

            return Request(arrangement, NoDependencies(), Pragmas("WB-DIR-003", 900, "WB-DIR-002", 950), StandardConfiguration());
        }

        private ExecutionRequest PatchDenseRequest()
        {
            StructureInstance condition = Structure("WB-STR-002", 110);
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), Patched("WB-INS-002", 10, "WB-PAT-001", 60)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-012", 20, "WB-PAT-002", 61)),
                SourceSlot.ForInstruction(new SourcePosition(3), Patched("WB-INS-002", 11, "WB-PAT-006", 62)),
                SourceSlot.ForInstruction(new SourcePosition(4), Patched("WB-INS-002", 12, "WB-PAT-003", 63)),
                SourceSlot.ForInstruction(new SourcePosition(5), Patched("WB-INS-012", 21, "WB-PAT-005", 64)),
                SourceSlot.ForStructureHeader(new SourcePosition(6), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(7), condition, Patched("WB-INS-002", 13, "WB-PAT-004", 65))
            });

            return Request(arrangement, NoDependencies(), NoPragmas(), StandardConfiguration());
        }

        private ExecutionRequest ParityFlipRequest(int seed)
        {
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), AssignConstant("core-01", CoreRegister.Value, seed)),
                SourceSlot.ForInstruction(new SourcePosition(2), Patched("WB-INS-012", 10, "WB-PAT-005", 60))
            });

            return Request(arrangement, NoDependencies(), Pragmas("WB-DIR-003", 900, "WB-DIR-002", 950), StandardConfiguration());
        }

        private ProcessExecutionConfiguration ThrottleConfiguration()
        {
            Assert.IsTrue(
                _catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-001"), out ProcessRuleDefinition definition),
                "WB-PRC-001");

            return new ProcessExecutionConfiguration(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                new ProcessThresholds(new ScoreValue(64), new ScoreValue(112), new ScoreValue(192)),
                new ProcessRuleInstance(new InstanceID(RuleInstance), definition),
                new SourcePosition(DesignatedFinalOutput));
        }

        private InstructionInstance Instruction(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(id), out InstructionDefinition definition), id);

            return new InstructionInstance(new InstanceID(instance), definition, null);
        }

        private InstructionInstance Patched(
            string instructionID,
            int hostInstance,
            string patchID,
            int patchInstance)
        {
            Assert.IsTrue(_catalog.TryGetInstruction(new InstructionID(instructionID), out InstructionDefinition definition), instructionID);
            Assert.IsTrue(_catalog.TryGetPatch(new PatchID(patchID), out PatchDefinition patch), patchID);

            return new InstructionInstance(new InstanceID(hostInstance), definition, new PatchInstance(new InstanceID(patchInstance), patch));
        }

        private StructureInstance Structure(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetStructure(new StructureID(id), out StructureDefinition definition), id);

            return new StructureInstance(new InstanceID(instance), definition);
        }

        private DirectiveInstance Directive(string id, int instance)
        {
            Assert.IsTrue(_catalog.TryGetDirective(new DirectiveID(id), out DirectiveDefinition definition), id);

            return new DirectiveInstance(new InstanceID(instance), definition);
        }

        private List<DirectiveInstance> Pragmas(
            string firstID,
            int firstInstance,
            string secondID,
            int secondInstance)
        {
            return new List<DirectiveInstance> { Directive(firstID, firstInstance), Directive(secondID, secondInstance) };
        }

        private static ExecutionRequest Request(
            SourceArrangement arrangement,
            List<DependencyInstance> installed,
            List<DirectiveInstance> pragmas,
            ProcessExecutionConfiguration configuration)
        {
            CompiledSource source = new(
                arrangement,
                pragmas,
                new CompilationCostBreakdown(CompilationClassification.Initial, 0, true, 0, new List<CostModifierEntry>(), 0, false));

            return new ExecutionRequest(source, configuration, StandardStamps(), ZeroState(), installed);
        }

        private static ExecutionRequest WithStamps(ExecutionRequest request, IReadOnlyList<RevisionStamp> stamps)
        {
            return new ExecutionRequest(
                request.Source,
                request.Configuration,
                stamps,
                request.InitialState,
                request.InstalledDependencies);
        }

        private static ExecutionRequest WithSessionSeed(ExecutionRequest request, string sessionSeed)
        {
            ProcessExecutionConfiguration original = request.Configuration;
            ProcessExecutionConfiguration perturbed = new(
                original.ExecutionIdentity,
                original.CompilationIdentity,
                original.CompiledSourceRevision,
                original.ProcessIdentity,
                original.CoreIdentity,
                original.ProcessRuleConfigurationIdentity,
                sessionSeed,
                original.Thresholds,
                original.ProcessRule,
                original.DesignatedFinalCoreOutputPosition);

            return new ExecutionRequest(
                request.Source,
                perturbed,
                request.RevisionStamps,
                request.InitialState,
                request.InstalledDependencies);
        }

        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

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

        private static List<RevisionStamp> StandardStamps()
        {
            return new List<RevisionStamp>
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };
        }

        private static InitialExecutionState ZeroState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }

        private static List<DependencyInstance> NoDependencies()
        {
            return new List<DependencyInstance>();
        }

        private static List<DirectiveInstance> NoPragmas()
        {
            return new List<DirectiveInstance>();
        }

        private static CoreLine AssignConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Assign, target, OperandSpec.FromConstant(constant)));
        }

        private static CoreLine MultiplyConstant(string identity, CoreRegister target, int constant)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Multiply, target, OperandSpec.FromConstant(constant)));
        }

        private static CoreLine AddRegister(string identity, CoreRegister target, CoreRegister source)
        {
            return new CoreLine(identity, new CoreLineOperation(CoreLineOperator.Add, target, OperandSpec.FromRegister(source)));
        }

        /// <summary>
        /// The six Patch instance identities the Patch-dense fixture attaches, in host order.
        /// </summary>
        /// <returns>The Patch instance identity values.</returns>
        private static int[] PatchInstances()
        {
            return new[] { 60, 61, 62, 63, 64, 65 };
        }

        /// <summary>
        /// Counts how many of the given Patch instances appear as an effect origin anywhere in the
        /// record. Patch origins ride event evidence rather than the header, so this is where a
        /// fixture's Patch density is actually observable.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="patchInstances">The Patch instance identity values to look for.</param>
        /// <returns>How many of them occur as an effect origin.</returns>
        private static int PatchOriginsPresent(ExecutionRecord record, int[] patchInstances)
        {
            int present = 0;
            for (int i = 0; i < patchInstances.Length; i++)
            {
                InstanceID wanted = new(patchInstances[i]);
                for (int index = 0; index < record.Events.Count; index++)
                {
                    if (record.Events[index].Evidence.EffectOriginInstance != wanted)
                        continue;

                    present++;
                    break;
                }
            }

            return present;
        }

        private static int CountSubtype(ExecutionRecord record, string subtype)
        {
            int count = 0;
            for (int index = 0; index < record.Events.Count; index++)
            {
                if (record.Events[index].Evidence.Subtype == subtype)
                    count++;
            }

            return count;
        }
    }
}
