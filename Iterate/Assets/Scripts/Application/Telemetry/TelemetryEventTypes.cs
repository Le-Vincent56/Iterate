namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// The stable event labels written as each telemetry line's <c>type</c>.
    /// </summary>
    public static class TelemetryEventTypes
    {
        /// <summary>
        /// A run began with its Session.
        /// </summary>
        public const string RunStarted = "run_started";

        /// <summary>
        /// A run ended, with its termination.
        /// </summary>
        public const string RunEnded = "run_ended";

        /// <summary>
        /// A Process was entered, with its Buffer state.
        /// </summary>
        public const string ProcessEntered = "process_entered";

        /// <summary>
        /// A new configuration snapshot.
        /// </summary>
        public const string Configuration = "configuration";

        /// <summary>
        /// One command, accepted or refused.
        /// </summary>
        public const string Command = "command";

        /// <summary>
        /// A compilation attempt, committed or blocked.
        /// </summary>
        public const string Compilation = "compilation";

        /// <summary>
        /// An execution finished, with its output and Condition counts.
        /// </summary>
        public const string ExecutionCompleted = "execution_completed";

        /// <summary>
        /// The Buffer changed after an accepted command.
        /// </summary>
        public const string BufferChanged = "buffer_changed";

        /// <summary>
        /// A Process failed, with its disposition and consecutive failure count.
        /// </summary>
        public const string ProcessFailed = "process_failed";

        /// <summary>
        /// A stage was left, with its dwell.
        /// </summary>
        public const string StageExited = "stage_exited";

        /// <summary>
        /// The trailer an export appends to a run.
        /// </summary>
        public const string Export = "export";

        /// <summary>
        /// The recorder contained a defect and stopped recording the run.
        /// </summary>
        public const string TelemetryFault = "telemetry_fault";
    }
}