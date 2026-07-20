using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests that <see cref="SelectionPurposes.All"/> is the CAB-EVT-781 vocabulary seeded verbatim — the
    /// nine current-or-foreseeable purposes, including the two "— non-gameplay" suffixes (em-dash U+2014) —
    /// and rejects any token outside it.
    /// </summary>
    public sealed class SelectionPurposesTests
    {
        [Test]
        public void All_ContainsExactlyTheNineCanonicalPurposes()
        {
            // Arrange
            ControlledVocabulary purposes = SelectionPurposes.All;
            string[] expected =
            {
                "Active Branch exposure",
                "Basic Indexing",
                "Shop offer replacement",
                "Reward generation",
                "Random target selection",
                "Random ordering",
                "Future runtime effect",
                "Simulation policy sampling — non-gameplay",
                "Presentation variation — non-gameplay"
            };

            // Act & Assert
            Assert.That(purposes.Count, Is.EqualTo(9));
            for (int index = 0; index < expected.Length; index++)
                Assert.That(purposes.Contains(expected[index]), Is.True, expected[index]);
        }

        [Test]
        public void All_DoesNotContainUnregisteredPurpose()
        {
            Assert.That(SelectionPurposes.All.Contains("Nonexistent purpose"), Is.False);
        }

        [Test]
        public void All_IsCaseSensitive()
        {
            // Ordinal, case-sensitive membership (CAB-EVT-844): a differently-cased token is not a member.
            Assert.That(SelectionPurposes.All.Contains("random ordering"), Is.False);
        }
    }
}
