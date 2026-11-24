using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Telemetry;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests <see cref="TelemetryRunService"/> over an in-memory store: terminations read from the
    /// last complete line, a missing end and a cut-short last line both closed without end, an
    /// export trailer after byte-identical lines, and a clear that refuses the active run.
    /// </summary>
    public sealed class TelemetryRunServiceTests
    {
        private static readonly DateTimeOffset _started = new DateTimeOffset(new DateTime(2026, 11, 24), TimeSpan.Zero).AddHours(12);

        private InMemoryTelemetryRunStore _store;
        private TelemetryRig _rig;
        private TelemetryRunService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemoryTelemetryRunStore();
            _rig = new TelemetryRig();
            _service = new TelemetryRunService(_store, _rig.Recorder, _rig.Clock);
        }

        [TestCase("completed", TelemetryTermination.Completed)]
        [TestCase("failed", TelemetryTermination.Failed)]
        [TestCase("abandoned", TelemetryTermination.Abandoned)]
        [TestCase("error", TelemetryTermination.Error)]
        public void List_ARunEndedLastLine_MapsItsTermination(string termination, TelemetryTermination expected)
        {
            _store.Runs["run-a"] = $"{Line("run-a", 0, TelemetryEventTypes.RunStarted, null)}{Line("run-a", 1, TelemetryEventTypes.RunEnded, termination)}";

            TelemetryRunSummary summary = _service.ListAsync(CancellationToken.None).GetAwaiter().GetResult()[0];

            Assert.AreEqual(new TelemetryRunSummary("run-a", _started, "ordinary", expected), summary);
        }

        [Test]
        public void List_ARunWithNoEndLine_IsClosedWithoutEnd()
        {
            _store.Runs["run-a"] = $"{Line("run-a", 0, TelemetryEventTypes.RunStarted, null)}{Line("run-a", 1, TelemetryEventTypes.Command, null)}";

            Assert.AreEqual(TelemetryTermination.ClosedWithoutEnd, _service.ListAsync(CancellationToken.None).GetAwaiter().GetResult()[0].Termination);
        }

        [Test]
        public void List_ACutShortEndLine_IsIgnoredAndTheRunIsClosedWithoutEnd()
        {
            string ended = Line("run-a", 1, TelemetryEventTypes.RunEnded, "completed");
            _store.Runs["run-a"] = $"{Line("run-a", 0, TelemetryEventTypes.RunStarted, null)}{ended.Substring(0, ended.Length - 1)}";

            Assert.AreEqual(TelemetryTermination.ClosedWithoutEnd, _service.ListAsync(CancellationToken.None).GetAwaiter().GetResult()[0].Termination);
        }

        [Test]
        public void List_AnEmptyRun_IsClosedWithoutEnd()
        {
            _store.Runs["run-a"] = "";

            Assert.AreEqual(TelemetryTermination.ClosedWithoutEnd, _service.ListAsync(CancellationToken.None).GetAwaiter().GetResult()[0].Termination);
        }

        [Test]
        public void Export_AppendsOneTrailerAfterTheUnchangedLines()
        {
            string text = $"{Line("run-a", 0, TelemetryEventTypes.RunStarted, null)}{Line("run-a", 1, TelemetryEventTypes.RunEnded, "abandoned")}";
            _store.Runs["run-a"] = text;
            _rig.Clock.Advance(5000);

            _service.ExportAsync(new[] { "run-a" }, "exports", CancellationToken.None).GetAwaiter().GetResult();
            string exported = _store.Exports[Path.Combine("exports", "run-a.jsonl")];

            StringAssert.StartsWith(text, exported);
            string trailer = exported.Substring(text.Length);
            Assert.AreEqual(trailer.Length - 1, trailer.IndexOf('\n'), "Exactly one line was appended.");
            JsonObject parsed = (JsonObject)new CatalogJsonReader().Parse(trailer);
            Assert.AreEqual(TelemetryEventTypes.Export, TelemetryRig.Text(parsed, "type"));
            Assert.AreEqual(2, TelemetryRig.Number(parsed, "seq"));
            Assert.AreEqual("2026-11-24T12:00:05.000Z", TelemetryRig.Text(parsed, "at"));
            Assert.AreEqual("abandoned", TelemetryRig.Text(TelemetryRig.Data(parsed), "termination"));
            Assert.IsFalse(TelemetryRig.Flag(TelemetryRig.Data(parsed), "active"));
            Assert.AreEqual(text, _store.Runs["run-a"], "The run itself is untouched.");
        }

        [Test]
        public void Export_TheActiveRun_SaysItIsActiveAndClosedWithoutEnd()
        {
            _rig.Begin();
            _store.Runs["run-1"] = _rig.Writer.Text("run-1");

            _service.ExportAsync(new[] { "run-1" }, "exports", CancellationToken.None).GetAwaiter().GetResult();
            string exported = _store.Exports[Path.Combine("exports", "run-1.jsonl")];

            string[] lines = exported.TrimEnd('\n').Split('\n');
            JsonObject trailer = (JsonObject)new CatalogJsonReader().Parse(lines[lines.Length - 1]);
            Assert.IsTrue(TelemetryRig.Flag(TelemetryRig.Data(trailer), "active"));
            Assert.AreEqual("closed_without_end", TelemetryRig.Text(TelemetryRig.Data(trailer), "termination"));
        }

        [Test]
        public void Clear_DeletesTheNamedRunsAndRefusesTheActiveOne()
        {
            _rig.Begin();
            _store.Runs["run-0"] = Line("run-0", 0, TelemetryEventTypes.RunStarted, null);
            _store.Runs["run-1"] = _rig.Writer.Text("run-1");
            _store.Runs["run-2"] = Line("run-2", 0, TelemetryEventTypes.RunStarted, null);

            TelemetryClearResult cleared = _service.ClearAsync(new[] { "run-0", "run-1", "run-9" }, CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "run-0" }, cleared.Deleted);
            CollectionAssert.AreEqual(new[] { "run-1", "run-9" }, cleared.Refused);
            Assert.IsTrue(_store.Runs.ContainsKey("run-1"), "The active run is still on disk.");
            Assert.IsTrue(_store.Runs.ContainsKey("run-2"), "An unnamed run is untouched.");
        }

        /// <summary>
        /// One encoded line of a run.
        /// </summary>
        /// <param name="run">The run.</param>
        /// <param name="sequence">The line number.</param>
        /// <param name="type">The event type.</param>
        /// <param name="termination">The <c>run_ended</c> termination, or null.</param>
        /// <returns>The line.</returns>
        private static string Line(
            string run,
            int sequence,
            string type,
            string termination
        )
        {
            JsonObjectBuilder data = new();
            if (termination != null)
                data.Add("termination", termination);

            return TelemetryEventCodec.Encode(new TelemetryEvent(
                run,
                sequence,
                type,
                _started,
                0L,
                "0.1.0",
                "0.1",
                "fixture-0.0.1",
                "ordinary",
                "Build",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                data.Build()
            ));
        }
    }
}
