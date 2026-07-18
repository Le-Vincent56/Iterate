using UnityEngine;
using Didionysymus.Lattice.Runtime;
using Iterate.Application.Logging;

namespace Iterate.Composition.Root
{
    /// <summary>
    /// Emits the boot-complete line once composition finishes: Phase 1 binds the logger factory,
    /// Phase 2 creates the Boot logger and logs. Its console line is the visible proof that the
    /// scene walk, two-phase injection, and the sink are all wired.
    /// </summary>
    public sealed class BootAnnouncer : MonoBehaviour, IInitializable
    {
        private IGameLoggerFactory _loggers;

        private IGameLogger _log;

        /// <summary>
        /// Phase 1: binds the injected logger factory. Bind only — no subscriptions, no other
        /// instances touched.
        /// </summary>
        /// <param name="loggers">The project-scope logger factory.</param>
        [Inject]
        public void Construct(IGameLoggerFactory loggers)
        {
            _loggers = loggers;
        }

        /// <summary>
        /// Phase 2: creates the Boot-category logger and announces that composition completed.
        /// </summary>
        public void Initialize()
        {
            _log = _loggers.Create(LogCategories.Boot);
            _log.Info("Boot complete!");
        }
    }
}