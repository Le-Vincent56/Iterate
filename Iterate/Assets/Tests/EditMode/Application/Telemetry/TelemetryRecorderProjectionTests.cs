using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Persistence.Tests;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Application.Telemetry;
using Iterate.Domain.Compilation;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests the recorder's projections: every command has one, compilations carry Bytes before and
    /// after, executions carry the configuration executed and the record's Condition counts, a final
    /// execution's failure reads in cause-before-effect order, the automatic submission is flagged,
    /// unchanged arrangements are not written twice, shop and Buffer commands carry their data, and
    /// no line holds a seed, a Session identity or random-decision evidence.
    /// </summary>
    public sealed class TelemetryRecorderProjectionTests
    {
        private static readonly string[] _randomDecisionMembers = { "request", "candidateEvidence", "revisionIdentity", "draws", "resultPermutation" };

        [Test]
        public void EveryRoutedCommand_HasAProjection()
        {
            CommandDataProjector projector = new();

            foreach (Type command in SessionCommandRoutes.Commands)
                Assert.IsTrue(projector.Projects(command), command.Name);

            Assert.AreEqual(23, SessionCommandRoutes.Commands.Count);
        }

        [Test]
        public void BlockedCompile_IsARefusedCommandThenABlockedCompilation()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.BrokeSystem, "session-1", "seed-1");
            rig.Execute(new CompileAndExecute());
            rig.Execute(new ReturnToBuild());
            rig.Execute(new InstallLine(rig.Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));

            CommandResult result = rig.Execute(new CompileAndExecute());
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.IsFalse(result.Accepted);
            JsonObject command = lines[lines.Count - 2];
            JsonObject compilation = lines[lines.Count - 1];
            Assert.AreEqual("refused", TelemetryRig.Text(command, "result"));
            Assert.AreEqual(TelemetryEventTypes.Compilation, TelemetryRig.Text(compilation, "type"));
            Assert.IsTrue(TelemetryRig.Flag(TelemetryRig.Data(compilation), "blocked"));
            Assert.AreEqual(nameof(CompilationBlockReason.InsufficientBytes), TelemetryRig.Text(TelemetryRig.Data(compilation), "blockReason"));
            Assert.AreEqual(TelemetryRig.Number(TelemetryRig.Data(compilation), "bytesBefore"), TelemetryRig.Number(TelemetryRig.Data(compilation), "bytesAfter"));
        }

        [Test]
        public void AcceptedCompile_CarriesTheDebitAndTheConfigurationExecuted()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.PlaySystem, "session-1", "seed-1");
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            string installed = TelemetryRig.Text(rig.Lines("run-1")[rig.Lines("run-1").Count - 2], "configuration");

            CommandResult result = rig.Execute(new CompileAndExecute());
            List<JsonObject> lines = rig.Lines("run-1");

            ExecutionOutcome outcome = (ExecutionOutcome)result.DomainResult;
            JsonObject compilation = TelemetryRig.Data(TelemetryRig.OfType(lines, TelemetryEventTypes.Compilation)[0]);
            long before = TelemetryRig.Number(compilation, "bytesBefore");
            long after = TelemetryRig.Number(compilation, "bytesAfter");
            Assert.AreEqual(outcome.Attempt.Breakdown.FinalCost, before - after);
            Assert.AreEqual(rig.Queries.Process().Bytes, after);
            JsonObject executed = TelemetryRig.Data(TelemetryRig.OfType(lines, TelemetryEventTypes.ExecutionCompleted)[0]);
            Assert.AreEqual(installed, TelemetryRig.Text(executed, "executed"));
            Assert.AreEqual(outcome.Record.FinalState.FinalOutput.Value, TelemetryRig.Number(executed, "output"));
            Assert.AreEqual(Count(outcome.Record, ExecutionEventSubtypes.ConditionTrue), TelemetryRig.Number(executed, "conditionsTrue"));
            Assert.AreEqual(Count(outcome.Record, ExecutionEventSubtypes.ConditionFalse), TelemetryRig.Number(executed, "conditionsFalse"));
            Assert.AreEqual(outcome.Evaluation.Reached?.ToString(), TelemetryRig.Text(executed, "tier"));
        }

        [Test]
        public void FinalExecutionFailure_ReadsCauseBeforeEffectWithTheFailedAttemptsOutput()
        {
            TelemetryRig rig = new();
            rig.Begin();

            rig.FailTheProcess();
            List<JsonObject> lines = rig.Lines("run-1");
            List<string> types = rig.Types("run-1");

            int command = types.LastIndexOf(TelemetryEventTypes.Command);
            CollectionAssert.AreEqual(
                new[]
                {
                    TelemetryEventTypes.Command,
                    TelemetryEventTypes.Compilation,
                    TelemetryEventTypes.ExecutionCompleted,
                    TelemetryEventTypes.ProcessFailed,
                    TelemetryEventTypes.StageExited,
                    TelemetryEventTypes.ProcessEntered,
                },
                types.GetRange(command, 6)
            );
            JsonObject failed = TelemetryRig.Data(lines[command + 3]);
            Assert.AreEqual(TelemetryRig.Number(TelemetryRig.Data(lines[command + 2]), "output"), TelemetryRig.Number(failed, "output"));
            Assert.AreEqual(1, TelemetryRig.Number(failed, "consecutiveFailures"));
            Assert.AreEqual("RestartProcess", TelemetryRig.Text(failed, "disposition"));
            Assert.AreEqual(rig.Scopes.Process.Counters.Allowance, TelemetryRig.Number(lines[command + 2], "execution"), "The execution line counts the failed attempt, the Process's last, not the restart.");
            Assert.AreEqual(0, TelemetryRig.Number(lines[command + 5], "execution"), "The restarted Process has run nothing.");
        }

        [Test]
        public void PassReachingFinalExecution_IsFlaggedAutoSubmitted()
        {
            TelemetryRig rig = new(null, SessionFlowPlayContent.PatchSystem);
            rig.Begin();
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            while (rig.Scopes.Process.Counters.ExecutionsRemaining > 1)
            {
                Assert.AreEqual(SessionStage.ResultReview, rig.Execute(new CompileAndExecute()).Stage);
                Assert.IsTrue(rig.Execute(new Continue()).Accepted);
            }

            CommandResult final = rig.Execute(new CompileAndExecute());
            List<JsonObject> executions = TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.ExecutionCompleted);

            Assert.AreNotEqual(SessionStage.ResultReview, final.Stage);
            Assert.IsTrue(TelemetryRig.Flag(TelemetryRig.Data(executions[executions.Count - 1]), "autoSubmitted"));
            Assert.IsFalse(TelemetryRig.Flag(TelemetryRig.Data(executions[0]), "autoSubmitted"));
        }

        [Test]
        public void ExplicitSubmit_IsNotFlaggedAutoSubmitted()
        {
            TelemetryRig rig = new(null, SessionFlowPlayContent.PatchSystem);
            rig.Begin();
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            rig.Execute(new CompileAndExecute());

            rig.Execute(new Submit());
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.IsFalse(TelemetryRig.Flag(TelemetryRig.Data(TelemetryRig.OfType(lines, TelemetryEventTypes.ExecutionCompleted)[0]), "autoSubmitted"));
            Assert.AreEqual("Submit", TelemetryRig.Text(TelemetryRig.OfType(lines, TelemetryEventTypes.Command)[2], "subject"));
        }

        [Test]
        public void UnchangedArrangementAcrossTwoCompiles_WritesOneConfiguration()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.PlaySystem, "session-1", "seed-1");
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            rig.Execute(new CompileAndExecute());
            rig.Execute(new Continue());

            rig.Execute(new CompileAndExecute());
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.AreEqual(2, TelemetryRig.OfType(lines, TelemetryEventTypes.Configuration).Count, "The entry snapshot and the install's.");
            foreach (JsonObject executed in TelemetryRig.OfType(lines, TelemetryEventTypes.ExecutionCompleted))
                Assert.AreEqual("cfg-2", TelemetryRig.Text(TelemetryRig.Data(executed), "executed"));
        }

        [Test]
        public void EachArrangementChange_IsWrittenUnderTheNextIdentity()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.PlaySystem, "session-1", "seed-1");
            rig.Execute(new InstallLine(rig.Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));

            rig.Execute(new MoveLine(new SourcePosition(2), new SourcePosition(5)));
            rig.Execute(new MoveLine(new SourcePosition(5), new SourcePosition(2)));
            List<JsonObject> commands = TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.Command);

            Assert.AreEqual("cfg-2", TelemetryRig.Text(commands[0], "configuration"));
            Assert.AreEqual("cfg-3", TelemetryRig.Text(commands[1], "configuration"));
            Assert.AreEqual("cfg-4", TelemetryRig.Text(commands[2], "configuration"), "A change back is still a change from the current snapshot.");
            Assert.AreEqual(4, TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.Configuration).Count);
        }

        [Test]
        public void ArchiveItem_RecordsItsItemAndOrigin()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.PlaySystem, "session-1", "seed-1");
            InstanceID item = rig.Item(SessionFlowFixtures.Instruction);

            rig.Execute(new ArchiveItem(item));
            List<JsonObject> commands = TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.Command);

            JsonObject data = TelemetryRig.Data(commands[commands.Count - 1]);
            Assert.AreEqual(item.Value, TelemetryRig.Number(data, "item"));
            Assert.AreEqual("buffer", TelemetryRig.Text(data, "origin"));
        }

        [Test]
        public void ShopCommands_RecordPatchTargetsAndDependencyRAM()
        {
            TelemetryRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.PlaySystem, "session-1", "seed-1");
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            rig.Execute(new CompileAndExecute());
            Assert.AreEqual(SessionStage.Shop, rig.Execute(new Submit()).Stage);
            InstanceID host = rig.Item(SessionFlowFixtures.Instruction);

            Assert.IsTrue(rig.Execute(new InstallPatch(3, null, host, 1)).Accepted);
            Assert.IsTrue(rig.Execute(new InstallDependency(4, Array.Empty<InstanceID>())).Accepted);
            List<JsonObject> commands = TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.Command);

            JsonObject patch = TelemetryRig.Data(commands[commands.Count - 2]);
            Assert.AreEqual(host.Value, TelemetryRig.Number(patch, "host"));
            Assert.AreEqual(1, TelemetryRig.Number(patch, "socket"));
            Assert.AreEqual(3, TelemetryRig.Number(patch, "slot"));
            JsonObject dependency = TelemetryRig.Data(commands[commands.Count - 1]);
            Assert.IsTrue(dependency.TryGet("destroyFirst", out JsonValue destroyed));
            Assert.AreEqual(0, ((JsonArray)destroyed).Items.Count);
            Assert.AreEqual(rig.Scopes.Session.Economy.Dependencies.Usage, TelemetryRig.Number(dependency, "ramAfter"));
            Assert.LessOrEqual(TelemetryRig.Number(dependency, "ramBefore"), TelemetryRig.Number(dependency, "ramAfter"));
            Assert.Less(TelemetryRig.Number(dependency, "tokensAfter"), TelemetryRig.Number(dependency, "tokensBefore"));
        }

        [Test]
        public void SeedSweep_AFullSessionAndAnOrdinaryOne_HoldNoSeedSessionIdentityOrDrawEvidence()
        {
            TelemetryRig rig = new(null, SessionFlowPlayContent.PatchSystem);
            rig.Begin();
            rig.Execute(new InstallLine(rig.Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            rig.Execute(new CompileAndExecute());
            rig.Execute(new Submit());
            rig.Onboarding.PendingWork.GetAwaiter().GetResult();
            rig.Begin();
            rig.Execute(new CompileAndExecute());
            rig.Execute(new AbandonSession());

            Sweep(rig, "run-1", "onboarding", "onboarding-seed");
            Sweep(rig, "run-2", "ordinary-1", "ordinary-seed-1");
        }

        [Test]
        public void SeedSweep_AResumedOnboarding_HoldsNoSeedOrSessionIdentity()
        {
            FakeSaveFileStore store = new();
            TelemetryRig first = new(store);
            first.Begin();
            first.FailTheProcess();
            first.Onboarding.PendingWork.GetAwaiter().GetResult();
            TelemetryRig second = new(store);

            second.Begin();
            second.FailTheProcess();

            Sweep(first, "run-1", "onboarding", "onboarding-seed");
            Sweep(second, "run-1", "onboarding", "onboarding-seed");
        }

        /// <summary>
        /// Asserts no line of a run holds the seed identity, the Session identity outside the
        /// <c>mode</c> member (the onboarding Session identity is also the onboarding mode's label),
        /// or any member of the random-decision record.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <param name="run">The run.</param>
        /// <param name="session">The Session identity.</param>
        /// <param name="seed">The seed identity.</param>
        private static void Sweep(
            TelemetryRig rig,
            string run,
            string session,
            string seed
        )
        {
            Assert.Greater(rig.Writer.Lines[run].Count, 3);
            foreach (string text in rig.Writer.Lines[run])
            {
                StringAssert.DoesNotContain(seed, text);
                StringAssert.DoesNotContain("seed", text.ToLowerInvariant());
            }

            foreach (JsonObject line in rig.Lines(run))
                Walk(line, null, session);
        }

        /// <summary>
        /// Walks a value, failing on the Session identity outside <c>mode</c> or a random-decision member.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The member it was read from, or null.</param>
        /// <param name="session">The Session identity.</param>
        private static void Walk(JsonValue value, string key, string session)
        {
            switch (value)
            {
                case JsonObject members:
                    foreach (string member in members.Keys)
                    {
                        CollectionAssert.DoesNotContain(_randomDecisionMembers, member);
                        members.TryGet(member, out JsonValue child);
                        Walk(child, member, session);
                    }

                    break;

                case JsonArray items:
                    foreach (JsonValue item in items.Items)
                        Walk(item, key, session);

                    break;

                case JsonString text:
                    if (key != "mode")
                        Assert.AreNotEqual(session, text.Value, $"The Session identity appeared under {key}.");

                    break;
            }
        }

        /// <summary>
        /// Counts a record's events of a subtype.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <param name="subtype">The subtype.</param>
        /// <returns>The count.</returns>
        private static int Count(ExecutionRecord record, string subtype)
        {
            int count = 0;
            foreach (EventRecord recorded in record.Events)
            {
                if (recorded.Evidence.Subtype == subtype)
                    count++;
            }

            return count;
        }
    }
}
