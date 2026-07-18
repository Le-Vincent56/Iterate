using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests construction clamping, exhaustion, conversions, arithmetic, and comparisons of
    /// <see cref="ByteAmount"/>.
    /// </summary>
    public sealed class ByteAmountTests
    {
        [Test]
        public void Constructor_NegativeInput_ClampsToZero()
        {
            // Arrange & Act
            ByteAmount amount = new(-5);

            // Assert
            Assert.AreEqual(0, amount.Value);
        }

        [Test]
        public void Constructor_PositiveInput_KeepsValue()
        {
            ByteAmount amount = new(3);

            Assert.AreEqual(3, amount.Value);
        }

        [Test]
        public void IsExhausted_AtZero_IsTrue()
        {
            ByteAmount amount = new(0);

            Assert.IsTrue(amount.IsExhausted);
        }

        [Test]
        public void IsExhausted_AboveZero_IsFalse()
        {
            ByteAmount amount = new(1);

            Assert.IsFalse(amount.IsExhausted);
        }

        [Test]
        public void SubtractionOperator_BelowZero_ClampsToZero()
        {
            ByteAmount amount = new(2);

            ByteAmount result = amount - 5;

            Assert.AreEqual(0, result.Value);
        }

        [Test]
        public void AdditionOperator_AddsAmount()
        {
            ByteAmount amount = new(2);

            ByteAmount result = amount + 3;

            Assert.AreEqual(5, result.Value);
        }

        [Test]
        public void ExplicitConversions_RoundTrip()
        {
            ByteAmount amount = (ByteAmount)4;

            Assert.AreEqual(4, (int)amount);
        }

        [Test]
        public void ComparisonOperators_OrderByValue()
        {
            ByteAmount smaller = new(1);
            ByteAmount larger = new(2);

            Assert.IsTrue(smaller < larger);
            Assert.IsTrue(smaller <= larger);
            Assert.IsTrue(larger > smaller);
            Assert.IsTrue(larger >= smaller);
        }

        [Test]
        public void Equality_SameValue_AreEqual()
        {
            Assert.AreEqual(new ByteAmount(7), new ByteAmount(7));
        }
    }
}
