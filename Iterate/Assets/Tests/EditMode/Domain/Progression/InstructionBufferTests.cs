using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Instruction Buffer's ordinary behaviour: fixed numbered slots that an item keeps until
    /// it leaves, the lowest-open-slot rule for arrivals and returns, the peek-then-take seam
    /// Compilation builds against, and the archive and consume states that free capacity.
    /// </summary>
    public sealed class InstructionBufferTests
    {
        [Test]
        public void ANewBuffer_ShowsEverySlotOpen()
        {
            InstructionBuffer buffer = new(3);

            Assert.AreEqual(3, buffer.Capacity);
            Assert.AreEqual(3, buffer.Slots.Count);
            for (int index = 0; index < buffer.Slots.Count; index++)
            {
                Assert.AreEqual(index + 1, buffer.Slots[index].Number);
                Assert.IsNull(buffer.Slots[index].Item, "a new Buffer holds nothing.");
            }
        }

        [Test]
        public void Admit_TakesTheLowestOpenSlot()
        {
            InstructionBuffer buffer = new(3);

            AdmissionResult first = buffer.Admit(Item(1));
            AdmissionResult second = buffer.Admit(Item(2));

            Assert.AreEqual(AdmissionOutcome.Admitted, first.Outcome);
            Assert.AreEqual(1, first.SlotNumber);
            Assert.AreEqual(2, second.SlotNumber);
        }

        [Test]
        public void Admit_AfterAGapOpens_ReusesTheLowestOpenSlot()
        {
            InstructionBuffer buffer = new(3);
            RepositoryItem first = Item(1);
            buffer.Admit(first);
            buffer.Admit(Item(2));
            buffer.Archive(first.InstanceID);

            AdmissionResult admitted = buffer.Admit(Item(3));

            Assert.AreEqual(1, admitted.SlotNumber, "slot 1 is the lowest open slot again.");
        }

        [Test]
        public void AnItem_KeepsItsSlotWhileOthersComeAndGo()
        {
            InstructionBuffer buffer = new(3);
            RepositoryItem first = Item(1);
            RepositoryItem second = Item(2);
            buffer.Admit(first);
            buffer.Admit(second);

            buffer.Archive(first.InstanceID);
            buffer.Admit(Item(3));

            Assert.AreEqual(second.InstanceID, buffer.Slots[1].Item.InstanceID, "slot 2 never moved.");
        }

        [Test]
        public void Archive_FreesTheSlotAndRecordsTheItemArchived()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem item = Item(1);
            buffer.Admit(item);

            ArchiveResult result = buffer.Archive(item.InstanceID);

            Assert.IsTrue(result.Succeeded);
            Assert.IsNull(buffer.Slots[0].Item);
            Assert.AreEqual(1, buffer.ArchivedItems.Count);
            Assert.AreEqual(BufferItemState.Archived, buffer.StateOf(item.InstanceID));
        }

        [Test]
        public void Archive_AnItemTheBufferDoesNotHold_IsRejected()
        {
            InstructionBuffer buffer = new(2);

            ArchiveResult result = buffer.Archive(new InstanceID(99));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ArchiveRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void TryPeekInstruction_FindsAnAdmittedInstruction()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem item = Item(1);
            buffer.Admit(item);

            bool found = buffer.TryPeekInstruction(item.InstanceID, out InstructionInstance instance);

            Assert.IsTrue(found);
            Assert.AreEqual(item.InstanceID, instance.InstanceID);
        }

        [Test]
        public void TryPeekStructure_DoesNotMatchAnInstruction()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem item = Item(1);
            buffer.Admit(item);

            Assert.IsFalse(buffer.TryPeekStructure(item.InstanceID, out StructureInstance instance));
            Assert.IsNull(instance);
        }

        [Test]
        public void TryPeekDirective_FindsAnAdmittedDirective()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem directive = Directive(1);
            buffer.Admit(directive);

            Assert.IsTrue(buffer.TryPeekDirective(directive.InstanceID, out DirectiveInstance instance));
            Assert.AreEqual(directive.InstanceID, instance.InstanceID);
        }

        [Test]
        public void Take_AnInstruction_FreesTheSlotWithoutRecordingAState()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem item = Item(1);
            buffer.Admit(item);

            buffer.Take(item.InstanceID);

            Assert.IsNull(buffer.Slots[0].Item);
            Assert.AreEqual(0, buffer.ConsumedItems.Count, "an installed Instruction now lives in the source.");
            Assert.AreEqual(0, buffer.ArchivedItems.Count);
        }

        [Test]
        public void Take_ADirective_RecordsItConsumed()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem directive = Directive(1);
            buffer.Admit(directive);

            buffer.Take(directive.InstanceID);

            Assert.AreEqual(1, buffer.ConsumedItems.Count);
            Assert.AreEqual(BufferItemState.Consumed, buffer.StateOf(directive.InstanceID));
        }

        [Test]
        public void AcceptRemoved_ReturnsAnInstructionToTheLowestOpenSlot()
        {
            InstructionBuffer buffer = new(3);
            RepositoryItem first = Item(1);
            buffer.Admit(first);
            buffer.Admit(Item(2));
            buffer.Take(first.InstanceID);

            buffer.AcceptRemoved(first.Instruction);

            Assert.AreEqual(first.InstanceID, buffer.Slots[0].Item.InstanceID);
        }

        [Test]
        public void AcceptRemoved_WithNoRemovalCapacity_Throws()
        {
            InstructionBuffer buffer = new(1);
            RepositoryItem item = Item(1);
            buffer.Admit(item);

            Assert.IsFalse(buffer.HasRemovalCapacity);
            Assert.Throws<InvalidOperationException>(() => buffer.AcceptRemoved(Item(2).Instruction));
        }

        [Test]
        public void HasRemovalCapacity_IsFalseOnlyWhenEverySlotIsFull()
        {
            InstructionBuffer buffer = new(2);
            buffer.Admit(Item(1));

            Assert.IsTrue(buffer.HasRemovalCapacity);

            buffer.Admit(Item(2));

            Assert.IsFalse(buffer.HasRemovalCapacity);
        }

        [Test]
        public void ArchivedAndConsumedItems_LeaveActiveCapacity()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem archived = Item(1);
            RepositoryItem consumed = Directive(2);
            buffer.Admit(archived);
            buffer.Admit(consumed);

            buffer.Archive(archived.InstanceID);
            buffer.Take(consumed.InstanceID);

            Assert.IsTrue(buffer.HasRemovalCapacity);
            Assert.IsNull(buffer.Slots[0].Item);
            Assert.IsNull(buffer.Slots[1].Item);
        }

        [Test]
        public void StateOf_AnItemStillPresent_IsPresent()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem item = Item(1);
            buffer.Admit(item);

            Assert.AreEqual(BufferItemState.Present, buffer.StateOf(item.InstanceID));
        }

        [Test]
        public void Records_AreInTransactionOrder()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem item = Item(1);
            buffer.Admit(item);
            buffer.Archive(item.InstanceID);

            Assert.AreEqual(2, buffer.Records.Count);
            Assert.IsInstanceOf<AdmissionRecord>(buffer.Records[0]);
            Assert.IsInstanceOf<ArchiveRecord>(buffer.Records[1]);
        }

        /// <summary>
        /// Builds an Instruction Repository item with the given identity.
        /// </summary>
        /// <param name="id">The instance identity number.</param>
        /// <returns>The Repository item.</returns>
        private static RepositoryItem Item(int id)
        {
            return RepositoryItem.From(
                ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                new InstanceID(id)
            );
        }

        /// <summary>
        /// Builds a Directive Repository item with the given identity.
        /// </summary>
        /// <param name="id">The instance identity number.</param>
        /// <returns>The Repository item.</returns>
        private static RepositoryItem Directive(int id)
        {
            return RepositoryItem.From(
                ProgressionFixtures.Directive(ProgressionFixtures.Overclock),
                new InstanceID(id)
            );
        }
    }
}
