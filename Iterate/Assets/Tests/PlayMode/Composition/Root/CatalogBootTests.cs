using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Iterate.Application.Content;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Scene boot proof: activating the ProjectScope object loads the shipped catalog through the real
    /// composition and async-startable path, reaching <see cref="CatalogState.Loaded"/> and logging the
    /// Catalog line — while the existing boot line still appears (the <c>base.Awake()</c> guard holds).
    /// </summary>
    public sealed class CatalogBootTests
    {
        private GameObject _projectScope;

        [TearDown]
        public void TearDown()
        {
            if (_projectScope != null)
            {
                Object.DestroyImmediate(_projectScope);
            }
        }

        [UnityTest]
        public IEnumerator Boot_ProjectScopeActivates_LoadsCatalogAndLogsBothLines()
        {
            LogAssert.Expect(LogType.Log, "[Boot] Boot complete!");
            // The revision and definition count are the shipped catalog's, and both move whenever
            // content is added: the count went 45 to 46 when the PROCESS_RULE category and WB-PRC-001
            // landed, then 46 to 79 when the catalog extension's eight package and configuration kinds
            // and WB-PRC-002 landed at revision 0.2.0. Both are owned and asserted by
            // ShippedCatalogTests; this line pins them only as a by-product of matching the whole log
            // message, which is why a content change surfaces here as a missing-log failure rather than
            // as a count mismatch.
            LogAssert.Expect(LogType.Log, "[Catalog] Catalog loaded | revision=0.2.0 | definitions=79");

            _projectScope = new GameObject("ProjectScope");
            _projectScope.SetActive(false);
            _projectScope.AddComponent<ProjectLifetimeScope>();
            _projectScope.AddComponent<BootAnnouncer>();
            _projectScope.SetActive(true);

            ProjectLifetimeScope scope = _projectScope.GetComponent<ProjectLifetimeScope>();
            CatalogHolder holder = scope.Resolver.Resolve<CatalogHolder>();

            int frames = 0;
            while (holder.State == CatalogState.Unloaded && frames < 600)
            {
                frames++;
                yield return null;
            }

            Assert.AreEqual(CatalogState.Loaded, holder.State);

            // The startable marks the holder Loaded and only then logs, and it runs its continuation off
            // the main thread (ConfigureAwait(false)). So observing Loaded does not imply the log has
            // been emitted — the wait above can exit in the gap between those two statements, leaving
            // LogAssert with nothing to match when it is evaluated at teardown. Yield further frames so
            // the continuation reaches its log call first.
            for (int settle = 0; settle < 30; settle++)
            {
                yield return null;
            }
        }
    }
}
