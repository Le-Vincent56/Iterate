using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Execution;
using Iterate.Domain.Progression;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Builds the engine request for one execution from the Process and Session state. It reads no
    /// ledger and spends nothing, so it can run before the compilation cost is paid. A Process rule
    /// reaches the engine only when it declares an execution-domain effect, because the engine reads a
    /// rule's presence as evidence that a Process counter exists.
    /// </summary>
    internal static class ExecutionRequestFactory
    {
        private const string ContentCatalogStamp = "Content Catalog";
        private const string RandomServiceStamp = "Random Service";
        private const string NoRuleConfiguration = "none";

        /// <summary>
        /// Builds the request for the Process's next execution.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="source">The committed compiled source.</param>
        /// <returns>The execution request.</returns>
        internal static ExecutionRequest Build(SessionContext context, CompiledSource source)
        {
            ProcessState process = context.Process;
            SessionState session = context.Session;
            ContentCatalog catalog = context.Catalog;
            ProcessConfigurationDefinition configuration = process.Configuration;
            ProcessRuleInstance rule = DeclaresExecutionEffect(process.ProcessRule) ? process.ProcessRule : null;
            string ordinal = (process.Counters.ExecutionsRun + 1).ToString(CultureInfo.InvariantCulture);
            ProcessThresholdSpec thresholds = configuration.Thresholds;

            ProcessExecutionConfiguration executionConfiguration = new(
                $"{configuration.ID.Value}:execution:{ordinal}",
                $"{configuration.ID.Value}:compilation:{ordinal}",
                catalog.Revision,
                configuration.ID.Value,
                configuration.Core.Value,
                rule == null ? NoRuleConfiguration : rule.Definition.ID.Value,
                session.SessionSeedIdentity,
                new ProcessThresholds(
                    new ScoreValue(thresholds.Pass),
                    new ScoreValue(thresholds.Optimize),
                    new ScoreValue(thresholds.Benchmark)
                ),
                rule,
                null
            );

            List<RevisionStamp> stamps = new()
            {
                new RevisionStamp(ContentCatalogStamp, catalog.Revision),
                new RevisionStamp(RandomServiceStamp, DeterminismService.RevisionIdentity)
            };

            List<DependencyInstance> installed = new(session.Economy.Dependencies.AllInstalled);

            return new ExecutionRequest(
                source,
                executionConfiguration,
                stamps,
                new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0)),
                installed
            );
        }

        /// <summary>
        /// Whether a Process rule declares an execution-domain effect.
        /// </summary>
        /// <param name="rule">The rule, or null.</param>
        /// <returns>True when the rule declares one.</returns>
        internal static bool DeclaresExecutionEffect(ProcessRuleInstance rule)
        {
            if (rule == null)
                return false;

            IReadOnlyList<EffectDefinition> effects = rule.Definition.Effects;
            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index].PhaseDomain == PhaseDomain.Execution)
                    return true;
            }

            return false;
        }
    }
}