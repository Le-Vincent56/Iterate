using System;
using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The append-only assembler for one execution's evidence, created once per Process scope and reused
    /// across executions. Identities are minted in append order (events) and open order (units); every
    /// method validates fully before mutating, so a rejected call mints no identity and leaves the builder
    /// usable. Per-unit child-event and descendant-unit linkage is derived at freeze from stored evidence,
    /// never accumulated during append. Finalization produces an aliasing-free frozen record and resets
    /// the builder, retaining buffer capacity for the next execution.
    /// </summary>
    public sealed class ExecutionTraceBuilder
    {
        private readonly List<EventRecord> _events;
        private readonly List<RuntimeUnitOpening> _unitOpenings;
        private readonly List<RuntimeUnitClosure> _unitClosures;
        private readonly List<int> _startOrders;
        private readonly List<int> _completionOrders;
        private readonly List<EvidenceDefect> _defects;
        private ExecutionEvidenceHeader _header;
        private bool _inProgress;
        private int _completionCounter;

        public ExecutionTraceBuilder()
        {
            _events = new List<EventRecord>(SafetyCeilings.EffectReactionsPerExecution);
            _unitOpenings = new List<RuntimeUnitOpening>(SafetyCeilings.SourceExecutionUnitsPerExecution);
            _unitClosures = new List<RuntimeUnitClosure>(SafetyCeilings.SourceExecutionUnitsPerExecution);
            _startOrders = new List<int>(SafetyCeilings.SourceExecutionUnitsPerExecution);
            _completionOrders = new List<int>(SafetyCeilings.SourceExecutionUnitsPerExecution);
            _defects = new List<EvidenceDefect>(SafetyCeilings.SourceExecutionUnitsPerExecution);
        }

        /// <summary>
        /// Begins a new execution with the given reproduction header.
        /// </summary>
        /// <param name="header">The reproduction header; never null.</param>
        public void Begin(ExecutionEvidenceHeader header)
        {
            if (_inProgress)
                throw new InvalidOperationException("An execution is already in progress; finalize it before beginning another.");

            _header = header ?? throw new ArgumentException("Begin requires a header.", nameof(header));
            _inProgress = true;
        }

        /// <summary>
        /// Opens a runtime unit, minting the next one-based identity and start order.
        /// </summary>
        /// <param name="opening">The opening evidence; a referenced parent unit must have been minted.</param>
        /// <returns>The minted unit identity.</returns>
        public RuntimeUnitID OpenUnit(RuntimeUnitOpening opening)
        {
            if (!_inProgress)
                throw new InvalidOperationException("OpenUnit requires an execution in progress; call Begin first.");
            
            if (opening == null)
                throw new ArgumentException("OpenUnit requires an opening.", nameof(opening));

            if (opening.ParentUnit != null)
                RequireMintedUnit(opening.ParentUnit.Value, nameof(opening));

            int identity = _unitOpenings.Count + 1;
            _unitOpenings.Add(opening);
            _unitClosures.Add(null);
            _startOrders.Add(identity);
            _completionOrders.Add(0);
            return new RuntimeUnitID(identity);
        }

        /// <summary>
        /// Appends an event, minting the next one-based identity, which is also its chronological order.
        /// </summary>
        /// <param name="evidence">The event evidence; referenced units and events must already exist.</param>
        /// <returns>The minted event identity.</returns>
        public TraceEventID AppendEvent(EventEvidence evidence)
        {
            if (!_inProgress)
                throw new InvalidOperationException("AppendEvent requires an execution in progress; call Begin first.");
            
            if (evidence == null)
                throw new ArgumentException("AppendEvent requires evidence.", nameof(evidence));
            
            if (evidence.ContainingUnit != null)
                RequireOpenUnit(evidence.ContainingUnit.Value, nameof(evidence));
            
            if (evidence.ParentEvent != null)
                RequireMintedEvent(evidence.ParentEvent.Value, nameof(evidence));
            
            if (evidence.CausingEvent != null)
                RequireMintedEvent(evidence.CausingEvent.Value, nameof(evidence));

            TraceEventID identity = new TraceEventID(_events.Count + 1);
            _events.Add(new EventRecord(identity, evidence));
            return identity;
        }

        /// <summary>
        /// Completes an open unit, recording its closure and assigning the next completion order.
        /// </summary>
        /// <param name="unit">The unit to complete; must be open.</param>
        /// <param name="closure">The closure evidence; a referenced primary-operation event must exist.</param>
        public void CompleteUnit(RuntimeUnitID unit, RuntimeUnitClosure closure)
        {
            if (!_inProgress)
                throw new InvalidOperationException("CompleteUnit requires an execution in progress; call Begin first.");
            
            if (closure == null)
                throw new ArgumentException("CompleteUnit requires a closure.", nameof(closure));
            
            RequireOpenUnit(unit, nameof(unit));
            if (closure.PrimaryOperationEvent != null)
                RequireMintedEvent(closure.PrimaryOperationEvent.Value, nameof(closure));

            int index = unit.Value - 1;
            _unitClosures[index] = closure;
            _completionOrders[index] = ++_completionCounter;
        }

        /// <summary>
        /// Records a missing-evidence defect. A referenced anchor identity must already exist; the content
        /// of the gap itself never throws.
        /// </summary>
        /// <param name="defect">The defect to record.</param>
        public void ReportDefect(EvidenceDefect defect)
        {
            if (!_inProgress)
                throw new InvalidOperationException("ReportDefect requires an execution in progress; call Begin first.");
            
            if (defect == null)
                throw new ArgumentException("ReportDefect requires a defect.", nameof(defect));
            
            if (defect.Event != null)
                RequireMintedEvent(defect.Event.Value, nameof(defect));
            
            if (defect.Unit != null)
                RequireMintedUnit(defect.Unit.Value, nameof(defect));

            _defects.Add(defect);
        }

        /// <summary>
        /// Freezes the execution into an immutable record, deriving per-unit linkage and the derived
        /// histories, then resets the builder for reuse while retaining buffer capacity.
        /// </summary>
        /// <param name="completion">The execution completion status.</param>
        /// <param name="safetyStatus">The execution safety status.</param>
        /// <param name="safetyCounts">The high-water safety tallies.</param>
        /// <param name="finalState">The final register state.</param>
        /// <returns>The frozen execution record, owning its own arrays.</returns>
        public ExecutionRecord Finalize(
            ExecutionCompletionStatus completion,
            SafetyStatus safetyStatus,
            SafetyCounts safetyCounts,
            FinalExecutionState finalState
        )
        {
            if (!_inProgress)
                throw new InvalidOperationException("Finalize requires an execution in progress; call Begin first.");
            
            for (int index = 0; index < _unitClosures.Count; index++)
            {
                if (_unitClosures[index] == null)
                    throw new InvalidOperationException("Finalize requires every unit to be closed; a unit remains open.");
            }

            int unitCount = _unitOpenings.Count;
            int eventCount = _events.Count;

            int[] childCounts = new int[unitCount];
            int[] descendantCounts = new int[unitCount];
            for (int e = 0; e < eventCount; e++)
            {
                RuntimeUnitID? containing = _events[e].Evidence.ContainingUnit;
                if (containing != null)
                    childCounts[containing.Value.Value - 1]++;
            }
            for (int u = 0; u < unitCount; u++)
            {
                RuntimeUnitID? parent = _unitOpenings[u].ParentUnit;
                if (parent != null)
                    descendantCounts[parent.Value.Value - 1]++;
            }

            TraceEventID[][] childEvents = new TraceEventID[unitCount][];
            RuntimeUnitID[][] descendantUnits = new RuntimeUnitID[unitCount][];
            for (int u = 0; u < unitCount; u++)
            {
                childEvents[u] = new TraceEventID[childCounts[u]];
                descendantUnits[u] = new RuntimeUnitID[descendantCounts[u]];
            }

            int[] childFill = new int[unitCount];
            int[] descendantFill = new int[unitCount];
            for (int e = 0; e < eventCount; e++)
            {
                RuntimeUnitID? containing = _events[e].Evidence.ContainingUnit;
                if (containing != null)
                {
                    int index = containing.Value.Value - 1;
                    childEvents[index][childFill[index]++] = _events[e].Identity;
                }
            }
            
            for (int u = 0; u < unitCount; u++)
            {
                RuntimeUnitID? parent = _unitOpenings[u].ParentUnit;
                if (parent != null)
                {
                    int index = parent.Value.Value - 1;
                    descendantUnits[index][descendantFill[index]++] = new RuntimeUnitID(u + 1);
                }
            }

            RuntimeUnitRecord[] units = new RuntimeUnitRecord[unitCount];
            for (int u = 0; u < unitCount; u++)
            {
                units[u] = new RuntimeUnitRecord(
                    new RuntimeUnitID(u + 1),
                    _unitOpenings[u],
                    _unitClosures[u],
                    childEvents[u],
                    descendantUnits[u],
                    _startOrders[u],
                    _completionOrders[u]);
            }

            EventRecord[] events = _events.ToArray();
            EvidenceDefect[] defects = _defects.ToArray();

            int traversalCount = 0;
            for (int u = 0; u < unitCount; u++)
            {
                if (_unitOpenings[u].Activation == ActivationKind.CanonicalTraversal)
                    traversalCount++;
            }
            
            RuntimeUnitID[] traversalOrder = new RuntimeUnitID[traversalCount];
            int traversalFill = 0;
            for (int u = 0; u < unitCount; u++)
            {
                if (_unitOpenings[u].Activation == ActivationKind.CanonicalTraversal)
                    traversalOrder[traversalFill++] = new RuntimeUnitID(u + 1);
            }

            int thresholdCount = 0;
            for (int e = 0; e < eventCount; e++)
            {
                if (string.Equals(_events[e].Evidence.Family, EventFamilies.Threshold, StringComparison.Ordinal))
                    thresholdCount++;
            }
            
            TraceEventID[] thresholdHistory = new TraceEventID[thresholdCount];
            int thresholdFill = 0;
            for (int e = 0; e < eventCount; e++)
            {
                if (string.Equals(_events[e].Evidence.Family, EventFamilies.Threshold, StringComparison.Ordinal))
                    thresholdHistory[thresholdFill++] = _events[e].Identity;
            }

            int counterCount = 0;
            for (int e = 0; e < eventCount; e++)
            {
                if (_events[e].Evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter })
                {
                    counterCount++;
                }
            }
            
            TraceEventID[] counterHistory = new TraceEventID[counterCount];
            int counterFill = 0;
            for (int e = 0; e < eventCount; e++)
            {
                if (_events[e].Evidence.Payload is QuantityChangePayload counterPayload
                    && counterPayload.Category == QuantityCategory.ProcessCounter)
                {
                    counterHistory[counterFill++] = _events[e].Identity;
                }
            }

            ExecutionRecord record = new ExecutionRecord(
                _header,
                units,
                events,
                traversalOrder,
                thresholdHistory,
                counterHistory,
                safetyCounts,
                safetyStatus,
                finalState,
                defects,
                completion
            );

            Reset();
            return record;
        }

        /// <summary>
        /// Validates that a referenced unit was minted and is still open.
        /// </summary>
        /// <param name="unit">The referenced unit identity.</param>
        /// <param name="parameterName">The parameter name reported on failure.</param>
        private void RequireOpenUnit(RuntimeUnitID unit, string parameterName)
        {
            int index = unit.Value - 1;
            if (index < 0 || index >= _unitOpenings.Count)
                throw new ArgumentException("The referenced unit was never minted.", parameterName);
            if (_unitClosures[index] != null)
                throw new ArgumentException("The referenced unit is already closed.", parameterName);
        }

        /// <summary>
        /// Validates that a referenced unit was minted, whether or not it is still open.
        /// </summary>
        /// <param name="unit">The referenced unit identity.</param>
        /// <param name="parameterName">The parameter name reported on failure.</param>
        private void RequireMintedUnit(RuntimeUnitID unit, string parameterName)
        {
            int index = unit.Value - 1;
            if (index < 0 || index >= _unitOpenings.Count)
                throw new ArgumentException("The referenced unit was never minted.", parameterName);
        }

        /// <summary>
        /// Validates that a referenced event was minted.
        /// </summary>
        /// <param name="eventIdentity">The referenced event identity.</param>
        /// <param name="parameterName">The parameter name reported on failure.</param>
        private void RequireMintedEvent(TraceEventID eventIdentity, string parameterName)
        {
            if (eventIdentity.Value < 1 || eventIdentity.Value > _events.Count)
                throw new ArgumentException("The referenced event was never minted.", parameterName);
        }

        /// <summary>
        /// Clears every backing list, retaining capacity, and resets counters so the builder is
        /// immediately reusable.
        /// </summary>
        private void Reset()
        {
            _events.Clear();
            _unitOpenings.Clear();
            _unitClosures.Clear();
            _startOrders.Clear();
            _completionOrders.Clear();
            _defects.Clear();
            _header = null;
            _inProgress = false;
            _completionCounter = 0;
        }
    }
}