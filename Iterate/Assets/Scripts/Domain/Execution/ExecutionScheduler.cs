using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The Execution Engine spine: a synchronous entry point that drives the nine canonical execution
    /// phases in pinned order over an explicit frame stack, writing the Process-scoped
    /// <see cref="ExecutionTraceBuilder"/> and returning the frozen <see cref="ExecutionRecord"/>. Each
    /// Core/Instruction unit emits the full eight-event SOURCE/OPERATION stream — activation first,
    /// disposition finalization before completion — offers its pending operation to the effect engine
    /// at the modification band, and offers its resolved operation and finalized quantity change at
    /// the immediate-reaction boundary in candidate-event causal order. A Structure header opens a
    /// unit-less Structure walk that consumes its reserved footprint: Repeat iterations execute each
    /// occupied contained child as a distinct unit per iteration, Conditions capture one predicate
    /// snapshot and evaluate once per entry, and every governed child carries its Structure context.
    /// A FALSE Condition skips each occupied governed child at the pre-operation band — one skip
    /// event with its explicit cause, one boundary offer; a qualified rescue transforms the same unit
    /// to continue to full resolution. Qualified reactions resolve inside the causing unit's closure
    /// as captured sibling batches, each descendant branch resolving to closure before the remaining
    /// siblings. Qualified added-execution creators commit after their batch's reactions and capture
    /// one request each on the creating branch's frame; every unit drains its own captured batch at
    /// its tail — after the unit closes, before its frame pops and before traversal advances — so
    /// depth-first branch completion is structural recursion. Every unit additionally carries the
    /// four retained occurrence facts the host Patches read: its source position, whether it
    /// occupies the final occupied player-controlled position (a dedicated hoist beside the boundary
    /// index — the two serve different laws), the enclosing Condition's retained evaluation outcome
    /// (threaded through traversal for canonical units and through the request memento for
    /// re-entered ones), and the runtime-adjacency snapshot captured when the unit opened; the
    /// adjacency tracker records every closure, an operand-adjustment modification contributes its
    /// declared delta at the modification band, socketed effects stamp their host instance onto
    /// their qualification evidence, every allowance consumption names the triggering unit, and
    /// target-lock updates ride their batches undrained until the boundary restaging. Every event a
    /// unit emits carries that unit's branch lineage, so canonical branches stay byte-identical to
    /// lineage-free evidence. The builder is the only instance field; all per-execution state
    /// travels in a nested context constructed fresh per call, so the scheduler is reusable and
    /// reentrancy-clean. Every request-derived operation that can throw completes before the builder
    /// is begun, so a rejected request always leaves the builder usable.
    /// A configured Process rule adds one reading to every unit: the pending operation is matched
    /// once, before the pre-operation band, and a counter effect sitting at its ceiling produces an
    /// intervention that skips the operation, while one below its ceiling is captured as a gain the
    /// unit commits only if its primary operation actually resolves. Counter reactions resolve
    /// after the ordinary reactions of the same boundary, so a cooling request follows both its
    /// causing change and that change's own reaction closure, and the cooling window closes when
    /// the designated final Core output activates.
    /// </summary>
    public sealed class ExecutionScheduler
    {
        /// <summary>
        /// The declared stage token for a modification-band transformation.
        /// </summary>
        private const string ModificationStage = "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION";
        
        /// <summary>
        /// The one wired named scheduling boundary the scheduler detects.
        /// </summary>
        private const string PlayerTraversalBoundary = "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL";
        
        /// <summary>
        /// The skip-cause prefix a Process-rule intervention composes, completed with the
        /// intervening rule's stable identity — the CONDITION_FALSE convention.
        /// </summary>
        private const string ProcessRuleSkipCause = "PROCESS_RULE_INTERVENTION:";

        /// <summary>
        /// The canonical band name a Process-counter threshold crossing carries.
        /// </summary>
        private const string ThrottlingBand = "THROTTLING";

        private readonly ExecutionTraceBuilder _builder;

        public ExecutionScheduler(ExecutionTraceBuilder builder)
        {
            _builder = builder ?? throw new ArgumentException("A scheduler requires a trace builder.", nameof(builder));
        }

        /// <summary>
        /// Executes one request through the nine phases and returns the frozen execution record.
        /// </summary>
        /// <param name="request">The fully-validated execution request.</param>
        /// <returns>The frozen execution record.</returns>
        /// <exception cref="ArgumentException">Thrown when the request is null.</exception>
        public ExecutionRecord Execute(ExecutionRequest request)
        {
            if (request == null)
                throw new ArgumentException("Execute requires a request.", nameof(request));

            ExecutionContext context = new ExecutionContext(request);
            AcceptCompilationHandoff(context);
            InitializeExecutionScope(context);
            ResetRuntime(context);

            ExecutionRecord record;
            try
            {
                TraverseSource(context);
                ResolveTraversalBoundaryEffects(context);
                CloseExecution(context);
                record = FinalizeEvidence(context);
            }
            catch (SafetyAbortSignal signal)
            {
                record = FinalizeSafetyAbort(context, signal);
            }

            ExpireCompilationScopedEffects(context);
            return HandOffToResultReview(context, record);
        }

        /// <summary>
        /// Phase 1: accepts the locked compiled source. Every slot kind the arrangement can carry is
        /// executable and the arrangement's own construction guarantees footprint contiguity,
        /// containment ownership, and no nesting, so no content re-check remains; the phase is the
        /// named handoff point.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void AcceptCompilationHandoff(ExecutionContext context)
        {
        }

        /// <summary>
        /// Phase 2: assembles the evidence header fully — including the installed Dependency instance
        /// list — begins the builder, and appends the EXECUTION_STARTED lifecycle bookend. The effect
        /// engine was assembled in the context from the request's already-interpreted effects, so all
        /// throwing work completes before the builder is begun.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void InitializeExecutionScope(ExecutionContext context)
        {
            ExecutionRequest request = context.Request;
            ProcessExecutionConfiguration configuration = request.Configuration;

            IReadOnlyList<DirectiveInstance> pragmas = request.Source.Pragmas;
            List<InstanceID> activeDirectives = new List<InstanceID>(pragmas.Count);
            for (int i = 0; i < pragmas.Count; i++)
            {
                activeDirectives.Add(pragmas[i].InstanceID);
            }

            IReadOnlyList<DependencyInstance> installed = request.InstalledDependencies;
            List<InstanceID> installedDependencies = new List<InstanceID>(installed.Count);
            for (int i = 0; i < installed.Count; i++)
            {
                installedDependencies.Add(installed[i].InstanceID);
            }

            ExecutionEvidenceHeader header = new ExecutionEvidenceHeader(
                configuration.ExecutionIdentity,
                configuration.CompilationIdentity,
                configuration.CompiledSourceRevision,
                configuration.ProcessIdentity,
                configuration.CoreIdentity,
                configuration.ProcessRuleConfigurationIdentity,
                configuration.SessionSeedIdentity,
                request.RevisionStamps,
                activeDirectives,
                installedDependencies,
                new List<InstanceID>(),
                request.InitialState
            );

            _builder.Begin(header);
            AppendBookend(context, ExecutionEventSubtypes.ExecutionStarted);
        }

        /// <summary>
        /// Phase 3: neutrally resets Value, Signal, and Score to zero, appending a QUANTITY_RESET event for
        /// each with its initial-state prior, outside any unit. No shipped trigger observes the reset
        /// subtype, so reset evidence produces no boundary offers.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ResetRuntime(ExecutionContext context)
        {
            InitialExecutionState initialState = context.Request.InitialState;
            ResetRegister(context, CoreRegister.Value, "Value", initialState.InitialValue.Value);
            ResetRegister(context, CoreRegister.Signal, "Signal", initialState.InitialSignal.Value);
            ResetRegister(context, CoreRegister.Score, "Score", initialState.InitialScore.Value);
            InitializeProcessCounter(context);
        }
        
        /// <summary>
        /// Appends the governing Process counter's initialization evidence — its reset boundary —
        /// and returns the counter to zero. No counter value carries between executions. Silent when
        /// the Process declares no rule.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void InitializeProcessCounter(ExecutionContext context)
        {
            if (!context.HasProcessRule)
                return;

            context.Counter.Reset();
            QuantityChangePayload payload = new QuantityChangePayload(
                CounterIdentity(context),
                QuantityCategory.ProcessCounter,
                QuantityOperationType.Assign,
                0,
                0,
                null,
                Array.Empty<QuantityModifierEvidence>(),
                null,
                0,
                0
            );

            _builder.AppendEvent(new EventEvidence(
                EventFamilies.Quantity,
                ExecutionEventSubtypes.QuantityReset,
                Array.Empty<string>(),
                0,
                null,
                null,
                null,
                null,
                null,
                context.Request.Configuration.ProcessRule.InstanceID,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                payload
            ));
        }

        /// <summary>
        /// Returns the counter identity the governing Process rule's requests name.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <returns>The counter identity token, or the empty string when no request declares one.</returns>
        private static string CounterIdentity(ExecutionContext context)
        {
            IReadOnlyList<ActiveEffect> effects = context.Request.InterpretedEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].CounterRequest != null)
                    return effects[i].CounterRequest.Counter;
            }

            return string.Empty;
        }

        /// <summary>
        /// Phase 4: walks the arrangement top to bottom by index, opening a runtime unit for each
        /// top-level Core and Instruction slot and dispatching each Structure header to the Structure
        /// walk, which consumes the complete reserved footprint so contained slots are traversed only
        /// through their governing Structure.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <exception cref="InvalidOperationException">Thrown when a contained slot reaches the top level — unreachable behind arrangement validation.</exception>
        private void TraverseSource(ExecutionContext context)
        {
            SourceArrangement arrangement = context.Request.Source.Arrangement;
            IReadOnlyList<SourceSlot> slots = arrangement.Slots;
            int lastPlayerIndex = LastPlayerControlledIndex(slots);
            bool boundaryResolved = false;

            if (lastPlayerIndex < 0)
            {
                ResolvePlayerTraversalBoundary(context);
                boundaryResolved = true;
            }

            int index = 0;
            while (index < slots.Count)
            {
                SourceSlot slot = slots[index];
                switch (slot.Kind)
                {
                    case SourceSlotKind.Empty:
                        index++;
                        break;

                    case SourceSlotKind.Core:
                    case SourceSlotKind.Instruction:
                        TraverseUnit(context, slot, null, null, null, null);
                        index++;
                        break;

                    case SourceSlotKind.StructureHeader:
                        index = TraverseStructure(context, slot, arrangement, index);
                        break;

                    default:
                        throw new InvalidOperationException("A contained slot reached top-level traversal.");
                }

                if (boundaryResolved || index <= lastPlayerIndex) continue;
                
                ResolvePlayerTraversalBoundary(context);
                boundaryResolved = true;
            }

            IReadOnlyList<CancelledRequest> stillPending = context.PendingRequests.DrainPending();
            for (int i = 0; i < stillPending.Count; i++)
            {
                AppendCancellation(context, stillPending[i]);
            }
        }
        
        /// <summary>
        /// Returns the greatest top-level slot index that a player-controlled object occupies — a
        /// Structure header's index stands for its whole footprint, since the walk consumes it — or
        /// -1 when the arrangement holds none. This index places the player-traversal boundary; the
        /// final occupied player-controlled position is a separate context fact, because the two
        /// serve different laws.
        /// </summary>
        /// <param name="slots">The arrangement's slots.</param>
        /// <returns>The last player-controlled index, or -1.</returns>
        private static int LastPlayerControlledIndex(IReadOnlyList<SourceSlot> slots)
        {
            int last = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlotKind kind = slots[i].Kind;
                if (kind is SourceSlotKind.Instruction or SourceSlotKind.StructureHeader)
                    last = i;
            }

            return last;
        }

        /// <summary>
        /// Phase 5: resolves declared traversal-boundary effects. A named no-op — nothing is declared
        /// at this content, and deliberately so. The end-of-player-controlled-source-traversal
        /// boundary resolves inside phase 4, not here: canon places it after every player position's
        /// units and descendant branches complete but before traversal resumes into later immutable
        /// Core source objects, and orders ALIGN's Value adjustment between the two. A boundary
        /// resolved in this phase would land after the Core lines it must precede. Phase 5 remains
        /// the home of effects declared at *complete* traversal; none ship.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ResolveTraversalBoundaryEffects(ExecutionContext context) { }

        /// <summary>
        /// Phase 6: appends the EXECUTION_COMPLETED lifecycle bookend.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void CloseExecution(ExecutionContext context) => AppendBookend(context, ExecutionEventSubtypes.ExecutionCompleted);

        /// <summary>
        /// Phase 7: finalizes the evidence into a frozen record with the final register state and the
        /// safety counts.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <returns>The frozen execution record.</returns>
        private ExecutionRecord FinalizeEvidence(ExecutionContext context)
        {
            ExecutionRegisters registers = context.Registers;
            FinalExecutionState finalState = new FinalExecutionState(
                (ValueAmount)registers.Value,
                (SignalValue)registers.Signal,
                (ScoreValue)registers.Score,
                (ScoreValue)registers.Score
            );

            return _builder.Finalize(
                ExecutionCompletionStatus.Completed,
                context.CurrentSafetyStatus,
                context.Tallies.ToCounts(),
                finalState
            );
        }

        /// <summary>
        /// Phase 8: expires execution-scoped effect state — the engine's registered descriptors and the
        /// frequency ledger's consumed allowances are explicitly cleared, evidence-free, after the freeze.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ExpireCompilationScopedEffects(ExecutionContext context)
        {
            context.Engine.Clear();
            context.Ledger.Clear();
        }

        /// <summary>
        /// Phase 9: hands the frozen record off to Result Review by returning it; its handoff status was
        /// derived at freeze.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="record">The frozen execution record.</param>
        /// <returns>The frozen execution record.</returns>
        private ExecutionRecord HandOffToResultReview(ExecutionContext context, ExecutionRecord record)
        {
            return record;
        }

        /// <summary>
        /// Walks one Structure entry: activation and entry events under the entry context, the
        /// kind-specific body, and the exit event after complete child governance closure. Consumes
        /// the complete reserved footprint and returns the slot index past it.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="headerSlot">The Structure header slot.</param>
        /// <param name="arrangement">The source arrangement being traversed.</param>
        /// <param name="headerIndex">The header's slot index.</param>
        /// <returns>The slot index past the Structure's footprint.</returns>
        private int TraverseStructure(
            ExecutionContext context,
            SourceSlot headerSlot,
            SourceArrangement arrangement,
            int headerIndex)
        {
            StructureInstance structure = headerSlot.Structure;
            string entryIdentity = StructureIdentities.Entry(structure.InstanceID, headerSlot.Position, 1);
            StructureContext entryContext = new StructureContext(
                new List<InstanceID> { structure.InstanceID },
                entryIdentity,
                null,
                null);

            _builder.AppendEvent(StructureEvent(
                context,
                ExecutionEventSubtypes.StructureActivated,
                headerSlot,
                entryContext
            ));
            _builder.AppendEvent(StructureEvent(
                context,
                ExecutionEventSubtypes.StructureEntered,
                headerSlot,
                entryContext
            ));

            int firstContained = headerIndex + 1;
            int pastFootprint = headerIndex + structure.Definition.SourceFootprint;
            if (structure.Definition.StructureKind == StructureKind.Repeat)
                TraverseRepeat(context, headerSlot, arrangement, firstContained, pastFootprint, entryIdentity, entryContext);
            else
                TraverseCondition(context, headerSlot, arrangement, firstContained, pastFootprint, entryIdentity, entryContext);

            _builder.AppendEvent(StructureEvent(
                context,
                ExecutionEventSubtypes.StructureExited,
                headerSlot,
                entryContext
            ));
            return pastFootprint;
        }

        /// <summary>
        /// Runs a Repeat body: the count captured once at entry, then per ascending iteration a
        /// per-iteration context, the iteration start event, each occupied contained child executed as
        /// a full runtime unit in contained order reading current state, and the iteration completion
        /// event after child causal closure — including each child's own descendant branches. Empty
        /// contained positions produce nothing.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="headerSlot">The Structure header slot.</param>
        /// <param name="arrangement">The source arrangement being traversed.</param>
        /// <param name="firstContained">The first contained slot index.</param>
        /// <param name="pastFootprint">The slot index past the footprint.</param>
        /// <param name="entryIdentity">The entry identity.</param>
        /// <param name="entryContext">The entry context.</param>
        private void TraverseRepeat(
            ExecutionContext context,
            SourceSlot headerSlot,
            SourceArrangement arrangement,
            int firstContained,
            int pastFootprint,
            string entryIdentity,
            StructureContext entryContext
        )
        {
            _builder.AppendEvent(StructureEvent(
                context,
                ExecutionEventSubtypes.RepeatCountCaptured,
                headerSlot, 
                entryContext
            ));

            int count = headerSlot.Structure.Definition.RepeatCount;
            for (int iteration = 1; iteration <= count; iteration++)
            {
                StructureContext iterationContext = new StructureContext(
                    entryContext.StructureAncestry,
                    entryIdentity,
                    StructureIdentities.Iteration(entryIdentity, iteration),
                    null);

                _builder.AppendEvent(StructureEvent(
                    context, 
                    ExecutionEventSubtypes.RepeatIterationStarted,
                    headerSlot,
                    iterationContext
                ));
                for (int i = firstContained; i < pastFootprint; i++)
                {
                    SourceSlot contained = arrangement.Slots[i];
                    if (contained.Kind == SourceSlotKind.ContainedInstruction)
                        TraverseUnit(context, contained, iterationContext, null, null, null);
                }

                _builder.AppendEvent(StructureEvent(
                    context,
                    ExecutionEventSubtypes.RepeatIterationCompleted,
                    headerSlot,
                    iterationContext
                ));
            }
        }

        /// <summary>
        /// Runs a Condition body: one predicate snapshot captured at entry, one evaluation from the
        /// snapshot under the per-evaluation context, then the result event. TRUE schedules each
        /// occupied contained child normally under the evaluation context carrying the retained TRUE
        /// outcome; FALSE gives each occupied governed child one activation and one skipped
        /// disposition with this evaluation bound as the skip cause and the retained FALSE outcome —
        /// so a rescued child stays ineligible for succeeding-Condition observers.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="headerSlot">The Structure header slot.</param>
        /// <param name="arrangement">The source arrangement being traversed.</param>
        /// <param name="firstContained">The first contained slot index.</param>
        /// <param name="pastFootprint">The slot index past the footprint.</param>
        /// <param name="entryIdentity">The entry identity.</param>
        /// <param name="entryContext">The entry context.</param>
        private void TraverseCondition(
            ExecutionContext context,
            SourceSlot headerSlot,
            SourceArrangement arrangement,
            int firstContained,
            int pastFootprint,
            string entryIdentity,
            StructureContext entryContext
        )
        {
            string evaluationIdentity = StructureIdentities.Evaluation(entryIdentity);
            StructureContext evaluationContext = new StructureContext(
                entryContext.StructureAncestry,
                entryIdentity,
                null,
                evaluationIdentity
            );

            StructurePredicate predicate = headerSlot.Structure.Definition.Predicate;
            int snapshotValue = context.Registers.Read(predicate.Register);
            _builder.AppendEvent(StructureEvent(
                context,
                ExecutionEventSubtypes.ConditionSnapshotCaptured,
                headerSlot,
                evaluationContext
            ));

            if (ConditionPredicateEvaluator.Evaluate(predicate, snapshotValue))
            {
                TraceEventID resultEvent = _builder.AppendEvent(StructureEvent(
                    context, 
                    ExecutionEventSubtypes.ConditionTrue,
                    headerSlot,
                    evaluationContext
                ));
                OfferConditionSuccess(context, arrangement, firstContained, pastFootprint, resultEvent, evaluationIdentity, evaluationContext);

                for (int i = firstContained; i < pastFootprint; i++)
                {
                    SourceSlot contained = arrangement.Slots[i];
                    if (contained.Kind == SourceSlotKind.ContainedInstruction)
                        TraverseUnit(context, contained, evaluationContext, null, null, ConditionOutcome.True);
                }

                return;
            }

            _builder.AppendEvent(StructureEvent(
                context, 
                ExecutionEventSubtypes.ConditionFalse,
                headerSlot,
                evaluationContext
            ));
            string skipCause = "CONDITION_FALSE:" + evaluationIdentity;
            for (int i = firstContained; i < pastFootprint; i++)
            {
                SourceSlot contained = arrangement.Slots[i];
                if (contained.Kind == SourceSlotKind.ContainedInstruction)
                    TraverseUnit(context, contained, evaluationContext, skipCause, null, ConditionOutcome.False);
            }
        }

        /// <summary>
        /// Opens a unit for a Core or Instruction slot and drives it through the named stages: open
        /// and activate, pending operation, the pre-operation band — skip determination and the
        /// rescue offer — then either the skipped closure or the operation path and the resolved or
        /// rescued closure, then the adjacency record, the post-unit offer, and the frame's
        /// captured-request drain before the frame pops. The runtime-adjacency snapshot is captured
        /// before the unit opens, so the unit observes only predecessors that completed before it. A
        /// non-null request opens the unit as an added execution on the requested branch instead of
        /// a canonical traversal.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The Core or Instruction slot to execute.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="skipCause">The explicit skip cause, or null when the unit is not skipped.</param>
        /// <param name="request">The originating request, or null for a canonical traversal.</param>
        /// <param name="conditionResult">The enclosing Condition's retained evaluation outcome, or null outside one.</param>
        private void TraverseUnit(
            ExecutionContext context,
            SourceSlot slot,
            StructureContext structureContext,
            string skipCause,
            AddedExecutionRequest request,
            ConditionOutcome? conditionResult
        )
        {
            bool adjacentAfterSuccessfulScore = context.Adjacency.CurrentPredecessorQualifies;
            bool isCore = slot.Kind == SourceSlotKind.Core;
            RuntimeUnitID unit = OpenAndActivateUnit(context, slot, isCore, structureContext, request);
            ExecutionFrame frame = context.FrameStack[^1];
            EffectOriginLineage lineage = frame.Lineage;

            TraceEventID pendingEvent = EmitPendingOperation(context, slot, isCore, unit, structureContext, lineage);
            OperationOccurrence pendingOccurrence = BuildOperationOccurrence(context, slot, isCore, unit, pendingEvent, 0);
            EffectMatchBatch pendingBatch = context.Engine.MatchPendingOperation(pendingOccurrence);

            string effectiveSkipCause = skipCause;
            ActiveEffect counterEffect = null;
            if (skipCause == null && pendingBatch.CounterInterventions.Count > 0)
            {
                counterEffect = pendingBatch.CounterInterventions[0];
                effectiveSkipCause = OfferCounterIntervention(context, counterEffect, slot, isCore, unit, pendingEvent, lineage);
            }
            else if (skipCause == null && pendingBatch.DeferredCounterGains.Count > 0)
            {
                counterEffect = pendingBatch.DeferredCounterGains[0];
            }

            EventDisposition bandDisposition = ResolvePreOperationBand(context,
                slot,
                isCore,
                unit,
                pendingEvent,
                structureContext,
                effectiveSkipCause,
                lineage
            );

            TraceEventID completionEvent;
            bool actuallyIncreasedScore = false;
            if (bandDisposition == EventDisposition.Skipped)
            {
                completionEvent = CloseSkippedUnit(context, slot, isCore, unit, structureContext, lineage);
            }
            else
            {
                TraceEventID quantityEvent = ResolveOperationPath(
                    context,
                    slot,
                    isCore,
                    unit,
                    pendingEvent,
                    pendingOccurrence,
                    pendingBatch,
                    counterEffect,
                    structureContext,
                    frame,
                    conditionResult,
                    out actuallyIncreasedScore
                );
                completionEvent = FinalizeAndCloseUnit(context, slot, isCore, unit, quantityEvent, bandDisposition, structureContext, lineage);
            }

            context.Adjacency.RecordCompletion(
                bandDisposition,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                actuallyIncreasedScore
            );
            ResolvePendingRequests(context, slot, isCore, unit, frame, bandDisposition);
            OfferPostUnit(context, slot, isCore, unit, frame, completionEvent, bandDisposition, structureContext, conditionResult, adjacentAfterSuccessfulScore);
            DrainCapturedRequests(context, frame);
            context.FrameStack.RemoveAt(context.FrameStack.Count - 1);
        }

        /// <summary>
        /// Stage 1: preflights and opens the runtime unit, counts it, pushes its frame, and emits the
        /// activation and execution-start events. A canonical unit is self-rooted at depth zero with
        /// the empty lineage; a requested unit carries its request's identity, depth, lineage, and
        /// parent, and roots under its parent frame's original activation.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="request">The originating request, or null for a canonical traversal.</param>
        /// <returns>The opened unit.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the unit ceiling has been reached — preflight guards it.</exception>
        private RuntimeUnitID OpenAndActivateUnit(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            StructureContext structureContext,
            AddedExecutionRequest request
        )
        {
            IReadOnlyList<BreachedLimit> breached = context.Tallies.EvaluateUnitOpening();
            if (breached != null)
            {
                throw new SafetyAbortSignal(
                    EventFamilies.Source,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P" + slot.Position.LineNumber,
                    InnermostOpenUnit(context) ?? default,
                    breached
                );
            }

            ActivationKind activation = request == null ? ActivationKind.CanonicalTraversal : ActivationKind.AddedExecution;
            string requestIdentity = request?.RequestIdentity;
            int depth = request?.ProposedDepth ?? 0;
            EffectOriginLineage lineage = request == null ? EffectOriginLineage.Empty : request.Lineage;
            RuntimeUnitID? parentUnit = request?.ParentUnit;

            RuntimeUnitOpening opening = isCore
                ? new RuntimeUnitOpening(
                    null,
                    slot.Core.Identity,
                    OwnershipClassification.CoreOwned,
                    slot.Position,
                    activation,
                    requestIdentity,
                    depth,
                    lineage,
                    structureContext,
                    parentUnit
                )
                : new RuntimeUnitOpening(
                    slot.Instruction.InstanceID,
                    null,
                    OwnershipClassification.PlayerOwned,
                    slot.Position,
                    activation,
                    requestIdentity,
                    depth,
                    lineage,
                    structureContext,
                    parentUnit
                );

            RuntimeUnitID unit = _builder.OpenUnit(opening);
            context.Tallies.RecordUnitOpened();
            EmitApproachedLimits(context, unit);
            
            if (isCore && context.Request.Configuration.DesignatedFinalCoreOutputPosition == slot.Position)
                context.Engine.CloseCoolingWindow();

            RuntimeUnitID rootActivation = unit;
            if (request != null && context.FrameStack.Count > 0)
                rootActivation = context.FrameStack[^1].RootActivation;

            context.FrameStack.Add(new ExecutionFrame(unit, lineage, depth, rootActivation));

            _builder.AppendEvent(UnitStreamEvent(
                context,
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceObjectActivated,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));
            _builder.AppendEvent(UnitStreamEvent(
                context,
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceExecutionStarted,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));
            return unit;
        }

        /// <summary>
        /// Stage 2: emits the pending-operation event and begins the pending-operation tally window.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The pending-operation event.</returns>
        private TraceEventID EmitPendingOperation(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            StructureContext structureContext,
            EffectOriginLineage lineage
        )
        {
            TraceEventID pendingEvent = _builder.AppendEvent(UnitStreamEvent(
                context, 
                EventFamilies.Operation,
                ExecutionEventSubtypes.PrimaryOperationPending,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));
            context.Tallies.BeginPendingOperation();
            return pendingEvent;
        }

        /// <summary>
        /// Stage 3: the pre-operation band. Without a skip cause the unit proceeds Resolved. With one,
        /// the skip event is recorded with its explicit cause and offered to the engine's skip
        /// boundary: no qualified rescue leaves the unit Skipped; exactly one resolves the pinned
        /// qualification, commitment, and rescued-disposition chain and the unit proceeds Rescued.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="pendingEvent">The pending-operation event.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="skipCause">The explicit skip cause, or null when the unit is not skipped.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The unit's disposition after the band.</returns>
        /// <exception cref="InvalidOperationException">Thrown when more than one rescue qualifies — an undeclared conflict is an authoring defect.</exception>
        private EventDisposition ResolvePreOperationBand(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID pendingEvent,
            StructureContext structureContext,
            string skipCause,
            EffectOriginLineage lineage
        )
        {
            if (skipCause == null)
                return EventDisposition.Resolved;

            TraceEventID skipEvent = _builder.AppendEvent(SkipEvent(
                context, 
                slot,
                isCore,
                unit, 
                pendingEvent,
                skipCause,
                structureContext,
                lineage
            ));
            SkipOccurrence occurrence = new SkipOccurrence(
                unit,
                skipEvent,
                1,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                skipCause,
                true
            );

            EffectMatchBatch batch = context.Engine.MatchSkip(occurrence);
            if (batch.Qualified.Count == 0)
                return EventDisposition.Skipped;

            if (batch.Qualified.Count > 1)
                throw new InvalidOperationException("More than one rescue effect qualified against one skip; an undeclared rescuer conflict is an authoring defect.");

            ActiveEffect rescueEffect = batch.Qualified[0];
            _builder.AppendEvent(EffectChainEvent(
                context,
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectQualified,
                skipEvent,
                2,
                rescueEffect.Origin,
                rescueEffect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));
            context.Engine.Commit(rescueEffect, unit);
            _builder.AppendEvent(EffectChainEvent(
                context, 
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectCommitted,
                skipEvent,
                2,
                rescueEffect.Origin,
                rescueEffect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));
            _builder.AppendEvent(EffectChainEvent(
                context, 
                EventFamilies.Disposition,
                ExecutionEventSubtypes.SourceExecutionRescued,
                skipEvent,
                2,
                rescueEffect.Origin,
                rescueEffect.HostInstance,
                unit,
                EventDisposition.Rescued,
                null,
                lineage
            ));
            
            RecordTransformation(context, unit, rescueEffect.Origin);
            
            return EventDisposition.Rescued;
        }
        
        /// <summary>
        /// Records one Process-rule intervention against a pending operation and returns the skip
        /// cause it produces. The intervention identifies the rule, the counter state read at the
        /// pre-check, the affected source execution, and the resulting disposition; it changes no
        /// counter, so its payload reports a zero delta over the value it read. One transformation
        /// is recorded, since the intervention transforms the pending operation.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The qualified counter-intervention effect.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="pendingEvent">The pending-operation event.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The composed skip cause.</returns>
        /// <exception cref="SafetyAbortSignal">Thrown when the transformation ceiling has been reached.</exception>
        private string OfferCounterIntervention(
            ExecutionContext context,
            ActiveEffect effect,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID pendingEvent,
            EffectOriginLineage lineage
        )
        {
            RecordTransformation(context, unit, effect.Origin);

            int reading = context.Counter.Value;
            QuantityChangePayload state = new QuantityChangePayload(
                effect.CounterRequest.Counter,
                QuantityCategory.ProcessCounter,
                QuantityOperationType.Increment,
                effect.CounterRequest.Delta,
                reading,
                null,
                Array.Empty<QuantityModifierEvidence>(),
                new QuantityBoundsEvidence(
                    effect.CounterRequest.HasFloor ? effect.CounterRequest.Floor : (int?)null,
                    effect.CounterRequest.HasCeiling ? effect.CounterRequest.Ceiling : (int?)null),
                0,
                reading
            );

            _builder.AppendEvent(new EventEvidence(
                EventFamilies.Intervention,
                ExecutionEventSubtypes.ProcessRuleIntervened,
                Array.Empty<string>(),
                1,
                unit,
                null,
                pendingEvent,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                isCore ? slot.Core.Identity : null,
                effect.Origin,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                slot.Position,
                null,
                lineage,
                null,
                0,
                null,
                EventDisposition.Skipped,
                null,
                context.CurrentSafetyStatus,
                state
            ));

            return ProcessRuleSkipCause + context.Request.Configuration.ProcessRule.Definition.ID.Value;
        }

        /// <summary>
        /// Preflights and records one transformation of the current pending operation, throwing the
        /// abort signal when the transformation ceiling has been reached.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="origin">The transforming effect's origin.</param>
        /// <exception cref="SafetyAbortSignal">Thrown when the transformation ceiling has been reached.</exception>
        private void RecordTransformation(ExecutionContext context, RuntimeUnitID unit, InstanceID origin)
        {
            IReadOnlyList<BreachedLimit> breached = context.Tallies.EvaluateTransformation();
            if (breached != null)
            {
                throw new SafetyAbortSignal(
                    EventFamilies.Operation,
                    ExecutionEventSubtypes.PrimaryOperationModified,
                    origin.ToString(),
                    unit,
                    breached
                );
            }

            context.Tallies.RecordTransformation();
            EmitApproachedLimits(context, unit);
        }

        /// <summary>
        /// Stage 4 (skipped): finalizes and closes a skipped, unrescued unit — no modification offer,
        /// no resolution, no quantity delta, no threshold checks, no reaction offers. The frame stays
        /// on the stack for the post-unit offer and drain.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The unit's completion event.</returns>
        private TraceEventID CloseSkippedUnit(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            StructureContext structureContext,
            EffectOriginLineage lineage)
        {
            _builder.AppendEvent(DispositionFinalizedEvent(
                context, 
                slot,
                isCore,
                unit,
                EventDisposition.Skipped,
                structureContext,
                lineage
            ));
            TraceEventID completionEvent = _builder.AppendEvent(UnitStreamEvent(
                context, 
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceExecutionCompleted,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));

            RuntimeUnitClosure closure = new RuntimeUnitClosure(
                null,
                EventDisposition.Skipped,
                null,
                Array.Empty<string>(),
                UnitClosureStatus.NormalCompletion,
                context.CurrentSafetyStatus
            );
            _builder.CompleteUnit(unit, closure);
            return completionEvent;
        }

        /// <summary>
        /// Stage 4 (operating): drives the operation path — the modification boundary with first-time
        /// commitments and selected-host re-applications, each contributing its declared delta
        /// (an operand adjustment carries it on the modification operation, a quantity-change
        /// modification on its constant operand), resolution, the finalized quantity event with
        /// modifier evidence, threshold crossings, result finalization, both immediate-reaction
        /// boundaries in candidate-event causal order, and the primary-quantity batch's creator
        /// commitments after its reactions fully resolve. Reports whether the primary operation
        /// actually increased Score, for the adjacency record at closure. Target-lock updates ride
        /// the quantity batch undrained at this stage of the replacement.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="pendingEvent">The pending-operation event.</param>
        /// <param name="pendingOccurrence">The pending-operation occurrence matched before the pre-operation band.</param>
        /// <param name="modificationBatch">The batch captured at that single pending-operation match.</param>
        /// <param name="counterEffect">The Process-counter effect that pre-checked this operation, or null when none did.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="frame">The unit's own frame.</param>
        /// <param name="conditionResult">The enclosing Condition's retained evaluation outcome, or null outside one.</param>
        /// <param name="actuallyIncreasedScore">Set true when the primary operation's actual Score delta was positive.</param>
        /// <returns>The unit's finalized quantity event.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the transformation ceiling has been reached — preflight guards it.</exception>
        private TraceEventID ResolveOperationPath(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID pendingEvent,
            OperationOccurrence pendingOccurrence,
            EffectMatchBatch modificationBatch,
            ActiveEffect counterEffect,
            StructureContext structureContext,
            ExecutionFrame frame,
            ConditionOutcome? conditionResult,
            out bool actuallyIncreasedScore
        )
        {
            EffectOriginLineage lineage = frame.Lineage;
            AppendNearMisses(context, modificationBatch, pendingEvent, 1, unit, lineage);

            int modifierSum = 0;
            IReadOnlyList<QuantityModifierEvidence> modifiers = Array.Empty<QuantityModifierEvidence>();
            if (modificationBatch.Qualified.Count > 0 || modificationBatch.Reapplications.Count > 0)
            {
                List<QuantityModifierEvidence> applied = new List<QuantityModifierEvidence>(modificationBatch.Qualified.Count + modificationBatch.Reapplications.Count);
                for (int i = 0; i < modificationBatch.Qualified.Count; i++)
                {
                    ActiveEffect effect = modificationBatch.Qualified[i];

                    _builder.AppendEvent(EffectChainEvent(
                        context, 
                        EventFamilies.Qualification,
                        ExecutionEventSubtypes.EffectQualified,
                        pendingEvent,
                        1,
                        effect.Origin,
                        effect.HostInstance,
                        unit,
                        null,
                        null,
                        lineage
                    ));
                    context.Engine.CommitModification(effect, pendingOccurrence.HostInstance);
                    _builder.AppendEvent(EffectChainEvent(
                        context, 
                        EventFamilies.Qualification,
                        ExecutionEventSubtypes.EffectCommitted,
                        pendingEvent,
                        1,
                        effect.Origin,
                        effect.HostInstance,
                        unit,
                        null,
                        null,
                        lineage
                    ));
                    _builder.AppendEvent(EffectChainEvent(
                        context, 
                        EventFamilies.Operation,
                        ExecutionEventSubtypes.PrimaryOperationModified,
                        pendingEvent,
                        1,
                        effect.Origin,
                        effect.HostInstance,
                        unit,
                        null,
                        null,
                        lineage
                    ));

                    int amount = ModificationAmount(effect);
                    modifierSum += amount;
                    applied.Add(new QuantityModifierEvidence(effect.Origin.ToString(), ModificationStage, amount));
                }

                for (int i = 0; i < modificationBatch.Reapplications.Count; i++)
                {
                    ActiveEffect effect = modificationBatch.Reapplications[i];

                    _builder.AppendEvent(EffectChainEvent(
                        context, 
                        EventFamilies.Qualification,
                        ExecutionEventSubtypes.EffectQualified,
                        pendingEvent,
                        1,
                        effect.Origin,
                        effect.HostInstance,
                        unit, 
                        null,
                        null,
                        lineage
                    ));
                    _builder.AppendEvent(EffectChainEvent(
                        context, 
                        EventFamilies.Operation,
                        ExecutionEventSubtypes.PrimaryOperationModified,
                        pendingEvent, 
                        1,
                        effect.Origin,
                        effect.HostInstance,
                        unit,
                        null,
                        null,
                        lineage
                     ));

                    int amount = ModificationAmount(effect);
                    modifierSum += amount;
                    applied.Add(new QuantityModifierEvidence(effect.Origin.ToString(), ModificationStage, amount));
                }

                modifiers = applied;
            }

            EvaluatedOperation evaluation = isCore
                ? OperationEvaluator.EvaluateCoreLine(slot.Core.Operation, context.Registers, slot.Position, modifierSum)
                : OperationEvaluator.EvaluateInstruction(slot.Instruction.Definition.PrimaryOperation, context.Registers, slot.Position, modifierSum);
            actuallyIncreasedScore = evaluation.Register == CoreRegister.Score && evaluation.FinalDelta > 0;

            TraceEventID resolvedEvent = _builder.AppendEvent(UnitStreamEvent(
                context, 
                EventFamilies.Operation,
                ExecutionEventSubtypes.PrimaryOperationResolved,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));
            TraceEventID quantityEvent = AppendQuantityEvent(
                context, 
                slot,
                isCore,
                evaluation,
                unit,
                modifiers,
                structureContext,
                lineage
            );
            context.Registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, 1, unit, lineage);

            _builder.AppendEvent(UnitStreamEvent(
                context, 
                EventFamilies.Operation,
                ExecutionEventSubtypes.PrimaryOperationResultFinalized,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));
            
            if (counterEffect != null)
                CommitCounterRequest(context, counterEffect, resolvedEvent, 1, unit, lineage);

            OperationOccurrence resolvedOccurrence = BuildOperationOccurrence(context, slot, isCore, unit, resolvedEvent, 0);
            EffectMatchBatch resolvedBatch = context.Engine.MatchResolvedOperation(resolvedOccurrence);
            ResolveReactionBatch(context, resolvedBatch, resolvedEvent, 0, unit, slot.Position, lineage);

            QuantityOccurrence primaryQuantity = new QuantityOccurrence(
                unit,
                quantityEvent,
                0,
                evaluation.Register,
                evaluation.FinalDelta,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                null,
                true,
                lineage,
                isCore ? null : slot,
                isCore ? null : structureContext
            );
            EffectMatchBatch quantityBatch = context.Engine.MatchQuantityChange(primaryQuantity);
            ResolveReactionBatch(context, quantityBatch, quantityEvent, 0, unit, slot.Position, lineage);
            ResolveLockUpdates(context, quantityBatch, primaryQuantity, quantityEvent, 0, unit, lineage, conditionResult);
            CommitCreators(context, quantityBatch, quantityEvent, 0, frame, slot, structureContext, unit, false, conditionResult);

            return quantityEvent;
        }

        /// <summary>
        /// Stage 5: finalizes the unit's disposition, emits completion, and closes the unit — a
        /// rescued unit preserves its original skip as the transformed disposition. The frame stays
        /// on the stack for the post-unit offer and drain.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="quantityEvent">The unit's finalized quantity event.</param>
        /// <param name="finalDisposition">The unit's final disposition.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The unit's completion event.</returns>
        private TraceEventID FinalizeAndCloseUnit(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID quantityEvent,
            EventDisposition finalDisposition,
            StructureContext structureContext,
            EffectOriginLineage lineage)
        {
            _builder.AppendEvent(DispositionFinalizedEvent(
                context, 
                slot,
                isCore,
                unit,
                finalDisposition,
                structureContext,
                lineage
            ));
            TraceEventID completionEvent = _builder.AppendEvent(UnitStreamEvent(
                context, 
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceExecutionCompleted,
                slot,
                isCore,
                unit,
                structureContext,
                lineage
            ));

            RuntimeUnitClosure closure = new RuntimeUnitClosure(
                finalDisposition == EventDisposition.Rescued ? EventDisposition.Skipped : (EventDisposition?)null,
                finalDisposition,
                quantityEvent,
                Array.Empty<string>(),
                UnitClosureStatus.NormalCompletion,
                context.CurrentSafetyStatus
            );
            _builder.CompleteUnit(unit, closure);
            return completionEvent;
        }

        /// <summary>
        /// Offers the closed unit to the post-unit boundary with the four retained facts: the unit's
        /// source position, whether it occupies the final occupied player-controlled position, the
        /// enclosing Condition's retained evaluation outcome, and the adjacency snapshot captured
        /// when the unit opened. The unit is closed, so its near-miss and request-chain evidence
        /// lives in the parent causal context — unit-less, caused by the completion event at depth
        /// one.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The closed unit.</param>
        /// <param name="frame">The unit's own frame.</param>
        /// <param name="completionEvent">The unit's completion event.</param>
        /// <param name="finalDisposition">The unit's final disposition.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="conditionResult">The enclosing Condition's retained evaluation outcome, or null outside one.</param>
        /// <param name="adjacentAfterSuccessfulScore">The adjacency snapshot captured when the unit opened.</param>
        private void OfferPostUnit(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            ExecutionFrame frame,
            TraceEventID completionEvent,
            EventDisposition finalDisposition,
            StructureContext structureContext,
            ConditionOutcome? conditionResult,
            bool adjacentAfterSuccessfulScore
        )
        {
            bool isFinalOccupiedPlayerLine = !isCore
                && context.FinalOccupiedPlayerPosition != null
                && slot.Position == context.FinalOccupiedPlayerPosition.Value;

            PostUnitOccurrence occurrence = new PostUnitOccurrence(
                unit,
                completionEvent,
                finalDisposition,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                structureContext,
                frame.Lineage,
                slot.Position,
                isFinalOccupiedPlayerLine,
                conditionResult,
                adjacentAfterSuccessfulScore
            );

            EffectMatchBatch batch = context.Engine.MatchPostUnit(occurrence);
            AppendNearMisses(context, batch, completionEvent, 1, null, frame.Lineage);
            CommitCreators(context, batch, completionEvent, 0, frame, slot, structureContext, unit, true, conditionResult);
        }
        
        /// <summary>
        /// Offers a successful Condition evaluation to the engine's Condition-success boundary before
        /// any governed child executes, and establishes each committed creator's request as pending
        /// on the first occupied contained child, carrying the retained TRUE outcome — the request
        /// exists only because the evaluation succeeded. The chain is unit-less — no unit is open at
        /// a Structure evaluation — and the request's parent unit stays absent until that child
        /// closes.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="arrangement">The source arrangement being traversed.</param>
        /// <param name="firstContained">The first contained slot index.</param>
        /// <param name="pastFootprint">The slot index past the footprint.</param>
        /// <param name="resultEvent">The CONDITION_TRUE result event.</param>
        /// <param name="evaluationIdentity">The evaluation's identity.</param>
        /// <param name="evaluationContext">The per-evaluation Structure context.</param>
        private void OfferConditionSuccess(
            ExecutionContext context,
            SourceArrangement arrangement,
            int firstContained,
            int pastFootprint,
            TraceEventID resultEvent,
            string evaluationIdentity,
            StructureContext evaluationContext
        )
        {
            SourceSlot firstOccupied = null;
            for (int i = firstContained; i < pastFootprint; i++)
            {
                SourceSlot contained = arrangement.Slots[i];
                if (contained.Kind != SourceSlotKind.ContainedInstruction) continue;
                
                firstOccupied = contained;
                break;
            }

            ConditionSuccessOccurrence occurrence = new ConditionSuccessOccurrence(
                resultEvent,
                evaluationIdentity,
                firstOccupied?.Instruction.InstanceID,
                firstOccupied?.Position,
                evaluationContext,
                EffectOriginLineage.Empty
            );

            EffectMatchBatch batch = context.Engine.MatchConditionSuccess(occurrence);
            AppendNearMisses(context, batch, resultEvent, 1, null, EffectOriginLineage.Empty);
            if (batch.Creators.Count == 0)
                return;

            for (int i = 0; i < batch.Creators.Count; i++)
            {
                ActiveEffect effect = batch.Creators[i];
                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectQualified,
                    resultEvent,
                    1,
                    effect.Origin,
                    effect.HostInstance,
                    null,
                    null,
                    null,
                    EffectOriginLineage.Empty
                ));
                context.Engine.Commit(effect);
                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectCommitted,
                    resultEvent,
                    1,
                    effect.Origin,
                    effect.HostInstance,
                    null,
                    null,
                    null,
                    EffectOriginLineage.Empty
                ));

                AddedExecutionRequest request = new AddedExecutionRequest(
                    AddedExecutionRequestIdentities.For(effect, resultEvent),
                    effect.Origin,
                    effect.DefinitionID,
                    resultEvent,
                    null,
                    firstOccupied,
                    evaluationContext,
                    EffectOriginLineage.Empty.Append(effect.Origin),
                    1,
                    null,
                    ConditionOutcome.True
                );

                TraceEventID requestedEvent = _builder.AppendEvent(AddedExecutionEvent(
                    context, 
                    ExecutionEventSubtypes.AddedExecutionRequested,
                    resultEvent,
                    1,
                    null,
                    request,
                    firstOccupied
                ));

                context.PendingRequests.Establish(request with { RequestedEvent = requestedEvent });
            }
        }
                
        /// <summary>
        /// Resolves the end-of-player-controlled-traversal boundary as two sequential offers.
        /// First, the observation window closes. Offer 1 matches boundary creators only
        /// and resolves each locked branch to completion; its batch's qualified and near-missed
        /// boundary-quantity effects were computed against pre-Burst registers and are discarded
        /// unemitted. Offer 2 is a fresh match for boundary quantity effects against then-current
        /// registers — so a Burst branch's results are visible to ALIGN — and is the sole source of
        /// quantity qualification, near-miss, and resolution evidence. Every event is outside any unit.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ResolvePlayerTraversalBoundary(ExecutionContext context)
        {
            context.Engine.CloseObservationWindow();
            ExecutionRegisters registers = context.Registers;

            BoundaryOccurrence creatorOffer = new BoundaryOccurrence(
                PlayerTraversalBoundary,
                registers.Value,
                registers.Signal,
                registers.Score
            );
            EffectMatchBatch creatorBatch = context.Engine.MatchBoundary(creatorOffer);
            for (int i = 0; i < creatorBatch.Creators.Count; i++)
            {
                ResolveBoundaryCreator(context, creatorBatch.Creators[i]);
            }

            BoundaryOccurrence quantityOffer = new BoundaryOccurrence(
                PlayerTraversalBoundary,
                registers.Value,
                registers.Signal,
                registers.Score
            );
            EffectMatchBatch quantityBatch = context.Engine.MatchBoundary(quantityOffer);
            AppendNearMisses(context, quantityBatch, null, 0, null, EffectOriginLineage.Empty);
            for (int i = 0; i < quantityBatch.Qualified.Count; i++)
            {
                ResolveBoundaryQuantityEffect(context, quantityBatch.Qualified[i], registers);
            }
        }
        
        /// <summary>
        /// Drains the quantity batch's target-lock updates: per update, the qualification and
        /// commitment chain caused by the finalized quantity event at chain depth, no register write,
        /// then the engine stores the occurrence-derived memento keyed by the update's origin. Only a
        /// player-owned primary-operation quantity batch carries updates (a reaction-caused or
        /// boundary change nulls the host slot), so this is the one drain site. The memento carries the
        /// host slot, its Structure context, the quantity event, the unit, and the threaded retained
        /// Condition outcome the boundary bracket re-offers at the Burst descendant's closure. Resolves
        /// after the batch's reactions and before its creators; the two kinds never co-occur at one
        /// quantity event at this content, so the order is unobservable and fixed for stable evidence.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="batch">The captured quantity batch.</param>
        /// <param name="occurrence">The player-owned primary-operation quantity occurrence.</param>
        /// <param name="quantityEvent">The finalized quantity event.</param>
        /// <param name="observedDepth">The quantity event's causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <param name="retainedConditionResult">The enclosing Condition's retained outcome, or null outside one.</param>
        private void ResolveLockUpdates(
            ExecutionContext context,
            EffectMatchBatch batch,
            QuantityOccurrence occurrence,
            TraceEventID quantityEvent,
            int observedDepth,
            RuntimeUnitID unit,
            EffectOriginLineage lineage,
            ConditionOutcome? retainedConditionResult
        )
        {
            int chainDepth = observedDepth + 1;
            for (int i = 0; i < batch.TargetLockUpdates.Count; i++)
            {
                ActiveEffect effect = batch.TargetLockUpdates[i];
                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectQualified,
                    quantityEvent,
                    chainDepth,
                    effect.Origin,
                    effect.HostInstance,
                    unit,
                    null,
                    null,
                    lineage
                ));

                TargetLock lockMemento = new TargetLock(
                    occurrence.HostSlot,
                    occurrence.HostSlot.Instruction.InstanceID,
                    occurrence.SlotContext,
                    quantityEvent,
                    unit,
                    retainedConditionResult
                );
                context.Engine.CommitTargetLock(effect, lockMemento);

                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectCommitted,
                    quantityEvent,
                    chainDepth,
                    effect.Origin,
                    effect.HostInstance,
                    unit,
                    null,
                    null,
                    lineage
                ));
            }
        }

        /// <summary>
        /// Resolves one boundary creator at the window-close boundary: reads its held lock, emits the
        /// uncaused ADDED_EXECUTION_TARGET_LOCKED finalization naming the locked host, consumes the
        /// creator's once-per-execution allowance, builds the request from the lock (identity from the
        /// TARGET_LOCKED event, lineage the creator over the empty boundary lineage, inherited context
        /// and retained Condition outcome from the lock), emits the ADDED_EXECUTION_REQUESTED event
        /// caused by the TARGET_LOCKED event at depth one, validates the locked host, and either
        /// brackets the descendant branch (frameless: preflighting the depth ceiling only, the
        /// per-activation ceiling engaging through the descendant's own self-rooted frame) or cancels
        /// the request with the validator-composed reason.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The qualified boundary creator.</param>
        /// <exception cref="InvalidOperationException">Thrown when the lineage-depth ceiling would be exceeded — the ONCE allowance and origin lineage make this unreachable at this content.</exception>
        private void ResolveBoundaryCreator(ExecutionContext context, ActiveEffect effect)
        {
            TargetLock lockMemento = context.Engine.TargetLockFor(effect.Origin);
            SourceSlot lockedSlot = lockMemento.LockedSlot;

            TraceEventID targetLockedEvent = _builder.AppendEvent(TargetLockedEvent(
                context,
                effect.Origin,
                lockedSlot
            ));
            context.Engine.Commit(effect);

            AddedExecutionRequest request = new AddedExecutionRequest(
                AddedExecutionRequestIdentities.For(effect, targetLockedEvent),
                effect.Origin,
                effect.DefinitionID,
                targetLockedEvent,
                null,
                lockedSlot,
                lockMemento.InheritedContext,
                EffectOriginLineage.Empty.Append(effect.Origin),
                1,
                lockMemento.LockedUnit,
                lockMemento.RetainedConditionResult
            );

            TraceEventID requestedEvent = _builder.AppendEvent(AddedExecutionEvent(
                context, 
                ExecutionEventSubtypes.AddedExecutionRequested,
                targetLockedEvent,
                1,
                null,
                request,
                lockedSlot
            ));
            request = request with { RequestedEvent = requestedEvent };

            string checkToken = LockedHostValidator.Validate(lockMemento, context.Request.Source.Arrangement);
            if (checkToken != null)
            {
                AppendCancellation(context, new CancelledRequest(request, LockedHostValidator.ComposeReason(checkToken)));
                return;
            }

            IReadOnlyList<BreachedLimit> breached = context.Tallies.EvaluateDescendant(
                request.ProposedDepth,
                lockMemento.LockedUnit);
            if (breached != null)
            {
                throw new SafetyAbortSignal(
                    EventFamilies.AddedExecution,
                    ExecutionEventSubtypes.AddedExecutionStarted,
                    request.RequestIdentity,
                    InnermostOpenUnit(context) ?? default,
                    breached);
            }

            context.Tallies.RecordDescendant(request.ProposedDepth, lockMemento.LockedUnit);
            EmitApproachedLimits(context, null);

            _builder.AppendEvent(AddedExecutionEvent(
                context, 
                ExecutionEventSubtypes.AddedExecutionStarted,
                requestedEvent,
                1,
                null,
                request,
                lockedSlot
            ));

            TraverseUnit(context, lockedSlot, request.InheritedContext, null, request, request.RetainedConditionResult);

            _builder.AppendEvent(AddedExecutionEvent(
                context, 
                ExecutionEventSubtypes.AddedExecutionCompleted,
                requestedEvent,
                1,
                null,
                request,
                lockedSlot
            ));
        }

        /// <summary>
        /// Resolves one boundary quantity effect against then-current registers — the shipped
        /// end-of-player boundary-quantity chain, unchanged: request, commitment, resolution, its own
        /// source-less quantity change with register write, threshold crossings, and the ordinary
        /// reaction offer. Every event is outside any unit; the request and resolution pair carries no
        /// cause.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The qualified boundary quantity effect.</param>
        /// <param name="registers">The current registers the effect reads and writes.</param>
        private void ResolveBoundaryQuantityEffect(ExecutionContext context, ActiveEffect effect, ExecutionRegisters registers)
        {
            TraceEventID requestedEvent = _builder.AppendEvent(BoundaryChainEvent(
                context, 
                ExecutionEventSubtypes.BoundaryEffectRequested, 
                null,
                0,
                effect.Origin
            ));
            context.Engine.Commit(effect);
            TraceEventID resolvedEvent = _builder.AppendEvent(BoundaryChainEvent(
                context, 
                ExecutionEventSubtypes.BoundaryEffectResolved,
                requestedEvent,
                1,
                effect.Origin
            ));

            EvaluatedOperation evaluation = OperationEvaluator.EvaluateBoundaryEffect(effect.Operation, registers);
            TraceEventID quantityEvent = _builder.AppendEvent(ReactionQuantityEvent(
                context, 
                evaluation,
                resolvedEvent,
                2,
                effect.Origin,
                null,
                EffectOriginLineage.Empty
            ));
            registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, 3, null, EffectOriginLineage.Empty);

            QuantityOccurrence boundaryQuantity = new QuantityOccurrence(
                null,
                quantityEvent,
                2,
                evaluation.Register,
                evaluation.FinalDelta,
                null,
                effect.Origin,
                false,
                EffectOriginLineage.Empty,
                null,
                null
            );
            EffectMatchBatch reactions = context.Engine.MatchQuantityChange(boundaryQuantity);
            ResolveReactionBatch(context, reactions, quantityEvent, 2, null, null, EffectOriginLineage.Empty);
        }

        /// <summary>
        /// Assembles the ADDED_EXECUTION_TARGET_LOCKED event: the window-close lock finalization,
        /// uncaused at causal depth zero (the close is a boundary fact, not an event consequence),
        /// unit-less, naming the locked host as both its player-owned source origin and its target
        /// identity, at the locked slot's position, carrying the resolving creator's effect origin. The
        /// request chain that follows derives its identity and cause from this event.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effectOrigin">The resolving creator's origin instance.</param>
        /// <param name="lockedSlot">The locked host slot.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence TargetLockedEvent(ExecutionContext context, InstanceID effectOrigin, SourceSlot lockedSlot)
        {
            InstanceID host = lockedSlot.Instruction.InstanceID;
            return new EventEvidence(
                EventFamilies.AddedExecution,
                ExecutionEventSubtypes.AddedExecutionTargetLocked,
                Array.Empty<string>(),
                0,
                null,
                null,
                null,
                host,
                null,
                effectOrigin,
                OwnershipClassification.PlayerOwned,
                lockedSlot.Position,
                host.ToString(),
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles a boundary effect's REACTION-family chain event: outside any unit, carrying the
        /// effect origin and no source origin of its own — a boundary effect manufactures no source
        /// line.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="subtype">The REACTION subtype token.</param>
        /// <param name="causingEvent">The causing event, or null for the uncaused request.</param>
        /// <param name="depth">The event's causal depth.</param>
        /// <param name="effectOrigin">The resolving effect's origin instance.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence BoundaryChainEvent(
            ExecutionContext context,
            string subtype,
            TraceEventID? causingEvent,
            int depth,
            InstanceID effectOrigin
        )
        {
            return new EventEvidence(
                EventFamilies.Reaction,
                subtype,
                Array.Empty<string>(),
                depth,
                null,
                null,
                causingEvent,
                null,
                null,
                effectOrigin,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            );
        }
                
        /// <summary>
        /// Offers a closed player-owned unit's host to the pending tracker: a matured request joins
        /// this unit's own captured batch as its immediate descendant, and a cancelled one records
        /// its evidence with the tracker's reason. Core-owned units host no added execution, so they
        /// never carry a pending request.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The closed unit.</param>
        /// <param name="frame">The unit's own frame.</param>
        /// <param name="finalDisposition">The unit's final disposition.</param>
        private void ResolvePendingRequests(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            ExecutionFrame frame,
            EventDisposition finalDisposition
        )
        {
            if (isCore)
                return;

            PendingResolution resolution = context.PendingRequests.TryClose(
                slot.Instruction.InstanceID,
                finalDisposition,
                out AddedExecutionRequest matured,
                out CancelledRequest cancelled
            );

            switch (resolution)
            {
                case PendingResolution.Matured:
                    frame.Capture(matured with { ParentUnit = unit });
                    return;
                
                case PendingResolution.Cancelled:
                    AppendCancellation(context, cancelled);
                    break;
            }
        }

        /// <summary>
        /// Appends one cancelled request's evidence: unit-less, caused by the request's own request
        /// event at depth one, carrying the creator origin, the request's lineage and inherited
        /// context, the cancelled disposition, and the tracker's reason. The consumed allowance stays
        /// consumed.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="cancelled">The cancelled request with its reason.</param>
        private void AppendCancellation(ExecutionContext context, CancelledRequest cancelled)
        {
            AddedExecutionRequest request = cancelled.Request;
            SourceSlot lockedSlot = request.LockedSlot;

            _builder.AppendEvent(new EventEvidence(
                EventFamilies.AddedExecution,
                ExecutionEventSubtypes.AddedExecutionCancelled,
                Array.Empty<string>(),
                1,
                null,
                null,
                request.RequestedEvent.Value,
                lockedSlot.Instruction.InstanceID,
                null,
                request.CreatorOrigin,
                OwnershipClassification.PlayerOwned,
                lockedSlot.Position,
                null,
                request.Lineage,
                request.RequestIdentity,
                request.ProposedDepth,
                request.InheritedContext,
                EventDisposition.Cancelled,
                cancelled.Reason,
                context.CurrentSafetyStatus,
                null
            ));
        }

        /// <summary>
        /// Commits each qualified creator in the batch and captures one request per commitment on the
        /// creating branch's frame: the qualification and commitment chain carrying any socketed
        /// host, then the request event carrying the locked host, the request's own lineage, the
        /// inherited Structure context, and the retained Condition outcome the descendant's closure
        /// re-offers. Runs after the batch's reactions fully resolve, so request creation follows
        /// reaction closure at every boundary. Consumption names the triggering unit, so a
        /// source-execution-scoped allowance consumes once per source execution of that unit.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="batch">The captured batch.</param>
        /// <param name="candidateEvent">The candidate event the batch observed.</param>
        /// <param name="depth">The candidate event's causal depth.</param>
        /// <param name="frame">The creating branch's frame.</param>
        /// <param name="lockedSlot">The triggering unit's slot, locked as the request's host.</param>
        /// <param name="structureContext">The inherited Structure context, or null at top level.</param>
        /// <param name="unit">The causing unit; the request's parent either way.</param>
        /// <param name="unitLessChain">Whether the chain events sit outside the unit — true at the post-unit boundary, where the unit has already closed.</param>
        /// <param name="retainedConditionResult">The enclosing Condition's retained evaluation outcome, or null outside one.</param>
        private void CommitCreators(
            ExecutionContext context,
            EffectMatchBatch batch,
            TraceEventID candidateEvent,
            int depth,
            ExecutionFrame frame,
            SourceSlot lockedSlot,
            StructureContext structureContext,
            RuntimeUnitID? unit,
            bool unitLessChain,
            ConditionOutcome? retainedConditionResult
        )
        {
            int chainDepth = depth + 1;
            RuntimeUnitID? chainUnit = unitLessChain ? (RuntimeUnitID?)null : unit;

            for (int i = 0; i < batch.Creators.Count; i++)
            {
                ActiveEffect effect = batch.Creators[i];
                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectQualified,
                    candidateEvent, 
                    chainDepth, 
                    effect.Origin,
                    effect.HostInstance,
                    chainUnit,
                    null,
                    null,
                    frame.Lineage
                ));
                context.Engine.Commit(effect, unit);
                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectCommitted,
                    candidateEvent,
                    chainDepth,
                    effect.Origin,
                    effect.HostInstance,
                    chainUnit,
                    null,
                    null,
                    frame.Lineage
                ));

                AddedExecutionRequest request = new AddedExecutionRequest(
                    AddedExecutionRequestIdentities.For(effect, candidateEvent),
                    effect.Origin,
                    effect.DefinitionID,
                    candidateEvent,
                    null,
                    lockedSlot,
                    structureContext,
                    frame.Lineage.Append(effect.Origin),
                    frame.AddedExecutionDepth + 1,
                    unit,
                    retainedConditionResult
                );

                TraceEventID requestedEvent = _builder.AppendEvent(AddedExecutionEvent(
                    context, 
                    ExecutionEventSubtypes.AddedExecutionRequested,
                    candidateEvent,
                    chainDepth,
                    chainUnit,
                    request,
                    lockedSlot
                ));

                frame.Capture(request with { RequestedEvent = requestedEvent });
            }
        }

        /// <summary>
        /// Drains the frame's captured batch in capture order after the unit closes: each request
        /// preflights against the depth and per-activation ceilings, brackets its descendant unit
        /// with the started and completed events, and executes it as a complete source execution
        /// carrying the request's retained Condition outcome. The descendant drains its own batch
        /// inside its traversal, so depth-first branch completion is structural; traversal advances
        /// only after the batch empties.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="frame">The draining frame.</param>
        /// <exception cref="InvalidOperationException">Thrown when preflight prohibits the descendant.</exception>
        private void DrainCapturedRequests(ExecutionContext context, ExecutionFrame frame)
        {
            IReadOnlyList<AddedExecutionRequest> batch = frame.CapturedBatch;
            for (int i = 0; i < batch.Count; i++)
            {
                AddedExecutionRequest request = batch[i];
                
                IReadOnlyList<BreachedLimit> breached = context.Tallies.EvaluateDescendant(
                    request.ProposedDepth,
                    frame.RootActivation
                );
                if (breached != null)
                {
                    throw new SafetyAbortSignal(
                        EventFamilies.AddedExecution,
                        ExecutionEventSubtypes.AddedExecutionStarted,
                        request.RequestIdentity,
                        InnermostOpenUnit(context) ?? default,
                        breached
                    );
                }

                context.Tallies.RecordDescendant(request.ProposedDepth, frame.RootActivation);
                EmitApproachedLimits(context, null);

                _builder.AppendEvent(AddedExecutionEvent(
                    context, 
                    ExecutionEventSubtypes.AddedExecutionStarted,
                    request.RequestedEvent.Value,
                    1,
                    null,
                    request,
                    request.LockedSlot
                ));

                TraverseUnit(context, request.LockedSlot, request.InheritedContext, null, request, request.RetainedConditionResult);

                _builder.AppendEvent(AddedExecutionEvent(
                    context, 
                    ExecutionEventSubtypes.AddedExecutionCompleted,
                    request.RequestedEvent.Value,
                    1,
                    null,
                    request,
                    request.LockedSlot
                ));
            }
        }

        /// <summary>
        /// Resolves one captured sibling batch at the immediate-reaction boundary: near-misses append
        /// first, then each qualified sibling resolves in declared order, each descendant branch
        /// closing before the next sibling begins.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="batch">The captured batch.</param>
        /// <param name="observedEvent">The candidate event the batch observed.</param>
        /// <param name="observedDepth">The candidate event's causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="position">The unit's source position, for reaction evaluation.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        private void ResolveReactionBatch(
            ExecutionContext context,
            EffectMatchBatch batch,
            TraceEventID observedEvent,
            int observedDepth,
            RuntimeUnitID? unit,
            SourcePosition? position,
            EffectOriginLineage lineage
        )
        {
            AppendNearMisses(context, batch, observedEvent, observedDepth + 1, unit, lineage);
            for (int i = 0; i < batch.Qualified.Count; i++)
            {
                if (batch.Qualified[i].CounterRequest == null)
                    ResolveReaction(context, batch.Qualified[i], observedEvent, observedDepth, unit, position, lineage);
            }

            for (int i = 0; i < batch.Qualified.Count; i++)
            {
                if (batch.Qualified[i].CounterRequest != null)
                    ResolveCounterReaction(context, batch.Qualified[i], observedEvent, observedDepth, unit, lineage);
            }
        }

        /// <summary>
        /// Resolves one immediate reaction inside the causing unit's closure: qualification and
        /// commitment evidence carrying any socketed host, the reaction event, the reaction's own
        /// quantity change with register write and threshold crossings, then the depth-first offer of
        /// that change back to the engine so the descendant branch closes before returning. Creators
        /// never qualify from a reaction-caused change, so no creator call site exists here.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The qualified reaction effect.</param>
        /// <param name="observedEvent">The candidate event the effect observed.</param>
        /// <param name="observedDepth">The candidate event's causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="position">The unit's source position, for reaction evaluation.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <exception cref="InvalidOperationException">Thrown when the reaction ceiling has been reached — preflight guards it.</exception>
        private void ResolveReaction(
            ExecutionContext context,
            ActiveEffect effect,
            TraceEventID observedEvent,
            int observedDepth,
            RuntimeUnitID? unit,
            SourcePosition? position,
            EffectOriginLineage lineage
        )
        {
            IReadOnlyList<BreachedLimit> breached = context.Tallies.EvaluateReaction();
            if (breached != null)
            {
                throw new SafetyAbortSignal(
                    EventFamilies.Reaction,
                    ExecutionEventSubtypes.ImmediateReactionResolved,
                    effect.Origin.ToString(),
                    InnermostOpenUnit(context) ?? default,
                    breached
                );
            }

            int chainDepth = observedDepth + 1;
            _builder.AppendEvent(EffectChainEvent(
                context, 
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectQualified,
                observedEvent,
                chainDepth,
                effect.Origin,
                effect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));
            context.Engine.Commit(effect, unit);
            _builder.AppendEvent(EffectChainEvent(
                context, 
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectCommitted,
                observedEvent,
                chainDepth,
                effect.Origin,
                effect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));
            context.Tallies.RecordReaction();
            EmitApproachedLimits(context, unit);
            TraceEventID reactionEvent = _builder.AppendEvent(EffectChainEvent(
                context, 
                EventFamilies.Reaction,
                ExecutionEventSubtypes.ImmediateReactionResolved,
                observedEvent,
                chainDepth,
                effect.Origin,
                effect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));

            EvaluatedOperation evaluation = OperationEvaluator.EvaluateInstruction(effect.Operation, context.Registers, position, 0);
            int quantityDepth = chainDepth + 1;
            TraceEventID quantityEvent = _builder.AppendEvent(ReactionQuantityEvent(
                context, 
                evaluation,
                reactionEvent,
                quantityDepth,
                effect.Origin,
                unit,
                lineage
            ));
            context.Registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, quantityDepth + 1, unit, lineage);

            QuantityOccurrence occurrence = new QuantityOccurrence(
                unit,
                quantityEvent,
                quantityDepth,
                evaluation.Register,
                evaluation.FinalDelta,
                null,
                effect.Origin,
                false,
                lineage,
                null,
                null
            );
            EffectMatchBatch descendants = context.Engine.MatchQuantityChange(occurrence);
            ResolveReactionBatch(context, descendants, quantityEvent, quantityDepth, unit, position, lineage);
        }
        
        /// <summary>
        /// Resolves one counter reaction inside the causing unit's closure: the qualification and
        /// commitment chain, the reaction event, then the counter change it requests. A counter
        /// change writes no register and is offered to no boundary — a counter qualifies other
        /// effects only where a rule declares it, and none does — so the branch closes here.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The qualified counter-reaction effect.</param>
        /// <param name="observedEvent">The candidate event the effect observed.</param>
        /// <param name="observedDepth">The candidate event's causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <exception cref="SafetyAbortSignal">Thrown when the reaction ceiling has been reached.</exception>
        private void ResolveCounterReaction(
            ExecutionContext context,
            ActiveEffect effect,
            TraceEventID observedEvent,
            int observedDepth,
            RuntimeUnitID? unit,
            EffectOriginLineage lineage
        )
        {
            IReadOnlyList<BreachedLimit> breached = context.Tallies.EvaluateReaction();
            if (breached != null)
            {
                throw new SafetyAbortSignal(
                    EventFamilies.Reaction,
                    ExecutionEventSubtypes.ImmediateReactionResolved,
                    effect.Origin.ToString(),
                    InnermostOpenUnit(context) ?? default,
                    breached
                );
            }

            int chainDepth = observedDepth + 1;
            _builder.AppendEvent(EffectChainEvent(
                context,
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectQualified,
                observedEvent,
                chainDepth,
                effect.Origin,
                effect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));
            context.Engine.Commit(effect, unit);
            _builder.AppendEvent(EffectChainEvent(
                context,
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectCommitted,
                observedEvent,
                chainDepth,
                effect.Origin,
                effect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));
            context.Tallies.RecordReaction();
            EmitApproachedLimits(context, unit);
            TraceEventID reactionEvent = _builder.AppendEvent(EffectChainEvent(
                context,
                EventFamilies.Reaction,
                ExecutionEventSubtypes.ImmediateReactionResolved,
                observedEvent,
                chainDepth,
                effect.Origin,
                effect.HostInstance,
                unit,
                null,
                null,
                lineage
            ));

            CommitCounterRequest(context, effect, reactionEvent, chainDepth + 1, unit, lineage);
        }

        /// <summary>
        /// Builds the typed occurrence for a unit's primary operation at the pending or resolved
        /// boundary: operator, target register, and operand shape from the payload, ownership and the
        /// owning host instance from the slot kind.
        /// </summary>
        /// <param name="context">The per-execution context, read for the governing counter snapshot.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="candidateEvent">The candidate event the occurrence mirrors.</param>
        /// <param name="causalDepth">The candidate event's causal depth.</param>
        /// <returns>The operation occurrence.</returns>
        private static OperationOccurrence BuildOperationOccurrence(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID candidateEvent,
            int causalDepth
        )
        {
            CoreLineOperator op;
            CoreRegister register;
            OperandSpec operand;
            if (isCore)
            {
                op = slot.Core.Operation.Operator;
                register = slot.Core.Operation.Target;
                operand = slot.Core.Operation.Operand;
            }
            else
            {
                QuantityChangeOperation primary = slot.Instruction.Definition.PrimaryOperation;
                op = primary.Operator == QuantityOperator.Add ? CoreLineOperator.Add : CoreLineOperator.Multiply;
                register = primary.Register;
                operand = primary.Operand;
            }

            return new OperationOccurrence(
                unit,
                candidateEvent,
                causalDepth,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                register,
                op,
                operand.Source,
                operand.Source == OperandSource.Register ? operand.SourceRegister : (CoreRegister?)null,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                context.CounterSnapshot()
            );
        }

        /// <summary>
        /// Returns the amount a qualified modification contributes to the pending operation: an
        /// operand-adjustment modification carries its delta on the modification operation, while a
        /// quantity-change modification carries it on its constant operand.
        /// </summary>
        /// <param name="effect">The qualified modification effect.</param>
        /// <returns>The contributed amount.</returns>
        private static int ModificationAmount(ActiveEffect effect)
        {
            return effect.OperationModification?.OperandDelta ?? effect.Operation.Operand.Constant;
        }

        /// <summary>
        /// Appends one EFFECT_FAILED_TO_QUALIFY event per near-miss in the batch, each caused by the
        /// candidate event at the given depth, carrying the failed-to-qualify disposition with the
        /// failed requirement, the effect's socketed host when one exists, the emitting branch's
        /// lineage, and a registry-clean empty qualifier list.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="batch">The captured batch.</param>
        /// <param name="candidateEvent">The candidate event the near-misses observed.</param>
        /// <param name="depth">The near-miss events' causal depth.</param>
        /// <param name="unit">The containing unit, or null when the offer is unit-less.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        private void AppendNearMisses(
            ExecutionContext context,
            EffectMatchBatch batch,
            TraceEventID? candidateEvent,
            int depth,
            RuntimeUnitID? unit,
            EffectOriginLineage lineage
        )
        {
            for (int i = 0; i < batch.NearMisses.Count; i++)
            {
                EffectNearMiss nearMiss = batch.NearMisses[i];
                _builder.AppendEvent(EffectChainEvent(
                    context, 
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectFailedToQualify,
                    candidateEvent,
                    depth,
                    nearMiss.Effect.Origin,
                    nearMiss.Effect.HostInstance,
                    unit,
                    EventDisposition.FailedToQualify,
                    nearMiss.FailedRequirement,
                    lineage)
                );
            }
        }

        /// <summary>
        /// Assembles a unit-less Structure lifecycle event: STRUCTURE family, uncaused, depth zero,
        /// player-owned with the Structure's instance as host, at the header position, carrying the
        /// Structure context, payload-free. Structure walks are canonical, so the lineage is empty.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="subtype">The STRUCTURE subtype token.</param>
        /// <param name="headerSlot">The Structure header slot.</param>
        /// <param name="structureContext">The context the event executes within.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence StructureEvent(
            ExecutionContext context,
            string subtype,
            SourceSlot headerSlot,
            StructureContext structureContext
        )
        {
            return new EventEvidence(
                EventFamilies.Structure,
                subtype,
                Array.Empty<string>(),
                0,
                null,
                null,
                null,
                headerSlot.Structure.InstanceID,
                null,
                null,
                OwnershipClassification.PlayerOwned,
                headerSlot.Position,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                structureContext,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles an ADDED_EXECUTION-family event: caused by the given event at the given depth,
        /// carrying the creator origin, the request identity and proposed depth together, the
        /// request's lineage and inherited context, and the locked host's ownership and position.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="subtype">The ADDED_EXECUTION subtype token.</param>
        /// <param name="causingEvent">The causing event.</param>
        /// <param name="depth">The event's causal depth.</param>
        /// <param name="unit">The containing unit, or null when the event is unit-less.</param>
        /// <param name="request">The request the event records.</param>
        /// <param name="lockedSlot">The request's locked host slot.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence AddedExecutionEvent(
            ExecutionContext context,
            string subtype,
            TraceEventID causingEvent,
            int depth,
            RuntimeUnitID? unit,
            AddedExecutionRequest request,
            SourceSlot lockedSlot
        )
        {
            return new EventEvidence(
                EventFamilies.AddedExecution,
                subtype,
                Array.Empty<string>(),
                depth,
                unit,
                null,
                causingEvent,
                lockedSlot.Instruction.InstanceID,
                null,
                request.CreatorOrigin,
                OwnershipClassification.PlayerOwned,
                lockedSlot.Position,
                null,
                request.Lineage,
                request.RequestIdentity,
                request.ProposedDepth,
                request.InheritedContext,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles the skip event: DISPOSITION family, caused by the pending-operation event at
        /// depth one, in-unit, mirroring the unit's ownership, host or Core-line identity, and
        /// position, carrying the skipped disposition with its explicit cause, the emitting branch's
        /// lineage, and the unit's Structure context, payload-free.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="pendingEvent">The causing pending-operation event.</param>
        /// <param name="skipCause">The explicit skip cause.</param>
        /// <param name="structureContext">The governing Structure context.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence SkipEvent(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID pendingEvent,
            string skipCause,
            StructureContext structureContext,
            EffectOriginLineage lineage)
        {
            return new EventEvidence(
                EventFamilies.Disposition,
                ExecutionEventSubtypes.SourceExecutionSkipped,
                Array.Empty<string>(),
                1,
                unit,
                null,
                pendingEvent,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                isCore ? slot.Core.Identity : null,
                null,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                slot.Position,
                null,
                lineage,
                null,
                0,
                structureContext,
                EventDisposition.Skipped,
                skipCause,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles a SOURCE/OPERATION stream event mirroring the unit's ownership, host or Core-line
        /// identity, and position — uncaused, at depth zero, in-unit, payload-free, carrying the
        /// emitting branch's lineage and the unit's Structure context when governed.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="family">The event family token.</param>
        /// <param name="subtype">The event subtype token.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence UnitStreamEvent(
            ExecutionContext context,
            string family,
            string subtype,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            StructureContext structureContext,
            EffectOriginLineage lineage
        )
        {
            return new EventEvidence(
                family,
                subtype,
                Array.Empty<string>(),
                0,
                unit,
                null,
                null,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                isCore ? slot.Core.Identity : null,
                null,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                slot.Position,
                null,
                lineage,
                null,
                0,
                structureContext,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles the disposition-finalization stream event: the unit-mirroring stream shape —
        /// uncaused, at depth zero, in-unit, payload-free — carrying the unit's final disposition, the
        /// emitting branch's lineage, and its Structure context when governed.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="disposition">The unit's final disposition.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence DispositionFinalizedEvent(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            EventDisposition disposition,
            StructureContext structureContext,
            EffectOriginLineage lineage
        )
        {
            return new EventEvidence(
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceExecutionDispositionFinalized,
                Array.Empty<string>(),
                0,
                unit,
                null,
                null,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                isCore ? slot.Core.Identity : null,
                null,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                slot.Position,
                null,
                lineage,
                null,
                0,
                structureContext,
                disposition,
                null,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles a QUALIFICATION/REACTION/DISPOSITION/modified-operation chain event: caused by
        /// the observed candidate at the given depth, carrying the effect origin, the effect's
        /// socketed host when one exists, and the emitting branch's lineage, payload-free, with no
        /// source origin of its own.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="family">The event family token.</param>
        /// <param name="subtype">The event subtype token.</param>
        /// <param name="causingEvent">The observed candidate event.</param>
        /// <param name="depth">The chain event's causal depth.</param>
        /// <param name="effectOrigin">The observing effect's origin instance.</param>
        /// <param name="effectHost">The effect's socketed host instance, or null when unsocketed.</param>
        /// <param name="unit">The containing unit, or null when the chain is unit-less.</param>
        /// <param name="disposition">The disposition, or null when none.</param>
        /// <param name="dispositionReason">The disposition reason, or null when none.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence EffectChainEvent(
            ExecutionContext context,
            string family,
            string subtype,
            TraceEventID? causingEvent,
            int depth,
            InstanceID effectOrigin,
            InstanceID? effectHost,
            RuntimeUnitID? unit,
            EventDisposition? disposition,
            string dispositionReason,
            EffectOriginLineage lineage
        )
        {
            return new EventEvidence(
                family,
                subtype,
                Array.Empty<string>(),
                depth,
                unit,
                null,
                causingEvent,
                effectHost,
                null,
                effectOrigin,
                null,
                null,
                null,
                lineage,
                null,
                0,
                null,
                disposition,
                dispositionReason,
                context.CurrentSafetyStatus,
                null
            );
        }

        /// <summary>
        /// Assembles a reaction's own quantity event: caused by the reaction event at the given depth,
        /// carrying the effect origin, the emitting branch's lineage, and the resolved payload but no
        /// ownership, host, or position — a reaction never appears source-originated.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="evaluation">The resolved reaction evaluation.</param>
        /// <param name="causingEvent">The causing reaction event.</param>
        /// <param name="depth">The quantity event's causal depth.</param>
        /// <param name="effectOrigin">The causing effect's origin instance.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence ReactionQuantityEvent(
            ExecutionContext context,
            EvaluatedOperation evaluation,
            TraceEventID causingEvent,
            int depth,
            InstanceID effectOrigin,
            RuntimeUnitID? unit,
            EffectOriginLineage lineage
        )
        {
            QuantityChangePayload payload = new QuantityChangePayload(
                RegisterName(evaluation.Register),
                QuantityCategory.RuntimeVariable,
                evaluation.OperationType,
                evaluation.RequestedAmount,
                evaluation.PriorValue,
                null,
                Array.Empty<QuantityModifierEvidence>(),
                null,
                evaluation.FinalDelta,
                evaluation.FinalValue
            );

            return new EventEvidence(
                EventFamilies.Quantity,
                evaluation.Subtype,
                Array.Empty<string>(),
                depth,
                unit,
                null,
                causingEvent,
                null,
                null,
                effectOrigin,
                null,
                null,
                null,
                lineage,
                null,
                0,
                null,
                EventDisposition.Resolved,
                null,
                context.CurrentSafetyStatus,
                payload
            );
        }

        /// <summary>
        /// Appends one QUANTITY_RESET event for a register with its initial-state prior, then writes the
        /// register to zero.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="register">The register being reset.</param>
        /// <param name="identity">The register's canonical quantity identity.</param>
        /// <param name="prior">The register's value before the reset.</param>
        private void ResetRegister(
            ExecutionContext context,
            CoreRegister register,
            string identity,
            int prior
        )
        {
            QuantityChangePayload payload = new QuantityChangePayload(
                identity,
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Assign,
                0,
                prior,
                null,
                Array.Empty<QuantityModifierEvidence>(),
                null,
                0 - prior,
                0
            );

            EventEvidence evidence = new EventEvidence(
                EventFamilies.Quantity,
                ExecutionEventSubtypes.QuantityReset,
                Array.Empty<string>(),
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                payload
            );

            _builder.AppendEvent(evidence);
            context.Registers.Write(register, 0);
        }

        /// <summary>
        /// Appends the finalized quantity event for a traversal unit, mirroring the unit's ownership,
        /// host or Core-line identity, and position, with one modifier evidence entry per applied
        /// modification, carrying the emitting branch's lineage and the unit's Structure context when
        /// governed.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="evaluation">The resolved evaluation.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="modifiers">The applied modifier evidence, or empty.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The minted quantity-event identity.</returns>
        private TraceEventID AppendQuantityEvent(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            EvaluatedOperation evaluation,
            RuntimeUnitID unit,
            IReadOnlyList<QuantityModifierEvidence> modifiers,
            StructureContext structureContext,
            EffectOriginLineage lineage
        )
        {
            QuantityChangePayload payload = new QuantityChangePayload(
                RegisterName(evaluation.Register),
                QuantityCategory.RuntimeVariable,
                evaluation.OperationType,
                evaluation.RequestedAmount,
                evaluation.PriorValue,
                null,
                modifiers,
                null,
                evaluation.FinalDelta,
                evaluation.FinalValue
            );

            EventEvidence evidence = new EventEvidence(
                EventFamilies.Quantity,
                evaluation.Subtype,
                Array.Empty<string>(),
                0,
                unit,
                null,
                null,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                isCore ? slot.Core.Identity : null,
                null,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                slot.Position,
                null,
                lineage,
                null,
                0,
                structureContext,
                EventDisposition.Resolved,
                null,
                context.CurrentSafetyStatus,
                payload
            );

            return _builder.AppendEvent(evidence);
        }

        /// <summary>
        /// Emits one threshold event per Score-band crossing, caused by the given quantity event at the
        /// given depth (the causing event's depth plus one), carrying the emitting branch's lineage.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="evaluation">The resolved Score evaluation.</param>
        /// <param name="causingEvent">The causing quantity event.</param>
        /// <param name="depth">The crossing events' causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        private void EmitThresholdCrossings(
            ExecutionContext context,
            EvaluatedOperation evaluation,
            TraceEventID causingEvent,
            int depth,
            RuntimeUnitID? unit,
            EffectOriginLineage lineage
        )
        {
            ProcessThresholds thresholds = context.Request.Configuration.Thresholds;
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(thresholds, evaluation.PriorValue, evaluation.FinalValue);
            for (int i = 0; i < crossings.Count; i++)
            {
                ThresholdCrossing crossing = crossings[i];
                EventEvidence evidence = new EventEvidence(
                    EventFamilies.Threshold,
                    crossing.Subtype,
                    Array.Empty<string>(),
                    depth,
                    unit,
                    null,
                    causingEvent,
                    null,
                    null,
                    null,
                    null,
                    null,
                    crossing.ThresholdName,
                    lineage,
                    null,
                    0,
                    null,
                    null,
                    null,
                    context.CurrentSafetyStatus,
                    null
                );

                _builder.AppendEvent(evidence);
            }
        }
        
        /// <summary>
        /// Applies one counter request and appends its evidence: prior value, requested delta, the
        /// declared bounds when one reduced the change, the final delta and value, the requesting
        /// rule as origin, and the causing event. A request is never refused — a bound reduces it,
        /// and a fully reduced request still resolves, with a zero final delta and its bound
        /// evidence retained. Any threshold the change crosses is recorded after it finalizes.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The effect whose counter request is being committed.</param>
        /// <param name="causingEvent">The event that caused the change.</param>
        /// <param name="depth">The change's causal depth.</param>
        /// <param name="unit">The containing unit, or null outside one.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        private void CommitCounterRequest(
            ExecutionContext context,
            ActiveEffect effect,
            TraceEventID causingEvent,
            int depth,
            RuntimeUnitID? unit,
            EffectOriginLineage lineage
        )
        {
            CounterRequestOperation request = effect.CounterRequest;
            ProcessCounterCommit commit = context.Counter.Apply(request);

            QuantityChangePayload payload = new QuantityChangePayload(
                request.Counter,
                QuantityCategory.ProcessCounter,
                request.Delta < 0 ? QuantityOperationType.Decrement : QuantityOperationType.Increment,
                request.Delta,
                commit.PriorValue,
                null,
                Array.Empty<QuantityModifierEvidence>(),
                commit.BoundApplied
                    ? new QuantityBoundsEvidence(
                        request.HasFloor ? request.Floor : (int?)null,
                        request.HasCeiling ? request.Ceiling : (int?)null
                    )
                    : null,
                commit.FinalDelta,
                commit.FinalValue
            );

            TraceEventID changeEvent = _builder.AppendEvent(new EventEvidence(
                EventFamilies.Quantity,
                ExecutionEventSubtypes.QuantityChanged,
                Array.Empty<string>(),
                depth,
                unit,
                null,
                causingEvent,
                null,
                null,
                effect.Origin,
                null,
                null,
                null,
                lineage,
                null,
                0,
                null,
                EventDisposition.Resolved,
                null,
                context.CurrentSafetyStatus,
                payload
            ));

            EmitCounterCrossings(context, commit, changeEvent, depth + 1, unit, lineage);
        }

        /// <summary>
        /// Appends the threshold crossing a finalized counter change produces, if any: an entry when
        /// the value reaches the rule's declared ceiling from below, an exit when it leaves that
        /// ceiling. A change that does not move the value across the ceiling records nothing, so
        /// repeated entries and exits stay individually reconstructable.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="commit">The commit the counter produced.</param>
        /// <param name="causingEvent">The counter-change event.</param>
        /// <param name="depth">The crossing event's causal depth.</param>
        /// <param name="unit">The containing unit, or null outside one.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        private void EmitCounterCrossings(
            ExecutionContext context,
            ProcessCounterCommit commit,
            TraceEventID causingEvent,
            int depth,
            RuntimeUnitID? unit,
            EffectOriginLineage lineage
        )
        {
            int? ceiling = CounterCeiling(context);
            if (ceiling == null)
                return;

            bool wasAtCeiling = commit.PriorValue >= ceiling.Value;
            bool isAtCeiling = commit.FinalValue >= ceiling.Value;
            if (wasAtCeiling == isAtCeiling)
                return;

            _builder.AppendEvent(new EventEvidence(
                EventFamilies.Threshold,
                isAtCeiling
                    ? ExecutionEventSubtypes.ThresholdCrossedUpward
                    : ExecutionEventSubtypes.ThresholdCrossedDownward,
                Array.Empty<string>(),
                depth,
                unit,
                null,
                causingEvent,
                null,
                null,
                null,
                null,
                null,
                ThrottlingBand,
                lineage,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            ));
        }

        /// <summary>
        /// Returns the ceiling the governing Process rule declares for its counter - the value whose
        /// crossing is a threshold entry — or null when no request declares one.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <returns>The declared ceiling, or null.</returns>
        private static int? CounterCeiling(ExecutionContext context)
        {
            IReadOnlyList<ActiveEffect> effects = context.Request.InterpretedEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                CounterRequestOperation request = effects[i].CounterRequest;
                if (request != null && request.HasCeiling)
                    return request.Ceiling;
            }

            return null;
        }

        /// <summary>
        /// Appends a LIFECYCLE bookend event with the given subtype, outside any unit, causal depth zero.
        /// The execution's safety status is read from the context, so an aborted execution's bookend
        /// carries the abort rather than a stale normal status.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="subtype">The lifecycle subtype token.</param>
        private void AppendBookend(ExecutionContext context, string subtype)
        {
            EventEvidence evidence = new EventEvidence(
                EventFamilies.Lifecycle,
                subtype,
                Array.Empty<string>(),
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                null
            );

            _builder.AppendEvent(evidence);
        }

        /// <summary>
        /// Returns the canonical quantity identity for a register.
        /// </summary>
        /// <param name="register">The register to name.</param>
        /// <returns>The canonical register name.</returns>
        /// <exception cref="ArgumentException">Thrown when the register is not a known member.</exception>
        private static string RegisterName(CoreRegister register)
        {
            switch (register)
            {
                case CoreRegister.Value:
                    return "Value";

                case CoreRegister.Signal:
                    return "Signal";

                case CoreRegister.Score:
                    return "Score";

                default:
                    throw new ArgumentException($"Unknown register {register}.", nameof(register));
            }
        }
        
        /// <summary>
        /// Emits one SAFETY_LIMIT_APPROACHED event for each limit that has just reached its ceiling
        /// for the first time, and raises the execution's safety status on first contact. Reaching a
        /// ceiling is permitted, so this records a legal diagnostic state and never changes
        /// mechanics. The tallies report which limits sit at their ceilings; which of those are new
        /// is remembered here.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="unit">The containing unit, or null outside any unit.</param>
        private void EmitApproachedLimits(ExecutionContext context, RuntimeUnitID? unit)
        {
            SafetyLimitFlags atCeiling = context.Tallies.LimitsAtCeiling;
            SafetyLimitFlags newlyAtCeiling = atCeiling & ~context.ApproachedLimits;
            if (newlyAtCeiling == SafetyLimitFlags.None)
                return;

            context.ApproachedLimits |= newlyAtCeiling;
            if (context.CurrentSafetyStatus == SafetyStatus.Normal)
                context.CurrentSafetyStatus = SafetyStatus.SafetyLimitApproached;

            for (int bit = 1; bit <= (int)SafetyLimitFlags.TransformationsOnPendingOperation; bit <<= 1)
            {
                SafetyLimitFlags limit = (SafetyLimitFlags)bit;
                if ((newlyAtCeiling & limit) == SafetyLimitFlags.None)
                    continue;

                _builder.AppendEvent(SafetyEvent(
                    context,
                    ExecutionEventSubtypes.SafetyLimitApproached,
                    unit,
                    null,
                    new SafetyAbortPayload(
                        SafetyAbortSignal.ComposeOccurrenceIdentity(
                            EventFamilies.Safety,
                            ExecutionEventSubtypes.SafetyLimitApproached,
                            SafetyAbortSignal.LimitName(limit)),
                        unit ?? default,
                        LimitAtCeiling(limit)
                    )
                ));
            }
        }
        
        /// <summary>
        /// Runs abort finalization inside the existing freeze seam: the attempted occurrence's
        /// evidence, the branch termination it causes, the affected unit's closure when one is still
        /// open, and the safety bookend in place of ordinary completion — then the same Finalize
        /// call the ordinary path makes, with aborted statuses. Events completed before the breach
        /// are preserved exactly as they were recorded; nothing is rolled back and no ancestor's
        /// closure is rewritten.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="signal">The unwind signal carrying the breach evidence.</param>
        /// <returns>The frozen aborted record.</returns>
        private ExecutionRecord FinalizeSafetyAbort(ExecutionContext context, SafetyAbortSignal signal)
        {
            context.CurrentSafetyStatus = SafetyStatus.SafetyAborted;

            RuntimeUnitID? openUnit = InnermostOpenUnit(context);
            TraceEventID reached = _builder.AppendEvent(SafetyEvent(
                context,
                ExecutionEventSubtypes.SafetyLimitReached,
                openUnit,
                null,
                new SafetyAbortPayload(
                    signal.OverLimitOccurrenceIdentity,
                    signal.AffectedUnit,
                    signal.BreachedLimits
                )
            ));

            _builder.AppendEvent(SafetyEvent(
                context,
                ExecutionEventSubtypes.CausalBranchTerminated,
                openUnit,
                reached,
                null
            ));

            if (openUnit != null)
            {
                _builder.CompleteUnit(openUnit.Value, new RuntimeUnitClosure(
                    null,
                    EventDisposition.Skipped,
                    null,
                    Array.Empty<string>(),
                    UnitClosureStatus.SafetyIntervention,
                    context.CurrentSafetyStatus
                ));
            }

            AppendBookend(context, ExecutionEventSubtypes.ExecutionSafetyAborted);

            ExecutionRegisters registers = context.Registers;
            FinalExecutionState finalState = new FinalExecutionState(
                (ValueAmount)registers.Value,
                (SignalValue)registers.Signal,
                (ScoreValue)registers.Score,
                (ScoreValue)registers.Score
            );

            return _builder.Finalize(
                ExecutionCompletionStatus.SafetyAborted,
                SafetyStatus.SafetyAborted,
                context.Tallies.ToCounts(),
                finalState
            );
        }
        
        /// <summary>
        /// The unit that is still open in the builder at the moment of a breach, or null when none
        /// is. A frame outlives its unit's closure — a unit closes before its descendants drain — so
        /// the innermost frame's unit is open only when the breach landed between its opening and
        /// its closure, which happens at the reaction site alone. At most one unit is ever open.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <returns>The open unit, or null.</returns>
        private RuntimeUnitID? InnermostOpenUnit(ExecutionContext context)
        {
            if (context.FrameStack.Count == 0)
                return null;

            RuntimeUnitID unit = context.FrameStack[^1].Unit;
            return _builder.IsUnitOpen(unit) ? unit : (RuntimeUnitID?)null;
        }
        
        /// <summary>
        /// Builds one SAFETY-family event outside the ordinary evidence helpers: unit-scoped when a
        /// unit is open and unit-less otherwise, disposition-free, at causal depth zero. Like every
        /// other emission helper it reads the execution's safety status from the context rather than
        /// receiving it.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="subtype">The safety subtype token.</param>
        /// <param name="unit">The containing unit, or null when none is open.</param>
        /// <param name="causingEvent">The event that caused this one, or null.</param>
        /// <param name="payload">The safety payload, or null.</param>
        /// <returns>The composed evidence.</returns>
        private EventEvidence SafetyEvent(
            ExecutionContext context,
            string subtype,
            RuntimeUnitID? unit,
            TraceEventID? causingEvent,
            EventPayload payload
        )
        {
            return new EventEvidence(
                EventFamilies.Safety,
                subtype,
                Array.Empty<string>(),
                0,
                unit,
                null,
                causingEvent,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                context.CurrentSafetyStatus,
                payload
            );
        }

        /// <summary>
        /// The single-element breached-limit list describing one registry row sitting exactly at its
        /// ceiling. Reaching a ceiling is permitted, so the count equals the ceiling by construction:
        /// every record path refuses the occurrence that would carry a count past it.
        /// </summary>
        /// <param name="limit">The registry row at its ceiling.</param>
        /// <returns>The one-element list.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is not exactly one registry row.</exception>
        private static IReadOnlyList<BreachedLimit> LimitAtCeiling(SafetyLimitFlags limit)
        {
            int ceiling;
            switch (limit)
            {
                case SafetyLimitFlags.AddedExecutionLineageDepth:
                    ceiling = SafetyCeilings.AddedExecutionLineageDepth;
                    break;
                case SafetyLimitFlags.AddedExecutionsPerActivation:
                    ceiling = SafetyCeilings.AddedExecutionsPerActivation;
                    break;
                case SafetyLimitFlags.SourceExecutionUnits:
                    ceiling = SafetyCeilings.SourceExecutionUnitsPerExecution;
                    break;
                case SafetyLimitFlags.EffectReactions:
                    ceiling = SafetyCeilings.EffectReactionsPerExecution;
                    break;
                case SafetyLimitFlags.TransformationsOnPendingOperation:
                    ceiling = SafetyCeilings.TransformationsPerPendingOperation;
                    break;
                default:
                    throw new ArgumentException("An at-ceiling limit requires exactly one registry row.", nameof(limit));
            }

            List<BreachedLimit> atCeiling = new List<BreachedLimit>(1);
            atCeiling.Add(new BreachedLimit(SafetyAbortSignal.LimitName(limit), ceiling, ceiling));
            return atCeiling;
        }

        /// <summary>
        /// The per-execution mutable state, constructed fresh at the top of each execute and passed to
        /// every phase method so the scheduler carries no cross-execution state but the builder. The
        /// frequency ledger is constructed first and the effect engine over the request's
        /// already-interpreted effects, so engine assembly cannot throw on content. The
        /// runtime-adjacency tracker resets with the context, and the final occupied
        /// player-controlled position is hoisted once from the arrangement.
        /// </summary>
        private sealed class ExecutionContext
        {
            /// <summary>
            /// The execution request being run.
            /// </summary>
            public ExecutionRequest Request { get; }

            /// <summary>
            /// The mutable registers for this execution.
            /// </summary>
            public ExecutionRegisters Registers { get; }

            /// <summary>
            /// The explicit frame stack of open causal branches.
            /// </summary>
            public List<ExecutionFrame> FrameStack { get; }

            /// <summary>
            /// The running safety tallies for this execution.
            /// </summary>
            public ExecutionSafetyTallies Tallies { get; }

            /// <summary>
            /// The per-execution frequency ledger.
            /// </summary>
            public FrequencyLedger Ledger { get; }

            /// <summary>
            /// The per-execution effect engine over the request's interpreted effects.
            /// </summary>
            public EffectEngine Engine { get; }
            
            /// <summary>
            /// The per-execution pending added-execution tracker.
            /// </summary>
            public PendingAddedExecutionTracker PendingRequests { get; }

            /// <summary>
            /// The per-execution runtime-adjacency tracker, recorded at every unit closure and
            /// snapshotted at every unit opening.
            /// </summary>
            public RuntimeAdjacencyTracker Adjacency { get; }

            /// <summary>
            /// The execution's running safety status. It begins Normal, rises once to
            /// SafetyLimitApproached at first exact ceiling contact, and is set to SafetyAborted by
            /// abort finalization before the first abort event is emitted. Every evidence-emission
            /// helper reads it here rather than receiving it, so no emission site can stamp a stale
            /// literal.
            /// </summary>
            public SafetyStatus CurrentSafetyStatus { get; set; }

            /// <summary>
            /// The limits already announced as approached, so first contact emits exactly one event per
            /// limit. First-contact memory lives here rather than in the tallies, which stay pure
            /// counters.
            /// </summary>
            public SafetyLimitFlags ApproachedLimits { get; set; }
            
            /// <summary>
            /// The greatest one-based position an occupied player-controlled slot holds — an
            /// Instruction, a contained Instruction, or a Structure header; empty positions never
            /// count — or null when the arrangement holds none. Hoisted once per execution; distinct
            /// from the boundary-placement index, which is zero-based and header-standing.
            /// </summary>
            public SourcePosition? FinalOccupiedPlayerPosition { get; }
            
            /// <summary>
            /// The governing Process rule's counter, initialized to zero. Constructed regardless of
            /// configuration; it is only ever read through <see cref="CounterSnapshot"/>, which
            /// returns null when the Process declares no rule.
            /// </summary>
            public ProcessCounterState Counter { get; }

            /// <summary>
            /// Whether a Process rule governs this execution.
            /// </summary>
            public bool HasProcessRule { get; }

            /// <summary>
            /// The counter value to offer at an operation boundary, or null when no Process rule
            /// governs the execution — which leaves every counter effect structurally ineligible
            /// rather than near-missing.
            /// </summary>
            /// <returns>The current counter value, or null.</returns>
            public int? CounterSnapshot() => HasProcessRule ? Counter.Value : (int?)null;

            public ExecutionContext(ExecutionRequest request)
            {
                Request = request;
                Registers = new ExecutionRegisters(request.InitialState);
                FrameStack = new List<ExecutionFrame>();
                Tallies = new ExecutionSafetyTallies();
                Ledger = new FrequencyLedger();
                Engine = new EffectEngine(request.InterpretedEffects, Ledger);
                PendingRequests = new PendingAddedExecutionTracker();
                Adjacency = new RuntimeAdjacencyTracker();
                CurrentSafetyStatus = SafetyStatus.Normal;
                ApproachedLimits = SafetyLimitFlags.None;
                FinalOccupiedPlayerPosition = ComputeFinalOccupiedPlayerPosition(request.Source.Arrangement.Slots);
                Counter = new ProcessCounterState();
                HasProcessRule = request.Configuration.ProcessRule != null;
            }

            /// <summary>
            /// Scans the arrangement once for the greatest occupied player-controlled position.
            /// </summary>
            /// <param name="slots">The arrangement's slots, in ascending position order.</param>
            /// <returns>The final occupied player-controlled position, or null when none exists.</returns>
            private static SourcePosition? ComputeFinalOccupiedPlayerPosition(IReadOnlyList<SourceSlot> slots)
            {
                SourcePosition? final = null;
                for (int i = 0; i < slots.Count; i++)
                {
                    SourceSlotKind kind = slots[i].Kind;
                    if (kind is SourceSlotKind.Instruction or SourceSlotKind.ContainedInstruction or SourceSlotKind.StructureHeader)
                    {
                        final = slots[i].Position;
                    }
                }

                return final;
            }
        }
    }
}