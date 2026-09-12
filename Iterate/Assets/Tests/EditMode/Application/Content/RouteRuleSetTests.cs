using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the routes file rules: a route's Process and shop references resolve to a Process
    /// configuration and a shop respectively.
    /// </summary>
    public sealed class RouteRuleSetTests
    {
        [Test]
        public void ValidRoutes_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.RoutesFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownProcessReference_ReportsUnknownID()
        {
            string routes = CatalogExtensionFixtures.ValidRoutesFile.Replace(
                @"""process"": ""WB-PROC-001""",
                @"""process"": ""WB-PROC-404"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RoutesFile, routes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AProcessReferenceNamingAShop_ReportsWrongKind()
        {
            string routes = CatalogExtensionFixtures.ValidRoutesFile.Replace(
                @"""process"": ""WB-PROC-001""",
                @"""process"": ""WB-SHOP-001"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RoutesFile, routes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.wrong-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AShopReferenceNamingAProcess_ReportsWrongKind()
        {
            string routes = CatalogExtensionFixtures.ValidRoutesFile.Replace(
                @"""shop"": ""WB-SHOP-001""",
                @"""shop"": ""WB-PROC-001"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RoutesFile, routes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.wrong-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownShopReference_ReportsUnknownID()
        {
            string routes = CatalogExtensionFixtures.ValidRoutesFile.Replace(
                @"""shop"": ""WB-SHOP-001""",
                @"""shop"": ""WB-SHOP-404"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RoutesFile, routes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ARouteMissingItsShop_ReportsMissingField()
        {
            const string routes = @"[
                { ""id"": ""WB-ROUTE-001"", ""displayName"": ""PARITY CHECK"", ""process"": ""WB-PROC-001"" }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.RoutesFile, routes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "definition.missing-field"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
