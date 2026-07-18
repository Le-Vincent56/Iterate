using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests that <see cref="ScoreValue"/> preserves negative values (registers carry no floor),
    /// converts explicitly, and compares by value.
    /// </summary>
    public sealed class ScoreValueTests
    {
        [Test]
        public void Constructor_NegativeInput_IsPreserved()
        {
            // Arrange & Act
            ScoreValue register = new(-3);

            // Assert
            Assert.AreEqual(-3, register.Value);
        }

        [Test]
        public void ExplicitConversions_RoundTrip()
        {
            ScoreValue register = (ScoreValue)9;

            Assert.AreEqual(9, (int)register);
        }

        [Test]
        public void ComparisonOperators_OrderByValue()
        {
            ScoreValue smaller = new(-1);
            ScoreValue larger = new(1);

            Assert.IsTrue(smaller < larger);
            Assert.IsTrue(smaller <= larger);
            Assert.IsTrue(larger > smaller);
            Assert.IsTrue(larger >= smaller);
        }

        [Test]
        public void Equality_SameValue_AreEqual()
        {
            Assert.AreEqual(new ScoreValue(2), new ScoreValue(2));
        }
    }
}
