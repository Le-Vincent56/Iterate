using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests a Process failure, which happens when the final execution misses Pass, and the three
    /// dispositions the lifecycle may answer with. Ending the Session closes both scopes and announces
    /// neither completion nor abandonment. A restart rebuilds the Process from its initial scripted
    /// state and keeps the Session exactly as it was: its checkpoint document is identical. A return
    /// to Branch configuration opens a draft seeded from the Branch the failed Process was played
    /// with.
    /// </summary>
    public sealed class FailureDispositionTests
    {
        [Test]
        public void EndSession_ClosesBothScopesAndEndsTheSessionAsFailed()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);

            CommandResult result = FailTheProcess(rig);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
            Assert.AreEqual(SessionEndReason.Failed, rig.StageChanges[rig.StageChanges.Count - 1].EndReason);
            Assert.AreEqual(0, rig.Scopes.OpenScopes);
            CollectionAssert.Contains(rig.Log, "failed WB-PROC-101");
            CollectionAssert.DoesNotContain(rig.Log, "completed");
            CollectionAssert.DoesNotContain(rig.Log, "abandoned");
        }

        [Test]
        public void RestartProcess_RebuildsTheProcessAndLeavesTheSessionIdentical()
        {
            SessionFlowRig rig = new();
            rig.Lifecycle.Disposition = ProcessFailureDisposition.RestartProcess;
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            SessionState session = rig.Scopes.Session;
            ProcessState failed = rig.Scopes.Process;
            string before = Checkpoint(session);

            CommandResult result = FailTheProcess(rig);

            ProcessState restarted = rig.Scopes.Process;
            Assert.AreEqual(SessionStage.Build, result.Stage);
            Assert.AreNotSame(failed, restarted);
            Assert.AreEqual(before, Checkpoint(session));
            Assert.AreEqual(30, restarted.Bytes.Balance.Value);
            Assert.AreEqual(0, restarted.Counters.ExecutionsRun);
            Assert.AreEqual(SessionFlowPlayContent.StarterItems().Length, OccupiedSlots(restarted));
            Assert.AreEqual(2, rig.Scopes.ProcessScopesOpened);
            Assert.AreEqual(2, rig.Lifecycle.LastAllocatedAtEntry.Count, "The restart is a Process entry and is announced as one.");
        }

        [Test]
        public void ReturnToBranchConfiguration_OpensADraftSeededFromTheFailedProcessBranch()
        {
            SessionFlowRig rig = new();
            rig.Lifecycle.Disposition = ProcessFailureDisposition.ReturnToBranchConfiguration;
            rig.Begin(SessionFlowFixtures.BranchFirstSystem);
            SessionState session = rig.Scopes.Session;
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID));
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));
            ActiveBranchConfiguration played = session.ActiveBranch.Confirmed;

            CommandResult result = FailTheProcess(rig);

            Assert.AreEqual(SessionStage.BranchConfiguration, result.Stage);
            Assert.IsTrue(session.ActiveBranch.HasConfigurationInProgress);
            foreach (InstanceID eligible in played.Eligible)
                Assert.IsTrue(session.ActiveBranch.StateOf(eligible).HasFlag(BranchInstanceState.Selected), eligible.ToString());

            Assert.AreEqual(1, rig.Scopes.ProcessScopesOpened);
            Assert.AreEqual(1, rig.Scopes.OpenScopes, "Only the Session scope stays open.");
        }

        /// <summary>
        /// Runs every execution the Process allows on its current source, returning to Build between
        /// them, so the last one misses Pass and the Process fails.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <returns>The final execution's command result.</returns>
        private static CommandResult FailTheProcess(SessionFlowRig rig)
        {
            ProcessState process = rig.Scopes.Process;
            while (process.Counters.ExecutionsRemaining > 1)
            {
                Assert.IsTrue(rig.Flow.Execute(new CompileAndExecute()).Accepted);
                Assert.IsTrue(rig.Flow.Execute(new ReturnToBuild()).Accepted);
            }

            return rig.Flow.Execute(new CompileAndExecute());
        }

        /// <summary>
        /// Counts the Buffer slots holding an item.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <returns>The count.</returns>
        private static int OccupiedSlots(ProcessState process)
        {
            int count = 0;
            foreach (BufferSlot slot in process.Buffer.Slots)
            {
                if (slot.Item != null)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// The Session's checkpoint document as it would be saved.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <returns>The document text.</returns>
        private static string Checkpoint(SessionState session)
        {
            CheckpointCaptureResult captured = CheckpointCapture.Capture(session, SessionFlowPlayContent.PlayFirst, null, 0);
            Assert.IsTrue(captured.Succeeded);
            return VersionedDocument.Write(SaveFiles.FormatVersion, CheckpointStateCodec.ToJson(captured.State));
        }
    }
}
