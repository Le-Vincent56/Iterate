using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The immutable, fully-validated request the Execution Engine consumes: the locked compiled source,
    /// the Process execution configuration, the reproduction revision stamps, and the initial register
    /// state. Construction enforces the walking-skeleton content contract — the arrangement may contain
    /// only Core, Empty, and Instruction slots, so Structure-bearing content fails at the boundary rather
    /// than mis-executing.
    /// </summary>
    /// <param name="Source">The locked compiled source; non-null, Core/Empty/Instruction slots only.</param>
    /// <param name="Configuration">The Process execution configuration; non-null.</param>
    /// <param name="RevisionStamps">The reproduction revision stamps; non-null and non-empty.</param>
    /// <param name="InitialState">The register state before the neutral reset; non-null.</param>
    public sealed record ExecutionRequest(
        CompiledSource Source,
        ProcessExecutionConfiguration Configuration,
        IReadOnlyList<RevisionStamp> RevisionStamps,
        InitialExecutionState InitialState
    )
    {
        /// <summary>
        /// The locked compiled source. Validated non-null and content-contract-compliant at construction.
        /// </summary>
        public CompiledSource Source { get; } = RequireExecutableSource(Source);

        /// <summary>
        /// The Process execution configuration. Validated non-null at construction.
        /// </summary>
        public ProcessExecutionConfiguration Configuration { get; } = RequireConfiguration(Configuration);

        /// <summary>
        /// The reproduction revision stamps. Validated non-null and non-empty at construction.
        /// </summary>
        public IReadOnlyList<RevisionStamp> RevisionStamps { get; } = RequireStamps(RevisionStamps);

        /// <summary>
        /// The register state before the neutral reset. Validated non-null at construction.
        /// </summary>
        public InitialExecutionState InitialState { get; } = RequireInitialState(InitialState);

        /// <summary>
        /// Validates that the source is present and its arrangement carries only Core, Empty, and
        /// Instruction slots.
        /// </summary>
        /// <param name="source">The candidate compiled source.</param>
        /// <returns>The source unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the source is null or carries an unsupported slot.</exception>
        private static CompiledSource RequireExecutableSource(CompiledSource source)
        {
            if (source == null)
                throw new ArgumentException("An ExecutionRequest requires a compiled source.", nameof(source));

            IReadOnlyList<SourceSlot> slots = source.Arrangement.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlotKind kind = slots[i].Kind;
                if (kind != SourceSlotKind.Core && kind != SourceSlotKind.Empty && kind != SourceSlotKind.Instruction)
                    throw new ArgumentException("An ExecutionRequest arrangement may contain only Core, Empty, and Instruction slots.", nameof(source));
            }

            return source;
        }

        /// <summary>
        /// Validates that the configuration is present.
        /// </summary>
        /// <param name="configuration">The candidate configuration.</param>
        /// <returns>The configuration unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the configuration is null.</exception>
        private static ProcessExecutionConfiguration RequireConfiguration(ProcessExecutionConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentException("An ExecutionRequest requires a configuration.", nameof(configuration));

            return configuration;
        }

        /// <summary>
        /// Validates that the revision stamps are present and non-empty.
        /// </summary>
        /// <param name="stamps">The candidate revision stamps.</param>
        /// <returns>The stamps unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the stamps are null or empty.</exception>
        private static IReadOnlyList<RevisionStamp> RequireStamps(IReadOnlyList<RevisionStamp> stamps)
        {
            if (stamps == null || stamps.Count == 0)
                throw new ArgumentException("An ExecutionRequest requires at least one revision stamp.", nameof(stamps));

            return stamps;
        }

        /// <summary>
        /// Validates that the initial state is present.
        /// </summary>
        /// <param name="initialState">The candidate initial state.</param>
        /// <returns>The initial state unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the initial state is null.</exception>
        private static InitialExecutionState RequireInitialState(InitialExecutionState initialState)
        {
            if (initialState == null)
                throw new ArgumentException("An ExecutionRequest requires an initial state.", nameof(initialState));

            return initialState;
        }
    }
}