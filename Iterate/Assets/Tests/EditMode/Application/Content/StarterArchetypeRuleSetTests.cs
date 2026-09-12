using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Starter Archetypes file rules: the starting Repository resolves to items and admits
    /// duplicates, and the starter Dependency reference resolves to a Dependency. The starter
    /// Dependency's zero-RAM invariant is not a validator rule — it needs cross-file state, and is
    /// enforced where the Session is built instead.
    /// </summary>
    public sealed class StarterArchetypeRuleSetTests
    {
        [Test]
        public void ValidArchetypes_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.ArchetypesFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ADuplicateStartingItem_IsLegal()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.IsFalse(
                CatalogTestFixtures.HasRule(errors, "definition.duplicate-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AStarterDependencyNamingAnInstruction_ReportsStarterDependencyNotDependency()
        {
            string archetypes = CatalogExtensionFixtures.ValidArchetypesFile.Replace(
                @"""starterDependency"": ""WB-DEP-001""",
                @"""starterDependency"": ""WB-INS-002"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ArchetypesFile, archetypes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "archetype.starter-dependency-not-dependency"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownStarterDependency_ReportsUnknownID()
        {
            string archetypes = CatalogExtensionFixtures.ValidArchetypesFile.Replace(
                @"""starterDependency"": ""WB-DEP-001""",
                @"""starterDependency"": ""WB-DEP-404"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ArchetypesFile, archetypes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownStartingItem_ReportsUnknownID()
        {
            string archetypes = CatalogExtensionFixtures.ValidArchetypesFile.Replace(
                @"""WB-INS-003""",
                @"""WB-INS-404"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ArchetypesFile, archetypes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnEmptyStartingRepository_ReportsStartingRepositoryEmpty()
        {
            string archetypes = CatalogExtensionFixtures.ValidArchetypesFile.Replace(
                @"""startingRepository"": [ ""WB-INS-002"", ""WB-INS-002"", ""WB-INS-003"" ]",
                @"""startingRepository"": []"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ArchetypesFile, archetypes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "archetype.starting-repository-empty"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
