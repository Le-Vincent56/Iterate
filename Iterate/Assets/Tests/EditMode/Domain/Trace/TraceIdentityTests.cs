using System;
using NUnit.Framework;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for the index-stable in-record identities <see cref="TraceEventID"/> and
    /// <see cref="RuntimeUnitID"/>: one-based construction, invariant rendering, and value equality.
    /// </summary>
    public sealed class TraceIdentityTests
    {
        [Test]
        public void TraceEventID_ValidValues_ExposeValue()
        {
            // Arrange & Act
            TraceEventID first = new(1);
            TraceEventID ceiling = new(4096);

            // Assert
            Assert.That(first.Value, Is.EqualTo(1));
            Assert.That(ceiling.Value, Is.EqualTo(4096));
        }

        [Test]
        public void RuntimeUnitID_ValidValues_ExposeValue()
        {
            // Arrange & Act
            RuntimeUnitID first = new(1);
            RuntimeUnitID ceiling = new(4096);

            // Assert
            Assert.That(first.Value, Is.EqualTo(1));
            Assert.That(ceiling.Value, Is.EqualTo(4096));
        }

        [Test]
        public void TraceEventID_ZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new TraceEventID(0));
            Assert.Throws<ArgumentException>(() => _ = new TraceEventID(-1));
        }

        [Test]
        public void RuntimeUnitID_ZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitID(0));
            Assert.Throws<ArgumentException>(() => _ = new RuntimeUnitID(-1));
        }

        [Test]
        public void ToString_RendersPrefixedOrdinal()
        {
            // Arrange
            TraceEventID eventIdentity = new(7);
            RuntimeUnitID unitIdentity = new(7);

            // Act & Assert
            Assert.That(eventIdentity.ToString(), Is.EqualTo("E7"));
            Assert.That(unitIdentity.ToString(), Is.EqualTo("U7"));
        }

        [Test]
        public void Equality_IsByValue()
        {
            // Arrange
            TraceEventID eventSeven = new(7);
            TraceEventID eventSevenAgain = new(7);
            TraceEventID eventEight = new(8);
            RuntimeUnitID unitSeven = new(7);
            RuntimeUnitID unitSevenAgain = new(7);
            RuntimeUnitID unitEight = new(8);

            // Act & Assert
            Assert.That(eventSevenAgain, Is.EqualTo(eventSeven));
            Assert.That(eventEight, Is.Not.EqualTo(eventSeven));
            Assert.That(eventSevenAgain.GetHashCode(), Is.EqualTo(eventSeven.GetHashCode()));
            Assert.That(unitSevenAgain, Is.EqualTo(unitSeven));
            Assert.That(unitEight, Is.Not.EqualTo(unitSeven));
            Assert.That(unitSevenAgain.GetHashCode(), Is.EqualTo(unitSeven.GetHashCode()));
        }
    }
}
