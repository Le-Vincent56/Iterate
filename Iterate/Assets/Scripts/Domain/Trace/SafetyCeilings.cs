namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The five execution safety ceilings. The builder derives its retained-buffer capacities from these,
    /// and the safety machinery enforces them; they live here because the builder's capacities depend on
    /// them.
    /// </summary>
    public static class SafetyCeilings
    {
        /// <summary>
        /// The maximum added-execution lineage depth.
        /// </summary>
        public const int AddedExecutionLineageDepth = 32;

        /// <summary>
        /// The maximum number of added executions per activation.
        /// </summary>
        public const int AddedExecutionsPerActivation = 1024;

        /// <summary>
        /// The maximum number of source-execution units per execution.
        /// </summary>
        public const int SourceExecutionUnitsPerExecution = 4096;

        /// <summary>
        /// The maximum number of effect reactions per execution.
        /// </summary>
        public const int EffectReactionsPerExecution = 16384;

        /// <summary>
        /// The maximum number of transformations per pending operation.
        /// </summary>
        public const int TransformationsPerPendingOperation = 32;
    }
}