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
    /// depth-first branch completion is structural recursion. Every event a unit emits carries that
    /// unit's branch lineage, so canonical branches stay byte-identical to lineage-free evidence. The
    /// builder is the only instance field; all per-execution state travels in a nested context
    /// constructed fresh per call, so the scheduler is reusable and reentrancy-clean. Every
    /// request-derived operation that can throw completes before the builder is begun, so a rejected
    /// request always leaves the builder usable.
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
            TraverseSource(context);
            ResolveTraversalBoundaryEffects(context);
            CloseExecution(context);
            ExecutionRecord record = FinalizeEvidence(context);
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
            AppendBookend(ExecutionEventSubtypes.ExecutionStarted);
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
                        TraverseUnit(context, slot, null, null, null);
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
                AppendCancellation(stillPending[i]);
            }
        }
        
        /// <summary>
        /// Returns the greatest top-level slot index that a player-controlled object occupies — a
        /// Structure header's index stands for its whole footprint, since the walk consumes it — or
        /// -1 when the arrangement holds none.
        /// </summary>
        /// <param name="slots">The arrangement's slots.</param>
        /// <returns>The last player-controlled index, or -1.</returns>
        private static int LastPlayerControlledIndex(IReadOnlyList<SourceSlot> slots)
        {
            int last = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlotKind kind = slots[i].Kind;
                if (kind == SourceSlotKind.Instruction || kind == SourceSlotKind.StructureHeader)
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
        private void CloseExecution(ExecutionContext context)
        {
            AppendBookend(ExecutionEventSubtypes.ExecutionCompleted);
        }

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
                SafetyStatus.Normal,
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

            _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.StructureActivated, headerSlot, entryContext));
            _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.StructureEntered, headerSlot, entryContext));

            int firstContained = headerIndex + 1;
            int pastFootprint = headerIndex + structure.Definition.SourceFootprint;
            if (structure.Definition.StructureKind == StructureKind.Repeat)
                TraverseRepeat(context, headerSlot, arrangement, firstContained, pastFootprint, entryIdentity, entryContext);
            else
                TraverseCondition(context, headerSlot, arrangement, firstContained, pastFootprint, entryIdentity, entryContext);

            _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.StructureExited, headerSlot, entryContext));
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
            StructureContext entryContext)
        {
            _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.RepeatCountCaptured, headerSlot, entryContext));

            int count = headerSlot.Structure.Definition.RepeatCount;
            for (int iteration = 1; iteration <= count; iteration++)
            {
                StructureContext iterationContext = new StructureContext(
                    entryContext.StructureAncestry,
                    entryIdentity,
                    StructureIdentities.Iteration(entryIdentity, iteration),
                    null);

                _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.RepeatIterationStarted, headerSlot, iterationContext));
                for (int i = firstContained; i < pastFootprint; i++)
                {
                    SourceSlot contained = arrangement.Slots[i];
                    if (contained.Kind == SourceSlotKind.ContainedInstruction)
                        TraverseUnit(context, contained, iterationContext, null, null);
                }

                _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.RepeatIterationCompleted, headerSlot, iterationContext));
            }
        }

        /// <summary>
        /// Runs a Condition body: one predicate snapshot captured at entry, one evaluation from the
        /// snapshot under the per-evaluation context, then the result event. TRUE schedules each
        /// occupied contained child normally under the evaluation context; FALSE gives each occupied
        /// governed child one activation and one skipped disposition with this evaluation bound as the
        /// skip cause.
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
            _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.ConditionSnapshotCaptured, headerSlot, evaluationContext));

            if (ConditionPredicateEvaluator.Evaluate(predicate, snapshotValue))
            {
                TraceEventID resultEvent = _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.ConditionTrue, headerSlot, evaluationContext));
                OfferConditionSuccess(context, arrangement, firstContained, pastFootprint, resultEvent, evaluationIdentity, evaluationContext);

                for (int i = firstContained; i < pastFootprint; i++)
                {
                    SourceSlot contained = arrangement.Slots[i];
                    if (contained.Kind == SourceSlotKind.ContainedInstruction)
                        TraverseUnit(context, contained, evaluationContext, null, null);
                }

                return;
            }

            _builder.AppendEvent(StructureEvent(ExecutionEventSubtypes.ConditionFalse, headerSlot, evaluationContext));
            string skipCause = "CONDITION_FALSE:" + evaluationIdentity;
            for (int i = firstContained; i < pastFootprint; i++)
            {
                SourceSlot contained = arrangement.Slots[i];
                if (contained.Kind == SourceSlotKind.ContainedInstruction)
                    TraverseUnit(context, contained, evaluationContext, skipCause, null);
            }
        }

        /// <summary>
        /// Opens a unit for a Core or Instruction slot and drives it through the named stages: open
        /// and activate, pending operation, the pre-operation band — skip determination and the
        /// rescue offer — then either the skipped closure or the operation path and the resolved or
        /// rescued closure, then the post-unit offer and the frame's captured-request drain before
        /// the frame pops. A non-null request opens the unit as an added execution on the requested
        /// branch instead of a canonical traversal.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The Core or Instruction slot to execute.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="skipCause">The explicit skip cause, or null when the unit is not skipped.</param>
        /// <param name="request">The originating request, or null for a canonical traversal.</param>
        private void TraverseUnit(
            ExecutionContext context,
            SourceSlot slot,
            StructureContext structureContext,
            string skipCause,
            AddedExecutionRequest request
        )
        {
            bool isCore = slot.Kind == SourceSlotKind.Core;
            RuntimeUnitID unit = OpenAndActivateUnit(context, slot, isCore, structureContext, request);
            ExecutionFrame frame = context.FrameStack[^1];
            EffectOriginLineage lineage = frame.Lineage;

            TraceEventID pendingEvent = EmitPendingOperation(context, slot, isCore, unit, structureContext, lineage);
            EventDisposition bandDisposition = ResolvePreOperationBand(context,
                slot,
                isCore,
                unit,
                pendingEvent,
                structureContext,
                skipCause,
                lineage
            );

            TraceEventID completionEvent;
            if (bandDisposition == EventDisposition.Skipped)
            {
                completionEvent = CloseSkippedUnit(context, slot, isCore, unit, structureContext, lineage);
            }
            else
            {
                TraceEventID quantityEvent = ResolveOperationPath(context, slot, isCore, unit, pendingEvent, structureContext, frame);
                completionEvent = FinalizeAndCloseUnit(context, slot, isCore, unit, quantityEvent, bandDisposition, structureContext, lineage);
            }

            ResolvePendingRequests(context, slot, isCore, unit, frame, bandDisposition);
            OfferPostUnit(context, slot, isCore, unit, frame, completionEvent, bandDisposition, structureContext);
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
            AddedExecutionRequest request)
        {
            if (!context.Tallies.PreflightUnitOpening())
                throw new InvalidOperationException("Preflight prohibits opening another source-execution unit.");

            ActivationKind activation = request == null ? ActivationKind.CanonicalTraversal : ActivationKind.AddedExecution;
            string requestIdentity = request?.RequestIdentity;
            int depth = request == null ? 0 : request.ProposedDepth;
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

            RuntimeUnitID rootActivation = unit;
            if (request != null && context.FrameStack.Count > 0)
                rootActivation = context.FrameStack[context.FrameStack.Count - 1].RootActivation;

            context.FrameStack.Add(new ExecutionFrame(unit, lineage, depth, rootActivation));

            _builder.AppendEvent(UnitStreamEvent(EventFamilies.Source, ExecutionEventSubtypes.SourceObjectActivated, slot, isCore, unit, structureContext, lineage));
            _builder.AppendEvent(UnitStreamEvent(EventFamilies.Source, ExecutionEventSubtypes.SourceExecutionStarted, slot, isCore, unit, structureContext, lineage));
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
            EffectOriginLineage lineage)
        {
            TraceEventID pendingEvent = _builder.AppendEvent(UnitStreamEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationPending, slot, isCore, unit, structureContext, lineage));
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
            EffectOriginLineage lineage)
        {
            if (skipCause == null)
                return EventDisposition.Resolved;

            TraceEventID skipEvent = _builder.AppendEvent(SkipEvent(slot, isCore, unit, pendingEvent, skipCause, structureContext, lineage));
            SkipOccurrence occurrence = new SkipOccurrence(
                unit,
                skipEvent,
                1,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                skipCause,
                true);

            EffectMatchBatch batch = context.Engine.MatchSkip(occurrence);
            if (batch.Qualified.Count == 0)
                return EventDisposition.Skipped;

            if (batch.Qualified.Count > 1)
                throw new InvalidOperationException("More than one rescue effect qualified against one skip; an undeclared rescuer conflict is an authoring defect.");

            ActiveEffect rescueEffect = batch.Qualified[0];
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, skipEvent, 2, rescueEffect.Origin, unit, null, null, lineage));
            context.Engine.Commit(rescueEffect);
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectCommitted, skipEvent, 2, rescueEffect.Origin, unit, null, null, lineage));
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Disposition, ExecutionEventSubtypes.SourceExecutionRescued, skipEvent, 2, rescueEffect.Origin, unit, EventDisposition.Rescued, null, lineage));
            return EventDisposition.Rescued;
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
            _builder.AppendEvent(DispositionFinalizedEvent(slot, isCore, unit, EventDisposition.Skipped, structureContext, lineage));
            TraceEventID completionEvent = _builder.AppendEvent(UnitStreamEvent(EventFamilies.Source, ExecutionEventSubtypes.SourceExecutionCompleted, slot, isCore, unit, structureContext, lineage));

            RuntimeUnitClosure closure = new RuntimeUnitClosure(
                null,
                EventDisposition.Skipped,
                null,
                Array.Empty<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal
            );
            _builder.CompleteUnit(unit, closure);
            return completionEvent;
        }

        /// <summary>
        /// Stage 4 (operating): drives the operation path — the modification boundary with first-time
        /// commitments and selected-host re-applications, resolution, the finalized quantity event
        /// with modifier evidence, threshold crossings, result finalization, both immediate-reaction
        /// boundaries in candidate-event causal order, and the primary-quantity batch's creator
        /// commitments after its reactions fully resolve.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="pendingEvent">The pending-operation event.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="frame">The unit's own frame.</param>
        /// <returns>The unit's finalized quantity event.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the transformation ceiling has been reached — preflight guards it.</exception>
        private TraceEventID ResolveOperationPath(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            TraceEventID pendingEvent,
            StructureContext structureContext,
            ExecutionFrame frame
        )
        {
            EffectOriginLineage lineage = frame.Lineage;
            OperationOccurrence pendingOccurrence = BuildOperationOccurrence(slot, isCore, unit, pendingEvent, 0);
            EffectMatchBatch modificationBatch = context.Engine.MatchPendingOperation(pendingOccurrence);
            AppendNearMisses(modificationBatch, pendingEvent, 1, unit, lineage);

            int modifierSum = 0;
            IReadOnlyList<QuantityModifierEvidence> modifiers = Array.Empty<QuantityModifierEvidence>();
            if (modificationBatch.Qualified.Count > 0 || modificationBatch.Reapplications.Count > 0)
            {
                List<QuantityModifierEvidence> applied = new List<QuantityModifierEvidence>(
                    modificationBatch.Qualified.Count + modificationBatch.Reapplications.Count);
                for (int i = 0; i < modificationBatch.Qualified.Count; i++)
                {
                    ActiveEffect effect = modificationBatch.Qualified[i];
                    if (!context.Tallies.PreflightTransformation())
                        throw new InvalidOperationException("Preflight prohibits another transformation of the pending operation.");

                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, pendingEvent, 1, effect.Origin, unit, null, null, lineage));
                    context.Engine.CommitModification(effect, pendingOccurrence.HostInstance);
                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectCommitted, pendingEvent, 1, effect.Origin, unit, null, null, lineage));
                    context.Tallies.RecordTransformation();
                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationModified, pendingEvent, 1, effect.Origin, unit, null, null, lineage));

                    int amount = effect.Operation.Operand.Constant;
                    modifierSum += amount;
                    applied.Add(new QuantityModifierEvidence(effect.Origin.ToString(), ModificationStage, amount));
                }

                for (int i = 0; i < modificationBatch.Reapplications.Count; i++)
                {
                    ActiveEffect effect = modificationBatch.Reapplications[i];
                    if (!context.Tallies.PreflightTransformation())
                        throw new InvalidOperationException("Preflight prohibits another transformation of the pending operation.");

                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, pendingEvent, 1, effect.Origin, unit, null, null, lineage));
                    context.Tallies.RecordTransformation();
                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationModified, pendingEvent, 1, effect.Origin, unit, null, null, lineage));

                    int amount = effect.Operation.Operand.Constant;
                    modifierSum += amount;
                    applied.Add(new QuantityModifierEvidence(effect.Origin.ToString(), ModificationStage, amount));
                }

                modifiers = applied;
            }

            EvaluatedOperation evaluation = isCore
                ? OperationEvaluator.EvaluateCoreLine(slot.Core.Operation, context.Registers, slot.Position, modifierSum)
                : OperationEvaluator.EvaluateInstruction(slot.Instruction.Definition.PrimaryOperation, context.Registers, slot.Position, modifierSum);

            TraceEventID resolvedEvent = _builder.AppendEvent(UnitStreamEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationResolved, slot, isCore, unit, structureContext, lineage));
            TraceEventID quantityEvent = AppendQuantityEvent(slot, isCore, evaluation, unit, modifiers, structureContext, lineage);
            context.Registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, 1, unit, lineage);

            _builder.AppendEvent(UnitStreamEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationResultFinalized, slot, isCore, unit, structureContext, lineage));

            OperationOccurrence resolvedOccurrence = BuildOperationOccurrence(slot, isCore, unit, resolvedEvent, 0);
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
                lineage
            );
            EffectMatchBatch quantityBatch = context.Engine.MatchQuantityChange(primaryQuantity);
            ResolveReactionBatch(context, quantityBatch, quantityEvent, 0, unit, slot.Position, lineage);
            CommitCreators(context, quantityBatch, quantityEvent, 0, frame, slot, structureContext, unit, false);

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
            _builder.AppendEvent(DispositionFinalizedEvent(slot, isCore, unit, finalDisposition, structureContext, lineage));
            TraceEventID completionEvent = _builder.AppendEvent(UnitStreamEvent(EventFamilies.Source, ExecutionEventSubtypes.SourceExecutionCompleted, slot, isCore, unit, structureContext, lineage));

            RuntimeUnitClosure closure = new RuntimeUnitClosure(
                finalDisposition == EventDisposition.Rescued ? EventDisposition.Skipped : (EventDisposition?)null,
                finalDisposition,
                quantityEvent,
                Array.Empty<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal
            );
            _builder.CompleteUnit(unit, closure);
            return completionEvent;
        }

        /// <summary>
        /// Offers the closed unit to the post-unit boundary. The unit is closed, so its near-miss and
        /// request-chain evidence lives in the parent causal context — unit-less, caused by the
        /// completion event at depth one.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The closed unit.</param>
        /// <param name="frame">The unit's own frame.</param>
        /// <param name="completionEvent">The unit's completion event.</param>
        /// <param name="finalDisposition">The unit's final disposition.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        private void OfferPostUnit(
            ExecutionContext context,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit,
            ExecutionFrame frame,
            TraceEventID completionEvent,
            EventDisposition finalDisposition,
            StructureContext structureContext)
        {
            PostUnitOccurrence occurrence = new PostUnitOccurrence(
                unit,
                completionEvent,
                finalDisposition,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                isCore ? (InstanceID?)null : slot.Instruction.InstanceID,
                structureContext,
                frame.Lineage);

            EffectMatchBatch batch = context.Engine.MatchPostUnit(occurrence);
            AppendNearMisses(batch, completionEvent, 1, null, frame.Lineage);
            CommitCreators(context, batch, completionEvent, 0, frame, slot, structureContext, unit, true);
        }
        
                /// <summary>
        /// Offers a successful Condition evaluation to the engine's Condition-success boundary before
        /// any governed child executes, and establishes each committed creator's request as pending
        /// on the first occupied contained child. The chain is unit-less — no unit is open at a
        /// Structure evaluation — and the request's parent unit stays absent until that child closes.
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
            AppendNearMisses(batch, resultEvent, 1, null, EffectOriginLineage.Empty);
            if (batch.Creators.Count == 0)
                return;

            for (int i = 0; i < batch.Creators.Count; i++)
            {
                ActiveEffect effect = batch.Creators[i];
                _builder.AppendEvent(EffectChainEvent(
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectQualified,
                    resultEvent,
                    1,
                    effect.Origin,
                    null,
                    null,
                    null,
                    EffectOriginLineage.Empty
                ));
                context.Engine.Commit(effect);
                _builder.AppendEvent(EffectChainEvent(
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectCommitted,
                    resultEvent,
                    1,
                    effect.Origin,
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
                    null
                );

                TraceEventID requestedEvent = _builder.AppendEvent(AddedExecutionEvent(
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
        /// Resolves the end-of-player-controlled-traversal boundary: the batch of qualifying boundary
        /// effects is captured against current registers before the first one resolves, then each
        /// resolves in batch order — request, commitment, resolution, and its own source-less quantity
        /// change with register write, threshold crossings, and the ordinary reaction offer. Every
        /// event is outside any unit; the request and resolution pair carries no cause.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ResolvePlayerTraversalBoundary(ExecutionContext context)
        {
            ExecutionRegisters registers = context.Registers;
            BoundaryOccurrence occurrence = new BoundaryOccurrence(
                PlayerTraversalBoundary,
                registers.Value,
                registers.Signal,
                registers.Score
            );

            EffectMatchBatch batch = context.Engine.MatchBoundary(occurrence);
            AppendNearMisses(batch, null, 0, null, EffectOriginLineage.Empty);

            for (int i = 0; i < batch.Qualified.Count; i++)
            {
                ActiveEffect effect = batch.Qualified[i];
                TraceEventID requestedEvent = _builder.AppendEvent(BoundaryChainEvent(
                    ExecutionEventSubtypes.BoundaryEffectRequested, null, 0, effect.Origin));
                context.Engine.Commit(effect);
                TraceEventID resolvedEvent = _builder.AppendEvent(BoundaryChainEvent(
                    ExecutionEventSubtypes.BoundaryEffectResolved, requestedEvent, 1, effect.Origin));

                EvaluatedOperation evaluation = OperationEvaluator.EvaluateBoundaryEffect(effect.Operation, registers);
                TraceEventID quantityEvent = _builder.AppendEvent(ReactionQuantityEvent(
                    evaluation, resolvedEvent, 2, effect.Origin, null, EffectOriginLineage.Empty));
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
                    EffectOriginLineage.Empty
                );
                EffectMatchBatch reactions = context.Engine.MatchQuantityChange(boundaryQuantity);
                ResolveReactionBatch(context, reactions, quantityEvent, 2, null, null, EffectOriginLineage.Empty);
            }
        }

        /// <summary>
        /// Assembles a boundary effect's REACTION-family chain event: outside any unit, carrying the
        /// effect origin and no source origin of its own — a boundary effect manufactures no source
        /// line.
        /// </summary>
        /// <param name="subtype">The REACTION subtype token.</param>
        /// <param name="causingEvent">The causing event, or null for the uncaused request.</param>
        /// <param name="depth">The event's causal depth.</param>
        /// <param name="effectOrigin">The resolving effect's origin instance.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence BoundaryChainEvent(
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
                SafetyStatus.Normal,
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
                    AppendCancellation(cancelled);
                    break;
            }
        }

        /// <summary>
        /// Appends one cancelled request's evidence: unit-less, caused by the request's own request
        /// event at depth one, carrying the creator origin, the request's lineage and inherited
        /// context, the cancelled disposition, and the tracker's reason. The consumed allowance stays
        /// consumed.
        /// </summary>
        /// <param name="cancelled">The cancelled request with its reason.</param>
        private void AppendCancellation(CancelledRequest cancelled)
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
                SafetyStatus.Normal,
                null
            ));
        }

        /// <summary>
        /// Commits each qualified creator in the batch and captures one request per commitment on the
        /// creating branch's frame: the qualification and commitment chain, then the request event
        /// carrying the locked host, the request's own lineage, and the inherited Structure context.
        /// Runs after the batch's reactions fully resolve, so request creation follows reaction
        /// closure at every boundary.
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
        private void CommitCreators(
            ExecutionContext context,
            EffectMatchBatch batch,
            TraceEventID candidateEvent,
            int depth,
            ExecutionFrame frame,
            SourceSlot lockedSlot,
            StructureContext structureContext,
            RuntimeUnitID? unit,
            bool unitLessChain
        )
        {
            int chainDepth = depth + 1;
            RuntimeUnitID? chainUnit = unitLessChain ? (RuntimeUnitID?)null : unit;

            for (int i = 0; i < batch.Creators.Count; i++)
            {
                ActiveEffect effect = batch.Creators[i];
                _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, candidateEvent, chainDepth, effect.Origin, chainUnit, null, null, frame.Lineage));
                context.Engine.Commit(effect);
                _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectCommitted, candidateEvent, chainDepth, effect.Origin, chainUnit, null, null, frame.Lineage));

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
                    unit);

                TraceEventID requestedEvent = _builder.AppendEvent(AddedExecutionEvent(
                    ExecutionEventSubtypes.AddedExecutionRequested,
                    candidateEvent,
                    chainDepth,
                    chainUnit,
                    request,
                    lockedSlot));

                frame.Capture(request with { RequestedEvent = requestedEvent });
            }
        }

        /// <summary>
        /// Drains the frame's captured batch in capture order after the unit closes: each request
        /// preflights against the depth and per-activation ceilings, brackets its descendant unit
        /// with the started and completed events, and executes it as a complete source execution.
        /// The descendant drains its own batch inside its traversal, so depth-first branch completion
        /// is structural; traversal advances only after the batch empties.
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
                if (!context.Tallies.PreflightDescendant(request.ProposedDepth, frame.RootActivation))
                    throw new InvalidOperationException("Preflight prohibits creating another added execution; a lineage-depth or per-activation ceiling would be exceeded.");

                context.Tallies.RecordDescendant(request.ProposedDepth, frame.RootActivation);

                _builder.AppendEvent(AddedExecutionEvent(
                    ExecutionEventSubtypes.AddedExecutionStarted,
                    request.RequestedEvent.Value,
                    1,
                    null,
                    request,
                    request.LockedSlot));

                TraverseUnit(context, request.LockedSlot, request.InheritedContext, null, request);

                _builder.AppendEvent(AddedExecutionEvent(
                    ExecutionEventSubtypes.AddedExecutionCompleted,
                    request.RequestedEvent.Value,
                    1,
                    null,
                    request,
                    request.LockedSlot));
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
            AppendNearMisses(batch, observedEvent, observedDepth + 1, unit, lineage);
            for (int i = 0; i < batch.Qualified.Count; i++)
            {
                ResolveReaction(context, batch.Qualified[i], observedEvent, observedDepth, unit, position, lineage);
            }
        }

        /// <summary>
        /// Resolves one immediate reaction inside the causing unit's closure: qualification and
        /// commitment evidence, the reaction event, the reaction's own quantity change with register
        /// write and threshold crossings, then the depth-first offer of that change back to the engine
        /// so the descendant branch closes before returning. Creators never qualify from a
        /// reaction-caused change, so no creator call site exists here.
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
            if (!context.Tallies.PreflightReaction())
                throw new InvalidOperationException("Preflight prohibits resolving another effect reaction.");

            int chainDepth = observedDepth + 1;
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, observedEvent, chainDepth, effect.Origin, unit, null, null, lineage));
            context.Engine.Commit(effect);
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectCommitted, observedEvent, chainDepth, effect.Origin, unit, null, null, lineage));
            context.Tallies.RecordReaction();
            TraceEventID reactionEvent = _builder.AppendEvent(EffectChainEvent(EventFamilies.Reaction, ExecutionEventSubtypes.ImmediateReactionResolved, observedEvent, chainDepth, effect.Origin, unit, null, null, lineage));

            EvaluatedOperation evaluation = OperationEvaluator.EvaluateInstruction(effect.Operation, context.Registers, position, 0);
            int quantityDepth = chainDepth + 1;
            TraceEventID quantityEvent = _builder.AppendEvent(ReactionQuantityEvent(evaluation, reactionEvent, quantityDepth, effect.Origin, unit, lineage));
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
                lineage
            );
            EffectMatchBatch descendants = context.Engine.MatchQuantityChange(occurrence);
            ResolveReactionBatch(context, descendants, quantityEvent, quantityDepth, unit, position, lineage);
        }

        /// <summary>
        /// Builds the typed occurrence for a unit's primary operation at the pending or resolved
        /// boundary: operator, target register, and operand shape from the payload, ownership and the
        /// owning host instance from the slot kind.
        /// </summary>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="candidateEvent">The candidate event the occurrence mirrors.</param>
        /// <param name="causalDepth">The candidate event's causal depth.</param>
        /// <returns>The operation occurrence.</returns>
        private static OperationOccurrence BuildOperationOccurrence(
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
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned
            );
        }

        /// <summary>
        /// Appends one EFFECT_FAILED_TO_QUALIFY event per near-miss in the batch, each caused by the
        /// candidate event at the given depth, carrying the failed-to-qualify disposition with the
        /// failed requirement, the emitting branch's lineage, and a registry-clean empty qualifier
        /// list.
        /// </summary>
        /// <param name="batch">The captured batch.</param>
        /// <param name="candidateEvent">The candidate event the near-misses observed.</param>
        /// <param name="depth">The near-miss events' causal depth.</param>
        /// <param name="unit">The containing unit, or null when the offer is unit-less.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        private void AppendNearMisses(
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
                    EventFamilies.Qualification,
                    ExecutionEventSubtypes.EffectFailedToQualify,
                    candidateEvent,
                    depth,
                    nearMiss.Effect.Origin,
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
        /// <param name="subtype">The STRUCTURE subtype token.</param>
        /// <param name="headerSlot">The Structure header slot.</param>
        /// <param name="structureContext">The context the event executes within.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence StructureEvent(
            string subtype,
            SourceSlot headerSlot,
            StructureContext structureContext)
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
                SafetyStatus.Normal,
                null
            );
        }

        /// <summary>
        /// Assembles an ADDED_EXECUTION-family event: caused by the given event at the given depth,
        /// carrying the creator origin, the request identity and proposed depth together, the
        /// request's lineage and inherited context, and the locked host's ownership and position.
        /// </summary>
        /// <param name="subtype">The ADDED_EXECUTION subtype token.</param>
        /// <param name="causingEvent">The causing event.</param>
        /// <param name="depth">The event's causal depth.</param>
        /// <param name="unit">The containing unit, or null when the event is unit-less.</param>
        /// <param name="request">The request the event records.</param>
        /// <param name="lockedSlot">The request's locked host slot.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence AddedExecutionEvent(
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
                SafetyStatus.Normal,
                null
            );
        }

        /// <summary>
        /// Assembles the skip event: DISPOSITION family, caused by the pending-operation event at
        /// depth one, in-unit, mirroring the unit's ownership, host or Core-line identity, and
        /// position, carrying the skipped disposition with its explicit cause, the emitting branch's
        /// lineage, and the unit's Structure context, payload-free.
        /// </summary>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="pendingEvent">The causing pending-operation event.</param>
        /// <param name="skipCause">The explicit skip cause.</param>
        /// <param name="structureContext">The governing Structure context.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence SkipEvent(
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
                SafetyStatus.Normal,
                null
            );
        }

        /// <summary>
        /// Assembles a SOURCE/OPERATION stream event mirroring the unit's ownership, host or Core-line
        /// identity, and position — uncaused, at depth zero, in-unit, payload-free, carrying the
        /// emitting branch's lineage and the unit's Structure context when governed.
        /// </summary>
        /// <param name="family">The event family token.</param>
        /// <param name="subtype">The event subtype token.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence UnitStreamEvent(
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
                SafetyStatus.Normal,
                null
            );
        }

        /// <summary>
        /// Assembles the disposition-finalization stream event: the unit-mirroring stream shape —
        /// uncaused, at depth zero, in-unit, payload-free — carrying the unit's final disposition, the
        /// emitting branch's lineage, and its Structure context when governed.
        /// </summary>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="disposition">The unit's final disposition.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence DispositionFinalizedEvent(
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
                SafetyStatus.Normal,
                null
            );
        }

        /// <summary>
        /// Assembles a QUALIFICATION/REACTION/DISPOSITION/modified-operation chain event: caused by
        /// the observed candidate at the given depth, carrying the effect origin and the emitting
        /// branch's lineage, payload-free, with no source origin of its own.
        /// </summary>
        /// <param name="family">The event family token.</param>
        /// <param name="subtype">The event subtype token.</param>
        /// <param name="causingEvent">The observed candidate event.</param>
        /// <param name="depth">The chain event's causal depth.</param>
        /// <param name="effectOrigin">The observing effect's origin instance.</param>
        /// <param name="unit">The containing unit, or null when the chain is unit-less.</param>
        /// <param name="disposition">The disposition, or null when none.</param>
        /// <param name="dispositionReason">The disposition reason, or null when none.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence EffectChainEvent(
            string family,
            string subtype,
            TraceEventID? causingEvent,
            int depth,
            InstanceID effectOrigin,
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
                disposition,
                dispositionReason,
                SafetyStatus.Normal,
                null
            );
        }

        /// <summary>
        /// Assembles a reaction's own quantity event: caused by the reaction event at the given depth,
        /// carrying the effect origin, the emitting branch's lineage, and the resolved payload but no
        /// ownership, host, or position — a reaction never appears source-originated.
        /// </summary>
        /// <param name="evaluation">The resolved reaction evaluation.</param>
        /// <param name="causingEvent">The causing reaction event.</param>
        /// <param name="depth">The quantity event's causal depth.</param>
        /// <param name="effectOrigin">The causing effect's origin instance.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence ReactionQuantityEvent(
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
                SafetyStatus.Normal,
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
                SafetyStatus.Normal,
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
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="evaluation">The resolved evaluation.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="modifiers">The applied modifier evidence, or empty.</param>
        /// <param name="structureContext">The governing Structure context, or null at top level.</param>
        /// <param name="lineage">The emitting branch's effect-origin lineage.</param>
        /// <returns>The minted quantity-event identity.</returns>
        private TraceEventID AppendQuantityEvent(
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
                SafetyStatus.Normal,
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
                    SafetyStatus.Normal,
                    null
                );

                _builder.AppendEvent(evidence);
            }
        }

        /// <summary>
        /// Appends a LIFECYCLE bookend event with the given subtype, outside any unit, causal depth zero.
        /// </summary>
        /// <param name="subtype">The lifecycle subtype token.</param>
        private void AppendBookend(string subtype)
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
                SafetyStatus.Normal,
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
        /// The per-execution mutable state, constructed fresh at the top of each execute and passed to
        /// every phase method so the scheduler carries no cross-execution state but the builder. The
        /// frequency ledger is constructed first and the effect engine over the request's
        /// already-interpreted effects, so engine assembly cannot throw on content.
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

            public ExecutionContext(ExecutionRequest request)
            {
                Request = request;
                Registers = new ExecutionRegisters(request.InitialState);
                FrameStack = new List<ExecutionFrame>();
                Tallies = new ExecutionSafetyTallies();
                Ledger = new FrequencyLedger();
                Engine = new EffectEngine(request.InterpretedEffects, Ledger);
                PendingRequests = new PendingAddedExecutionTracker();
            }
        }
    }
}