using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Application.Telemetry;
using Iterate.Domain.Compilation;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests <see cref="TelemetryCommandGuard"/> and the recorder's Presentation entry: a throwing
    /// command reaches the caller unchanged and ends the run as an error without its message, the
    /// buffered consequence is discarded, the guard changes nothing for an accepted command, and
    /// <see cref="TelemetryRecorder.Record"/> writes only inside a run.
    /// </summary>
    public sealed class TelemetryCommandGuardTests
    {
        [Test]
        public void ThrowingCommand_IsRethrownUnchangedAndEndsTheRunAsAnError()
        {
            TelemetryRig rig = new();
            rig.Begin();
            int entries = TelemetryRig.OfType(rig.Lines("run-1"), TelemetryEventTypes.ProcessEntered).Count;
            ThrowingSessionCommands throwing = new(rig);
            TelemetryCommandGuard guard = new(throwing, rig.Recorder);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => guard.Execute(new Continue()));
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.AreSame(throwing.Thrown, thrown);
            JsonObject command = lines[lines.Count - 2];
            JsonObject ended = lines[lines.Count - 1];
            Assert.AreEqual("Continue", TelemetryRig.Text(command, "subject"));
            Assert.AreEqual("error", TelemetryRig.Text(command, "result"));
            Assert.AreEqual(TelemetryEventTypes.RunEnded, TelemetryRig.Text(ended, "type"));
            Assert.AreEqual("error", TelemetryRig.Text(TelemetryRig.Data(ended), "termination"));
            Assert.AreEqual(nameof(InvalidOperationException), TelemetryRig.Text(TelemetryRig.Data(ended), "exception"));
            StringAssert.DoesNotContain("never write", rig.Writer.Text("run-1"));
            Assert.AreEqual(entries, TelemetryRig.OfType(lines, TelemetryEventTypes.ProcessEntered).Count, "The buffered entry was discarded.");
            Assert.IsNull(rig.Recorder.ActiveRun);
            Assert.AreEqual("close run-1", rig.Writer.Log[rig.Writer.Log.Count - 1]);
        }

        [Test]
        public void AfterAThrow_LaterCommandsOpenNothingUntilTheNextBegin()
        {
            TelemetryRig rig = new();
            rig.Begin();
            TelemetryCommandGuard guard = new(new ThrowingSessionCommands(rig), rig.Recorder);
            Assert.Throws<InvalidOperationException>(() => guard.Execute(new Continue()));
            int count = rig.Writer.Lines["run-1"].Count;

            rig.Execute(new InstallLine(rig.Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));
            rig.Execute(new CompileAndExecute());

            Assert.AreEqual(count, rig.Writer.Lines["run-1"].Count);
            Assert.AreEqual(1, rig.Writer.Lines.Count);
        }

        [Test]
        public void AcceptedCommand_ThroughTheGuard_WritesTheSameLinesAsWithout()
        {
            TelemetryRig guarded = new();
            TelemetryRig direct = new();
            guarded.Begin();
            direct.Begin();

            guarded.Execute(new InstallLine(guarded.Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));
            direct.Flow.Execute(new InstallLine(direct.Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));
            guarded.Execute(new CompileAndExecute());
            direct.Flow.Execute(new CompileAndExecute());

            Assert.AreEqual(direct.Writer.Text("run-1"), guarded.Writer.Text("run-1"));
        }

        [Test]
        public void Guard_ReturnsTheInnerResult()
        {
            TelemetryRig rig = new();
            rig.Begin();

            CommandResult result = rig.Commands.Execute(new Submit());

            Assert.AreEqual(CommandRejection.WrongStage, result.Rejection);
            Assert.AreSame(rig.Flow, rig.Commands.Inner);
        }

        [Test]
        public void Record_InsideARun_WritesOneEnvelopedLine()
        {
            TelemetryRig rig = new();
            rig.Begin();
            int count = rig.Writer.Lines["run-1"].Count;

            rig.Recorder.Record("lesson_started", "lesson-1", "line 2", new JsonObjectBuilder().Add("step", 1L).Build());
            List<JsonObject> lines = rig.Lines("run-1");

            Assert.AreEqual(count + 1, lines.Count);
            JsonObject recorded = lines[count];
            Assert.AreEqual("lesson_started", TelemetryRig.Text(recorded, "type"));
            Assert.AreEqual(count, TelemetryRig.Number(recorded, "seq"));
            Assert.AreEqual("lesson-1", TelemetryRig.Text(recorded, "subject"));
            Assert.AreEqual("onboarding", TelemetryRig.Text(recorded, "mode"));
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, TelemetryRig.Text(recorded, "process"));
            Assert.AreEqual(1, TelemetryRig.Number(TelemetryRig.Data(recorded), "step"));
        }

        [Test]
        public void Record_OutsideARun_WritesNothing()
        {
            TelemetryRig rig = new();

            rig.Recorder.Record("lesson_started", null, null, null);

            Assert.AreEqual(0, rig.Writer.Lines.Count);
            Assert.AreEqual(0, rig.Writer.Log.Count);
        }
    }
}
