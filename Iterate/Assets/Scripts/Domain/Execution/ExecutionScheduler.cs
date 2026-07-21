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
    /// <see cref="ExecutionTraceBuilder"/> and returning the frozen <see cref="ExecutionRecord"/>. The
    /// builder is the only instance field; all per-execution state travels in a nested context constructed
    /// fresh per call, so the scheduler is reusable and reentrancy-clean. Every request-derived operation
    /// that can throw completes before the builder is begun, so a rejected request always leaves the
    /// builder usable.
    /// </summary>
    public sealed class ExecutionScheduler
    {
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
        /// Phase 2: assembles the evidence header fully, begins the builder, and appends the
        /// EXECUTION_STARTED lifecycle bookend. All throwing header assembly completes before the builder
        /// is begun.
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
                new List<InstanceID>(),
                new List<InstanceID>(),
                request.InitialState
            );

            _builder.Begin(header);
            AppendBookend(ExecutionEventSubtypes.ExecutionStarted);
        }

        /// <summary>
        /// Phase 3: neutrally resets Value, Signal, and Score to zero, appending a QUANTITY_RESET event for
        /// each with its initial-state prior, outside any unit.
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
        /// Instruction slot, evaluating its operation, appending the quantity event, writing the register,
        /// emitting any Score-band crossings, and closing the unit.
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
        /// Phase 8: expires compilation-scoped effects. A named no-op — nothing is registered at this
        /// content, and this phase runs after the freeze.
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        private void ExpireCompilationScopedEffects(ExecutionContext context)
        {
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
        /// Opens a unit for a Core or Instruction slot, evaluates its operation, appends the quantity
        /// event and any Score crossings, writes the register, and closes the unit Resolved/NormalCompletion.
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

            EvaluatedOperation evaluation = isCore
                ? OperationEvaluator.EvaluateCoreLine(slot.Core.Operation, context.Registers, slot.Position)
                : OperationEvaluator.EvaluateInstruction(slot.Instruction.Definition.PrimaryOperation, context.Registers, slot.Position);

            TraceEventID quantityEvent = AppendQuantityEvent(slot, isCore, evaluation, unit);
            context.Registers.Write(evaluation.Register, evaluation.FinalValue);

            if (evaluation.Register == CoreRegister.Score)
                EmitThresholdCrossings(context, evaluation, quantityEvent, unit);

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
        /// host or Core-line identity, and position.
        /// </summary>
        /// <param name="slot">The executing slot.</param>
        /// <param name="isCore">Whether the slot is Core-owned.</param>
        /// <param name="evaluation">The resolved evaluation.</param>
        /// <param name="unit">The containing unit.</param>
        /// <returns>The minted quantity-event identity.</returns>
        private TraceEventID AppendQuantityEvent(
            SourceSlot slot,
            bool isCore,
            EvaluatedOperation evaluation,
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
        /// Emits one threshold event per Score-band crossing, caused by the quantity event and carrying
        /// causal depth one (the causing event's depth plus one).
        /// </summary>
        /// <param name="context">The per-execution context.</param>
        /// <param name="evaluation">The resolved Score evaluation.</param>
        /// <param name="causingEvent">The causing quantity event.</param>
        /// <param name="unit">The containing unit.</param>
        private void EmitThresholdCrossings(
            ExecutionContext context,
            EvaluatedOperation evaluation,
            TraceEventID causingEvent,
            RuntimeUnitID unit)
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
                    1,
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
        /// every phase method so the scheduler carries no cross-execution state but the builder.
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

            public ExecutionContext(ExecutionRequest request)
            {
                Request = request;
                Registers = new ExecutionRegisters(request.InitialState);
                FrameStack = new List<ExecutionFrame>();
                Tallies = new ExecutionSafetyTallies();
            }
        }
    }
}