using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Systems file rules: every stage carries exactly the reference its kind admits, and
    /// those references resolve to the right kind.
    /// </summary>
    public sealed class SystemRuleSetTests
    {
        [Test]
        public void ValidSystems_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.SystemsFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AProcessStageWithoutAProcess_ReportsStageShape()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""PROCESS"", ""process"": ""WB-PROC-001"" }",
                @"{ ""kind"": ""PROCESS"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.stage-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AShopStageWithoutAShop_ReportsStageShape()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""SHOP"", ""shop"": ""WB-SHOP-001"" }",
                @"{ ""kind"": ""SHOP"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.stage-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ARouteSelectionStageWithNoRoutes_ReportsStageShape()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""ROUTE_SELECTION"", ""routes"": [ ""WB-ROUTE-001"" ] }",
                @"{ ""kind"": ""ROUTE_SELECTION"", ""routes"": [] }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.stage-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AProcessStageCarryingAShop_ReportsStageShape()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""PROCESS"", ""process"": ""WB-PROC-001"" }",
                @"{ ""kind"": ""PROCESS"", ""process"": ""WB-PROC-001"", ""shop"": ""WB-SHOP-001"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.stage-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ARouteSelectionStageCarryingAProcess_ReportsStageShape()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""ROUTE_SELECTION"", ""routes"": [ ""WB-ROUTE-001"" ] }",
                @"{ ""kind"": ""ROUTE_SELECTION"", ""routes"": [ ""WB-ROUTE-001"" ], ""process"": ""WB-PROC-001"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.stage-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownStageKind_ReportsUnknownStageKind()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""SHOP"", ""shop"": ""WB-SHOP-001"" }",
                @"{ ""kind"": ""INTERLUDE"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.unknown-stage-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ARouteReferenceNamingAProcess_ReportsWrongKind()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"""routes"": [ ""WB-ROUTE-001"" ]",
                @"""routes"": [ ""WB-PROC-001"" ]"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.wrong-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownProcessStageReference_ReportsUnknownID()
        {
            string systems = CatalogExtensionFixtures.ValidSystemsFile.Replace(
                @"{ ""kind"": ""PROCESS"", ""process"": ""WB-PROC-001"" }",
                @"{ ""kind"": ""PROCESS"", ""process"": ""WB-PROC-404"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ASystemWithNoStages_ReportsStagesEmpty()
        {
            const string systems = @"[
                { ""id"": ""WB-SYS-001"", ""displayName"": ""System 1"", ""stages"": [] }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.SystemsFile, systems)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "system.stages-empty"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
