using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the six Build-stage commands over a play Process whose Buffer holds the whole play
    /// archetype. The scoring Core is fixed at lines 1 and 7, with lines 2 to 6 open. A refused edit
    /// leaves the arrangement as the same object, which is how "nothing changed" is shown. Archives go
    /// through the Process, so an installed archive observer fires and every archive leaves exactly
    /// one archive record.
    /// </summary>
    public sealed class BuildCommandTests
    {
        private SessionFlowRig _rig;
        private SessionState _session;

        [SetUp]
        public void SetUp()
        {
            _rig = new SessionFlowRig();
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            _session = _rig.Scopes.Session;
        }

        [Test]
        public void Install_IntoAnOpenPosition_IsAcceptedAndReportsBuildAndBuffer()
        {
            InstanceID item = Item(SessionFlowFixtures.Instruction, 0);

            CommandResult result = _rig.Flow.Execute(new InstallLine(item, new SourcePosition(2)));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(((BuildOperationResult)result.DomainResult).Succeeded);
            Assert.AreEqual(SourceSlotKind.Instruction, SlotAt(2).Kind);
            Assert.AreEqual(item, SlotAt(2).Instruction.InstanceID);
            Assert.AreEqual(new StateChange(ChangedAreas.Build | ChangedAreas.Buffer), _rig.StateChanges[0]);
        }

        [Test]
        public void Install_OntoACoreLine_IsRefusedAndLeavesTheArrangement()
        {
            SourceArrangement before = _rig.Scopes.Build.CurrentArrangement;

            CommandResult result = _rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction, 0), new SourcePosition(1)));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(SourceEditRejection.CoreImmutable, ((BuildOperationResult)result.DomainResult).Rejection);
            Assert.AreSame(before, _rig.Scopes.Build.CurrentArrangement);
            CollectionAssert.IsEmpty(_rig.StateChanges);
        }

        [Test]
        public void Install_AnOverwriteOntoAnOccupiedPosition_ReplacesTheLineAndArchivesTheDisplacedItem()
        {
            InstanceID displaced = Item(SessionFlowFixtures.Instruction, 0);
            InstanceID overwrite = Item(SessionFlowPlayContent.OverwriteInstruction, 0);
            _rig.Flow.Execute(new InstallLine(displaced, new SourcePosition(2)));
            int archiveRecords = ArchiveRecords();

            CommandResult result = _rig.Flow.Execute(new InstallLine(overwrite, new SourcePosition(2)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(overwrite, SlotAt(2).Instruction.InstanceID);
            CollectionAssert.Contains(_rig.Scopes.Build.ArchivedInstances, displaced);
            Assert.AreEqual(archiveRecords, ArchiveRecords(), "An overwrite archives in the Build, not through the Buffer.");
        }

        [Test]
        public void Install_AnOrdinaryItemOntoAnOccupiedPosition_IsRefusedAsNotAnOverwrite()
        {
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction, 0), new SourcePosition(2)));
            SourceArrangement before = _rig.Scopes.Build.CurrentArrangement;

            CommandResult result = _rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction, 1), new SourcePosition(2)));

            Assert.AreEqual(SourceEditRejection.NotAnOverwriteInstruction, ((BuildOperationResult)result.DomainResult).Rejection);
            Assert.AreSame(before, _rig.Scopes.Build.CurrentArrangement);
        }

        [Test]
        public void Remove_AnInstalledLine_IsAcceptedAndEmptiesThePosition()
        {
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction, 0), new SourcePosition(2)));

            CommandResult result = _rig.Flow.Execute(new RemoveLine(new SourcePosition(2)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SourceSlotKind.Empty, SlotAt(2).Kind);
        }

        [Test]
        public void Remove_AnEmptyPosition_IsRefused()
        {
            SourceArrangement before = _rig.Scopes.Build.CurrentArrangement;

            CommandResult result = _rig.Flow.Execute(new RemoveLine(new SourcePosition(3)));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(SourceEditRejection.OriginEmpty, ((BuildOperationResult)result.DomainResult).Rejection);
            Assert.AreSame(before, _rig.Scopes.Build.CurrentArrangement);
        }

        [Test]
        public void Move_ALineToAnEmptyPosition_IsAccepted()
        {
            InstanceID item = Item(SessionFlowFixtures.Instruction, 0);
            _rig.Flow.Execute(new InstallLine(item, new SourcePosition(2)));

            CommandResult result = _rig.Flow.Execute(new MoveLine(new SourcePosition(2), new SourcePosition(5)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(item, SlotAt(5).Instruction.InstanceID);
            Assert.AreEqual(SourceSlotKind.Empty, SlotAt(2).Kind);
        }

        [Test]
        public void Move_FromAnEmptyPosition_IsRefused()
        {
            SourceArrangement before = _rig.Scopes.Build.CurrentArrangement;

            CommandResult result = _rig.Flow.Execute(new MoveLine(new SourcePosition(3), new SourcePosition(4)));

            Assert.AreEqual(SourceEditRejection.OriginEmpty, ((BuildOperationResult)result.DomainResult).Rejection);
            Assert.AreSame(before, _rig.Scopes.Build.CurrentArrangement);
        }

        [Test]
        public void Swap_TwoInstalledLines_IsAccepted()
        {
            InstanceID first = Item(SessionFlowFixtures.Instruction, 0);
            InstanceID second = Item(SessionFlowPlayContent.OverwriteInstruction, 0);
            _rig.Flow.Execute(new InstallLine(first, new SourcePosition(2)));
            _rig.Flow.Execute(new InstallLine(second, new SourcePosition(3)));

            CommandResult result = _rig.Flow.Execute(new SwapLines(new SourcePosition(2), new SourcePosition(3)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(second, SlotAt(2).Instruction.InstanceID);
            Assert.AreEqual(first, SlotAt(3).Instruction.InstanceID);
        }

        [Test]
        public void Swap_WithACoreLine_IsRefused()
        {
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowFixtures.Instruction, 0), new SourcePosition(2)));
            SourceArrangement before = _rig.Scopes.Build.CurrentArrangement;

            CommandResult result = _rig.Flow.Execute(new SwapLines(new SourcePosition(1), new SourcePosition(2)));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreSame(before, _rig.Scopes.Build.CurrentArrangement);
        }

        [Test]
        public void ConfirmDirective_ADirectiveInTheBuffer_IsAcceptedAndPending()
        {
            CommandResult result = _rig.Flow.Execute(new ConfirmDirective(Item(SessionFlowPlayContent.Directive, 0)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, _rig.Scopes.Build.PendingPragmas.Count);
        }

        [Test]
        public void ConfirmDirective_AnInstruction_IsRefusedAsNotADirective()
        {
            CommandResult result = _rig.Flow.Execute(new ConfirmDirective(Item(SessionFlowFixtures.Instruction, 0)));

            Assert.AreEqual(SourceEditRejection.NotADirective, ((BuildOperationResult)result.DomainResult).Rejection);
            Assert.AreEqual(0, _rig.Scopes.Build.PendingPragmas.Count);
        }

        [Test]
        public void Archive_ABufferItem_IsAcceptedRecordsOneArchiveAndReportsBufferAndEconomy()
        {
            int archiveRecords = ArchiveRecords();

            CommandResult result = _rig.Flow.Execute(new ArchiveItem(Item(SessionFlowFixtures.Structure, 0)));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(((ArchiveResult)result.DomainResult).Succeeded);
            Assert.AreEqual(archiveRecords + 1, ArchiveRecords());
            Assert.AreEqual(new StateChange(ChangedAreas.Buffer | ChangedAreas.Economy), _rig.StateChanges[0]);
        }

        [Test]
        public void Archive_AnItemNotInTheBuffer_IsRefused()
        {
            int archiveRecords = ArchiveRecords();

            CommandResult result = _rig.Flow.Execute(new ArchiveItem(new InstanceID(999)));

            Assert.AreEqual(ArchiveRejection.UnknownInstance, ((ArchiveResult)result.DomainResult).Rejection);
            Assert.AreEqual(archiveRecords, ArchiveRecords());
        }

        [Test]
        public void Archive_TheIncomingItemWithNothingIncoming_IsRefused()
        {
            CommandResult result = _rig.Flow.Execute(new ArchiveItem(null));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(ArchiveRejection.NothingIncoming, ((ArchiveResult)result.DomainResult).Rejection);
        }

        [Test]
        public void Archive_UnderAnArchiveObservingDependency_FiresTheObserverOnceAndRecordsEachArchive()
        {
            SessionFlowRig rig = new();
            rig.Flow.BeginSession(SessionFlowPlayContent.GarbageCollectorArchetype(), SessionFlowPlayContent.PlaySystem, "session-1", "seed-1");
            ProcessState process = rig.Scopes.Process;
            SessionState session = rig.Scopes.Session;
            int bytesBefore = process.Bytes.Balance.Value;
            int recordsBefore = CountArchiveRecords(process);

            rig.Flow.Execute(new ArchiveItem(session.Repository.EntriesOf(SessionFlowFixtures.Structure)[0].Item.InstanceID));
            rig.Flow.Execute(new ArchiveItem(session.Repository.EntriesOf(SessionFlowPlayContent.Directive)[0].Item.InstanceID));

            Assert.AreEqual(bytesBefore + 1, process.Bytes.Balance.Value, "The first archive of the Process gains one Byte, and only the first.");
            Assert.AreEqual(recordsBefore + 2, CountArchiveRecords(process), "Each archive issued leaves one archive record.");
        }

        /// <summary>
        /// The instance identity of one Repository entry of a definition.
        /// </summary>
        /// <param name="definition">The content ID.</param>
        /// <param name="index">Which entry of that definition.</param>
        /// <returns>The instance identity.</returns>
        private InstanceID Item(string definition, int index)
        {
            return _session.Repository.EntriesOf(definition)[index].Item.InstanceID;
        }

        /// <summary>
        /// The current arrangement's slot at a line.
        /// </summary>
        /// <param name="line">The one-based line.</param>
        /// <returns>The slot.</returns>
        private SourceSlot SlotAt(int line)
        {
            return _rig.Scopes.Build.CurrentArrangement.SlotAt(new SourcePosition(line));
        }

        /// <summary>
        /// The archive records the rig's Buffer holds.
        /// </summary>
        /// <returns>The count.</returns>
        private int ArchiveRecords()
        {
            return CountArchiveRecords(_rig.Scopes.Process);
        }

        /// <summary>
        /// Counts a Process Buffer's archive records.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <returns>The count.</returns>
        private static int CountArchiveRecords(ProcessState process)
        {
            IReadOnlyList<BufferRecord> records = process.Buffer.Records;
            int count = 0;
            for (int index = 0; index < records.Count; index++)
            {
                if (records[index] is ArchiveRecord)
                    count++;
            }

            return count;
        }
    }
}
