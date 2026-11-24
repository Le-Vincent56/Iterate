using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Application.Telemetry;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Tests <see cref="TelemetrySnapshotMapper"/> over Session Flow's play content: no snapshot
    /// without a Process, every source line by data identity, one changed line per install,
    /// member-by-member equality (the guard de-duplication relies on), attached Patches with host
    /// and socket, pending pragmas, and the Buffer's exposure order.
    /// </summary>
    public sealed class TelemetrySnapshotMapperTests
    {
        private SessionFlowRig _rig;
        private TelemetrySnapshotMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _rig = new SessionFlowRig();
            _mapper = new TelemetrySnapshotMapper(_rig.Flow);
        }

        [Test]
        public void Snapshots_WithNoProcessOpen_AreNull()
        {
            Assert.IsNull(_mapper.Configuration());
            Assert.IsNull(_mapper.Buffer());
        }

        [Test]
        public void Configuration_AtBuild_ListsEverySourceLineOfTheEnteredCore()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            IReadOnlyList<SourceSlot> slots = _rig.Scopes.Build.CurrentArrangement.Slots;

            ConfigurationSnapshot snapshot = _mapper.Configuration();

            Assert.AreEqual(slots.Count, snapshot.Lines.Count);
            int core = 0;
            int empty = 0;
            for (int index = 0; index < slots.Count; index++)
            {
                ConfigurationLine line = snapshot.Lines[index];
                Assert.AreEqual(slots[index].Position.LineNumber, line.Line);
                Assert.AreEqual(slots[index].Kind, line.Kind);
                if (line.Kind == SourceSlotKind.Core)
                {
                    core++;
                    Assert.AreEqual(slots[index].Core.Identity, line.DefinitionID);
                    Assert.IsNull(line.Item);
                }

                if (line.Kind == SourceSlotKind.Empty)
                    empty++;
            }

            Assert.Greater(core, 0, "The entered Core has Core lines.");
            Assert.Greater(empty, 0, "The entered Core has empty lines.");
        }

        [Test]
        public void Configuration_AfterAnInstall_ChangesExactlyThatLine()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            ConfigurationSnapshot before = _mapper.Configuration();
            InstanceID item = Item(SessionFlowFixtures.Instruction);

            Assert.IsTrue(_rig.Flow.Execute(new InstallLine(item, new SourcePosition(2))).Accepted);
            ConfigurationSnapshot after = _mapper.Configuration();

            int changed = 0;
            for (int index = 0; index < before.Lines.Count; index++)
            {
                if (!before.Lines[index].Equals(after.Lines[index]))
                {
                    changed++;
                    Assert.AreEqual(2, after.Lines[index].Line);
                    Assert.AreEqual(item, after.Lines[index].Item);
                    Assert.AreEqual(SessionFlowFixtures.Instruction, after.Lines[index].DefinitionID);
                }
            }

            Assert.AreEqual(1, changed);
        }

        [Test]
        public void Configuration_MovedAwayAndBack_EqualsTheEarlierSnapshot()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction), new SourcePosition(2)));
            ConfigurationSnapshot placed = _mapper.Configuration();

            Assert.IsTrue(_rig.Flow.Execute(new MoveLine(new SourcePosition(2), new SourcePosition(5))).Accepted);
            ConfigurationSnapshot moved = _mapper.Configuration();
            Assert.IsTrue(_rig.Flow.Execute(new MoveLine(new SourcePosition(5), new SourcePosition(2))).Accepted);
            ConfigurationSnapshot back = _mapper.Configuration();

            Assert.AreNotEqual(placed, moved);
            Assert.AreEqual(placed, back);
            Assert.AreEqual(placed.GetHashCode(), back.GetHashCode());
            Assert.AreNotSame(placed, back);
        }

        [Test]
        public void Configuration_APatchedHostInstalled_ListsThePatchWithHostAndSocket()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            _rig.Flow.Execute(new CompileAndExecute());
            Assert.AreEqual(SessionStage.Shop, _rig.Flow.Execute(new Submit()).Stage);
            InstanceID host = Item(SessionFlowFixtures.Instruction);
            Assert.IsTrue(_rig.Flow.Execute(new InstallPatch(3, null, host, 1)).Accepted);
            Assert.AreEqual(SessionStage.Build, _rig.Flow.Execute(new LeaveShop()).Stage);

            Assert.IsTrue(_rig.Flow.Execute(new InstallLine(host, new SourcePosition(2))).Accepted);
            ConfigurationSnapshot snapshot = _mapper.Configuration();

            Assert.AreEqual(1, snapshot.Patches.Count);
            Assert.AreEqual(host, snapshot.Patches[0].Host);
            Assert.AreEqual(1, snapshot.Patches[0].Socket);
            Assert.AreEqual(SessionFlowPlayContent.Patch, snapshot.Patches[0].DefinitionID);
        }

        [Test]
        public void Configuration_AnActivatedPragma_IsListedAsPending()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            InstanceID directive = Item(SessionFlowPlayContent.Directive);

            Assert.IsTrue(_rig.Flow.Execute(new ConfirmDirective(directive)).Accepted);
            ConfigurationSnapshot snapshot = _mapper.Configuration();

            Assert.AreEqual(1, snapshot.Pragmas.Count);
            Assert.AreEqual(directive, snapshot.Pragmas[0].Directive);
            Assert.AreEqual(SessionFlowPlayContent.Directive, snapshot.Pragmas[0].DefinitionID);
        }

        [Test]
        public void Configuration_StatesTheArrangementAgainstTheCompiledOne()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            Assert.AreEqual(CompilationClassification.Initial, _mapper.Configuration().State);
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            _rig.Flow.Execute(new CompileAndExecute());
            Assert.IsTrue(_rig.Flow.Execute(new Continue()).Accepted);
            Assert.AreEqual(CompilationClassification.Unchanged, _mapper.Configuration().State);

            Assert.IsTrue(_rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction), new SourcePosition(3))).Accepted);

            Assert.AreNotEqual(CompilationClassification.Unchanged, _mapper.Configuration().State);
            Assert.AreNotEqual(CompilationClassification.Initial, _mapper.Configuration().State);
        }

        [Test]
        public void Buffer_MatchesTheProcessViewInExposureOrder()
        {
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);

            BufferSnapshot snapshot = _mapper.Buffer();
            ProcessView view = _rig.Queries.Process();

            CollectionAssert.AreEqual(view.Exposed, snapshot.Exposed);
            CollectionAssert.AreEqual(view.Slots, snapshot.Slots);
            Assert.AreEqual(view.Incoming, snapshot.Incoming);
            Assert.AreEqual(view.PendingArrivals, snapshot.PendingArrivals);
            Assert.AreEqual(_rig.Scopes.Process.Buffer.Capacity, snapshot.Capacity);
            Assert.AreEqual(snapshot, _mapper.Buffer(), "An unchanged Buffer snapshots equal.");
        }

        /// <summary>
        /// The first Repository instance of a definition.
        /// </summary>
        /// <param name="definition">The content ID.</param>
        /// <returns>The instance.</returns>
        private InstanceID Item(string definition)
        {
            return _rig.Scopes.Session.Repository.EntriesOf(definition)[0].Item.InstanceID;
        }
    }
}
