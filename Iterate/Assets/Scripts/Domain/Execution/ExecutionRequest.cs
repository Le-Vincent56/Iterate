using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The immutable, fully-validated request the Execution Engine consumes: the locked compiled source,
    /// the Process execution configuration, the reproduction revision stamps, the initial register
    /// state, and the installed Dependency instances. Construction enforces the content contract —
    /// every slot kind the arrangement can carry is executable, and every installed Dependency's
    /// EXECUTION effects must be interpretable — so unsupported content fails at the boundary rather
    /// than mis-executing, and interpretation runs exactly once.
    /// </summary>
    /// <param name="Source">The locked compiled source; non-null.</param>
    /// <param name="Configuration">The Process execution configuration; non-null.</param>
    /// <param name="RevisionStamps">The reproduction revision stamps; non-null and non-empty.</param>
    /// <param name="InitialState">The register state before the neutral reset; non-null.</param>
    /// <param name="InstalledDependencies">The installed Dependency instances; non-null, empty legal.</param>
    public sealed record ExecutionRequest(
        CompiledSource Source,
        ProcessExecutionConfiguration Configuration,
        IReadOnlyList<RevisionStamp> RevisionStamps,
        InitialExecutionState InitialState,
        IReadOnlyList<DependencyInstance> InstalledDependencies
    )
    {
        /// <summary>
        /// The locked compiled source. Validated non-null at construction; all six slot kinds are
        /// executable.
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
        /// The installed Dependency instances. Validated non-null at construction; empty is legal.
        /// </summary>
        public IReadOnlyList<DependencyInstance> InstalledDependencies { get; } = RequireInstalledDependencies(InstalledDependencies);

        /// <summary>
        /// The interpreted EXECUTION effects of every installed Dependency, computed once at
        /// construction so request validation and engine registration share one result. An
        /// uninterpretable installed Dependency fails the request here.
        /// </summary>
        public IReadOnlyList<ActiveEffect> InterpretedEffects { get; } = InterpretInstalled(InstalledDependencies);

        /// <summary>
        /// Validates that the source is present. The arrangement's own construction rules already
        /// guarantee footprint contiguity, containment ownership, and no nesting, and every slot kind
        /// is executable, so no per-slot check remains.
        /// </summary>
        /// <param name="source">The candidate compiled source.</param>
        /// <returns>The source unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the source is null.</exception>
        private static CompiledSource RequireExecutableSource(CompiledSource source)
        {
            if (source == null)
                throw new ArgumentException("An ExecutionRequest requires a compiled source.", nameof(source));

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

        /// <summary>
        /// Validates that the installed-Dependency list is present.
        /// </summary>
        /// <param name="installedDependencies">The candidate installed list.</param>
        /// <returns>The list unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the list is null.</exception>
        private static IReadOnlyList<DependencyInstance> RequireInstalledDependencies(IReadOnlyList<DependencyInstance> installedDependencies)
        {
            if (installedDependencies == null)
                throw new ArgumentException("An ExecutionRequest requires an installed-Dependency list.", nameof(installedDependencies));

            return installedDependencies;
        }

        /// <summary>
        /// Interprets every installed Dependency's EXECUTION effects in installation order, rejecting
        /// the request before any state is touched when one is uninterpretable.
        /// </summary>
        /// <param name="installedDependencies">The installed Dependency instances.</param>
        /// <returns>The interpreted effects; empty when nothing is installed.</returns>
        /// <exception cref="ArgumentException">Thrown when the list is null or a Dependency is uninterpretable.</exception>
        private static IReadOnlyList<ActiveEffect> InterpretInstalled(IReadOnlyList<DependencyInstance> installedDependencies)
        {
            RequireInstalledDependencies(installedDependencies);

            List<ActiveEffect> effects = new List<ActiveEffect>();
            for (int i = 0; i < installedDependencies.Count; i++)
            {
                IReadOnlyList<ActiveEffect> interpreted = EffectInterpreter.Interpret(installedDependencies[i]);
                for (int j = 0; j < interpreted.Count; j++)
                {
                    effects.Add(interpreted[j]);
                }
            }

            return effects;
        }
    }
}