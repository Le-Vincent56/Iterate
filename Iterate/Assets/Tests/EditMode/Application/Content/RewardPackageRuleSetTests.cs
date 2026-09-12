using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the reward-packages file rules: each component's reference resolves to the kind that
    /// component awards, amount-bearing components carry a positive amount, and the tier and kind
    /// tokens are controlled vocabulary.
    /// </summary>
    public sealed class RewardPackageRuleSetTests
    {
        [Test]
        public void ValidRewardPackages_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.RewardsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void APoolChoiceNamingAnInstruction_ReportsReferenceKindMismatch()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"{ ""tier"": ""PASS"", ""kind"": ""POOL_CHOICE"", ""reference"": ""WB-POOL-001"" }",
                @"{ ""tier"": ""PASS"", ""kind"": ""POOL_CHOICE"", ""reference"": ""WB-INS-002"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.reference-kind-mismatch"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ACachedPatchNamingAPool_ReportsReferenceKindMismatch()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"{ ""tier"": ""OPTIMIZE"", ""kind"": ""CACHED_PATCH"", ""reference"": ""WB-PAT-001"" }",
                @"{ ""tier"": ""OPTIMIZE"", ""kind"": ""CACHED_PATCH"", ""reference"": ""WB-POOL-001"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.reference-kind-mismatch"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AGuaranteedContentComponentResolvesToAnItem()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"{ ""tier"": ""OPTIMIZE"", ""kind"": ""CACHED_PATCH"", ""reference"": ""WB-PAT-001"" }",
                @"{ ""tier"": ""PASS"", ""kind"": ""GUARANTEED_CONTENT"", ""reference"": ""WB-STR-002"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.RewardsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AZeroTokenAmount_ReportsAmountNotPositive()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"""kind"": ""TOKENS"", ""amount"": 4",
                @"""kind"": ""TOKENS"", ""amount"": 0"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.amount-not-positive"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ANegativeRAMSetAmount_ReportsAmountNotPositive()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"""kind"": ""TOKENS"", ""amount"": 4",
                @"""kind"": ""RAM_SET"", ""amount"": -5"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.amount-not-positive"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnAmountBearingComponentWithoutAnAmount_ReportsComponentShape()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"{ ""tier"": ""PASS"", ""kind"": ""TOKENS"", ""amount"": 4 }",
                @"{ ""tier"": ""PASS"", ""kind"": ""TOKENS"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.component-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AContentBearingComponentCarryingAnAmount_ReportsComponentShape()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"{ ""tier"": ""PASS"", ""kind"": ""POOL_CHOICE"", ""reference"": ""WB-POOL-001"" }",
                @"{ ""tier"": ""PASS"", ""kind"": ""POOL_CHOICE"", ""reference"": ""WB-POOL-001"", ""amount"": 1 }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.component-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownTier_ReportsUnknownTier()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"""tier"": ""OPTIMIZE""",
                @"""tier"": ""PERFECT"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.unknown-tier"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownComponentKind_ReportsUnknownComponentKind()
        {
            string rewards = CatalogExtensionFixtures.ValidRewardsFile.Replace(
                @"""kind"": ""TOKENS"", ""amount"": 4",
                @"""kind"": ""GLORY"", ""amount"": 4"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RewardsFile, rewards)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reward.unknown-component-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
