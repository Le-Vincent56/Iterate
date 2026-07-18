using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests construction clamping, exhaustion, conversions, arithmetic, and comparisons of
    /// <see cref="TokenAmount"/>.
    /// </summary>
    public sealed class TokenAmountTests
    {
        [Test]
        public void Constructor_NegativeInput_ClampsToZero()
        {
            // Arrange & Act
            TokenAmount amount = new(-5);

            // Assert
            Assert.AreEqual(0, amount.Value);
        }

        [Test]
        public void Constructor_PositiveInput_KeepsValue()
        {
            TokenAmount amount = new(3);

            Assert.AreEqual(3, amount.Value);
        }

        [Test]
        public void IsExhausted_AtZero_IsTrue()
        {
            TokenAmount amount = new(0);

            Assert.IsTrue(amount.IsExhausted);
        }

        [Test]
        public void IsExhausted_AboveZero_IsFalse()
        {
            TokenAmount amount = new(1);

            Assert.IsFalse(amount.IsExhausted);
        }

        [Test]
        public void SubtractionOperator_BelowZero_ClampsToZero()
        {
            TokenAmount amount = new(2);

            TokenAmount result = amount - 5;

            Assert.AreEqual(0, result.Value);
        }

        [Test]
        public void AdditionOperator_AddsAmount()
        {
            TokenAmount amount = new(2);

            TokenAmount result = amount + 3;

            Assert.AreEqual(5, result.Value);
        }

        [Test]
        public void ExplicitConversions_RoundTrip()
        {
            TokenAmount amount = (TokenAmount)4;

            Assert.AreEqual(4, (int)amount);
        }

        [Test]
        public void ComparisonOperators_OrderByValue()
        {
            TokenAmount smaller = new(1);
            TokenAmount larger = new(2);

            Assert.IsTrue(smaller < larger);
            Assert.IsTrue(smaller <= larger);
            Assert.IsTrue(larger > smaller);
            Assert.IsTrue(larger >= smaller);
        }

        [Test]
        public void Equality_SameValue_AreEqual()
        {
            Assert.AreEqual(new TokenAmount(7), new TokenAmount(7));
        }
    }
}
