using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for the <see cref="EventRecord"/> readonly record struct: the chronological order
    /// equals the identity ordinal, null evidence is rejected, and struct equality delegates to the
    /// evidence's structural equality.
    /// </summary>
    public sealed class EventRecordTests
    {
        [Test]
        public void ChronologicalOrder_EqualsIdentityValue()
        {
            // Arrange
            EventRecord record = new(new TraceEventID(7), MinimalEvidence("EXECUTION_STARTED"));

            // Act & Assert
            Assert.That(record.ChronologicalOrder, Is.EqualTo(7));
        }

        [Test]
        public void Construction_NullEvidence_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new EventRecord(new TraceEventID(1), null));
        }

        [Test]
        public void Equality_DelegatesToEvidenceStructuralEquality()
        {
            // Arrange — equal identities, independently built but structurally equal evidence.
            EventRecord left = new(new TraceEventID(1), MinimalEvidence("EXECUTION_STARTED"));
            EventRecord right = new(new TraceEventID(1), MinimalEvidence("EXECUTION_STARTED"));

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
            Assert.That(right.GetHashCode(), Is.EqualTo(left.GetHashCode()));
        }

        [Test]
        public void Equality_DiffersWhenEvidenceDiffers()
        {
            // Arrange — same identity, different evidence subtype.
            EventRecord left = new(new TraceEventID(1), MinimalEvidence("EXECUTION_STARTED"));
            EventRecord right = new(new TraceEventID(1), MinimalEvidence("EXECUTION_COMPLETED"));

            // Act & Assert
            Assert.That(right, Is.Not.EqualTo(left));
        }

        private static EventEvidence MinimalEvidence(string subtype)
        {
            return new EventEvidence(
                EventFamilies.Lifecycle,
                subtype,
                new List<string>(),
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                SafetyStatus.Normal,
                null);
        }
    }
}
