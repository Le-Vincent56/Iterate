using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Diagnostics;
using Iterate.Domain.Content;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The record codec driven over the real thing: each of the six F1-F5 fixtures is executed against
    /// the frozen shipped catalog, written as a document, read back, and compared three ways — the
    /// decoded record equals the original, the replay comparer still matches it against its own request,
    /// and writing the decoded record reproduces the original text byte for byte.
    ///
    /// The third check is the one a committed baseline depends on. Equal records are not enough: a
    /// baseline is compared as text, so a codec that reordered a member or spelled a number differently
    /// would make every refresh look like a change while every record-level assertion stayed green.
    ///
    /// The second check is what makes this more than a serialization test. A record that round-trips but
    /// no longer replays would mean the codec had preserved the shape and lost the evidence.
    /// </summary>
    public sealed class ShippedCatalogRecordCodecTests
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
        public void EveryShippedFixture_RoundTripsToAnEqualRecord()
        {
            foreach (ShippedCatalogFixture fixture in ShippedCatalogFixtureRequests.All(_catalog))
            {
                ExecutionRecord baseline = Execute(fixture.Request);
                ExecutionRecordReadResult read = ExecutionRecordDocument.Read(ExecutionRecordDocument.Write(baseline));

                Assert.AreEqual(ExecutionRecordReadOutcome.Loaded, read.Outcome, $"{fixture.Name}: {read.Defect}");
                Assert.AreEqual(baseline, read.Record, fixture.Name);
            }
        }

        [Test]
        public void EveryShippedFixture_WritesByteIdenticallyAfterARoundTrip()
        {
            foreach (ShippedCatalogFixture fixture in ShippedCatalogFixtureRequests.All(_catalog))
            {
                string first = ExecutionRecordDocument.Write(Execute(fixture.Request));
                ExecutionRecordReadResult read = ExecutionRecordDocument.Read(first);

                Assert.AreEqual(ExecutionRecordReadOutcome.Loaded, read.Outcome, $"{fixture.Name}: {read.Defect}");
                Assert.AreEqual(first, ExecutionRecordDocument.Write(read.Record), fixture.Name);
            }
        }

        [Test]
        public void EveryShippedFixture_StillReplaysToAMatchAfterADecode()
        {
            foreach (ShippedCatalogFixture fixture in ShippedCatalogFixtureRequests.All(_catalog))
            {
                ExecutionRecordReadResult read = ExecutionRecordDocument.Read(
                    ExecutionRecordDocument.Write(Execute(fixture.Request))
                );

                ReplayComparisonResult result = new ReplayComparer().Compare(read.Record, fixture.Request);

                Assert.AreEqual(ReplayComparisonStatus.Match, result.Status, $"{fixture.Name}: {Describe(result)}");
                Assert.IsNull(result.Divergence, fixture.Name);
                Assert.IsNull(result.UnavailableComponent, fixture.Name);
            }
        }

        [Test]
        public void EveryShippedFixture_ProducesEventsForTheCodecToCarry()
        {
            // Without this the three round-trip tests above would pass just as well on an empty events
            // array, which is exactly the state the previous card left the codec in.
            foreach (ShippedCatalogFixture fixture in ShippedCatalogFixtureRequests.All(_catalog))
            {
                ExecutionRecord baseline = Execute(fixture.Request);

                Assert.Greater(baseline.Events.Count, 0, $"{fixture.Name} produces no events.");
            }
        }

        [Test]
        public void TheShippedFixtures_ReachOnlyTheQuantityChangePayload()
        {
            // Measured, not assumed. The shipped register exercises exactly one of the three payload
            // kinds: every F1-F5 fixture is deterministic end to end and none breaches a ceiling, so
            // random-decision and safety-abort payloads are pinned only by the hand-built fixtures in
            // the Application suite. Asserting their *absence* rather than merely noting it means that
            // if a future fixture starts producing one, this test fails and names the two coverage
            // remarks that then need correcting — the claim cannot rot into a false one unnoticed.
            bool quantityChange = false;
            bool randomDecision = false;
            bool safetyAbort = false;

            foreach (ShippedCatalogFixture fixture in ShippedCatalogFixtureRequests.All(_catalog))
            {
                ExecutionRecord baseline = Execute(fixture.Request);
                for (int index = 0; index < baseline.Events.Count; index++)
                {
                    EventPayload payload = baseline.Events[index].Evidence.Payload;
                    quantityChange |= payload is QuantityChangePayload;
                    randomDecision |= payload is RandomDecisionPayload;
                    safetyAbort |= payload is SafetyAbortPayload;
                }
            }

            Assert.IsTrue(quantityChange, "no shipped fixture produced a quantity-change payload.");
            Assert.IsFalse(
                randomDecision,
                "a shipped fixture now reaches RandomDecision; this remark and the Application suite's coverage note both need updating."
            );
            Assert.IsFalse(
                safetyAbort,
                "a shipped fixture now reaches SafetyAbort; this remark and the Application suite's coverage note both need updating."
            );
        }

        /// <summary>
        /// Executes a request on a fresh scheduler and trace builder.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Renders a failed comparison so a red reports its locus rather than only its status.
        /// </summary>
        /// <param name="result">The comparison result.</param>
        /// <returns>The failure message.</returns>
        private static string Describe(ReplayComparisonResult result)
        {
            if (result.Divergence != null)
                return result.Divergence.Description;

            if (result.UnavailableComponent != null)
                return $"exact reproduction unavailable: {result.UnavailableComponent}";

            return "matched";
        }
    }
}
