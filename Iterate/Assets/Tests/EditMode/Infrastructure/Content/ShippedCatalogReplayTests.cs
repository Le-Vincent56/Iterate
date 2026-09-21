using System;
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

            // The header's relevant-Patch list now names this fixture's six Patch instances in slot
            // order, 60..65 — the producer ECO-02 gave the field, replacing the absence this line
            // pinned while it had none. With it populated, AssertReplaysToMatch below exercises the
            // comparer's RELEVANT_PATCHES correspondence component against real content for the first
            // time rather than comparing two empty lists.
            Assert.AreEqual(
                new[] { new InstanceID(60), new InstanceID(61), new InstanceID(62), new InstanceID(63), new InstanceID(64), new InstanceID(65) },
                baseline.Header.RelevantPatchInstances);

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
            return ShippedCatalogFixtureRequests.MinimalCore(_catalog);
        }

        private ExecutionRequest ThrottledCoreRequest()
        {
            return ShippedCatalogFixtureRequests.ThrottledCore(_catalog);
        }

        private ExecutionRequest BurstOrderSignatureRequest()
        {
            return ShippedCatalogFixtureRequests.BurstOrderSignature(_catalog);
        }

        private ExecutionRequest PatchDenseRequest()
        {
            return ShippedCatalogFixtureRequests.PatchDense(_catalog);
        }

        private ExecutionRequest ParityFlipRequest(int seed)
        {
            return ShippedCatalogFixtureRequests.ParityFlip(_catalog, seed);
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
