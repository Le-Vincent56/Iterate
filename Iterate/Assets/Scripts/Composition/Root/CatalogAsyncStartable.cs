using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Content;
using Iterate.Application.Logging;
using Iterate.Domain.Content;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// The boot-time async adapter: loads and freezes the catalog and publishes the outcome to the
    /// holder. Registered under IAsyncStartable so Lattice's async-startable runner invokes it. Every
    /// await uses ConfigureAwait(false) — the holder and the log sink are thread-safe, so the load has
    /// no main-thread affinity. Self-handles every fault so the runner's catch-all is reserved for
    /// faults outside this adapter.
    /// </summary>
    public sealed class CatalogAsyncStartable : IAsyncStartable
    {
        private readonly CatalogLoader _loader;

        private readonly CatalogHolder _holder;

        private readonly IGameLoggerFactory _loggers;

        public CatalogAsyncStartable(CatalogLoader loader, CatalogHolder holder, IGameLoggerFactory loggers)
        {
            _loader = loader;
            _holder = holder;
            _loggers = loggers;
        }

        /// <summary>
        /// Loads the catalog, marking the holder Loaded on success or Failed on any defect, and logging
        /// the outcome through the Catalog category.
        /// </summary>
        /// <param name="cancellationToken">The lifetime-scoped cancellation token.</param>
        /// <returns>The completed start task.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            IGameLogger log = _loggers.Create(LogCategories.Catalog);
            try
            {
                ContentCatalog catalog = await _loader.LoadAsync(cancellationToken).ConfigureAwait(false);
                _holder.MarkLoaded(catalog);
                log.Info(
                    "Catalog loaded",
                    LogField.Of("revision", catalog.Revision),
                    LogField.Of("definitions", catalog.DefinitionCount)
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CatalogLoadException exception)
            {
                _holder.MarkFailed(exception.Errors);
                log.Error(DescribeFailure(exception.Errors), exception);
            }
            catch (Exception exception)
            {
                _holder.MarkFailed(new[] { new CatalogError("catalog", "$", "boot.unhandled", exception.Message) });
                log.Error("Catalog load faulted unexpectedly.", exception);
            }
        }

        /// <summary>
        /// Summarizes a load failure by naming the first error's file and rule.
        /// </summary>
        /// <param name="errors">The load error list.</param>
        /// <returns>The summary message.</returns>
        private static string DescribeFailure(IReadOnlyList<CatalogError> errors)
        {
            if (errors == null || errors.Count == 0)
                return "Catalog load failed.";

            CatalogError first = errors[0];
            return "Catalog load failed | file=" + first.File + " | rule=" + first.RuleName;
        }
    }
}