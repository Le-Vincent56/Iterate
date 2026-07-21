using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="RuntimeUnitClosure"/> and <see cref="RuntimeUnitRecord"/>: a
    /// disposition transformation preserves the original and final separately and must change the
    /// disposition, the created-request list is validated, the ordering fields are one-based and
    /// independent (a descendant may complete before an earlier sibling starts), and equality is
    /// structural over both linkage lists.
    /// </summary>
    public sealed class RuntimeUnitRecordTests
    {
        [Test]
        public void Closure_OriginalEqualsFinalDisposition_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitClosure(
                EventDisposition.Resolved,
                EventDisposition.Resolved,
                null,
                new List<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal));
        }

        [Test]
        public void Closure_SkippedTransformedToRescued_RoundTrips()
        {
            // Act — a rescue transforms a skipped disposition into a rescued one.
            RuntimeUnitClosure closure = new(
                EventDisposition.Skipped,
                EventDisposition.Rescued,
                null,
                new List<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal);

            // Assert
            Assert.That(closure.OriginalDisposition, Is.EqualTo(EventDisposition.Skipped));
            Assert.That(closure.FinalDisposition, Is.EqualTo(EventDisposition.Rescued));
        }

        [Test]
        public void Closure_EmptyCreatedRequestEntry_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitClosure(
                null,
                EventDisposition.Resolved,
                null,
                new List<string> { "" },
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal));
        }

        [Test]
        public void Closure_NullCreatedRequestList_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitClosure(
                null,
                EventDisposition.Resolved,
                null,
                null,
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal));
        }

        [Test]
        public void Record_ZeroStartOrder_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitRecord(
                new RuntimeUnitID(1),
                MinimalOpening(),
                MinimalClosure(),
                new List<TraceEventID>(),
                new List<RuntimeUnitID>(),
                0,
                1));
        }

        [Test]
        public void Record_ZeroCompletionOrder_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitRecord(
                new RuntimeUnitID(1),
                MinimalOpening(),
                MinimalClosure(),
                new List<TraceEventID>(),
                new List<RuntimeUnitID>(),
                1,
                0));
        }

        [Test]
        public void Record_FullRecord_RoundTrips()
        {
            // Arrange
            List<TraceEventID> childEvents = new() { new TraceEventID(1), new TraceEventID(2) };
            List<RuntimeUnitID> descendants = new() { new RuntimeUnitID(3) };

            // Act
            RuntimeUnitRecord record = new(
                new RuntimeUnitID(2),
                MinimalOpening(),
                MinimalClosure(),
                childEvents,
                descendants,
                1,
                4);

            // Assert
            Assert.That(record.Identity, Is.EqualTo(new RuntimeUnitID(2)));
            Assert.That(record.ChildEvents, Is.EqualTo(childEvents));
            Assert.That(record.DescendantUnits, Is.EqualTo(descendants));
            Assert.That(record.StartOrder, Is.EqualTo(1));
            Assert.That(record.CompletionOrder, Is.EqualTo(4));
        }

        [Test]
        public void Record_CompletionOrderBeforeStartOrder_IsAccepted()
        {
            // Act — a depth-first descendant completes (order 2) before an earlier sibling starts (order 5).
            RuntimeUnitRecord record = new(
                new RuntimeUnitID(3),
                MinimalOpening(),
                MinimalClosure(),
                new List<TraceEventID>(),
                new List<RuntimeUnitID>(),
                5,
                2);

            // Assert — the ordering fields are independent.
            Assert.That(record.StartOrder, Is.EqualTo(5));
            Assert.That(record.CompletionOrder, Is.EqualTo(2));
        }

        [Test]
        public void Record_Equality_IsStructuralOverBothLists()
        {
            // Arrange — independently built linkage lists with identical content.
            RuntimeUnitRecord left = new(
                new RuntimeUnitID(1),
                MinimalOpening(),
                MinimalClosure(),
                new List<TraceEventID> { new TraceEventID(1) },
                new List<RuntimeUnitID> { new RuntimeUnitID(2) },
                1,
                2);
            RuntimeUnitRecord right = new(
                new RuntimeUnitID(1),
                MinimalOpening(),
                MinimalClosure(),
                new List<TraceEventID> { new TraceEventID(1) },
                new List<RuntimeUnitID> { new RuntimeUnitID(2) },
                1,
                2);

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
            Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
        }

        private static RuntimeUnitOpening MinimalOpening()
        {
            return new RuntimeUnitOpening(
                null,
                "CORE:1",
                OwnershipClassification.CoreOwned,
                null,
                ActivationKind.CanonicalTraversal,
                null,
                0,
                EffectOriginLineage.Empty,
                null,
                null);
        }

        private static RuntimeUnitClosure MinimalClosure()
        {
            return new RuntimeUnitClosure(
                null,
                EventDisposition.Resolved,
                null,
                new List<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal);
        }
    }
}
