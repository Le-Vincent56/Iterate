using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Execution;
using Iterate.Domain.Execution.Tests;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Diagnostics.Tests
{
    /// <summary>
    /// Contract tests for <see cref="ReplayComparer"/> on hand-built minimal arrangements: a re-resolved
    /// identical request matches, every header component that fails to correspond is named by its own
    /// token with the recomputed record withheld (CAB-EVT-770), and a record whose stored evidence was
    /// altered reports the first differing area and element in the canonical walk order (CAB-PRES-112).
    /// Divergences are produced by record surgery — rebuilding a real record with one element altered —
    /// because a deterministic engine re-resolving the same request cannot itself produce one. Three of
    /// the walk areas (traversal order and the two derived histories) are engine-unreachable at any
    /// content, since equal events and units imply equal derived lists; those cases exist for
    /// defectively-stored records only and say so in their names.
    /// </summary>
    public sealed class ReplayComparerTests
    {
        [Test]
        public void IdenticalRequest_Matches_AndCarriesTheRecomputedRecord()
        {
            // Arrange
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.Match));
            Assert.That(result.RecomputedRecord, Is.Not.Null);
            Assert.That(result.RecomputedRecord, Is.EqualTo(stored));
            Assert.That(result.Divergence, Is.Null);
            Assert.That(result.UnavailableComponent, Is.Null);
        }

        [Test]
        public void NullStoredRecord_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparer().Compare(null, BaselineRequest()));
        }

        [Test]
        public void NullCandidateRequest_Throws()
        {
            ExecutionRecord stored = Execute(BaselineRequest());

            Assert.Throws<ArgumentException>(() => _ = new ReplayComparer().Compare(stored, null));
        }

        [Test]
        public void OneComparerInstance_ServesSuccessiveComparisonsIndependently()
        {
            // Arrange — the comparer is stateless: it builds its own builder and scheduler per call, so
            // nothing carries from one comparison into the next.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);
            ExecutionRecord altered = Rebuild(stored, finalState: WithScore(stored.FinalState, 999));
            ReplayComparer comparer = new();

            // Act
            ReplayComparisonResult first = comparer.Compare(stored, request);
            ReplayComparisonResult second = comparer.Compare(altered, request);
            ReplayComparisonResult third = comparer.Compare(stored, request);

            // Assert
            Assert.That(first.Status, Is.EqualTo(ReplayComparisonStatus.Match));
            Assert.That(second.Status, Is.EqualTo(ReplayComparisonStatus.Diverged));
            Assert.That(third.Status, Is.EqualTo(ReplayComparisonStatus.Match));
        }

        [Test]
        public void EveryIdentityMismatch_ReportsThatIdentityAndWithholdsTheRecord()
        {
            // Arrange — the seven header identities all travel on the configuration, perturbed one at a
            // time so each test iteration has exactly one non-corresponding component.
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            string[] tokens =
            {
                "IDENTITY:ExecutionIdentity",
                "IDENTITY:CompilationIdentity",
                "IDENTITY:CompiledSourceRevision",
                "IDENTITY:ProcessIdentity",
                "IDENTITY:CoreIdentity",
                "IDENTITY:ProcessRuleConfigurationIdentity",
                "IDENTITY:SessionSeedIdentity"
            };

            ProcessExecutionConfiguration[] perturbed =
            {
                Configuration("exec-B", "compilation", "source-rev", "process", "core", "rule-config", "session-seed"),
                Configuration("exec", "compilation-B", "source-rev", "process", "core", "rule-config", "session-seed"),
                Configuration("exec", "compilation", "source-rev-B", "process", "core", "rule-config", "session-seed"),
                Configuration("exec", "compilation", "source-rev", "process-B", "core", "rule-config", "session-seed"),
                Configuration("exec", "compilation", "source-rev", "process", "core-B", "rule-config", "session-seed"),
                Configuration("exec", "compilation", "source-rev", "process", "core", "rule-config-B", "session-seed"),
                Configuration("exec", "compilation", "source-rev", "process", "core", "rule-config", "session-seed-B")
            };

            for (int index = 0; index < tokens.Length; index++)
            {
                // Act
                ReplayComparisonResult result = new ReplayComparer().Compare(stored, WithConfiguration(baseline, perturbed[index]));

                // Assert
                Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable), tokens[index]);
                Assert.That(result.UnavailableComponent, Is.EqualTo(tokens[index]));
                Assert.That(result.RecomputedRecord, Is.Null, tokens[index]);
                Assert.That(result.Divergence, Is.Null, tokens[index]);
            }
        }

        [Test]
        public void RevisionStampValueMismatch_ReportsTheStampName()
        {
            // Arrange — the content-revision arm CAB-EVT-770 names directly.
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            List<RevisionStamp> perturbed = new()
            {
                new RevisionStamp("Content Catalog", "0.2.0"),
                new RevisionStamp("Random Service", "iterate-rng-1")
            };

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, WithStamps(baseline, perturbed));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("REVISION_STAMP:Content Catalog"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void RandomServiceRevisionMismatch_ReportsThatStamp()
        {
            // Arrange — the random-service arm of the same rule; it has no producer in the engine yet,
            // but the gate is on the stamp, which exists today.
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            List<RevisionStamp> perturbed = new()
            {
                new RevisionStamp("Content Catalog", "0.1.0"),
                new RevisionStamp("Random Service", "iterate-rng-2")
            };

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, WithStamps(baseline, perturbed));

            // Assert
            Assert.That(result.UnavailableComponent, Is.EqualTo("REVISION_STAMP:Random Service"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void RevisionStampCountMismatch_ReportsTheCount()
        {
            // Arrange
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            List<RevisionStamp> perturbed = new() { new RevisionStamp("Content Catalog", "0.1.0") };

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, WithStamps(baseline, perturbed));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("REVISION_STAMPS:count"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void ActiveDirectiveMismatch_ReportsActiveDirectives()
        {
            // Arrange — an identity source already advanced past the baseline's gives the candidate's
            // pragma a different instance identity, which is the header's active-Directive list.
            InstanceIDSource advanced = new();
            advanced.Next();
            advanced.Next();
            advanced.Next();

            ExecutionRecord stored = Execute(BaselineRequest());

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, SchedulerFixtures.PassCrossingRequest(advanced));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("ACTIVE_DIRECTIVES"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void InstalledDependencyMismatch_ReportsInstalledDependencies()
        {
            // Arrange — the same compiled source, so the Directive list still corresponds; only the
            // installed-Dependency list differs.
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            List<DependencyInstance> installed = new() { SchedulerFixtures.StandardLibraryInstance(90) };

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, WithInstalled(baseline, installed));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("INSTALLED_DEPENDENCIES"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void RelevantPatchMismatch_ReportsRelevantPatches()
        {
            // Arrange — this component is reached by stored-header surgery rather than by perturbing
            // the request, and not merely because no fixture happens to populate it: the engine
            // supplies an empty relevant-Patch list unconditionally when it builds the header, so no
            // request can produce a non-empty one at current content. The component is compared anyway,
            // because a header field the comparer skips is a field that can drift the day it is wired.
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);
            ExecutionRecord surgical = WithHeader(stored, WithRelevantPatches(stored.Header, new List<InstanceID> { new InstanceID(77) }));

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(surgical, baseline);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("RELEVANT_PATCHES"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void InitialStateMismatch_ReportsInitialState()
        {
            // Arrange
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, WithInitialState(baseline, SchedulerFixtures.State(4, 0, 0)));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("INITIAL_STATE"));
            Assert.That(result.RecomputedRecord, Is.Null);
        }

        [Test]
        public void CorrespondenceOrder_ReportsTheEarlierComponent()
        {
            // Arrange — two components fail to correspond at once; the identity precedes the initial
            // state in the component order, so the identity is the one named. This is what makes "the
            // first mismatched component" deterministic rather than incidental.
            ExecutionRequest baseline = BaselineRequest();
            ExecutionRecord stored = Execute(baseline);

            ExecutionRequest candidate = new(
                baseline.Source,
                Configuration("exec", "compilation", "source-rev", "process", "core", "rule-config", "session-seed-B"),
                baseline.RevisionStamps,
                SchedulerFixtures.State(4, 0, 0),
                baseline.InstalledDependencies);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(stored, candidate);

            // Assert
            Assert.That(result.UnavailableComponent, Is.EqualTo("IDENTITY:SessionSeedIdentity"));
        }

        [Test]
        public void ChangedEventSubtype_ReportsTheEventAreaAndIndex()
        {
            // Arrange — the baseline arrangement resolves no effects, so no EFFECT_* subtype occurs in
            // it and the substituted subtype is guaranteed to differ from the original.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            Assert.That(stored.Events.Count, Is.GreaterThan(3), "The fixture must record enough events for the indexed surgeries below.");

            List<EventRecord> events = new(stored.Events);
            string original = events[2].Evidence.Subtype;
            events[2] = WithSubtype(events[2], ExecutionEventSubtypes.EffectFailedToQualify);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, events: events), request);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.Diverged));
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.Events));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(2));
            Assert.That(result.Divergence.Description, Does.Contain(ExecutionEventSubtypes.EffectFailedToQualify));
            Assert.That(result.Divergence.Description, Does.Contain(original));
            Assert.That(result.RecomputedRecord, Is.Not.Null);
        }

        [Test]
        public void TruncatedEventList_ReportsTheEventAreaAtTheShorterLength()
        {
            // Arrange
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            List<EventRecord> events = new(stored.Events);
            events.RemoveAt(events.Count - 1);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, events: events), request);

            // Assert — the divergence is at the index one past the end of the shorter list.
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.Events));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(events.Count));
            Assert.That(result.Divergence.Description, Does.Contain("recomputed"));
        }

        [Test]
        public void ChangedUnitClosureStatus_ReportsTheUnitArea()
        {
            // Arrange — only the closure status changes; the two dispositions are copied unchanged,
            // because a closure whose dispositions match is rejected by its own constructor.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            List<RuntimeUnitRecord> units = new(stored.Units);
            units[0] = WithClosureStatus(units[0], UnitClosureStatus.Cancelled);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, units: units), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.Units));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(0));
        }

        [Test]
        public void TruncatedTraversalOrder_ReportsTraversalOrder_DefectivelyStoredRecordsOnly()
        {
            // Arrange — engine-unreachable: equal events and units imply an equal traversal order, so a
            // freeze-produced record can never reach this area. It is walked because the record
            // constructor does not re-validate the derived lists, so a defectively-stored record can
            // differ here and a diagnostic must not go silent when the evidence is at its worst.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);
            Assert.That(stored.TraversalOrder, Is.Not.Empty, "The fixture must traverse at least one unit for this case to be meaningful.");

            List<RuntimeUnitID> traversal = new(stored.TraversalOrder);
            traversal.RemoveAt(traversal.Count - 1);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, traversalOrder: traversal), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.TraversalOrder));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(traversal.Count));
        }

        [Test]
        public void TruncatedThresholdHistory_ReportsThresholdHistory_DefectivelyStoredRecordsOnly()
        {
            // Arrange — the same engine-unreachable class. The baseline crosses Pass, so the history is
            // non-empty; asserted rather than assumed so the case cannot pass vacuously.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);
            Assert.That(stored.ThresholdHistory, Is.Not.Empty, "The fixture must cross a threshold for this case to be meaningful.");

            List<TraceEventID> history = new(stored.ThresholdHistory);
            history.RemoveAt(history.Count - 1);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, thresholdHistory: history), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.ThresholdHistory));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(history.Count));
        }

        [Test]
        public void AppendedCounterHistoryEntry_ReportsCounterHistory_DefectivelyStoredRecordsOnly()
        {
            // Arrange — the same engine-unreachable class, reached by appending rather than truncating:
            // the baseline configures no Process rule, so its counter history is empty.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            List<TraceEventID> history = new(stored.CounterHistory) { new TraceEventID(1) };

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, counterHistory: history), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.CounterHistory));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(stored.CounterHistory.Count));
        }

        [Test]
        public void AddedDefect_ReportsTheDefectsArea()
        {
            // Arrange — a defect also flips the three derived statuses, which is exactly why they are
            // not walk areas of their own: they are pure functions of this ledger and the completion
            // status, so once the ledger matches they match by construction.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            List<EvidenceDefect> defects = new(stored.Defects) { new EvidenceDefect(null, null, "Header", "missing field") };

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, defects: defects), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.Defects));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(stored.Defects.Count));
        }

        [Test]
        public void ChangedFinalScore_ReportsFinalState()
        {
            // Arrange
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, finalState: WithScore(stored.FinalState, 999)), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.FinalState));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(-1));
            Assert.That(result.Divergence.Description, Does.Contain("999"));
        }

        [Test]
        public void ChangedSafetyCounts_ReportsSafetyCounts()
        {
            // Arrange
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);
            SafetyCounts counts = stored.SafetyCounts;
            SafetyCounts altered = new(
                counts.LineageDepthHighWater,
                counts.AddedDescendants,
                counts.SourceExecutionUnits,
                counts.EffectReactions + 5,
                counts.OperationTransformations);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, safetyCounts: altered), request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.SafetyCounts));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(-1));
        }

        [Test]
        public void AbortedCompletionPair_ReportsCompletionStatus()
        {
            // Arrange — the completion and safety statuses must flip together, because the record
            // constructor rejects an aborted completion without an aborted safety status. That coupling
            // is why completion is walked first: ordered the other way, this surgery would report
            // SafetyStatus and the CompletionStatus area would have no constructible producer at all.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            ExecutionRecord surgical = Rebuild(
                stored,
                safetyStatus: SafetyStatus.SafetyAborted,
                completionStatus: ExecutionCompletionStatus.SafetyAborted);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(surgical, request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.CompletionStatus));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(-1));
        }

        [Test]
        public void SafetyStatusAloneChanged_ReportsSafetyStatus()
        {
            // Arrange — a non-aborted safety status leaves the completion status legal and unchanged,
            // which is the only way to reach the SafetyStatus area on its own.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(
                Rebuild(stored, safetyStatus: SafetyStatus.SafetyLimitApproached),
                request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.SafetyStatus));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TwoDifferingEvents_ReportTheEarlierIndex()
        {
            // Arrange
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            Assert.That(stored.Events.Count, Is.GreaterThan(3), "The fixture must record enough events for the indexed surgeries below.");

            List<EventRecord> events = new(stored.Events);
            events[1] = WithSubtype(events[1], ExecutionEventSubtypes.EffectFailedToQualify);
            events[3] = WithSubtype(events[3], ExecutionEventSubtypes.EffectQualified);

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(Rebuild(stored, events: events), request);

            // Assert — earliest-first within an area, which is CAB-PRES-112's "earliest".
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.Events));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(1));
        }

        [Test]
        public void EventAndFinalStateBothDiffer_ReportEvents()
        {
            // Arrange — area order decides between areas, and events lead the walk.
            ExecutionRequest request = BaselineRequest();
            ExecutionRecord stored = Execute(request);

            List<EventRecord> events = new(stored.Events);
            events[0] = WithSubtype(events[0], ExecutionEventSubtypes.EffectFailedToQualify);

            ExecutionRecord surgical = Rebuild(stored, events: events, finalState: WithScore(stored.FinalState, 999));

            // Act
            ReplayComparisonResult result = new ReplayComparer().Compare(surgical, request);

            // Assert
            Assert.That(result.Divergence.Area, Is.EqualTo(ReplayDivergenceArea.Events));
            Assert.That(result.Divergence.ElementIndex, Is.EqualTo(0));
        }

        private static ExecutionRequest BaselineRequest()
        {
            return SchedulerFixtures.PassCrossingRequest(new InstanceIDSource());
        }

        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        private static ProcessExecutionConfiguration Configuration(
            string execution,
            string compilation,
            string sourceRevision,
            string process,
            string core,
            string ruleConfiguration,
            string sessionSeed)
        {
            return new ProcessExecutionConfiguration(
                execution,
                compilation,
                sourceRevision,
                process,
                core,
                ruleConfiguration,
                sessionSeed,
                SchedulerFixtures.StandardThresholds());
        }

        private static ExecutionRequest WithConfiguration(ExecutionRequest request, ProcessExecutionConfiguration configuration)
        {
            return new ExecutionRequest(
                request.Source,
                configuration,
                request.RevisionStamps,
                request.InitialState,
                request.InstalledDependencies);
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

        private static ExecutionRequest WithInstalled(ExecutionRequest request, IReadOnlyList<DependencyInstance> installed)
        {
            return new ExecutionRequest(
                request.Source,
                request.Configuration,
                request.RevisionStamps,
                request.InitialState,
                installed);
        }

        private static ExecutionRequest WithInitialState(ExecutionRequest request, InitialExecutionState initialState)
        {
            return new ExecutionRequest(
                request.Source,
                request.Configuration,
                request.RevisionStamps,
                initialState,
                request.InstalledDependencies);
        }

        private static ExecutionEvidenceHeader WithRelevantPatches(ExecutionEvidenceHeader header, IReadOnlyList<InstanceID> patches)
        {
            return new ExecutionEvidenceHeader(
                header.ExecutionIdentity,
                header.CompilationIdentity,
                header.CompiledSourceRevision,
                header.ProcessIdentity,
                header.CoreIdentity,
                header.ProcessRuleConfigurationIdentity,
                header.SessionSeedIdentity,
                header.RevisionStamps,
                header.ActiveDirectiveInstances,
                header.InstalledDependencyInstances,
                patches,
                header.InitialState);
        }

        private static ExecutionRecord WithHeader(ExecutionRecord record, ExecutionEvidenceHeader header)
        {
            return new ExecutionRecord(
                header,
                record.Units,
                record.Events,
                record.TraversalOrder,
                record.ThresholdHistory,
                record.CounterHistory,
                record.SafetyCounts,
                record.SafetyStatus,
                record.FinalState,
                record.Defects,
                record.CompletionStatus);
        }

        private static FinalExecutionState WithScore(FinalExecutionState state, int score)
        {
            return new FinalExecutionState(state.FinalValue, state.FinalSignal, new ScoreValue(score), state.FinalOutput);
        }

        private static EventRecord WithSubtype(EventRecord record, string subtype)
        {
            EventEvidence evidence = record.Evidence;
            EventEvidence altered = new(
                evidence.Family,
                subtype,
                evidence.Qualifiers,
                evidence.CausalDepth,
                evidence.ContainingUnit,
                evidence.ParentEvent,
                evidence.CausingEvent,
                evidence.HostInstance,
                evidence.CoreLineIdentity,
                evidence.EffectOriginInstance,
                evidence.Ownership,
                evidence.Position,
                evidence.TargetIdentity,
                evidence.Lineage,
                evidence.AddedExecutionRequestIdentity,
                evidence.AddedExecutionDepth,
                evidence.StructureContext,
                evidence.Disposition,
                evidence.DispositionReason,
                evidence.SafetyStatus,
                evidence.Payload);

            return new EventRecord(record.Identity, altered);
        }

        private static RuntimeUnitRecord WithClosureStatus(RuntimeUnitRecord record, UnitClosureStatus status)
        {
            RuntimeUnitClosure closure = record.Closure;
            RuntimeUnitClosure altered = new(
                closure.OriginalDisposition,
                closure.FinalDisposition,
                closure.PrimaryOperationEvent,
                closure.CreatedRequestIdentities,
                status,
                closure.SafetyStatus);

            return new RuntimeUnitRecord(
                record.Identity,
                record.Opening,
                altered,
                record.ChildEvents,
                record.DescendantUnits,
                record.StartOrder,
                record.CompletionOrder);
        }

        private static ExecutionRecord Rebuild(
            ExecutionRecord source,
            IReadOnlyList<RuntimeUnitRecord> units = null,
            IReadOnlyList<EventRecord> events = null,
            IReadOnlyList<RuntimeUnitID> traversalOrder = null,
            IReadOnlyList<TraceEventID> thresholdHistory = null,
            IReadOnlyList<TraceEventID> counterHistory = null,
            SafetyCounts? safetyCounts = null,
            SafetyStatus? safetyStatus = null,
            FinalExecutionState finalState = null,
            IReadOnlyList<EvidenceDefect> defects = null,
            ExecutionCompletionStatus? completionStatus = null)
        {
            return new ExecutionRecord(
                source.Header,
                units ?? source.Units,
                events ?? source.Events,
                traversalOrder ?? source.TraversalOrder,
                thresholdHistory ?? source.ThresholdHistory,
                counterHistory ?? source.CounterHistory,
                safetyCounts ?? source.SafetyCounts,
                safetyStatus ?? source.SafetyStatus,
                finalState ?? source.FinalState,
                defects ?? source.Defects,
                completionStatus ?? source.CompletionStatus);
        }
    }
}
