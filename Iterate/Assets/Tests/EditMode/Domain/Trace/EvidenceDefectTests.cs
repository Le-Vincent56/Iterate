using System;
using Iterate.Domain.Trace;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="EvidenceDefect"/>: a field name and reason are required, a defect
    /// anchors to at most one record identity, and the event-scoped, unit-scoped, and execution-scoped
    /// (both null) forms all construct.
    /// </summary>
    public sealed class EvidenceDefectTests
    {
        [Test]
        public void EmptyFieldName_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new EvidenceDefect(null, null, "", "missing"));
        }

        [Test]
        public void EmptyReason_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new EvidenceDefect(null, null, "Family", ""));
        }

        [Test]
        public void BothEventAndUnit_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new EvidenceDefect(
                new TraceEventID(1),
                new RuntimeUnitID(1),
                "Family",
                "conflicting anchors"));
        }

        [Test]
        public void EventScoped_Constructs()
        {
            // Act
            EvidenceDefect defect = new(new TraceEventID(2), null, "Subtype", "missing subtype");

            // Assert
            Assert.That(defect.Event, Is.EqualTo(new TraceEventID(2)));
            Assert.That(defect.Unit, Is.Null);
        }

        [Test]
        public void UnitScoped_Constructs()
        {
            // Act
            EvidenceDefect defect = new(null, new RuntimeUnitID(3), "Closure", "missing closure");

            // Assert
            Assert.That(defect.Event, Is.Null);
            Assert.That(defect.Unit, Is.EqualTo(new RuntimeUnitID(3)));
        }

        [Test]
        public void ExecutionScoped_Constructs()
        {
            // Act — both anchors null is the execution-scope form.
            EvidenceDefect defect = new(null, null, "Header", "missing header field");

            // Assert
            Assert.That(defect.Event, Is.Null);
            Assert.That(defect.Unit, Is.Null);
            Assert.That(defect.FieldName, Is.EqualTo("Header"));
            Assert.That(defect.Reason, Is.EqualTo("missing header field"));
        }
    }
}
