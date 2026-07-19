using System;
using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The thread-safe holder for the load outcome: an explicit <c>Unloaded → Loaded | Failed</c> state
    /// machine behind a single lock. The throwing marks make a double transition a surfaced programming
    /// error; <see cref="TryMarkFailed"/> is the race-safe fallback the boot runner uses so a lost race
    /// with the loader's own mark cannot throw.
    /// </summary>
    public sealed class CatalogHolder
    {
        private readonly object _gate = new();

        private CatalogState _state = CatalogState.Unloaded;

        private ContentCatalog _catalog;

        private IReadOnlyList<CatalogError> _errors = Array.Empty<CatalogError>();

        /// <summary>
        /// The current lifecycle state.
        /// </summary>
        public CatalogState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// The failure list; empty unless the state is <see cref="CatalogState.Failed"/>.
        /// </summary>
        public IReadOnlyList<CatalogError> Errors
        {
            get
            {
                lock (_gate)
                {
                    return _errors;
                }
            }
        }

        /// <summary>
        /// Gets the loaded catalog when the state is <see cref="CatalogState.Loaded"/>.
        /// </summary>
        /// <param name="catalog">The loaded catalog, or null when not loaded.</param>
        /// <returns>True when a loaded catalog is available.</returns>
        public bool TryGetCatalog(out ContentCatalog catalog)
        {
            lock (_gate)
            {
                catalog = _catalog;
                return _state == CatalogState.Loaded;
            }
        }

        /// <summary>
        /// Transitions from Unloaded to Loaded, publishing the catalog.
        /// </summary>
        /// <param name="catalog">The frozen catalog.</param>
        /// <exception cref="InvalidOperationException">Thrown when already terminal.</exception>
        public void MarkLoaded(ContentCatalog catalog)
        {
            lock (_gate)
            {
                RequireUnloaded();
                _state = CatalogState.Loaded;
                _catalog = catalog;
            }
        }

        /// <summary>
        /// Transitions from Unloaded to Failed, publishing the failure list.
        /// </summary>
        /// <param name="errors">The failures.</param>
        /// <exception cref="InvalidOperationException">Thrown when already terminal.</exception>
        public void MarkFailed(IReadOnlyList<CatalogError> errors)
        {
            lock (_gate)
            {
                RequireUnloaded();
                _state = CatalogState.Failed;
                _errors = errors ?? Array.Empty<CatalogError>();
            }
        }

        /// <summary>
        /// Transitions to Failed only if still Unloaded, returning whether it did so without throwing.
        /// </summary>
        /// <param name="errors">The failures.</param>
        /// <returns>True when the transition occurred; false when already terminal.</returns>
        public bool TryMarkFailed(IReadOnlyList<CatalogError> errors)
        {
            lock (_gate)
            {
                if (_state != CatalogState.Unloaded) return false;

                _state = CatalogState.Failed;
                _errors = errors ?? Array.Empty<CatalogError>();
                return true;
            }
        }

        /// <summary>
        /// Throws when the holder has already left the Unloaded state.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when already terminal.</exception>
        private void RequireUnloaded()
        {
            if (_state != CatalogState.Unloaded)
                throw new InvalidOperationException("The catalog holder has already reached a terminal state.");
        }
    }
}