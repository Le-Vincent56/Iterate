using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.Persistence.Tests;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Application.Telemetry;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests the recorder's runs: opening at a begin with the launch label, cause before effect, a
    /// refused command's consequences discarded, closing at the Session's end, back-to-back runs
    /// starting clean, dwell from the clock, the envelope's versions, and the fault barrier.
    /// </summary>
    public sealed class TelemetryRecorderRunTests
    {
        [Test]
        public void FreshOnboardingBegin_WritesRunStartedThenProcessEntered()
        {
            TelemetryRig rig = new();

            rig.Begin();
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.AreEqual(TelemetryEventTypes.RunStarted, TelemetryRig.Text(lines[0], "type"));
            Assert.AreEqual(0, TelemetryRig.Number(lines[0], "seq"));
            Assert.AreEqual("fresh", TelemetryRig.Text(TelemetryRig.Data(lines[0]), "launch"));
            Assert.AreEqual(TelemetryEventTypes.ProcessEntered, TelemetryRig.Text(lines[1], "type"));
            Assert.AreEqual(1, TelemetryRig.Number(lines[1], "seq"));
            Assert.AreEqual(TelemetryEventTypes.Configuration, TelemetryRig.Text(lines[2], "type"));
            Assert.AreEqual("onboarding", TelemetryRig.Text(lines[1], "mode"));
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, TelemetryRig.Text(lines[1], "process"));
            Assert.AreEqual("cfg-1", TelemetryRig.Text(lines[1], "configuration"));
            Assert.AreEqual("run-1", rig.Recorder.ActiveRun);
        }

        [Test]
        public void Relaunch_AfterAFailure_IsLabelledResumed()
        {
            FakeSaveFileStore store = new();
            TelemetryRig first = new(store);
            first.Begin();
            first.FailTheProcess();
            first.Onboarding.PendingWork.GetAwaiter().GetResult();
            TelemetryRig second = new(store);

            second.Begin();
            JsonObject started = TelemetryRig.Data(second.Lines("run-1")[0]);

            Assert.AreEqual("resumed", TelemetryRig.Text(started, "launch"));
            Assert.AreEqual(1, TelemetryRig.Number(started, "consecutiveFailures"));
        }

        [Test]
        public void Relaunch_WhoseRestoreIsRefused_IsLabelledFresh()
        {
            FakeSaveFileStore store = new();
            TelemetryRig first = new(store);
            SessionState session = SessionState.Create(first.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "onboarding", "onboarding-seed");
            CheckpointState elsewhere = CheckpointCapture.Capture(session, SessionFlowPlayContent.PlayFirst, null, 0).State;
            first.Persistence.SaveCheckpointAsync(elsewhere, CancellationToken.None).GetAwaiter().GetResult();
            TelemetryRig second = new(store);
            Assert.IsTrue(second.Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult().ResumesAtCheckpoint, "The checkpoint loads and restores.");

            Assert.IsTrue(second.Begin().Succeeded);

            Assert.AreEqual("fresh", TelemetryRig.Text(TelemetryRig.Data(second.Lines("run-1")[0]), "launch"));
            Assert.IsNull(second.Launch.Beginning, "The flag is cleared once the begin returns.");
        }

        [Test]
        public void BeginOutsideTheLaunchService_IsLabelledDirect()
        {
            TelemetryRig rig = new();

            rig.Flow.BeginSession(SessionFlowFixtures.Archetype(), SessionFlowFixtures.MainSystem, "session-1", "seed-1");

            Assert.AreEqual("direct", TelemetryRig.Text(TelemetryRig.Data(rig.Lines("run-1")[0]), "launch"));
            Assert.AreEqual("ordinary", TelemetryRig.Text(rig.Lines("run-1")[0], "mode"));
        }

        [Test]
        public void RefusedCommand_IsOneLineWithItsReasonAndNothingAfterIt()
        {
            TelemetryRig rig = new();
            rig.Begin();
            int before = rig.Writer.Lines["run-1"].Count;

            rig.Execute(new Submit());
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.AreEqual(before + 1, lines.Count);
            JsonObject refused = lines[lines.Count - 1];
            Assert.AreEqual(TelemetryEventTypes.Command, TelemetryRig.Text(refused, "type"));
            Assert.AreEqual("Submit", TelemetryRig.Text(refused, "subject"));
            Assert.AreEqual("refused", TelemetryRig.Text(refused, "result"));
            Assert.AreEqual(nameof(CommandRejection.WrongStage), TelemetryRig.Text(refused, "reason"));
        }

        [Test]
        public void RefusedBranchConfirm_WhoseEntryFired_WritesNoProcessEntered()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowFixtures.Archetype(), SessionFlowFixtures.MissingCoreSystem, "session-1", "seed-1");
            rig.Execute(new ConfigureBranch(BranchAction.Exclude, rig.Item(SessionFlowFixtures.Instruction)));
            int entries = rig.Entries;

            CommandResult result = rig.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(entries + 1, rig.Entries, "The entry was reported before the refusal.");
            List<JsonObject> lines = rig.Lines("run-1");
            CollectionAssert.DoesNotContain(rig.Types("run-1"), TelemetryEventTypes.ProcessEntered);
            Assert.AreEqual("refused", TelemetryRig.Text(lines[lines.Count - 1], "result"));
            Assert.AreEqual(nameof(ProcessCreationResult), TelemetryRig.Text(TelemetryRig.Data(lines[lines.Count - 1]), "domainResult"));
        }

        [Test]
        public void Abandon_WritesStageExitedThenRunEndedAndClosesTheFile()
        {
            TelemetryRig rig = new();
            rig.Begin();

            rig.Execute(new AbandonSession());
            List<string> types = rig.Types("run-1");
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.AreEqual(TelemetryEventTypes.Command, types[types.Count - 3]);
            Assert.AreEqual(TelemetryEventTypes.StageExited, types[types.Count - 2]);
            Assert.AreEqual(TelemetryEventTypes.RunEnded, types[types.Count - 1]);
            Assert.AreEqual("abandoned", TelemetryRig.Text(TelemetryRig.Data(lines[lines.Count - 1]), "termination"));
            Assert.AreEqual("close run-1", rig.Writer.Log[rig.Writer.Log.Count - 1]);
            Assert.IsNull(rig.Recorder.ActiveRun);
        }

        [Test]
        public void TwoSessionsBackToBack_AreTwoRunsThatEachStartClean()
        {
            TelemetryRig rig = new(null, SessionFlowPlayContent.PatchSystem);
            rig.Begin();
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            rig.Execute(new CompileAndExecute());
            Assert.AreEqual(SessionStage.SessionEnded, rig.Execute(new Submit()).Stage);
            rig.Onboarding.PendingWork.GetAwaiter().GetResult();
            rig.Clock.Advance(1000);

            rig.Begin();
            rig.Clock.Advance(40);
            rig.Execute(new CompileAndExecute());
            List<JsonObject> second = rig.Lines("run-2");

            Assert.AreEqual("completed", TelemetryRig.Text(TelemetryRig.Data(rig.Lines("run-1")[rig.Lines("run-1").Count - 1]), "termination"));
            Assert.Less(rig.Writer.Log.IndexOf("close run-1"), rig.Writer.Log.IndexOf("open run-2"));
            Assert.AreEqual(0, TelemetryRig.Number(second[0], "seq"));
            Assert.AreEqual("ordinary", TelemetryRig.Text(second[0], "mode"));
            Assert.AreEqual(0, TelemetryRig.Number(TelemetryRig.Data(second[0]), "consecutiveFailures"));
            Assert.AreEqual(0, TelemetryRig.Number(TelemetryRig.Data(second[1]), "consecutiveFailures"));
            Assert.AreEqual("cfg-1", TelemetryRig.Text(second[1], "configuration"));
            JsonObject exited = TelemetryRig.OfType(second, TelemetryEventTypes.StageExited)[0];
            Assert.AreEqual(40, TelemetryRig.Number(TelemetryRig.Data(exited), "dwell"), "The second run's dwell starts at its own begin.");
            for (int index = 0; index < second.Count; index++)
                Assert.AreEqual(index, TelemetryRig.Number(second[index], "seq"));
        }

        [Test]
        public void StageExited_DwellEqualsTheClocksAdvance()
        {
            TelemetryRig rig = new();
            rig.Begin();
            rig.Clock.Advance(250);
            rig.Execute(new InstallLine(rig.Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));
            rig.Clock.Advance(250);

            rig.Execute(new CompileAndExecute());
            JsonObject exited = TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.StageExited)[0];

            Assert.AreEqual(nameof(SessionStage.Build), TelemetryRig.Text(TelemetryRig.Data(exited), "stage"));
            Assert.AreEqual(500, TelemetryRig.Number(TelemetryRig.Data(exited), "dwell"));
        }

        [Test]
        public void OnboardingRestart_SplitsTheBuildDwellAtTheNewProcessEntry()
        {
            TelemetryRig rig = new();
            rig.Begin();
            while (rig.Scopes.Process.Counters.ExecutionsRemaining > 1)
            {
                rig.Execute(new CompileAndExecute());
                rig.Execute(new ReturnToBuild());
            }

            rig.Clock.Advance(100);
            rig.Execute(new CompileAndExecute());
            List<JsonObject> lines = rig.Lines("run-1");
            List<string> types = rig.Types("run-1");

            int entered = types.LastIndexOf(TelemetryEventTypes.ProcessEntered);
            Assert.Greater(entered, 1, "The restart entered the Process again.");
            Assert.AreEqual(TelemetryEventTypes.StageExited, types[entered - 1]);
            Assert.AreEqual(nameof(SessionStage.Build), TelemetryRig.Text(TelemetryRig.Data(lines[entered - 1]), "stage"));
            Assert.AreEqual(100, TelemetryRig.Number(TelemetryRig.Data(lines[entered - 1]), "dwell"));
            Assert.AreEqual(1, types.FindAll(type => type == TelemetryEventTypes.RunStarted).Count, "A restart does not begin a new run.");
        }

        [Test]
        public void EveryLine_CarriesTheBuildUXAndContentVersions()
        {
            TelemetryRig rig = new();
            rig.Begin();
            rig.FailTheProcess();

            foreach (JsonObject line in rig.Lines("run-1"))
            {
                Assert.AreEqual("0.1.0", TelemetryRig.Text(line, "build"));
                Assert.AreEqual("0.1", TelemetryRig.Text(line, "ux"));
                Assert.AreEqual(SessionFlowFixtures.Revision, TelemetryRig.Text(line, "content"));
            }
        }

        [Test]
        public void ForcedFault_IsContainedAndStopsOnlyTheRun()
        {
            TelemetryRig rig = new();
            rig.Begin();
            rig.Writer.ThrowOnceOn = "\"subject\":\"CompileAndExecute\"";

            CommandResult result = rig.Execute(new CompileAndExecute());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.ResultReview, rig.Queries.Stage);
            Assert.AreEqual(1, rig.After.Results.Count, "The observer after the tap still ran.");
            Assert.AreEqual(1, rig.TelemetryLogs(LogLevel.Error));
            List<string> types = rig.Types("run-1");
            Assert.AreEqual(TelemetryEventTypes.TelemetryFault, types[types.Count - 1]);
            CollectionAssert.DoesNotContain(types, TelemetryEventTypes.RunEnded);
            int count = types.Count;
            rig.Execute(new ReturnToBuild());
            rig.Execute(new AbandonSession());
            Assert.AreEqual(count, rig.Writer.Lines["run-1"].Count, "A faulted run writes nothing more.");
            Assert.AreEqual(1, rig.TelemetryLogs(LogLevel.Error));
            Assert.IsNull(rig.Recorder.ActiveRun, "The faulted run closed with its Session.");
        }

        [Test]
        public void AfterAFault_TheNextRunStartsClean()
        {
            TelemetryRig rig = new();
            rig.Begin();
            rig.Writer.ThrowOnceOn = "\"subject\":\"CompileAndExecute\"";
            rig.Execute(new CompileAndExecute());
            rig.Execute(new AbandonSession());

            rig.Flow.BeginSession(SessionFlowFixtures.Archetype(), SessionFlowFixtures.MainSystem, "session-2", "seed-2");

            List<string> types = rig.Types("run-2");
            Assert.AreEqual(TelemetryEventTypes.RunStarted, types[0]);
            Assert.AreEqual(TelemetryEventTypes.ProcessEntered, types[1]);
        }
    }
}
