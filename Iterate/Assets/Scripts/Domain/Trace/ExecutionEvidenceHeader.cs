using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The reproduction context of an execution: the execution, compilation, compiled-source-revision,
    /// Process, Core, Process-rule-configuration, and Session-seed identities; the component revision
    /// stamps; the active Directive, installed Dependency, and relevant Patch instance lists; and the
    /// initial register state. Every identity is required, and at least one revision stamp is required.
    /// Equality is structural over the four lists.
    /// </summary>
    public sealed record ExecutionEvidenceHeader
    {
        /// <summary>
        /// The identity of this execution; never empty.
        /// </summary>
        public string ExecutionIdentity { get; }

        /// <summary>
        /// The identity of the compilation this execution ran; never empty.
        /// </summary>
        public string CompilationIdentity { get; }

        /// <summary>
        /// The revision of the compiled source; never empty.
        /// </summary>
        public string CompiledSourceRevision { get; }

        /// <summary>
        /// The identity of the Process; never empty.
        /// </summary>
        public string ProcessIdentity { get; }

        /// <summary>
        /// The identity of the Core; never empty.
        /// </summary>
        public string CoreIdentity { get; }

        /// <summary>
        /// The identity of the Process rule configuration; never empty.
        /// </summary>
        public string ProcessRuleConfigurationIdentity { get; }

        /// <summary>
        /// The identity of the Session seed; never empty.
        /// </summary>
        public string SessionSeedIdentity { get; }

        /// <summary>
        /// The component revision stamps; never null, never empty.
        /// </summary>
        public IReadOnlyList<RevisionStamp> RevisionStamps { get; }

        /// <summary>
        /// The active Directive instances; never null, possibly empty.
        /// </summary>
        public IReadOnlyList<InstanceID> ActiveDirectiveInstances { get; }

        /// <summary>
        /// The installed Dependency instances; never null, possibly empty.
        /// </summary>
        public IReadOnlyList<InstanceID> InstalledDependencyInstances { get; }

        /// <summary>
        /// The relevant Patch instances; never null, possibly empty.
        /// </summary>
        public IReadOnlyList<InstanceID> RelevantPatchInstances { get; }

        /// <summary>
        /// The initial register state; never null.
        /// </summary>
        public InitialExecutionState InitialState { get; }

        public ExecutionEvidenceHeader(
            string executionIdentity,
            string compilationIdentity,
            string compiledSourceRevision,
            string processIdentity,
            string coreIdentity,
            string processRuleConfigurationIdentity,
            string sessionSeedIdentity,
            IReadOnlyList<RevisionStamp> revisionStamps,
            IReadOnlyList<InstanceID> activeDirectiveInstances,
            IReadOnlyList<InstanceID> installedDependencyInstances,
            IReadOnlyList<InstanceID> relevantPatchInstances,
            InitialExecutionState initialState
        )
        {
            if (string.IsNullOrEmpty(executionIdentity))
                throw new ArgumentException("A header requires an execution identity.", nameof(executionIdentity));
            
            if (string.IsNullOrEmpty(compilationIdentity))
                throw new ArgumentException("A header requires a compilation identity.", nameof(compilationIdentity));
            
            if (string.IsNullOrEmpty(compiledSourceRevision))
                throw new ArgumentException("A header requires a compiled-source revision.", nameof(compiledSourceRevision));
            
            if (string.IsNullOrEmpty(processIdentity))
                throw new ArgumentException("A header requires a Process identity.", nameof(processIdentity));
            
            if (string.IsNullOrEmpty(coreIdentity))
                throw new ArgumentException("A header requires a Core identity.", nameof(coreIdentity));
            
            if (string.IsNullOrEmpty(processRuleConfigurationIdentity))
                throw new ArgumentException("A header requires a Process rule-configuration identity.", nameof(processRuleConfigurationIdentity));
            
            if (string.IsNullOrEmpty(sessionSeedIdentity))
                throw new ArgumentException("A header requires a Session seed identity.", nameof(sessionSeedIdentity));
            
            if (revisionStamps == null || revisionStamps.Count == 0)
                throw new ArgumentException("A header requires at least one revision stamp.", nameof(revisionStamps));

            ExecutionIdentity = executionIdentity;
            CompilationIdentity = compilationIdentity;
            CompiledSourceRevision = compiledSourceRevision;
            ProcessIdentity = processIdentity;
            CoreIdentity = coreIdentity;
            ProcessRuleConfigurationIdentity = processRuleConfigurationIdentity;
            SessionSeedIdentity = sessionSeedIdentity;
            RevisionStamps = revisionStamps;
            ActiveDirectiveInstances = activeDirectiveInstances ?? throw new ArgumentException("A header requires an active-Directive list.", nameof(activeDirectiveInstances));
            InstalledDependencyInstances = installedDependencyInstances ?? throw new ArgumentException("A header requires an installed-Dependency list.", nameof(installedDependencyInstances));
            RelevantPatchInstances = relevantPatchInstances ?? throw new ArgumentException("A header requires a relevant-Patch list.", nameof(relevantPatchInstances));
            InitialState = initialState ?? throw new ArgumentException("A header requires an initial state.", nameof(initialState));
        }

        /// <summary>
        /// Structural value equality: every identity and the initial state plus element-wise comparison of
        /// the revision stamps and the three instance lists.
        /// </summary>
        /// <param name="other">The header to compare against.</param>
        /// <returns>True when every field and every list are equal.</returns>
        public bool Equals(ExecutionEvidenceHeader other)
        {
            if (other == null) return false;

            return string.Equals(ExecutionIdentity, other.ExecutionIdentity, StringComparison.Ordinal)
                && string.Equals(CompilationIdentity, other.CompilationIdentity, StringComparison.Ordinal)
                && string.Equals(CompiledSourceRevision, other.CompiledSourceRevision, StringComparison.Ordinal)
                && string.Equals(ProcessIdentity, other.ProcessIdentity, StringComparison.Ordinal)
                && string.Equals(CoreIdentity, other.CoreIdentity, StringComparison.Ordinal)
                && string.Equals(ProcessRuleConfigurationIdentity, other.ProcessRuleConfigurationIdentity, StringComparison.Ordinal)
                && string.Equals(SessionSeedIdentity, other.SessionSeedIdentity, StringComparison.Ordinal)
                && InitialState == other.InitialState
                && TraceEquality.ListEquals(RevisionStamps, other.RevisionStamps)
                && TraceEquality.ListEquals(ActiveDirectiveInstances, other.ActiveDirectiveInstances)
                && TraceEquality.ListEquals(InstalledDependencyInstances, other.InstalledDependencyInstances)
                && TraceEquality.ListEquals(RelevantPatchInstances, other.RelevantPatchInstances);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the execution identity and the four list
        /// counts.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ExecutionIdentity.GetHashCode();
                hash = hash * 31 + RevisionStamps.Count;
                hash = hash * 31 + ActiveDirectiveInstances.Count;
                hash = hash * 31 + InstalledDependencyInstances.Count;
                hash = hash * 31 + RelevantPatchInstances.Count;
                return hash;
            }
        }
    }
}