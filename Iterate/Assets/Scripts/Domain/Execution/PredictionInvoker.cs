using System;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The prediction-mode invocation seam: runs the same engine through the same nine
    /// phases on a dedicated trace builder and scheduler the live Process scope never sees, and returns
    /// the frozen record for the caller to inspect and discard. The caller owns building the request
    /// from value-copied live state, which is the same TA-RUN-003 rule every caller already obeys, and
    /// owns discarding the result: nothing here applies outcomes, because the application step lives in
    /// Session Flow and prediction is defined by omitting it.
    /// The result is discard-eligible by contract. Prediction never consumes or advances an actual
    /// random-decision context; at current content the engine wires no
    /// randomness at all, so that obligation is structurally trivial rather than enforced — which is
    /// precisely why this seam exists as a named type. <b>Forward obligation:</b> when the deterministic
    /// random service is wired into the engine, the copied decision-context basis is taken <i>here</i>,
    /// in this class, and nowhere else. A future child that wires randomness without routing prediction
    /// through this seam violates CAB-EVT-900 silently, and this comment is the guard against that.
    /// Disclosure of a predicted result stays separately governed and is
    /// presentation-side; it is deliberately not represented in this engine seam.
    /// The owned builder-and-scheduler pair is deliberate, and deliberately asymmetric with the
    /// stateless replay comparer: prediction may one day run at interaction cadence, where the builder's
    /// buffer reuse across executions is the point. The honest consequence is that a defect escaping the
    /// engine poisons this instance exactly as it poisons the live Process-scoped
    /// builder — the engine-wide posture stated plainly, not a new rule and not a suppression.
    /// </summary>
    public sealed class PredictionInvoker
    {
        private readonly ExecutionTraceBuilder _builder;
        private readonly ExecutionScheduler _scheduler;

        public PredictionInvoker()
        {
            _builder = new ExecutionTraceBuilder();
            _scheduler = new ExecutionScheduler(_builder);
        }

        /// <summary>
        /// Runs the request through the full engine on the owned builder and returns the frozen record.
        /// </summary>
        /// <param name="request">The request, built by the caller from value-copied live state.</param>
        /// <returns>The frozen execution record, which the caller inspects and discards.</returns>
        /// <exception cref="ArgumentException">Thrown when the request is null.</exception>
        public ExecutionRecord Predict(ExecutionRequest request)
        {
            if (request == null)
                throw new ArgumentException("Prediction requires a request.", nameof(request));

            return _scheduler.Execute(request);
        }
    }
}