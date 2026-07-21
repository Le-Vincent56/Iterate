using Iterate.Domain.Content;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The controlled event-family registry: the fifteen canonical family tokens, verbatim and
    /// case-sensitive, each exposed as a named constant and collected into one controlled
    /// vocabulary. The registry is governance-extensible, so families are validated strings rather
    /// than an enum; free-form display text never substitutes for this classification.
    /// </summary>
    public static class EventFamilies
    {
        /// <summary>
        /// Compilation, execution, runtime-unit, and handoff boundaries.
        /// </summary>
        public const string Lifecycle = "LIFECYCLE";

        /// <summary>
        /// Source activation, source execution, and traversal-related occurrences.
        /// </summary>
        public const string Source = "SOURCE";

        /// <summary>
        /// Pending, modified, resolved, and finalized primary operations.
        /// </summary>
        public const string Operation = "OPERATION";

        /// <summary>
        /// Candidate evaluation and qualification result.
        /// </summary>
        public const string Qualification = "QUALIFICATION";

        /// <summary>
        /// Variables, resources, counters, capacities, and their changes.
        /// </summary>
        public const string Quantity = "QUANTITY";

        /// <summary>
        /// Structure entry, iteration, predicate, child governance, and exit.
        /// </summary>
        public const string Structure = "STRUCTURE";

        /// <summary>
        /// Skip, prevention, cancellation, rescue, replacement, and related outcomes.
        /// </summary>
        public const string Disposition = "DISPOSITION";

        /// <summary>
        /// Immediate passive reactions and other non-source effect responses.
        /// </summary>
        public const string Reaction = "REACTION";

        /// <summary>
        /// Added-execution request, scheduling, cancellation, and resolution.
        /// </summary>
        public const string AddedExecution = "ADDED_EXECUTION";

        /// <summary>
        /// Directive, Dependency, Patch, Core-rule, and Process-rule intervention.
        /// </summary>
        public const string Intervention = "INTERVENTION";

        /// <summary>
        /// Threshold evaluation, crossing, and status consequence.
        /// </summary>
        public const string Threshold = "THRESHOLD";

        /// <summary>
        /// Atomic or governed Build, acquisition, and lifecycle operations.
        /// </summary>
        public const string Transaction = "TRANSACTION";

        /// <summary>
        /// Install, remove, archive, consume, destroy, delete, and unwrap outcomes.
        /// </summary>
        public const string ContentLifecycle = "CONTENT_LIFECYCLE";

        /// <summary>
        /// Deterministic random-decision and selection evidence.
        /// </summary>
        public const string RandomSelection = "RANDOM_SELECTION";

        /// <summary>
        /// Cycle, cap, termination, and safety-abort intervention.
        /// </summary>
        public const string Safety = "SAFETY";

        /// <summary>
        /// The complete controlled registry as one case-sensitive vocabulary.
        /// </summary>
        public static ControlledVocabulary All { get; } = new ControlledVocabulary(
            Lifecycle,
            Source,
            Operation,
            Qualification,
            Quantity,
            Structure,
            Disposition,
            Reaction,
            AddedExecution,
            Intervention,
            Threshold,
            Transaction,
            ContentLifecycle,
            RandomSelection,
            Safety
        );
    }
}