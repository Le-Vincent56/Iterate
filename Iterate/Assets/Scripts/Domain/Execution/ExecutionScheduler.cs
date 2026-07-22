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
    /// Core/Instruction unit emits the full SOURCE/OPERATION event stream, offers its pending operation
    /// to the effect engine at the modification band, and offers its resolved operation and finalized
    /// quantity change at the immediate-reaction boundary in candidate-event causal order. Qualified
    /// reactions resolve inside the causing unit's closure as captured sibling batches, each
    /// descendant branch resolving to closure before the remaining siblings. The builder is the only
    /// instance field; all per-execution state travels in a nested context constructed fresh per call,
    /// so the scheduler is reusable and reentrancy-clean. Every request-derived operation that can
    /// throw completes before the builder is begun, so a rejected request always leaves the builder
    /// usable.
    /// </summary>
    public sealed class ExecutionScheduler
    {
        /// <summary>
        /// The declared stage token for a modification-band transformation.
        /// </summary>
        private const string ModificationStage = "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION";

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
        /// Phase 1: accepts the locked compiled source and re-asserts the content contract as defense in
        /// depth. A request that validated at construction cannot legally fail here.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <exception cref="InvalidOperationException">Thrown when a validated request carries unsupported content.</exception>
        private void AcceptCompilationHandoff(ExecutionContext context)
        {
            IReadOnlyList<SourceSlot> slots = context.Request.Source.Arrangement.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlotKind kind = slots[i].Kind;
                if (kind != SourceSlotKind.Core && kind != SourceSlotKind.Empty && kind != SourceSlotKind.Instruction)
                    throw new InvalidOperationException("A validated request unexpectedly carries unsupported content.");
            }
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
        /// Phase 4: walks the arrangement top to bottom, opening a runtime unit for each Core and
        /// Instruction slot, emitting its full event stream through both occurrence boundaries, and
        /// closing the unit.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void TraverseSource(ExecutionContext context)
        {
            IReadOnlyList<SourceSlot> slots = context.Request.Source.Arrangement.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlot slot = slots[i];
                switch (slot.Kind)
                {
                    case SourceSlotKind.Empty:
                        break;

                    case SourceSlotKind.Core:
                    case SourceSlotKind.Instruction:
                        TraverseUnit(context, slot);
                        break;

                    default:
                        throw new InvalidOperationException("A validated request unexpectedly carries unsupported content.");
                }
            }
        }

        /// <summary>
        /// Phase 5: resolves declared traversal-boundary effects. A named no-op — nothing is declared at
        /// this content.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ResolveTraversalBoundaryEffects(ExecutionContext context)
        {
        }

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
        /// Opens a unit for a Core or Instruction slot and drives its full stream: source start, pending
        /// operation, the modification boundary, resolution, the finalized quantity event with modifier
        /// evidence, threshold crossings, result finalization, the immediate-reaction boundary — the
        /// resolved-operation batch then the finalized-quantity batch, in candidate-event causal
        /// order — and source completion, closing the unit Resolved/NormalCompletion.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="slot">The Core or Instruction slot to execute.</param>
        private void TraverseUnit(ExecutionContext context, SourceSlot slot)
        {
            if (!context.Tallies.PreflightUnitOpening())
                throw new InvalidOperationException("Preflight prohibits opening another source-execution unit.");

            bool isCore = slot.Kind == SourceSlotKind.Core;
            RuntimeUnitOpening opening = isCore
                ? new RuntimeUnitOpening(
                    null,
                    slot.Core.Identity,
                    OwnershipClassification.CoreOwned,
                    slot.Position,
                    ActivationKind.CanonicalTraversal,
                    null,
                    0,
                    EffectOriginLineage.Empty,
                    null,
                    null
                )
                : new RuntimeUnitOpening(
                    slot.Instruction.InstanceID,
                    null,
                    OwnershipClassification.PlayerOwned,
                    slot.Position,
                    ActivationKind.CanonicalTraversal,
                    null,
                    0,
                    EffectOriginLineage.Empty,
                    null,
                    null
                );

            RuntimeUnitID unit = _builder.OpenUnit(opening);
            context.Tallies.RecordUnitOpened();
            context.FrameStack.Add(new ExecutionFrame(unit, EffectOriginLineage.Empty));

            _builder.AppendEvent(UnitStreamEvent(EventFamilies.Source, ExecutionEventSubtypes.SourceExecutionStarted, slot, isCore, unit));
            TraceEventID pendingEvent = _builder.AppendEvent(UnitStreamEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationPending, slot, isCore, unit));
            context.Tallies.BeginPendingOperation();

            OperationOccurrence pendingOccurrence = BuildOperationOccurrence(slot, isCore, unit, pendingEvent, 0);
            EffectMatchBatch modificationBatch = context.Engine.MatchPendingOperation(pendingOccurrence);
            AppendNearMisses(modificationBatch, pendingEvent, 1, unit);

            int modifierSum = 0;
            IReadOnlyList<QuantityModifierEvidence> modifiers = Array.Empty<QuantityModifierEvidence>();
            if (modificationBatch.Qualified.Count > 0)
            {
                List<QuantityModifierEvidence> applied = new List<QuantityModifierEvidence>(modificationBatch.Qualified.Count);
                for (int i = 0; i < modificationBatch.Qualified.Count; i++)
                {
                    ActiveEffect effect = modificationBatch.Qualified[i];
                    if (!context.Tallies.PreflightTransformation())
                        throw new InvalidOperationException("Preflight prohibits another transformation of the pending operation.");

                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, pendingEvent, 1, effect.Origin, unit, null, null));
                    context.Engine.Commit(effect);
                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectCommitted, pendingEvent, 1, effect.Origin, unit, null, null));
                    context.Tallies.RecordTransformation();
                    _builder.AppendEvent(EffectChainEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationModified, pendingEvent, 1, effect.Origin, unit, null, null));

                    int amount = effect.Operation.Operand.Constant;
                    modifierSum += amount;
                    applied.Add(new QuantityModifierEvidence(effect.Origin.ToString(), ModificationStage, amount));
                }

                modifiers = applied;
            }

            EvaluatedOperation evaluation = isCore
                ? OperationEvaluator.EvaluateCoreLine(slot.Core.Operation, context.Registers, slot.Position, modifierSum)
                : OperationEvaluator.EvaluateInstruction(slot.Instruction.Definition.PrimaryOperation, context.Registers, slot.Position, modifierSum);

            TraceEventID resolvedEvent = _builder.AppendEvent(UnitStreamEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationResolved, slot, isCore, unit));
            TraceEventID quantityEvent = AppendQuantityEvent(slot, isCore, evaluation, unit, modifiers);
            context.Registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, 1, unit);

            _builder.AppendEvent(UnitStreamEvent(EventFamilies.Operation, ExecutionEventSubtypes.PrimaryOperationResultFinalized, slot, isCore, unit));

            OperationOccurrence resolvedOccurrence = BuildOperationOccurrence(slot, isCore, unit, resolvedEvent, 0);
            EffectMatchBatch resolvedBatch = context.Engine.MatchResolvedOperation(resolvedOccurrence);
            ResolveReactionBatch(context, resolvedBatch, resolvedEvent, 0, unit, slot.Position);

            QuantityOccurrence primaryQuantity = new QuantityOccurrence(
                unit,
                quantityEvent,
                0,
                evaluation.Register,
                evaluation.FinalDelta,
                isCore ? OwnershipClassification.CoreOwned : OwnershipClassification.PlayerOwned,
                null,
                true
            );
            EffectMatchBatch quantityBatch = context.Engine.MatchQuantityChange(primaryQuantity);
            ResolveReactionBatch(context, quantityBatch, quantityEvent, 0, unit, slot.Position);

            _builder.AppendEvent(UnitStreamEvent(EventFamilies.Source, ExecutionEventSubtypes.SourceExecutionCompleted, slot, isCore, unit));

            RuntimeUnitClosure closure = new RuntimeUnitClosure(
                null,
                EventDisposition.Resolved,
                quantityEvent,
                Array.Empty<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal
            );
            _builder.CompleteUnit(unit, closure);

            context.FrameStack.RemoveAt(context.FrameStack.Count - 1);
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
        private void ResolveReactionBatch(
            ExecutionContext context,
            EffectMatchBatch batch,
            TraceEventID observedEvent,
            int observedDepth,
            RuntimeUnitID unit,
            SourcePosition position
        )
        {
            AppendNearMisses(batch, observedEvent, observedDepth + 1, unit);
            for (int i = 0; i < batch.Qualified.Count; i++)
            {
                ResolveReaction(context, batch.Qualified[i], observedEvent, observedDepth, unit, position);
            }
        }

        /// <summary>
        /// Resolves one immediate reaction inside the causing unit's closure: qualification and
        /// commitment evidence, the reaction event, the reaction's own quantity change with register
        /// write and threshold crossings, then the depth-first offer of that change back to the engine
        /// so the descendant branch closes before returning.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="effect">The qualified reaction effect.</param>
        /// <param name="observedEvent">The candidate event the effect observed.</param>
        /// <param name="observedDepth">The candidate event's causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="position">The unit's source position, for reaction evaluation.</param>
        /// <exception cref="InvalidOperationException">Thrown when the reaction ceiling has been reached — preflight guards it.</exception>
        private void ResolveReaction(
            ExecutionContext context,
            ActiveEffect effect,
            TraceEventID observedEvent,
            int observedDepth,
            RuntimeUnitID unit,
            SourcePosition position
        )
        {
            if (!context.Tallies.PreflightReaction())
                throw new InvalidOperationException("Preflight prohibits resolving another effect reaction.");

            int chainDepth = observedDepth + 1;
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectQualified, observedEvent, chainDepth, effect.Origin, unit, null, null));
            context.Engine.Commit(effect);
            _builder.AppendEvent(EffectChainEvent(EventFamilies.Qualification, ExecutionEventSubtypes.EffectCommitted, observedEvent, chainDepth, effect.Origin, unit, null, null));
            context.Tallies.RecordReaction();
            TraceEventID reactionEvent = _builder.AppendEvent(EffectChainEvent(EventFamilies.Reaction, ExecutionEventSubtypes.ImmediateReactionResolved, observedEvent, chainDepth, effect.Origin, unit, null, null));

            EvaluatedOperation evaluation = OperationEvaluator.EvaluateInstruction(effect.Operation, context.Registers, position, 0);
            int quantityDepth = chainDepth + 1;
            TraceEventID quantityEvent = _builder.AppendEvent(ReactionQuantityEvent(evaluation, reactionEvent, quantityDepth, effect.Origin, unit));
            context.Registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, quantityDepth + 1, unit);

            QuantityOccurrence occurrence = new QuantityOccurrence(
                unit,
                quantityEvent,
                quantityDepth,
                evaluation.Register,
                evaluation.FinalDelta,
                null,
                effect.Origin,
                false
            );
            EffectMatchBatch descendants = context.Engine.MatchQuantityChange(occurrence);
            ResolveReactionBatch(context, descendants, quantityEvent, quantityDepth, unit, position);
        }

        /// <summary>
        /// Builds the typed occurrence for a unit's primary operation at the pending or resolved
        /// boundary: operator, target register, and operand shape from the payload, ownership from the
        /// slot kind.
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
        /// failed requirement and a registry-clean empty qualifier list.
        /// </summary>
        /// <param name="batch">The captured batch.</param>
        /// <param name="candidateEvent">The candidate event the near-misses observed.</param>
        /// <param name="depth">The near-miss events' causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        private void AppendNearMisses(
            EffectMatchBatch batch,
            TraceEventID candidateEvent,
            int depth,
            RuntimeUnitID unit
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
                    nearMiss.FailedRequirement)
                );
            }
        }

        /// <summary>
        /// Assembles a SOURCE/OPERATION stream event mirroring the unit's ownership, host or Core-line
        /// identity, and position — uncaused, at depth zero, in-unit, payload-free.
        /// </summary>
        /// <param name="family">The event family token.</param>
        /// <param name="subtype">The event subtype token.</param>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="unit">The containing unit.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence UnitStreamEvent(
            string family,
            string subtype,
            SourceSlot slot,
            bool isCore,
            RuntimeUnitID unit
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
        /// Assembles a QUALIFICATION/REACTION/modified-operation chain event: caused by the observed
        /// candidate at the given depth, carrying the effect origin, in-unit, payload-free, with no
        /// source origin of its own.
        /// </summary>
        /// <param name="family">The event family token.</param>
        /// <param name="subtype">The event subtype token.</param>
        /// <param name="causingEvent">The observed candidate event.</param>
        /// <param name="depth">The chain event's causal depth.</param>
        /// <param name="effectOrigin">The observing effect's origin instance.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="disposition">The disposition, or null when none.</param>
        /// <param name="dispositionReason">The disposition reason, or null when none.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence EffectChainEvent(
            string family,
            string subtype,
            TraceEventID causingEvent,
            int depth,
            InstanceID effectOrigin,
            RuntimeUnitID unit,
            EventDisposition? disposition,
            string dispositionReason
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
                EffectOriginLineage.Empty,
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
        /// carrying the effect origin and the resolved payload but no ownership, host, or position —
        /// a reaction never appears source-originated.
        /// </summary>
        /// <param name="evaluation">The resolved reaction evaluation.</param>
        /// <param name="causingEvent">The causing reaction event.</param>
        /// <param name="depth">The quantity event's causal depth.</param>
        /// <param name="effectOrigin">The causing effect's origin instance.</param>
        /// <param name="unit">The containing unit.</param>
        /// <returns>The assembled evidence.</returns>
        private static EventEvidence ReactionQuantityEvent(
            EvaluatedOperation evaluation,
            TraceEventID causingEvent,
            int depth,
            InstanceID effectOrigin,
            RuntimeUnitID unit
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
        /// modification.
        /// </summary>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="evaluation">The resolved evaluation.</param>
        /// <param name="unit">The containing unit.</param>
        /// <param name="modifiers">The applied modifier evidence, or empty.</param>
        /// <returns>The minted quantity-event identity.</returns>
        private TraceEventID AppendQuantityEvent(
            SourceSlot slot,
            bool isCore,
            EvaluatedOperation evaluation,
            RuntimeUnitID unit,
            IReadOnlyList<QuantityModifierEvidence> modifiers
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
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                EventDisposition.Resolved,
                null,
                SafetyStatus.Normal,
                payload
            );

            return _builder.AppendEvent(evidence);
        }

        /// <summary>
        /// Emits one threshold event per Score-band crossing, caused by the given quantity event at the
        /// given depth (the causing event's depth plus one).
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="evaluation">The resolved Score evaluation.</param>
        /// <param name="causingEvent">The causing quantity event.</param>
        /// <param name="depth">The crossing events' causal depth.</param>
        /// <param name="unit">The containing unit.</param>
        private void EmitThresholdCrossings(
            ExecutionContext context,
            EvaluatedOperation evaluation,
            TraceEventID causingEvent,
            int depth,
            RuntimeUnitID unit
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

            public ExecutionContext(ExecutionRequest request)
            {
                Request = request;
                Registers = new ExecutionRegisters(request.InitialState);
                FrameStack = new List<ExecutionFrame>();
                Tallies = new ExecutionSafetyTallies();
                Ledger = new FrequencyLedger();
                Engine = new EffectEngine(request.InterpretedEffects, Ledger);
            }
        }
    }
}