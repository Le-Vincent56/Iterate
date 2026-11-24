using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Iterate.Application.Content;
using Iterate.Application.Telemetry;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Proves the project scope's <c>Awake</c> initialises the telemetry recorder at boot, before any
    /// Session can begin. No Session is begun: a begin here would write a run into the player's real
    /// persistent-data folder, so what a begun Session records is proved in EditMode.
    /// </summary>
    public sealed class TelemetryBootTests
    {
        private GameObject _projectScope;

        [TearDown]
        public void TearDown()
        {
            if (_projectScope != null)
                Object.DestroyImmediate(_projectScope);
        }

        [UnityTest]
        public IEnumerator Boot_ProjectScopeActivates_InitialisesTheRecorderWithNoRunOpen()
        {
            _projectScope = new GameObject("ProjectScope");
            _projectScope.SetActive(false);
            _projectScope.AddComponent<ProjectLifetimeScope>();
            _projectScope.SetActive(true);
            ProjectLifetimeScope scope = _projectScope.GetComponent<ProjectLifetimeScope>();

            TelemetryRecorder recorder = scope.Resolver.Resolve<TelemetryRecorder>();

            Assert.IsTrue(recorder.Initialized);
            Assert.IsNull(recorder.ActiveRun);
            CatalogHolder holder = scope.Resolver.Resolve<CatalogHolder>();
            int frames = 0;
            while (holder.State == CatalogState.Unloaded && frames < 600)
            {
                frames++;
                yield return null;
            }

            // Let the catalog boot finish before teardown, so its continuation does not outlive the scope.
            for (int settle = 0; settle < 30; settle++)
                yield return null;
        }
    }
}
