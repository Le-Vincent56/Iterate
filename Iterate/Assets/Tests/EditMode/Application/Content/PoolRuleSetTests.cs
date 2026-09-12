using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the acquisition-pools file rules: the selection method is controlled vocabulary, a
    /// player-choice pool carries a count that cannot exceed its membership, a reroll pool carries no
    /// count, and every member's content resolves.
    /// </summary>
    public sealed class PoolRuleSetTests
    {
        [Test]
        public void ValidPools_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.PoolsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void APlayerChoicePoolWithoutACount_ReportsChoiceCountMissing()
        {
            string pools = CatalogExtensionFixtures.ValidPoolsFile.Replace(
                @"""selectionCount"": 1,",
                string.Empty
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "pool.choice-count-missing"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ACountExceedingTheMembership_ReportsCountExceedsMembers()
        {
            string pools = CatalogExtensionFixtures.ValidPoolsFile.Replace(
                @"""selectionCount"": 1,",
                @"""selectionCount"": 3,"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "pool.count-exceeds-members"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ACountEqualToTheMembership_ReportsNothing()
        {
            string pools = CatalogExtensionFixtures.ValidPoolsFile.Replace(
                @"""selectionCount"": 1,",
                @"""selectionCount"": 2,"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.PoolsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AUniformWithoutReplacementPoolWithNoCount_ReportsNothing()
        {
            string pools = CatalogExtensionFixtures.ValidPoolsFile
                .Replace(@"""selectionMethod"": ""PLAYER_CHOICE"",", @"""selectionMethod"": ""UNIFORM_WITHOUT_REPLACEMENT"",")
                .Replace(@"""selectionCount"": 1,", string.Empty)
                .Replace(
                    @"""members"": [ { ""content"": ""WB-INS-002"" }, { ""content"": ""WB-INS-003"" } ]",
                    @"""members"": [ { ""content"": ""WB-INS-002"", ""price"": 4 }, { ""content"": ""WB-INS-003"", ""price"": 6 } ]"
                );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.PoolsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownSelectionMethod_ReportsUnknownSelectionMethod()
        {
            string pools = CatalogExtensionFixtures.ValidPoolsFile.Replace(
                @"""selectionMethod"": ""PLAYER_CHOICE""",
                @"""selectionMethod"": ""WEIGHTED_DRAW"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "pool.unknown-selection-method"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AMemberNamingAnUndefinedContentID_ReportsUnknownID()
        {
            string pools = CatalogExtensionFixtures.ValidPoolsFile.Replace(
                @"{ ""content"": ""WB-INS-003"" }",
                @"{ ""content"": ""WB-INS-404"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
