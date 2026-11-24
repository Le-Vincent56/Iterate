using NUnit.Framework;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Domain.Progression;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// Tests <see cref="SessionLifecycleRouter"/>: each mode forwards every call to its own lifecycle
    /// and to no other. The onboarding lifecycle is real and is observed through the save files it
    /// writes; the canonical one saves nothing and ends a Session on failure.
    /// </summary>
    public sealed class SessionLifecycleRouterTests
    {
        private OnboardingRig _rig;
        private SessionState _session;

        [SetUp]
        public void SetUp()
        {
            _rig = new OnboardingRig();
            _session = SessionState.Create(_rig.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "session-1", "seed-1");
        }

        [Test]
        public void Mode_DefaultsToOrdinary()
        {
            Assert.AreEqual(SessionMode.Ordinary, _rig.Router.Mode);
        }

        [Test]
        public void Ordinary_EntryWritesNoSaveFile()
        {
            _rig.Router.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.IsNull(_rig.SavedCheckpoint());
        }

        [Test]
        public void Ordinary_FailureEndsTheSession()
        {
            Assert.AreEqual(ProcessFailureDisposition.EndSession, _rig.Router.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null));
        }

        [Test]
        public void Onboarding_EntryWritesTheCheckpoint()
        {
            _rig.Router.Mode = SessionMode.Onboarding;

            _rig.Router.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            CheckpointState saved = _rig.SavedCheckpoint();
            Assert.IsNotNull(saved);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne, saved.Process);
        }

        [Test]
        public void ChangingTheMode_RedirectsTheNextCall()
        {
            _rig.Router.Mode = SessionMode.Onboarding;
            Assert.AreEqual(ProcessFailureDisposition.RestartProcess, _rig.Router.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null));

            _rig.Router.Mode = SessionMode.Ordinary;

            Assert.AreEqual(ProcessFailureDisposition.EndSession, _rig.Router.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null));
        }
    }
}
