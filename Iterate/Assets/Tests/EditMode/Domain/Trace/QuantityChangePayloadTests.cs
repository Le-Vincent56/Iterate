using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="QuantityChangePayload"/> and its supporting evidence
    /// (<see cref="QuantityModifierEvidence"/>, <see cref="QuantityBoundsEvidence"/>): the
    /// prior/requested/final separation, the delta-consistency invariant, the resolved zero-delta case,
    /// and structural equality over the modifier list.
    /// </summary>
    public sealed class QuantityChangePayloadTests
    {
        [Test]
        public void Construction_RoundTripsEveryField()
        {
            // Arrange
            List<QuantityModifierEvidence> modifiers = new() { new QuantityModifierEvidence("MOD:1", "base", 2) };
            QuantityBoundsEvidence bounds = new(0, 100);

            // Act
            QuantityChangePayload payload = new(
                "VALUE:1",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                10,
                5,
                new TraceEventID(3),
                modifiers,
                bounds,
                10,
                15);

            // Assert
            Assert.That(payload.QuantityIdentity, Is.EqualTo("VALUE:1"));
            Assert.That(payload.Category, Is.EqualTo(QuantityCategory.RuntimeVariable));
            Assert.That(payload.Operation, Is.EqualTo(QuantityOperationType.Increase));
            Assert.That(payload.RequestedAmount, Is.EqualTo(10));
            Assert.That(payload.PriorValue, Is.EqualTo(5));
            Assert.That(payload.ReplacementLink, Is.EqualTo(new TraceEventID(3)));
            Assert.That(payload.AppliedModifiers, Is.EqualTo(modifiers));
            Assert.That(payload.AppliedBounds, Is.EqualTo(bounds));
            Assert.That(payload.FinalDelta, Is.EqualTo(10));
            Assert.That(payload.FinalValue, Is.EqualTo(15));
        }

        [Test]
        public void Construction_InconsistentDelta_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new QuantityChangePayload(
                "VALUE:1",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                10,
                5,
                null,
                new List<QuantityModifierEvidence>(),
                null,
                99,
                15));
        }

        [Test]
        public void Construction_ResolvedZeroDelta_IsRepresentable()
        {
            // Arrange — a request of 3 clamped by a max of 5 against a prior of 5 resolves to no change.
            QuantityBoundsEvidence clampToFive = new(null, 5);

            // Act
            QuantityChangePayload payload = new(
                "VALUE:1",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                3,
                5,
                null,
                new List<QuantityModifierEvidence>(),
                clampToFive,
                0,
                5);

            // Assert — a completed zero-delta result is a distinct, valid fact.
            Assert.That(payload.FinalDelta, Is.EqualTo(0));
            Assert.That(payload.FinalValue, Is.EqualTo(5));
        }

        [Test]
        public void Construction_EmptyQuantityIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new QuantityChangePayload(
                "",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                10,
                5,
                null,
                new List<QuantityModifierEvidence>(),
                null,
                10,
                15));
        }

        [Test]
        public void Construction_NullModifiers_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new QuantityChangePayload(
                "VALUE:1",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                10,
                5,
                null,
                null,
                null,
                10,
                15));
        }

        [Test]
        public void Construction_EmptyModifiers_Accepted()
        {
            // Act
            QuantityChangePayload payload = new(
                "VALUE:1",
                QuantityCategory.RuntimeVariable,
                QuantityOperationType.Increase,
                10,
                5,
                null,
                new List<QuantityModifierEvidence>(),
                null,
                10,
                15);

            // Assert
            Assert.That(payload.AppliedModifiers, Is.Empty);
        }

        [Test]
        public void QuantityModifierEvidence_EmptyIdentityOrStage_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new QuantityModifierEvidence("", "base", 2));
            Assert.Throws<ArgumentException>(() => _ = new QuantityModifierEvidence("MOD:1", "", 2));
        }

        [Test]
        public void QuantityBoundsEvidence_BothNull_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new QuantityBoundsEvidence(null, null));
        }

        [Test]
        public void QuantityBoundsEvidence_MinimumAboveMaximum_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new QuantityBoundsEvidence(10, 2));
        }

        [Test]
        public void QuantityBoundsEvidence_SingleSidedBound_Accepted()
        {
            // Act
            QuantityBoundsEvidence lowerOnly = new(5, null);
            QuantityBoundsEvidence upperOnly = new(null, 5);

            // Assert
            Assert.That(lowerOnly.Minimum, Is.EqualTo(5));
            Assert.That(lowerOnly.Maximum, Is.Null);
            Assert.That(upperOnly.Minimum, Is.Null);
            Assert.That(upperOnly.Maximum, Is.EqualTo(5));
        }

        [Test]
        public void Equality_IsStructuralOverModifiers()
        {
            // Arrange — independently built modifier lists with identical content.
            QuantityChangePayload left = new(
                "VALUE:1",
                QuantityCategory.SpendableResource,
                QuantityOperationType.Spend,
                4,
                10,
                null,
                new List<QuantityModifierEvidence> { new QuantityModifierEvidence("MOD:1", "base", -1) },
                null,
                -4,
                6);
            QuantityChangePayload right = new(
                "VALUE:1",
                QuantityCategory.SpendableResource,
                QuantityOperationType.Spend,
                4,
                10,
                null,
                new List<QuantityModifierEvidence> { new QuantityModifierEvidence("MOD:1", "base", -1) },
                null,
                -4,
                6);

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
        }
    }
}
