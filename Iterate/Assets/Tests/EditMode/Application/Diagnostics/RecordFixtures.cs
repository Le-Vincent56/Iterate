using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Determinism;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Diagnostics.Tests
{
    /// <summary>
    /// Hand-built <see cref="ExecutionRecord"/> values for the codec suites. Built rather than generated
    /// so each one isolates a shape the codec has to carry: a minimal record, an aborted one, one
    /// carrying defects, one whose unit sits inside nested Structures, and one opened by an added
    /// execution. The shipped-catalog fixtures exercise the real thing; these exercise the corners a
    /// real execution may not reach.
    ///
    /// Several constructor rules constrain what is representable and are honoured here rather than
    /// worked around: a closure's original and final disposition must differ, a Core-owned opening
    /// carries a Core line identity and no host instance while a player-owned one is the reverse, and a
    /// Structure context needs a non-empty ancestry.
    /// </summary>
    public static class RecordFixtures
    {
        /// <summary>
        /// The smallest legal record: one Core-owned unit, no events, no defects.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord Minimal()
        {
            return new ExecutionRecord(
                Header(),
                new List<RuntimeUnitRecord> { Unit(1, CoreOwnedOpening(), Closure()) },
                Array.Empty<EventRecord>(),
                new List<RuntimeUnitID> { new(1) },
                Array.Empty<TraceEventID>(),
                Array.Empty<TraceEventID>(),
                new SafetyCounts(0, 0, 1, 0, 0),
                SafetyStatus.Normal,
                new FinalExecutionState(new ValueAmount(4), new SignalValue(0), new ScoreValue(2), new ScoreValue(2)),
                Array.Empty<EvidenceDefect>(),
                ExecutionCompletionStatus.Completed
            );
        }

        /// <summary>
        /// A record whose execution was stopped by a safety ceiling. The completion status and the safety
        /// status are cross-checked by the constructor, so these two always move together.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord Aborted()
        {
            return new ExecutionRecord(
                Header(),
                new List<RuntimeUnitRecord>
                {
                    Unit(
                        1,
                        CoreOwnedOpening(),
                        new RuntimeUnitClosure(
                            null,
                            EventDisposition.Cancelled,
                            null,
                            Array.Empty<string>(),
                            UnitClosureStatus.SafetyIntervention,
                            SafetyStatus.SafetyAborted
                        )
                    )
                },
                Array.Empty<EventRecord>(),
                new List<RuntimeUnitID> { new(1) },
                Array.Empty<TraceEventID>(),
                Array.Empty<TraceEventID>(),
                new SafetyCounts(3, 2, 1, 0, 0),
                SafetyStatus.SafetyAborted,
                new FinalExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0), new ScoreValue(0)),
                Array.Empty<EvidenceDefect>(),
                ExecutionCompletionStatus.SafetyAborted
            );
        }

        /// <summary>
        /// A record carrying both kinds of defect anchor: one pinned to a unit and one pinned to nothing.
        /// A defect anchors to at most one record, so an event-anchored and a unit-anchored defect are
        /// never the same defect.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord WithDefects()
        {
            return new ExecutionRecord(
                Header(),
                new List<RuntimeUnitRecord> { Unit(1, CoreOwnedOpening(), Closure()) },
                Array.Empty<EventRecord>(),
                new List<RuntimeUnitID> { new(1) },
                Array.Empty<TraceEventID>(),
                Array.Empty<TraceEventID>(),
                new SafetyCounts(0, 0, 1, 0, 0),
                SafetyStatus.Normal,
                new FinalExecutionState(new ValueAmount(1), new SignalValue(0), new ScoreValue(0), new ScoreValue(0)),
                new List<EvidenceDefect>
                {
                    new(null, new RuntimeUnitID(1), "closure.status", "the unit closed without a disposition."),
                    new(null, null, "header.coreIdentity", "the Core identity was recovered from the compilation.")
                },
                ExecutionCompletionStatus.Completed
            );
        }

        /// <summary>
        /// A record whose unit sits inside two nested Structures, with a Condition evaluation but no
        /// Repeat iteration — the null-versus-empty case the codec must preserve exactly.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord WithStructureContext()
        {
            RuntimeUnitOpening opening = new(
                new InstanceID(7),
                null,
                OwnershipClassification.PlayerOwned,
                new SourcePosition(3),
                ActivationKind.CanonicalTraversal,
                null,
                0,
                EffectOriginLineage.Empty,
                new StructureContext(
                    new List<InstanceID> { new(4), new(5) },
                    "structure-entry-1",
                    null,
                    "condition-evaluation-1"
                ),
                null
            );

            return new ExecutionRecord(
                Header(),
                new List<RuntimeUnitRecord> { Unit(1, opening, Closure()) },
                Array.Empty<EventRecord>(),
                new List<RuntimeUnitID> { new(1) },
                new List<TraceEventID> { new(1) },
                new List<TraceEventID> { new(2) },
                new SafetyCounts(0, 0, 1, 1, 0),
                SafetyStatus.Normal,
                new FinalExecutionState(new ValueAmount(8), new SignalValue(1), new ScoreValue(3), new ScoreValue(3)),
                Array.Empty<EvidenceDefect>(),
                ExecutionCompletionStatus.Completed
            );
        }

        /// <summary>
        /// A record with a second unit opened by an added execution: a lineage two deep, a parent unit,
        /// and the originating request identity an added execution requires.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord WithAddedExecution()
        {
            RuntimeUnitOpening added = new(
                new InstanceID(9),
                null,
                OwnershipClassification.PlayerOwned,
                new SourcePosition(2),
                ActivationKind.AddedExecution,
                "request-1",
                2,
                EffectOriginLineage.Empty.Append(new InstanceID(9)).Append(new InstanceID(11)),
                null,
                new RuntimeUnitID(1)
            );

            return new ExecutionRecord(
                Header(),
                new List<RuntimeUnitRecord>
                {
                    Unit(1, CoreOwnedOpening(), Closure(), descendants: new List<RuntimeUnitID> { new(2) }),
                    Unit(2, added, Closure())
                },
                Array.Empty<EventRecord>(),
                new List<RuntimeUnitID> { new(1), new(2) },
                Array.Empty<TraceEventID>(),
                Array.Empty<TraceEventID>(),
                new SafetyCounts(2, 1, 2, 0, 0),
                SafetyStatus.SafetyLimitApproached,
                new FinalExecutionState(new ValueAmount(6), new SignalValue(0), new ScoreValue(6), new ScoreValue(6)),
                Array.Empty<EvidenceDefect>(),
                ExecutionCompletionStatus.Completed
            );
        }

        /// <summary>
        /// A record carrying a quantity-change payload with every optional part present: applied
        /// modifiers, both bounds, and a replacement link.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord WithQuantityChange()
        {
            QuantityChangePayload payload = new(
                "value",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                4,
                2,
                new TraceEventID(1),
                new List<QuantityModifierEvidence> { new("WB-PAT-001", "ADDITIVE", 2) },
                new QuantityBoundsEvidence(0, 99),
                4,
                6
            );

            return WithEvent(Evidence(EventFamilies.Quantity, "QUANTITY_CHANGED", payload, new[] { "PRIMARY" }));
        }

        /// <summary>
        /// A record carrying a random-decision payload whose context hash is genuine: the decision is
        /// made by the real service rather than hand-written, so a codec that mangled the hash fails
        /// against a value the service itself derived.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord WithRandomDecision()
        {
            DecisionContextComponents components = new(
                "seed-1",
                "fixture-0.0.1",
                "iterate-rules-1",
                "WB-SYS-001",
                "WB-PROC-002",
                "scope-1",
                "event-1",
                "origin-1",
                "Shop offer replacement",
                1
            );

            DecisionRequest request = new(
                "selection-1",
                "Shop offer replacement",
                "PROCESS",
                "POOL",
                "ELIGIBLE",
                "AT_DECISION",
                "DEFINITION_THEN_INSTANCE",
                1,
                SelectionMethod.UniformSingleSelection,
                ReplacementBehavior.RemovedForThisDecision,
                // A weighting rule is present exactly for weighted selection, and an unconsumed-suffix
                // disposition exactly for a random ordering of a captured finite set. Both are paired
                // rules on the constructor, so a uniform single selection must pass null for each.
                null,
                "ORDERING_KEY",
                null,
                InsufficientCandidateBehavior.FailToQualify,
                DecisionContext.Derive(components)
            );

            DecisionResult decision = new DeterminismService().Decide(
                request,
                CandidateSnapshot.Create(new List<CandidateEntry>
                {
                    new("WB-INS-002", new CandidateOrderingKey("WB-INS-002", null, null, null), null),
                    new("WB-INS-012", new CandidateOrderingKey("WB-INS-012", null, null, null), null)
                })
            );

            return WithEvent(Evidence(
                EventFamilies.RandomSelection,
                "RANDOM_DECISION_RESOLVED",
                new RandomDecisionPayload(decision.Record),
                Array.Empty<string>()
            ));
        }

        /// <summary>
        /// A record carrying a safety-abort payload. No shipped fixture breaches a ceiling, so this is
        /// the only place that payload kind is exercised at all.
        /// </summary>
        /// <returns>The record.</returns>
        public static ExecutionRecord WithSafetyAbort()
        {
            SafetyAbortPayload payload = new(
                "occurrence-1",
                new RuntimeUnitID(1),
                new List<BreachedLimit> { new("ADDED_EXECUTION_DEPTH", 8, 9) }
            );

            ExecutionRecord aborted = Aborted();
            return new ExecutionRecord(
                aborted.Header,
                aborted.Units,
                new List<EventRecord>
                {
                    new(new TraceEventID(1), Evidence(EventFamilies.Safety, "SAFETY_ABORTED", payload, Array.Empty<string>()))
                },
                aborted.TraversalOrder,
                aborted.ThresholdHistory,
                aborted.CounterHistory,
                aborted.SafetyCounts,
                aborted.SafetyStatus,
                aborted.FinalState,
                aborted.Defects,
                aborted.CompletionStatus
            );
        }

        /// <summary>
        /// Builds a minimal record carrying exactly one event.
        /// </summary>
        /// <param name="evidence">The event's evidence.</param>
        /// <returns>The record.</returns>
        private static ExecutionRecord WithEvent(EventEvidence evidence)
        {
            ExecutionRecord minimal = Minimal();
            return new ExecutionRecord(
                minimal.Header,
                minimal.Units,
                new List<EventRecord> { new(new TraceEventID(1), evidence) },
                minimal.TraversalOrder,
                minimal.ThresholdHistory,
                minimal.CounterHistory,
                minimal.SafetyCounts,
                minimal.SafetyStatus,
                minimal.FinalState,
                minimal.Defects,
                minimal.CompletionStatus
            );
        }

        /// <summary>
        /// Builds event evidence around a payload, Core-owned so no host instance is required.
        /// </summary>
        /// <param name="family">The event family, which must be a registry member.</param>
        /// <param name="subtype">The event subtype.</param>
        /// <param name="payload">The payload, or null.</param>
        /// <param name="qualifiers">The qualifiers, empty or not.</param>
        /// <returns>The evidence.</returns>
        private static EventEvidence Evidence(
            string family,
            string subtype,
            EventPayload payload,
            IReadOnlyList<string> qualifiers
        )
        {
            return new EventEvidence(
                family,
                subtype,
                qualifiers,
                0,
                new RuntimeUnitID(1),
                null,
                null,
                null,
                "core-line-1",
                null,
                OwnershipClassification.CoreOwned,
                new SourcePosition(1),
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                EventDisposition.Resolved,
                null,
                SafetyStatus.Normal,
                payload
            );
        }

        /// <summary>
        /// The header every fixture shares.
        /// </summary>
        /// <returns>The header.</returns>
        public static ExecutionEvidenceHeader Header()
        {
            return new ExecutionEvidenceHeader(
                "execution-1",
                "compilation-1",
                "source-revision-1",
                "WB-PROC-002",
                "WB-CORE-002",
                "WB-PRC-002",
                "seed-1",
                new List<RevisionStamp>
                {
                    new("Content Catalog", "fixture-0.0.1"),
                    new("Random Service", "iterate-rng-1")
                },
                new List<InstanceID> { new(20) },
                new List<InstanceID> { new(21), new(22) },
                new List<InstanceID> { new(23) },
                new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0))
            );
        }

        /// <summary>
        /// Builds a unit record at the given identity and order.
        /// </summary>
        /// <param name="identity">The unit's identity value, used for both orders.</param>
        /// <param name="opening">The unit's opening.</param>
        /// <param name="closure">The unit's closure.</param>
        /// <param name="descendants">The descendant units, or null for none.</param>
        /// <returns>The unit record.</returns>
        public static RuntimeUnitRecord Unit(
            int identity,
            RuntimeUnitOpening opening,
            RuntimeUnitClosure closure,
            IReadOnlyList<RuntimeUnitID> descendants = null
        )
        {
            return new RuntimeUnitRecord(
                new RuntimeUnitID(identity),
                opening,
                closure,
                Array.Empty<TraceEventID>(),
                descendants ?? Array.Empty<RuntimeUnitID>(),
                identity,
                identity
            );
        }

        /// <summary>
        /// A Core-owned opening: a Core line identity and no host instance, which is the pairing the
        /// constructor enforces.
        /// </summary>
        /// <returns>The opening.</returns>
        public static RuntimeUnitOpening CoreOwnedOpening()
        {
            return new RuntimeUnitOpening(
                null,
                "core-line-1",
                OwnershipClassification.CoreOwned,
                new SourcePosition(1),
                ActivationKind.CanonicalTraversal,
                null,
                0,
                EffectOriginLineage.Empty,
                null,
                null
            );
        }

        /// <summary>
        /// A closure that changed disposition, which the constructor requires of any closure naming an
        /// original one.
        /// </summary>
        /// <returns>The closure.</returns>
        public static RuntimeUnitClosure Closure()
        {
            return new RuntimeUnitClosure(
                EventDisposition.Skipped,
                EventDisposition.Resolved,
                new TraceEventID(1),
                new List<string> { "request-1" },
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal
            );
        }
    }
}
