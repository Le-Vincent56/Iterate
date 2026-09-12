using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Process-configurations file rules: strictly ascending thresholds, at least one
    /// execution, non-empty scripted arrival moments, a drawn load that cannot exceed Active Branch
    /// capacity, exposure only on a drawn load, and ID references that resolve to the right kind.
    /// </summary>
    public sealed class ProcessConfigurationRuleSetTests
    {
        private const string DrawnProcessTemplate = @"[
            {
                ""id"": ""WB-PROC-003"",
                ""displayName"": ""PARITY CHECK"",
                ""role"": ""ROUTE_PROCESS_3"",
                ""core"": ""WB-CORE-001"",
                ""thresholds"": { ""pass"": 16, ""optimize"": 28, ""benchmark"": 48 },
                ""executions"": 4,
                ""mandatoryExecutions"": false,
                ""startingBytes"": 3,
                ""bufferCapacity"": 5,
                ""sourceCapacity"": 1,
                ""initialSource"": [],
                ""bufferLoad"": { ""policy"": ""DRAWN"", ""initialCount"": INITIAL, ""arrivalsAfterExecutions"": ARRIVALS },
                ""exposure"": { ""guaranteed"": [ ""WB-STR-002"" ] },
                ""activeBranch"": { ""capacity"": CAPACITY, ""required"": [], ""quarantined"": [], ""recommendedTags"": [], ""cautionTags"": [] },
                ""rewardPackage"": ""WB-RWD-001""
            }
        ]";

        [Test]
        public void ValidProcessConfigurations_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.ProcessesFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ThresholdsNotAscending_ReportThresholdsNotAscending()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""thresholds"": { ""pass"": 20, ""optimize"": 30, ""benchmark"": 36 }",
                @"""thresholds"": { ""pass"": 30, ""optimize"": 30, ""benchmark"": 36 }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.thresholds-not-ascending"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void BenchmarkBelowOptimize_ReportsThresholdsNotAscending()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""thresholds"": { ""pass"": 20, ""optimize"": 30, ""benchmark"": 36 }",
                @"""thresholds"": { ""pass"": 20, ""optimize"": 30, ""benchmark"": 25 }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.thresholds-not-ascending"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ZeroExecutions_ReportsExecutionsBelowOne()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""executions"": 3",
                @"""executions"": 0"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.executions-below-one"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnEmptyScriptedArrivalMoment_ReportsScriptedMomentEmpty()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""arrivals"": [ { ""afterExecution"": 1, ""items"": [""WB-INS-003""] } ]",
                @"""arrivals"": [ { ""afterExecution"": 1, ""items"": [] } ]"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.scripted-moment-empty"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AScriptedMomentExceedingBufferCapacity_IsLegal()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""arrivals"": [ { ""afterExecution"": 1, ""items"": [""WB-INS-003""] } ]",
                @"""arrivals"": [ { ""afterExecution"": 1, ""items"": [""WB-INS-003"", ""WB-INS-002"", ""WB-INS-003""] } ]"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.ProcessesFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ADrawnLoadWithinBranchCapacity_ReportsNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, DrawnProcess("3", "[1, 2, 3]", "9"))
            );

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.ProcessesFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ADrawnLoadExceedingBranchCapacity_ReportsDrawnCountExceedsBranch()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, DrawnProcess("8", "[1, 2, 3]", "9"))
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.drawn-count-exceeds-branch"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ExposureOnAScriptedLoad_ReportsExposureWithoutDrawn()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""rewardPackage"": ""WB-RWD-001""",
                @"""exposure"": { ""guaranteed"": [ ""WB-INS-002"" ] }, ""rewardPackage"": ""WB-RWD-001"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.exposure-without-drawn"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownCoreReference_ReportsUnknownID()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""core"": ""WB-CORE-001""",
                @"""core"": ""WB-CORE-404"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ACoreReferenceNamingAnInstruction_ReportsWrongKind()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""core"": ""WB-CORE-001""",
                @"""core"": ""WB-INS-002"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.wrong-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownRole_ReportsUnknownRole()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""role"": ""TUTORIAL_1""",
                @"""role"": ""TUTORIAL_9"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.unknown-role"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownBufferLoadPolicy_ReportsUnknownLoadPolicy()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""policy"": ""SCRIPTED""",
                @"""policy"": ""IMPROVISED"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "process.unknown-load-policy"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnInitialSourceContentReference_IsResolved()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"{ ""position"": 2, ""content"": ""WB-INS-002"" }",
                @"{ ""position"": 2, ""content"": ""WB-INS-404"" }"
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.unknown-id"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AProcessRuleReferenceNamingAnInstruction_ReportsWrongKind()
        {
            string processes = CatalogExtensionFixtures.ValidProcessesFile.Replace(
                @"""processRule"": ""WB-PRC-001""",
                @"""processRule"": ""WB-INS-002"""
            );

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.ProcessesFile, processes)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "reference.wrong-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        /// <summary>
        /// Builds a drawn-load Process configuration with the given draw counts and Branch capacity.
        /// </summary>
        /// <param name="initialCount">The initial drawn count, as JSON.</param>
        /// <param name="arrivals">The arrival execution list, as a JSON array.</param>
        /// <param name="capacity">The Active Branch capacity, as JSON.</param>
        /// <returns>The Process-configurations file JSON.</returns>
        private static string DrawnProcess(string initialCount, string arrivals, string capacity)
        {
            return DrawnProcessTemplate
                .Replace("INITIAL", initialCount)
                .Replace("ARRIVALS", arrivals)
                .Replace("CAPACITY", capacity);
        }
    }
}
