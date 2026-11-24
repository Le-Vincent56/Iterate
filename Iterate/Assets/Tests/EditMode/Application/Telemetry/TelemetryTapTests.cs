using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Onboarding;
using Iterate.Application.Onboarding.Tests;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Application.Telemetry;
using Iterate.Domain.Progression;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests the two dependency-free taps. The lifecycle tap forwards each call to the router first
    /// and then raises one event, returning the router's disposition unchanged; the command tap
    /// raises once per observed command with the same instances. Neither throws unsubscribed.
    /// </summary>
    public sealed class TelemetryTapTests
    {
        private OnboardingRig _rig;
        private SessionState _session;
        private TelemetryLifecycleTap _tap;

        [SetUp]
        public void SetUp()
        {
            _rig = new OnboardingRig();
            _session = SessionState.Create(_rig.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "onboarding", "onboarding-seed");
            _tap = new TelemetryLifecycleTap(_rig.Router);
        }

        [Test]
        public void OnProcessEntry_InOnboardingMode_ReachesTheRouterAndWritesTheCheckpoint()
        {
            _rig.Router.Mode = SessionMode.Onboarding;

            _tap.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.IsNotNull(_rig.SavedCheckpoint());
        }

        [Test]
        public void OnProcessEntry_RaisesOneEventWithItsArguments()
        {
            List<ProcessEntry> entries = new();
            _tap.ProcessEntered += entries.Add;

            _tap.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, SessionFlowFixtures.RouteOne);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(new ProcessEntry(SessionFlowFixtures.TutorialOne, SessionFlowFixtures.RouteOne), entries[0]);
        }

        [Test]
        public void OnProcessFailed_InOrdinaryMode_ReturnsTheRoutersEndSession()
        {
            List<ProcessFailure> failures = new();
            _tap.ProcessFailed += failures.Add;

            ProcessFailureDisposition answered = _tap.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.AreEqual(ProcessFailureDisposition.EndSession, answered);
            Assert.AreEqual(new ProcessFailure(SessionFlowFixtures.TutorialOne, null, answered), failures[0]);
        }

        [Test]
        public void OnProcessFailed_InOnboardingMode_ReturnsTheRoutersOwnAnswer()
        {
            OnboardingRig direct = new();
            direct.Router.Mode = SessionMode.Onboarding;
            direct.Router.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);
            ProcessFailureDisposition expected = direct.Router.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);
            _rig.Router.Mode = SessionMode.Onboarding;
            _tap.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            ProcessFailureDisposition answered = _tap.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.AreEqual(expected, answered);
            Assert.AreEqual(ProcessFailureDisposition.RestartProcess, answered);
        }

        [Test]
        public void ProcessFailed_InOnboardingMode_SeesTheIncrementedFailureCount()
        {
            _rig.Router.Mode = SessionMode.Onboarding;
            _tap.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);
            int seen = -1;
            _tap.ProcessFailed += failure => seen = _rig.Onboarding.ConsecutiveFailures;

            _tap.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.AreEqual(1, seen);
        }

        [Test]
        public void SessionEnds_RaiseTheirEventsOnceEach()
        {
            int completed = 0;
            int abandoned = 0;
            _tap.SessionCompleted += () => completed++;
            _tap.SessionAbandoned += () => abandoned++;

            _tap.OnSessionCompleted(_session);
            _tap.OnSessionAbandoned(_session);

            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, abandoned);
        }

        [Test]
        public void Inner_IsTheRouterTheTapWasBuiltOver()
        {
            Assert.AreSame(_rig.Router, _tap.Inner);
        }

        [Test]
        public void CommandTap_RaisesOncePerObservationWithTheSameInstances()
        {
            TelemetryCommandTap tap = new();
            List<CommandObservation> observed = new();
            tap.CommandObserved += observed.Add;
            ISessionCommand command = new Continue();
            CommandResult result = CommandResult.Reject(CommandRejection.NoSession, null, SessionStage.NoSession);

            tap.Observed(command, result);

            Assert.AreEqual(1, observed.Count);
            Assert.AreSame(command, observed[0].Command);
            Assert.AreSame(result, observed[0].Result);
        }

        [Test]
        public void Taps_WithNoSubscriber_DoNotThrow()
        {
            TelemetryCommandTap commands = new();

            Assert.DoesNotThrow(() => commands.Observed(new Continue(), CommandResult.Reject(CommandRejection.NoSession, null, SessionStage.NoSession)));
            Assert.DoesNotThrow(() => _tap.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null));
            Assert.DoesNotThrow(() => _tap.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null));
            Assert.DoesNotThrow(() => _tap.OnSessionCompleted(_session));
            Assert.DoesNotThrow(() => _tap.OnSessionAbandoned(_session));
        }

        [Test]
        public void NullWriterAndStore_DoNoWork()
        {
            NullTelemetryWriter writer = new();
            NullTelemetryRunStore store = new();

            writer.Open("run-1");
            writer.Append("run-1", "{}\n");
            writer.Close("run-1");

            CollectionAssert.IsEmpty(store.ListAsync(default).GetAwaiter().GetResult());
            Assert.IsNull(store.ReadAsync("run-1", default).GetAwaiter().GetResult());
            Assert.IsFalse(store.DeleteAsync("run-1", default).GetAwaiter().GetResult());
            Assert.IsTrue(store.WriteExportAsync("anywhere", "{}\n", default).IsCompleted);
        }
    }
}
