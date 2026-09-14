using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the shops file rules: a shop with rerolls enabled must name the pool a reroll draws
    /// from, and every offered content ID must resolve to an item.
    /// </summary>
    public sealed class ShopRuleSetTests
    {
        [Test]
        public void ValidShops_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.ShopsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ADependencyFixedOfferInADependenciesDisabledShop_ReportsDependencyOfferDisabled()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile.Replace(
                @"""content"": ""WB-INS-002""",
                @"""content"": ""WB-DEP-002"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "shop.dependency-offer-disabled"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ADependencyFixedOfferInADependenciesEnabledShop_IsAccepted()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile
                .Replace(@"""content"": ""WB-INS-002""", @"""content"": ""WB-DEP-002""")
                .Replace(@"""dependenciesEnabled"": false", @"""dependenciesEnabled"": true");

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops)
            );

            Assert.IsFalse(
                CatalogTestFixtures.HasRule(errors, "shop.dependency-offer-disabled"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ADependencyPoolMemberBoundToADependenciesDisabledShop_ReportsDependencyOfferDisabled()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile.Replace(
                @"""rerollsEnabled"": false",
                @"""rerollsEnabled"": true, ""rerollPool"": ""WB-POOL-001"""
            );
            string pools = CatalogExtensionFixtures.ValidPoolsFile.Replace(
                @"{ ""content"": ""WB-INS-003"" }",
                @"{ ""content"": ""WB-DEP-002"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops),
                (CatalogExtensionFixtures.PoolsFile, pools)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "shop.dependency-offer-disabled"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void RerollsEnabledWithoutARerollPool_ReportsRerollPoolMissing()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile.Replace(
                @"""rerollsEnabled"": false",
                @"""rerollsEnabled"": true"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "shop.reroll-pool-missing"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void RerollsEnabledWithARerollPool_ReportsNothing()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile
                .Replace(@"""rerollsEnabled"": false", @"""rerollsEnabled"": true")
                .Replace(
                    @"""fixedOffers"": [ { ""offerID"": ""OFF-001"", ""content"": ""WB-INS-002"", ""price"": 4 } ]",
                    @"""fixedOffers"": [ { ""offerID"": ""OFF-001"", ""content"": ""WB-INS-002"", ""price"": 4 } ], ""rerollPool"": ""WB-POOL-001"""
                );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops)
            );

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.ShopsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void RerollsDisabledWithoutARerollPool_ReportsNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.IsFalse(
                CatalogTestFixtures.HasRule(errors, "shop.reroll-pool-missing"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnOfferNamingAnUndefinedContentID_ReportsUnknownID()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile.Replace(
                @"""content"": ""WB-INS-002""",
                @"""content"": ""WB-INS-404"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ARerollPoolNamingAnInstruction_ReportsWrongKind()
        {
            string shops = CatalogExtensionFixtures.ValidShopsFile
                .Replace(@"""rerollsEnabled"": false", @"""rerollsEnabled"": true")
                .Replace(
                    @"""fixedOffers"": [ { ""offerID"": ""OFF-001"", ""content"": ""WB-INS-002"", ""price"": 4 } ]",
                    @"""fixedOffers"": [], ""rerollPool"": ""WB-INS-002"""
                );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ShopsFile, shops)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.wrong-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
