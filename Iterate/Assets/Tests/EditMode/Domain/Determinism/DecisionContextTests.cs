using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests <see cref="DecisionContext.Derive"/> and the construction validation of
    /// <see cref="DecisionContextComponents"/>. The two known-answer hashes were computed from the
    /// canonical encoding fed through the reference-verified xxHash64 (seed 0), pinning the whole
    /// encode-then-hash chain. Sensitivity, component retention (CAB-EVT-845), stateless reproduction,
    /// and the absent-is-null-never-empty validation are all covered.
    /// </summary>
    public sealed class DecisionContextTests
    {
        [Test]
        public void Derive_MinimalFixture_MatchesKnownAnswerHash()
        {
            // Arrange
            DecisionContextComponents components = BuildMinimalComponents();

            // Act
            DecisionContext context = DecisionContext.Derive(components);

            // Assert
            Assert.That(context.Hash, Is.EqualTo(0x7FEB78C54B2CED6DUL));
        }

        [Test]
        public void Derive_FullyPopulatedFixture_MatchesKnownAnswerHash()
        {
            DecisionContextComponents components = BuildFullComponents();

            DecisionContext context = DecisionContext.Derive(components);

            Assert.That(context.Hash, Is.EqualTo(0x65128961C93AF024UL));
        }

        [Test]
        public void Derive_RetainsComponentsForDivergenceEvidence()
        {
            // Arrange
            DecisionContextComponents components = BuildFullComponents();

            // Act
            DecisionContext context = DecisionContext.Derive(components);

            // Assert
            Assert.That(context.Components, Is.EqualTo(components));
            Assert.That(context.Components.ProcessIdentity, Is.EqualTo("Prozeß-7"));
        }

        [Test]
        public void Derive_EqualComponents_ProduceEqualHashesAndContexts()
        {
            // Arrange & Act — two independent derivations of identical components (stateless reproduction).
            DecisionContext first = DecisionContext.Derive(BuildFullComponents());
            DecisionContext second = DecisionContext.Derive(BuildFullComponents());

            // Assert
            Assert.That(second.Hash, Is.EqualTo(first.Hash));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Derive_ChangingAnySingleComponent_ChangesHash()
        {
            // Arrange — each variant differs from the full baseline in exactly one component, including
            // the ordinal (1-of-a-kind) and one present-to-absent optional flip.
            ulong baseline = DecisionContext.Derive(BuildFullComponents()).Hash;
            DecisionContextComponents[] variants =
            {
                new("seed-43", "c1", "r1", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random ordering", 2),
                new("seed-42", "c2", "r1", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random ordering", 2),
                new("seed-42", "c1", "r2", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random ordering", 2),
                new("seed-42", "c1", "r1", "System-2", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random ordering", 2),
                new("seed-42", "c1", "r1", "System-1", "Prozeß-8", "scope-9", "evt-3", "origin-5", "Random ordering", 2),
                new("seed-42", "c1", "r1", "System-1", "Prozeß-7", "scope-8", "evt-3", "origin-5", "Random ordering", 2),
                new("seed-42", "c1", "r1", "System-1", "Prozeß-7", "scope-9", "evt-4", "origin-5", "Random ordering", 2),
                new("seed-42", "c1", "r1", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-6", "Random ordering", 2),
                new("seed-42", "c1", "r1", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random target selection", 2),
                new("seed-42", "c1", "r1", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random ordering", 3),
                new("seed-42", null, "r1", "System-1", "Prozeß-7", "scope-9", "evt-3", "origin-5", "Random ordering", 2)
            };

            // Act & Assert
            for (int index = 0; index < variants.Length; index++)
            {
                ulong variantHash = DecisionContext.Derive(variants[index]).Hash;
                Assert.That(variantHash, Is.Not.EqualTo(baseline), $"variant {index}");
            }
        }

        [Test]
        public void Derive_AdjacentOptionalSlotsSwapped_ProduceDifferentHashes()
        {
            // Arrange — the CAB-EVT-834 omission rule at the hash level: {A, absent, B} vs {A, B, absent}.
            DecisionContextComponents absentThenValue = new(
                "seed", "A", null, "B", null, null, null, null, "purpose", 1);
            DecisionContextComponents valueThenAbsent = new(
                "seed", "A", "B", null, null, null, null, null, "purpose", 1);

            // Act
            ulong first = DecisionContext.Derive(absentThenValue).Hash;
            ulong second = DecisionContext.Derive(valueThenAbsent).Hash;

            // Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void Constructor_NullRequiredSessionSeed_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                null, null, null, null, null, null, null, null, "purpose", 1));
        }

        [Test]
        public void Constructor_EmptyRequiredSessionSeed_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                "", null, null, null, null, null, null, null, "purpose", 1));
        }

        [Test]
        public void Constructor_NullRequiredPurpose_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, null, 1));
        }

        [Test]
        public void Constructor_EmptyRequiredPurpose_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "", 1));
        }

        [Test]
        public void Constructor_EmptyOptionalString_Throws()
        {
            // Absent is null, never the empty string (CAB-EVT-834 — an empty string is a data defect).
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                "seed", "", null, null, null, null, null, null, "purpose", 1));
        }

        [Test]
        public void Constructor_OrdinalZero_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "purpose", 0));
        }

        [Test]
        public void Constructor_OrdinalNegative_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "purpose", -1));
        }

        private static DecisionContextComponents BuildMinimalComponents()
        {
            return new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "purpose", 1);
        }

        private static DecisionContextComponents BuildFullComponents()
        {
            return new DecisionContextComponents(
                "seed-42",
                "c1",
                "r1",
                "System-1",
                "Prozeß-7",
                "scope-9",
                "evt-3",
                "origin-5",
                "Random ordering",
                2);
        }
    }
}
