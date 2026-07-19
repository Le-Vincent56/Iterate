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
            LogAssert.Expect(LogType.Log, "[Catalog] Catalog loaded | revision=0.1.0 | definitions=45");

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
        }
    }
}
