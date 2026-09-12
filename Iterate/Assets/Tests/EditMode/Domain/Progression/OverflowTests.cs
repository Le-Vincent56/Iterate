using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests overflow: an item arriving to a full Buffer stays outside it in a blocking state, the
    /// closed four-way resolution set each ends that state, and the drain that admits a held item the
    /// moment capacity appears is total — it never rejects and never throws, which is what lets it run
    /// inside Compilation's own edit path without leaving a half-applied edit.
    /// </summary>
    public sealed class OverflowTests
    {
        [Test]
        public void Admit_ToAFullBuffer_HoldsTheItemOutside()
        {
            InstructionBuffer buffer = Full(2);
            RepositoryItem incoming = Item(9);

            AdmissionResult result = buffer.Admit(incoming);

            Assert.AreEqual(AdmissionOutcome.Held, result.Outcome);
            Assert.IsTrue(buffer.IsOverflowing);
            Assert.AreEqual(incoming.InstanceID, buffer.IncomingItem.InstanceID);
            Assert.AreEqual(0, result.SlotNumber, "a held item occupies no slot.");
        }

        [Test]
        public void Admit_ToAFullBuffer_WritesAnOverflowRecord()
        {
            InstructionBuffer buffer = Full(1);

            buffer.Admit(Item(9));

            Assert.IsInstanceOf<OverflowRecord>(buffer.Records[buffer.Records.Count - 1]);
        }

        [Test]
        public void MultipleArrivals_QueueInAuthoredOrder()
        {
            InstructionBuffer buffer = Full(1);
            RepositoryItem first = Item(8);
            RepositoryItem second = Item(9);

            buffer.Admit(first);
            buffer.Admit(second);

            Assert.AreEqual(first.InstanceID, buffer.IncomingItem.InstanceID, "the head of the queue is the incoming item.");
            Assert.AreEqual(1, buffer.PendingArrivals.Count);
            Assert.AreEqual(second.InstanceID, buffer.PendingArrivals[0].InstanceID);
        }

        [Test]
        public void TheHeldItem_CannotBeTaken()
        {
            InstructionBuffer buffer = Full(1);
            RepositoryItem incoming = Item(9);
            buffer.Admit(incoming);

            Assert.IsFalse(buffer.TryPeekInstruction(incoming.InstanceID, out _), "the held item is outside the Buffer.");
            Assert.Throws<InvalidOperationException>(() => buffer.Take(incoming.InstanceID));
        }

        [Test]
        public void Take_DuringOverflow_AdmitsTheHeldItemIntoTheFreedSlot()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem resident = Item(1);
            buffer.Admit(resident);
            buffer.Admit(Item(2));
            RepositoryItem incoming = Item(9);
            buffer.Admit(incoming);

            buffer.Take(resident.InstanceID);

            Assert.IsFalse(buffer.IsOverflowing);
            Assert.IsNull(buffer.IncomingItem);
            Assert.AreEqual(incoming.InstanceID, buffer.Slots[0].Item.InstanceID);
        }

        [Test]
        public void ConsumingADirective_DuringOverflow_EndsOverflow()
        {
            InstructionBuffer buffer = new(1);
            RepositoryItem directive = Directive(1);
            buffer.Admit(directive);
            RepositoryItem incoming = Item(9);
            buffer.Admit(incoming);

            buffer.Take(directive.InstanceID);

            Assert.IsFalse(buffer.IsOverflowing);
            Assert.AreEqual(incoming.InstanceID, buffer.Slots[0].Item.InstanceID);
            Assert.AreEqual(1, buffer.ConsumedItems.Count);
        }

        [Test]
        public void ArchivingABufferedItem_DuringOverflow_EndsOverflow()
        {
            InstructionBuffer buffer = new(1);
            RepositoryItem resident = Item(1);
            buffer.Admit(resident);
            RepositoryItem incoming = Item(9);
            buffer.Admit(incoming);

            buffer.Archive(resident.InstanceID);

            Assert.IsFalse(buffer.IsOverflowing);
            Assert.AreEqual(incoming.InstanceID, buffer.Slots[0].Item.InstanceID);
        }

        [Test]
        public void ArchivingTheIncomingItem_EndsOverflowWithoutEnteringTheBuffer()
        {
            InstructionBuffer buffer = Full(1);
            RepositoryItem incoming = Item(9);
            buffer.Admit(incoming);

            ArchiveResult result = buffer.ArchiveIncoming();

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(buffer.IsOverflowing);
            Assert.AreEqual(1, buffer.ArchivedItems.Count);
            Assert.AreEqual(incoming.InstanceID, buffer.ArchivedItems[0].InstanceID);
            Assert.IsNotNull(buffer.Slots[0].Item, "the resident kept its slot; the incoming item never entered.");
            Assert.AreEqual(BufferItemState.Archived, buffer.StateOf(incoming.InstanceID));
        }

        [Test]
        public void ArchiveIncoming_WithNothingIncoming_IsRejected()
        {
            InstructionBuffer buffer = new(2);

            ArchiveResult result = buffer.ArchiveIncoming();

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ArchiveRejection.NothingIncoming, result.Rejection);
        }

        [Test]
        public void ADrain_AdmitsExactlyOneQueuedItemPerFreedSlot()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem first = Item(1);
            buffer.Admit(first);
            buffer.Admit(Item(2));
            buffer.Admit(Item(8));
            buffer.Admit(Item(9));

            buffer.Archive(first.InstanceID);

            Assert.IsTrue(buffer.IsOverflowing, "one freed slot admits one queued item, not both.");
            Assert.AreEqual(0, buffer.PendingArrivals.Count);
            Assert.AreEqual(9, buffer.IncomingItem.InstanceID.Value);
        }

        [Test]
        public void ADrain_RunsInsideTheSameCall()
        {
            InstructionBuffer buffer = new(1);
            RepositoryItem resident = Item(1);
            buffer.Admit(resident);
            buffer.Admit(Item(9));

            ArchiveResult result = buffer.Archive(resident.InstanceID);

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(buffer.IsOverflowing, "the drain completed before Archive returned.");
        }

        [Test]
        public void EveryConstructedOverflowState_ExitsByArchivingTheIncomingItem()
        {
            for (int capacity = 1; capacity <= 4; capacity++)
            {
                for (int queued = 1; queued <= 3; queued++)
                {
                    InstructionBuffer buffer = Full(capacity);
                    for (int index = 0; index < queued; index++)
                    {
                        buffer.Admit(Item(100 + index));
                    }

                    Assert.IsTrue(buffer.IsOverflowing, "capacity " + capacity + ", queued " + queued);

                    int exits = 0;
                    while (buffer.IsOverflowing)
                    {
                        Assert.IsTrue(
                            buffer.ArchiveIncoming().Succeeded,
                            "capacity " + capacity + ", queued " + queued + ": archiving the incoming item is the universal exit."
                        );

                        exits++;
                        Assert.LessOrEqual(exits, queued, "each archive removes one queued item.");
                    }

                    Assert.AreEqual(queued, exits);
                }
            }
        }

        [Test]
        public void AnInstallThroughARealBuildState_DuringOverflow_AdmitsTheHeldItemInsideThatApply()
        {
            InstructionBuffer buffer = new(2);
            RepositoryItem installable = Item(1);
            buffer.Admit(installable);
            buffer.Admit(Item(2));
            RepositoryItem incoming = Item(9);
            buffer.Admit(incoming);
            Assert.IsTrue(buffer.IsOverflowing);

            BuildState build = new(TwoSlotArrangement(), buffer, ProgressionFixtures.Parameters());
            BuildOperationResult result = build.Apply(new InstallEdit(installable.InstanceID, new SourcePosition(2)));

            Assert.IsTrue(result.Succeeded, "the install itself succeeds.");
            Assert.IsFalse(buffer.IsOverflowing, "the drain ran inside Apply and cannot fail.");
            Assert.AreEqual(incoming.InstanceID, buffer.Slots[0].Item.InstanceID);
        }

        [Test]
        public void AnArrivalToABufferWithSpace_NeverOverflows()
        {
            InstructionBuffer buffer = new(2);

            AdmissionResult result = buffer.Admit(Item(1));

            Assert.AreEqual(AdmissionOutcome.Admitted, result.Outcome);
            Assert.IsFalse(buffer.IsOverflowing);
            Assert.IsNull(buffer.IncomingItem);
        }

        /// <summary>
        /// Builds a Buffer filled to capacity with Instruction items.
        /// </summary>
        /// <param name="capacity">The Buffer capacity to fill.</param>
        /// <returns>The full Buffer.</returns>
        private static InstructionBuffer Full(int capacity)
        {
            InstructionBuffer buffer = new(capacity);
            for (int index = 0; index < capacity; index++)
            {
                buffer.Admit(Item(index + 1));
            }

            return buffer;
        }

        /// <summary>
        /// Builds a two-slot arrangement: one Core line and one empty player position.
        /// </summary>
        /// <returns>The arrangement.</returns>
        private static SourceArrangement TwoSlotArrangement()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));
            List<SourceSlot> slots = new()
            {
                SourceSlot.ForCore(new SourcePosition(1), new CoreLine("core-01", operation)),
                SourceSlot.ForEmpty(new SourcePosition(2))
            };

            return new SourceArrangement(slots);
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
