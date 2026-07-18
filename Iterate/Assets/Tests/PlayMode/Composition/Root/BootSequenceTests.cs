using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Iterate.Composition.Root;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Play-mode boot proof: a ProjectScope GameObject built at runtime must inject
    /// <see cref="BootAnnouncer"/> two-phase and emit the boot line through the console sink.
    /// </summary>
    public sealed class BootSequenceTests
    {
        private GameObject _projectScope;

        [TearDown]
        public void TearDown()
        {
            if (_projectScope != null)
            {
                // Immediate destruction runs OnDestroy now, disposing the root resolver before
                // the next test rather than at an unspecified later frame.
                Object.DestroyImmediate(_projectScope);
            }
        }

        [Test]
        public void Boot_ProjectScopeActivates_LogsBootComplete()
        {
            // Arrange: build the object inactive so both components exist before Awake runs.
            _projectScope = new GameObject("ProjectScope");
            _projectScope.SetActive(false);
            _projectScope.AddComponent<ProjectLifetimeScope>();
            _projectScope.AddComponent<BootAnnouncer>();
            LogAssert.Expect(LogType.Log, "[Boot] Boot complete!");

            // Act: activation runs LifetimeScopeBehaviour.Awake — build, scene walk, Phase 2.
            _projectScope.SetActive(true);

            // Assert: LogAssert verifies the expected line arrived.
        }
    }
}
