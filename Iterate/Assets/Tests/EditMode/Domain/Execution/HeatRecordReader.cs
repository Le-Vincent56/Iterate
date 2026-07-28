using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Shared read helpers over a frozen record for the Heat conformance suites: the committed
    /// Process-counter changes, the Heat threshold crossings, and the per-position unit and
    /// disposition lookups. Kept in one place so the four suites assert against one reading of the
    /// record rather than four slightly different ones.
    /// </summary>
    public static class HeatRecordReader
    {
        /// <summary>
        /// The canonical band name a Heat threshold crossing carries.
        /// </summary>
        public const string ThrottlingBand = "THROTTLING";

        /// <summary>
        /// The canonical identity of the Heat counter.
        /// </summary>
        public const string HeatIdentity = "HEAT";

        /// <summary>
        /// Runs one request through a fresh scheduler and builder.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        public static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Collects the committed Process-counter changes in emission order, excluding the
        /// initialization reset.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The counter-change payloads.</returns>
        public static IReadOnlyList<QuantityChangePayload> CounterCommits(ExecutionRecord record)
        {
            List<QuantityChangePayload> commits = new List<QuantityChangePayload>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.QuantityChanged)
                    continue;

                if (evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter } payload)
                    commits.Add(payload);
            }

            return commits;
        }

        /// <summary>
        /// Collects every event carrying Process-counter evidence, whatever its subtype: the
        /// initialization reset, each committed change, and each intervention that reported the
        /// counter without changing it. This is the same set the frozen record derives its counter
        /// history from, so the two are directly comparable.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The counter-evidence events in emission order.</returns>
        public static IReadOnlyList<EventEvidence> CounterEvidence(ExecutionRecord record)
        {
            List<EventEvidence> evidence = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter })
                    evidence.Add(record.Events[i].Evidence);
            }

            return evidence;
        }

        /// <summary>
        /// Returns the Process-counter initialization event — the counter's reset boundary — or null
        /// when no Process rule was configured.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The initialization event, or null.</returns>
        public static EventEvidence CounterInitialization(ExecutionRecord record)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.QuantityReset)
                    continue;

                if (evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter })
                    return evidence;
            }

            return null;
        }

        /// <summary>
        /// Reads the final counter value from the last counter change, or zero when none committed.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The final Heat value.</returns>
        public static int FinalHeat(ExecutionRecord record)
        {
            IReadOnlyList<QuantityChangePayload> commits = CounterCommits(record);

            return commits.Count == 0 ? 0 : commits[commits.Count - 1].FinalValue;
        }

        /// <summary>
        /// Collects the Heat threshold-crossing events, which carry the throttling band name.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The crossing events in emission order.</returns>
        public static IReadOnlyList<EventEvidence> HeatCrossings(ExecutionRecord record)
        {
            List<EventEvidence> crossings = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Family == EventFamilies.Threshold && evidence.TargetIdentity == ThrottlingBand)
                    crossings.Add(evidence);
            }

            return crossings;
        }

        /// <summary>
        /// Returns the disposition-finalized event for the unit at the given source position.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="position">The source position.</param>
        /// <returns>The disposition-finalized event.</returns>
        public static EventEvidence DispositionAt(ExecutionRecord record, SourcePosition position)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype == ExecutionEventSubtypes.SourceExecutionDispositionFinalized
                    && evidence.Position == position)
                {
                    return evidence;
                }
            }

            Assert.Fail("No disposition-finalized event at position " + position.LineNumber + ".");

            return null;
        }

        /// <summary>
        /// Returns the runtime unit opened at the given source position.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="position">The source position.</param>
        /// <returns>The runtime-unit record.</returns>
        public static RuntimeUnitRecord UnitAt(ExecutionRecord record, SourcePosition position)
        {
            for (int i = 0; i < record.Units.Count; i++)
            {
                if (record.Units[i].Opening.Position == position)
                    return record.Units[i];
            }

            Assert.Fail("No runtime unit at position " + position.LineNumber + ".");

            return null;
        }

        /// <summary>
        /// Counts the events carrying the given subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event count.</returns>
        public static int CountSubtype(ExecutionRecord record, string subtype)
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
        /// Returns the single event carrying the given subtype, failing when the count is not one.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The single matching event.</returns>
        public static EventEvidence SingleSubtype(ExecutionRecord record, string subtype)
        {
            EventEvidence found = null;
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype != subtype)
                    continue;

                found = record.Events[i].Evidence;
                count++;
            }

            Assert.AreEqual(1, count, "Expected exactly one " + subtype + " event.");

            return found;
        }

        /// <summary>
        /// Asserts that a counter commit is an ordinary unclamped gain of one from the given prior.
        /// </summary>
        /// <param name="commit">The counter-change payload.</param>
        /// <param name="prior">The expected prior counter value.</param>
        /// <param name="final">The expected final counter value.</param>
        public static void AssertGain(QuantityChangePayload commit, int prior, int final)
        {
            Assert.AreEqual(HeatIdentity, commit.QuantityIdentity);
            Assert.AreEqual(QuantityCategory.ProcessCounter, commit.Category);
            Assert.AreEqual(prior, commit.PriorValue);
            Assert.AreEqual(1, commit.RequestedAmount);
            Assert.AreEqual(1, commit.FinalDelta);
            Assert.AreEqual(final, commit.FinalValue);
            Assert.IsNull(commit.AppliedBounds, "No bound reduced an ordinary gain.");
        }

        /// <summary>
        /// Asserts that a counter commit is an ordinary unclamped cooling of one from the given prior.
        /// </summary>
        /// <param name="commit">The counter-change payload.</param>
        /// <param name="prior">The expected prior counter value.</param>
        /// <param name="final">The expected final counter value.</param>
        public static void AssertCooling(QuantityChangePayload commit, int prior, int final)
        {
            Assert.AreEqual(HeatIdentity, commit.QuantityIdentity);
            Assert.AreEqual(QuantityCategory.ProcessCounter, commit.Category);
            Assert.AreEqual(prior, commit.PriorValue);
            Assert.AreEqual(-1, commit.RequestedAmount);
            Assert.AreEqual(-1, commit.FinalDelta);
            Assert.AreEqual(final, commit.FinalValue);
            Assert.IsNull(commit.AppliedBounds, "No bound reduced an ordinary cooling.");
        }
    }
}
