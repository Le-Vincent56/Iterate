using System;
using System.Collections.Generic;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// Compares a stored execution record against a fresh re-resolution of a caller-supplied candidate
    /// request, and reports the first place they differ. The caller supplies the request because a
    /// stored record cannot be re-resolved alone: its header carries identities, revision stamps,
    /// instance lists, and the initial state, but never the compiled source or its arrangement.
    /// The comparer re-resolves first and then checks correspondence against the recomputed header,
    /// so the engine's own derivation stays the single source of header facts and no second copy of
    /// that derivation exists to drift from it. When correspondence fails, the recomputed record is
    /// withheld from the result even though the engine ran: a record computed under non-corresponding
    /// inputs is exactly the silent substitution we forbid, and withholding it makes the
    /// substitution impossible downstream rather than merely discouraged.
    /// This class is stateless — every call constructs its own builder and scheduler — so a defect
    /// escaping the engine  poisons nothing and the next call starts clean.
    /// That is deliberately asymmetric with the prediction seam, which keeps an owned pair because it
    /// may one day run at interaction cadence; replay runs at human cadence and buys statelessness
    /// instead. The per-call allocation is explicitly off the hot path: the Coding Standards hot-path
    /// rules bind the execution path, not a diagnostic that runs when a developer asks a question.
    /// The comparer never invokes an outcome-application step. No such step exists to
    /// omit today, so this is a standing contract rather than a present-tense fact: it must remain
    /// true when Session Flow lands and gains one.
    /// </summary>
    public sealed class ReplayComparer
    {
        /// <summary>
        /// Re-resolves the candidate request and compares the result against the stored record.
        /// </summary>
        /// <param name="stored">The stored execution record to compare against.</param>
        /// <param name="candidate">The request to re-resolve; the caller owns pairing it with the record.</param>
        /// <returns>The comparison result: a match, the first divergence, or the first non-corresponding component.</returns>
        public ReplayComparisonResult Compare(ExecutionRecord stored, ExecutionRequest candidate)
        {
            if (stored == null)
                throw new ArgumentException("A comparison requires a stored record.", nameof(stored));

            if (candidate == null)
                throw new ArgumentException("A comparison requires a candidate request.", nameof(candidate));

            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRecord recomputed = scheduler.Execute(candidate);

            string component = FirstNonCorrespondingComponent(stored.Header, recomputed.Header);

            if (component != null)
                return new ReplayComparisonResult(ReplayComparisonStatus.ExactReproductionUnavailable, component, null, null);

            if (stored.Equals(recomputed))
                return new ReplayComparisonResult(ReplayComparisonStatus.Match, null, null, recomputed);

            return new ReplayComparisonResult(ReplayComparisonStatus.Diverged, null, FirstDivergence(stored, recomputed), recomputed);
        }

        /// <summary>
        /// Names the first header component that fails to correspond, in a fixed order so that "first"
        /// is deterministic rather than incidental.
        /// </summary>
        /// <param name="stored">The stored header.</param>
        /// <param name="recomputed">The header the engine derived from the candidate request.</param>
        /// <returns>The component token, or null when every component corresponds.</returns>
        private static string FirstNonCorrespondingComponent(ExecutionEvidenceHeader stored, ExecutionEvidenceHeader recomputed)
        {
            if (!string.Equals(stored.ExecutionIdentity, recomputed.ExecutionIdentity, StringComparison.Ordinal))
                return "IDENTITY:ExecutionIdentity";

            if (!string.Equals(stored.CompilationIdentity, recomputed.CompilationIdentity, StringComparison.Ordinal))
                return "IDENTITY:CompilationIdentity";

            if (!string.Equals(stored.CompiledSourceRevision, recomputed.CompiledSourceRevision, StringComparison.Ordinal))
                return "IDENTITY:CompiledSourceRevision";

            if (!string.Equals(stored.ProcessIdentity, recomputed.ProcessIdentity, StringComparison.Ordinal))
                return "IDENTITY:ProcessIdentity";

            if (!string.Equals(stored.CoreIdentity, recomputed.CoreIdentity, StringComparison.Ordinal))
                return "IDENTITY:CoreIdentity";

            if (!string.Equals(stored.ProcessRuleConfigurationIdentity, recomputed.ProcessRuleConfigurationIdentity, StringComparison.Ordinal))
                return "IDENTITY:ProcessRuleConfigurationIdentity";

            if (!string.Equals(stored.SessionSeedIdentity, recomputed.SessionSeedIdentity, StringComparison.Ordinal))
                return "IDENTITY:SessionSeedIdentity";

            if (stored.RevisionStamps.Count != recomputed.RevisionStamps.Count)
                return "REVISION_STAMPS:count";

            for (int index = 0; index < stored.RevisionStamps.Count; index++)
            {
                if (stored.RevisionStamps[index] == recomputed.RevisionStamps[index])
                    continue;

                return "REVISION_STAMP:" + stored.RevisionStamps[index].Name;
            }

            if (!ListEquals(stored.ActiveDirectiveInstances, recomputed.ActiveDirectiveInstances))
                return "ACTIVE_DIRECTIVES";

            if (!ListEquals(stored.InstalledDependencyInstances, recomputed.InstalledDependencyInstances))
                return "INSTALLED_DEPENDENCIES";

            if (!ListEquals(stored.RelevantPatchInstances, recomputed.RelevantPatchInstances))
                return "RELEVANT_PATCHES";

            if (!stored.InitialState.Equals(recomputed.InitialState))
                return "INITIAL_STATE";

            return null;
        }

        /// <summary>
        /// Walks the record areas in their canonical order and returns the first difference found.
        /// </summary>
        /// <param name="stored">The stored record.</param>
        /// <param name="recomputed">The recomputed record.</param>
        /// <returns>The first divergence.</returns>
        private static ReplayDivergence FirstDivergence(ExecutionRecord stored, ExecutionRecord recomputed)
        {
            ReplayDivergence divergence = CompareElements(
                ReplayDivergenceArea.Events,
                "EVENT",
                stored.Events,
                recomputed.Events,
                RenderEvent);

            if (divergence != null)
                return divergence;

            divergence = CompareElements(
                ReplayDivergenceArea.Units,
                "UNIT",
                stored.Units,
                recomputed.Units,
                RenderUnit);

            if (divergence != null)
                return divergence;

            divergence = CompareElements(
                ReplayDivergenceArea.TraversalOrder,
                "TRAVERSAL_ORDER",
                stored.TraversalOrder,
                recomputed.TraversalOrder,
                RenderUnitIdentity);

            if (divergence != null)
                return divergence;

            divergence = CompareElements(
                ReplayDivergenceArea.ThresholdHistory,
                "THRESHOLD_HISTORY",
                stored.ThresholdHistory,
                recomputed.ThresholdHistory,
                RenderEventIdentity);

            if (divergence != null)
                return divergence;

            divergence = CompareElements(
                ReplayDivergenceArea.CounterHistory,
                "COUNTER_HISTORY",
                stored.CounterHistory,
                recomputed.CounterHistory,
                RenderEventIdentity);

            if (divergence != null)
                return divergence;

            divergence = CompareElements(
                ReplayDivergenceArea.Defects,
                "DEFECT",
                stored.Defects,
                recomputed.Defects,
                RenderDefect);

            if (divergence != null)
                return divergence;

            if (!stored.FinalState.Equals(recomputed.FinalState))
                return new ReplayDivergence(
                    ReplayDivergenceArea.FinalState,
                    -1,
                    $"FINAL_STATE: stored {RenderFinalState(stored.FinalState)}, recomputed {RenderFinalState(recomputed.FinalState)}");

            if (!stored.SafetyCounts.Equals(recomputed.SafetyCounts))
                return new ReplayDivergence(
                    ReplayDivergenceArea.SafetyCounts,
                    -1,
                    $"SAFETY_COUNTS: stored {RenderSafetyCounts(stored.SafetyCounts)}, recomputed {RenderSafetyCounts(recomputed.SafetyCounts)}");

            if (stored.CompletionStatus != recomputed.CompletionStatus)
                return new ReplayDivergence(
                    ReplayDivergenceArea.CompletionStatus,
                    -1,
                    $"COMPLETION_STATUS: stored {stored.CompletionStatus}, recomputed {recomputed.CompletionStatus}");

            if (stored.SafetyStatus != recomputed.SafetyStatus)
                return new ReplayDivergence(
                    ReplayDivergenceArea.SafetyStatus,
                    -1,
                    $"SAFETY_STATUS: stored {stored.SafetyStatus}, recomputed {recomputed.SafetyStatus}");

            throw new InvalidOperationException("Whole-record equality failed but no walked area differs.");
        }

        /// <summary>
        /// Finds the first differing element of two lists, or reports a length difference at the index
        /// one past the end of the shorter list.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="area">The area these lists belong to.</param>
        /// <param name="label">The description label for this area.</param>
        /// <param name="stored">The stored list.</param>
        /// <param name="recomputed">The recomputed list.</param>
        /// <param name="render">Renders one element's salient values for the description.</param>
        /// <returns>The divergence, or null when the lists are equal.</returns>
        private static ReplayDivergence CompareElements<T>(
            ReplayDivergenceArea area,
            string label,
            IReadOnlyList<T> stored,
            IReadOnlyList<T> recomputed,
            Func<T, string> render)
        {
            int shared = stored.Count < recomputed.Count ? stored.Count : recomputed.Count;

            for (int index = 0; index < shared; index++)
            {
                if (EqualityComparer<T>.Default.Equals(stored[index], recomputed[index]))
                    continue;

                return new ReplayDivergence(
                    area,
                    index,
                    $"{label}[{index}]: stored {render(stored[index])}, recomputed {render(recomputed[index])}");
            }

            if (stored.Count == recomputed.Count)
                return null;

            return new ReplayDivergence(
                area,
                shared,
                $"{label}[{shared}]: stored list holds {stored.Count}, recomputed list holds {recomputed.Count}");
        }

        /// <summary>
        /// Element-wise equality over two lists, used for the header's instance lists. The Trace
        /// namespace's own list-equality helper stays internal to Trace by design, so the comparer
        /// walks elements itself rather than widening that surface.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="left">The first list.</param>
        /// <param name="right">The second list.</param>
        /// <returns>True when both lists hold equal elements in the same order.</returns>
        private static bool ListEquals<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int index = 0; index < left.Count; index++)
            {
                if (!EqualityComparer<T>.Default.Equals(left[index], right[index]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Renders one event's identity and subtype.
        /// </summary>
        /// <param name="record">The event record.</param>
        /// <returns>The rendered event.</returns>
        private static string RenderEvent(EventRecord record)
        {
            return $"event {record.Identity.Value} subtype {record.Evidence.Subtype}";
        }

        /// <summary>
        /// Renders one runtime unit's identity and closure status.
        /// </summary>
        /// <param name="record">The unit record.</param>
        /// <returns>The rendered unit.</returns>
        private static string RenderUnit(RuntimeUnitRecord record)
        {
            return $"unit {record.Identity.Value} closure {record.Closure.Status}";
        }

        /// <summary>
        /// Renders one runtime unit identity.
        /// </summary>
        /// <param name="identity">The unit identity.</param>
        /// <returns>The rendered identity.</returns>
        private static string RenderUnitIdentity(RuntimeUnitID identity)
        {
            return $"unit {identity.Value}";
        }

        /// <summary>
        /// Renders one event identity.
        /// </summary>
        /// <param name="identity">The event identity.</param>
        /// <returns>The rendered identity.</returns>
        private static string RenderEventIdentity(TraceEventID identity)
        {
            return $"event {identity.Value}";
        }

        /// <summary>
        /// Renders one evidence defect's field and reason.
        /// </summary>
        /// <param name="defect">The defect.</param>
        /// <returns>The rendered defect.</returns>
        private static string RenderDefect(EvidenceDefect defect)
        {
            return $"{defect.FieldName} ({defect.Reason})";
        }

        /// <summary>
        /// Renders the four final registers.
        /// </summary>
        /// <param name="state">The final state.</param>
        /// <returns>The rendered state.</returns>
        private static string RenderFinalState(FinalExecutionState state)
        {
            return $"Value {state.FinalValue.Value} Signal {state.FinalSignal.Value} Score {state.FinalScore.Value} output {state.FinalOutput.Value}";
        }

        /// <summary>
        /// Renders the five safety tallies.
        /// </summary>
        /// <param name="counts">The safety counts.</param>
        /// <returns>The rendered tallies.</returns>
        private static string RenderSafetyCounts(SafetyCounts counts)
        {
            return $"depth {counts.LineageDepthHighWater} descendants {counts.AddedDescendants} units {counts.SourceExecutionUnits} reactions {counts.EffectReactions} transformations {counts.OperationTransformations}";
        }
    }
}