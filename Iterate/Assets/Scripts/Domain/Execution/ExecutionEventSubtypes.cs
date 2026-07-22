namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The CAB-verbatim event-subtype tokens the scheduler emits, held as constants so evidence pins the
    /// exact canonical strings rather than deriving them from enum names.
    /// </summary>
    public static class ExecutionEventSubtypes
    {
        /// <summary>
        /// The lifecycle subtype marking execution scope establishment.
        /// </summary>
        public const string ExecutionStarted = "EXECUTION_STARTED";

        /// <summary>
        /// The lifecycle subtype marking execution-completion closure.
        /// </summary>
        public const string ExecutionCompleted = "EXECUTION_COMPLETED";

        /// <summary>
        /// The quantity subtype marking a neutral runtime reset to zero.
        /// </summary>
        public const string QuantityReset = "QUANTITY_RESET";

        /// <summary>
        /// The quantity subtype marking an absolute assignment.
        /// </summary>
        public const string QuantityAssigned = "QUANTITY_ASSIGNED";

        /// <summary>
        /// The quantity subtype marking a relative change.
        /// </summary>
        public const string QuantityChanged = "QUANTITY_CHANGED";

        /// <summary>
        /// The threshold subtype marking an upward band crossing.
        /// </summary>
        public const string ThresholdCrossedUpward = "THRESHOLD_CROSSED_UPWARD";

        /// <summary>
        /// The threshold subtype marking a downward band crossing.
        /// </summary>
        public const string ThresholdCrossedDownward = "THRESHOLD_CROSSED_DOWNWARD";

        /// <summary>
        /// The source subtype marking a source object entering one distinct execution attempt.
        /// </summary>
        public const string SourceExecutionStarted = "SOURCE_EXECUTION_STARTED";

        /// <summary>
        /// The source subtype marking a source-execution attempt and its runtime-unit closure completing.
        /// </summary>
        public const string SourceExecutionCompleted = "SOURCE_EXECUTION_COMPLETED";

        /// <summary>
        /// The operation subtype identifying the operation a source execution is prepared to attempt.
        /// </summary>
        public const string PrimaryOperationPending = "PRIMARY_OPERATION_PENDING";

        /// <summary>
        /// The operation subtype marking a transformation applied to the pending primary operation.
        /// </summary>
        public const string PrimaryOperationModified = "PRIMARY_OPERATION_MODIFIED";

        /// <summary>
        /// The operation subtype identifying an operation permitted to perform its approved calculation.
        /// </summary>
        public const string PrimaryOperationResolved = "PRIMARY_OPERATION_RESOLVED";

        /// <summary>
        /// The operation subtype marking the primary operation's result becoming final.
        /// </summary>
        public const string PrimaryOperationResultFinalized = "PRIMARY_OPERATION_RESULT_FINALIZED";

        /// <summary>
        /// The qualification subtype recording that an effect's declared requirements succeeded.
        /// </summary>
        public const string EffectQualified = "EFFECT_QUALIFIED";

        /// <summary>
        /// The qualification subtype recording that one observing effect did not satisfy its requirements.
        /// </summary>
        public const string EffectFailedToQualify = "EFFECT_FAILED_TO_QUALIFY";

        /// <summary>
        /// The qualification subtype recording an effect committing to resolution under its frequency rule.
        /// </summary>
        public const string EffectCommitted = "EFFECT_COMMITTED";

        /// <summary>
        /// The reaction subtype marking a non-source effect response resolved within the causing runtime
        /// unit's causal closure.
        /// </summary>
        public const string ImmediateReactionResolved = "IMMEDIATE_REACTION_RESOLVED";
    }
}