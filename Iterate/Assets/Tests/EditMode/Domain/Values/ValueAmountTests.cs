using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests that <see cref="ValueAmount"/> preserves negative values (registers carry no floor),
    /// converts explicitly, and compares by value.
    /// </summary>
    public sealed class ValueAmountTests
    {
        [Test]
        public void Constructor_NegativeInput_IsPreserved()
        {
            // Arrange & Act
            ValueAmount register = new(-3);

            // Assert
            Assert.AreEqual(-3, register.Value);
        }

        [Test]
        public void ExplicitConversions_RoundTrip()
        {
            ValueAmount register = (ValueAmount)9;

            Assert.AreEqual(9, (int)register);
        }

        [Test]
        public void ComparisonOperators_OrderByValue()
        {
            ValueAmount smaller = new(-1);
            ValueAmount larger = new(1);

            Assert.IsTrue(smaller < larger);
            Assert.IsTrue(smaller <= larger);
            Assert.IsTrue(larger > smaller);
            Assert.IsTrue(larger >= smaller);
        }

        [Test]
        public void Equality_SameValue_AreEqual()
        {
            Assert.AreEqual(new ValueAmount(2), new ValueAmount(2));
        }
    }
}
