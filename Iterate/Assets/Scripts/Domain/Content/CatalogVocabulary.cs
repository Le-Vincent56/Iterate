namespace Iterate.Domain.Content
{
    /// <summary>
    /// The controlled string registries the effect schema references: each set is bound verbatim from
    /// its governing canon span and is the single authority both the catalog validator (membership
    /// checks) and future per-domain evaluators read. Small fixed sets are enums elsewhere; these are
    /// the deep or content-specific registries kept as validated strings.
    /// </summary>
    public static class CatalogVocabulary
    {
        /// <summary>
        /// The controlled event-subtype tokens the slice's triggers observe.
        /// </summary>
        public static readonly ControlledVocabulary EventSubtypes = new(
            "COMPILATION_COMMITTED",
            "COMPILATION_FAILED",
            "EFFECT_ACTIVATED",
            "EFFECT_EXPIRED",
            "EXECUTION_STARTED",
            "EXECUTION_COMPLETED",
            "RUNTIME_UNIT_STARTED",
            "RUNTIME_UNIT_COMPLETED",
            "SOURCE_EXECUTION_STARTED",
            "SOURCE_EXECUTION_COMPLETED",
            "SOURCE_EXECUTION_SKIPPED",
            "PRIMARY_OPERATION_PENDING",
            "PRIMARY_OPERATION_RESOLVED",
            "QUANTITY_CHANGED",
            "CONDITION_TRUE",
            "OBJECT_ARCHIVED",
            "BOUNDARY_EFFECT_REQUESTED",
            "ADDED_EXECUTION_REQUESTED"
        );

        /// <summary>
        /// The controlled causal timing bands.
        /// </summary>
        public static readonly ControlledVocabulary TimingBands = new(
            "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION",
            "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION",
            "PRIMARY_OPERATION_RESOLUTION",
            "IMMEDIATE_RESULT_REACTION",
            "ADDED_EXECUTION_HANDLING",
            "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"
        );

        /// <summary>
        /// The controlled named scheduling boundaries.
        /// </summary>
        public static readonly ControlledVocabulary TimingBoundaries = new(
            "END_OF_REPEAT_ITERATION",
            "END_OF_STRUCTURE_ENTRY",
            "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
            "BEFORE_DESIGNATED_CORE_OUTPUT",
            "END_OF_COMPLETE_SOURCE_TRAVERSAL",
            "EXECUTION_COMPLETION_CLOSURE"
        );

        /// <summary>
        /// The controlled trigger-qualifier kinds the slice uses.
        /// </summary>
        public static readonly ControlledVocabulary QualifierKinds = new(
            "POSITIONAL",
            "ACTUAL_DELTA_SIGN",
            "REGISTER",
            "OPERATION_CLASS",
            "STRUCTURE_CONTEXT",
            "PARITY"
        );

        /// <summary>
        /// The controlled frequency forms.
        /// </summary>
        public static readonly ControlledVocabulary FrequencyAllowances = new(
            "EVERY_QUALIFYING_EVENT",
            "FIRST_QUALIFYING_EVENT",
            "FIRST_SUCCESSFUL_RESOLUTION",
            "ONCE",
            "UP_TO_N"
        );

        /// <summary>
        /// The controlled frequency reset scopes.
        /// </summary>
        public static readonly ControlledVocabulary FrequencyScopes = new(
            "SOURCE_ACTIVATION",
            "SOURCE_EXECUTION",
            "LINE_PER_EXECUTION",
            "STRUCTURE_ENTRY",
            "REPEAT_ITERATION",
            "CONDITION_EVALUATION",
            "EXECUTION",
            "COMPILATION",
            "PROCESS",
            "SYSTEM",
            "SESSION",
            "DECLARED_SCOPE"
        );

        /// <summary>
        /// The controlled targeting kinds the slice uses.
        /// </summary>
        public static readonly ControlledVocabulary TargetingKinds = new(
            "NO_TARGET",
            "OWN_HOST",
            "TRIGGERING_UNIT",
            "SAME_REGISTER_AS_TRIGGER",
            "LOCKED_TARGET",
            "FIRST_CONTAINED_INSTRUCTION",
            "MOST_RECENT_QUALIFYING_UNIT"
        );

        /// <summary>
        /// The controlled disposition-outcome names.
        /// </summary>
        public static readonly ControlledVocabulary DispositionNames = new(
            "RESOLVED",
            "SKIPPED",
            "PREVENTED",
            "CANCELLED",
            "RESCUED",
            "REPLACED"
        );

        /// <summary>
        /// The controlled counter names.
        /// </summary>
        public static readonly ControlledVocabulary CounterNames = new("HEAT");

        /// <summary>
        /// The controlled cost kinds.
        /// </summary>
        public static readonly ControlledVocabulary CostKinds = new("COMPILATION");

        /// <summary>
        /// The controlled Process-setup configuration settings.
        /// </summary>
        public static readonly ControlledVocabulary ConfigurationSettings = new(
            "STARTING_BYTES",
            "INSTRUCTION_BUFFER_CAPACITY",
            "ACTIVE_BRANCH_CAPACITY"
        );

        /// <summary>
        /// The controlled prediction projections.
        /// </summary>
        public static readonly ControlledVocabulary PredictionProjections = new("CANONICAL_PREDICTION");
    }
}