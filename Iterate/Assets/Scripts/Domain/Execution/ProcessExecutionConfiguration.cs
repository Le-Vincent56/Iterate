using System;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The fixture-shaped Process execution configuration: the seven header identities that stamp an
    /// execution's evidence header and the Score-band thresholds it is judged against. All seven
    /// identities are required non-empty and the thresholds record non-null at construction.
    /// </summary>
    /// <param name="ExecutionIdentity">The execution identity.</param>
    /// <param name="CompilationIdentity">The compilation identity.</param>
    /// <param name="CompiledSourceRevision">The compiled-source revision identity.</param>
    /// <param name="ProcessIdentity">The Process identity.</param>
    /// <param name="CoreIdentity">The Core identity.</param>
    /// <param name="ProcessRuleConfigurationIdentity">The Process-rule-configuration identity.</param>
    /// <param name="SessionSeedIdentity">The Session-seed identity.</param>
    /// <param name="Thresholds">The Score-band thresholds; non-null.</param>
    public sealed record ProcessExecutionConfiguration(
        string ExecutionIdentity,
        string CompilationIdentity,
        string CompiledSourceRevision,
        string ProcessIdentity,
        string CoreIdentity,
        string ProcessRuleConfigurationIdentity,
        string SessionSeedIdentity,
        ProcessThresholds Thresholds
    )
    {
        /// <summary>
        /// The execution identity. Validated non-empty at construction.
        /// </summary>
        public string ExecutionIdentity { get; } = RequireText(ExecutionIdentity, nameof(ExecutionIdentity));

        /// <summary>
        /// The compilation identity. Validated non-empty at construction.
        /// </summary>
        public string CompilationIdentity { get; } = RequireText(CompilationIdentity, nameof(CompilationIdentity));

        /// <summary>
        /// The compiled-source revision identity. Validated non-empty at construction.
        /// </summary>
        public string CompiledSourceRevision { get; } = RequireText(CompiledSourceRevision, nameof(CompiledSourceRevision));

        /// <summary>
        /// The Process identity. Validated non-empty at construction.
        /// </summary>
        public string ProcessIdentity { get; } = RequireText(ProcessIdentity, nameof(ProcessIdentity));

        /// <summary>
        /// The Core identity. Validated non-empty at construction.
        /// </summary>
        public string CoreIdentity { get; } = RequireText(CoreIdentity, nameof(CoreIdentity));

        /// <summary>
        /// The Process-rule-configuration identity. Validated non-empty at construction.
        /// </summary>
        public string ProcessRuleConfigurationIdentity { get; } = RequireText(ProcessRuleConfigurationIdentity, nameof(ProcessRuleConfigurationIdentity));

        /// <summary>
        /// The Session-seed identity. Validated non-empty at construction.
        /// </summary>
        public string SessionSeedIdentity { get; } = RequireText(SessionSeedIdentity, nameof(SessionSeedIdentity));

        /// <summary>
        /// The Score-band thresholds. Validated non-null at construction.
        /// </summary>
        public ProcessThresholds Thresholds { get; } = RequireThresholds(Thresholds);

        /// <summary>
        /// Validates that a supplied identity is present and non-empty.
        /// </summary>
        /// <param name="value">The candidate identity.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The identity unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the identity is null or empty.</exception>
        private static string RequireText(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("A ProcessExecutionConfiguration requires a non-empty identity.", fieldName);

            return value;
        }

        /// <summary>
        /// Validates that the thresholds record is present.
        /// </summary>
        /// <param name="thresholds">The candidate thresholds.</param>
        /// <returns>The thresholds unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the thresholds record is null.</exception>
        private static ProcessThresholds RequireThresholds(ProcessThresholds thresholds)
        {
            if (thresholds == null)
                throw new ArgumentException("A ProcessExecutionConfiguration requires thresholds.", nameof(thresholds));

            return thresholds;
        }
    }
}