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
    }
}