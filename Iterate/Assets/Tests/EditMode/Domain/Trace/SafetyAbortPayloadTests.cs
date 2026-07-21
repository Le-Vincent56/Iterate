using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="SafetyAbortPayload"/> and <see cref="BreachedLimit"/>: the required
    /// occurrence identity, the non-empty breached-limit list preserved in declared order with no hidden
    /// priority, the per-limit bounds, and structural equality over the list.
    /// </summary>
    public sealed class SafetyAbortPayloadTests
    {
        [Test]
        public void Construction_EmptyOccurrenceIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SafetyAbortPayload(
                "",
                new RuntimeUnitID(1),
                new List<BreachedLimit> { new BreachedLimit("depth", 32, 33) }));
        }

        [Test]
        public void Construction_NullLimits_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SafetyAbortPayload(
                "OCC:1",
                new RuntimeUnitID(1),
                null));
        }

        [Test]
        public void Construction_EmptyLimits_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SafetyAbortPayload(
                "OCC:1",
                new RuntimeUnitID(1),
                new List<BreachedLimit>()));
        }

        [Test]
        public void Construction_MultipleLimits_PreservedInDeclaredOrder()
        {
            // Arrange — two simultaneously breached limits, no hidden priority.
            List<BreachedLimit> limits = new()
            {
                new BreachedLimit("depth", 32, 33),
                new BreachedLimit("descendants", 1024, 1025)
            };

            // Act
            SafetyAbortPayload payload = new("OCC:1", new RuntimeUnitID(2), limits);

            // Assert
            Assert.That(payload.OverLimitOccurrenceIdentity, Is.EqualTo("OCC:1"));
            Assert.That(payload.AffectedUnit, Is.EqualTo(new RuntimeUnitID(2)));
            Assert.That(payload.BreachedLimits, Is.EqualTo(limits));
            Assert.That(payload.BreachedLimits[0].LimitName, Is.EqualTo("depth"));
            Assert.That(payload.BreachedLimits[1].LimitName, Is.EqualTo("descendants"));
        }

        [Test]
        public void BreachedLimit_CeilingBelowOne_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new BreachedLimit("depth", 0, 1));
        }

        [Test]
        public void BreachedLimit_NegativeCountAtBreach_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new BreachedLimit("depth", 32, -1));
        }

        [Test]
        public void Equality_IsStructuralOverLimits()
        {
            // Arrange — independently built limit lists with identical content.
            SafetyAbortPayload left = new(
                "OCC:1",
                new RuntimeUnitID(1),
                new List<BreachedLimit> { new BreachedLimit("depth", 32, 33) });
            SafetyAbortPayload right = new(
                "OCC:1",
                new RuntimeUnitID(1),
                new List<BreachedLimit> { new BreachedLimit("depth", 32, 33) });

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
        }
    }
}
