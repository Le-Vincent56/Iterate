using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Content;
using Iterate.Application.Logging;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// The fault-contained async-boot runner: awaits the resolver's async-startable phase and
    /// guarantees the catalog holder reaches a terminal state on every exit path. Cancellation is
    /// swallowed; any other escape logs one Error through the seam and drives the holder to Failed via
    /// TryMarkFailed, which is race-safe against the catalog loader's own terminal mark. Extracted from
    /// the scope so the stranded-Unloaded regression can drive it against a headless container.
    /// </summary>
    public static class CatalogBootRunner
    {
        /// <summary>
        /// Awaits the async-startable phase and contains any fault into a holder transition.
        /// </summary>
        /// <param name="resolver">The root resolver whose async startables run.</param>
        /// <param name="cancellationToken">The lifetime-scoped cancellation token.</param>
        /// <returns>The completed boot-phase task.</returns>
        public static async Task RunAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            try
            {
                await resolver.RunAsyncStartablesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                CatalogHolder holder = resolver.Resolve<CatalogHolder>();
                IGameLoggerFactory loggers = resolver.Resolve<IGameLoggerFactory>();
                IGameLogger log = loggers.Create(LogCategories.Catalog);
                log.Error("Async boot phase faulted outside the catalog loader.", exception);
                holder.TryMarkFailed(BootFaultErrors(exception));
            }
        }

        /// <summary>
        /// Builds the single boot-runner-fault error for a fault escaping the async-startable phase.
        /// </summary>
        /// <param name="exception">The escaping fault.</param>
        /// <returns>The single-error list.</returns>
        private static IReadOnlyList<CatalogError> BootFaultErrors(Exception exception)
        {
            return new[] { new CatalogError("boot", "$", "boot.runner-fault", exception.Message) };
        }
    }
}