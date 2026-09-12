using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Cores file rules: contiguous one-based line positions, a designated final output
    /// naming a fixed-instruction line, and the per-kind line payload shape.
    /// </summary>
    public sealed class CoreRuleSetTests
    {
        [Test]
        public void ValidCores_ReportNothing()
        {
            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith();

            Assert.AreEqual(
                0,
                CatalogExtensionFixtures.CountFile(errors, CatalogExtensionFixtures.CoresFile),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void NonContiguousPositions_ReportPositionsNotContiguous()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""OPEN"" },
                        { ""position"": 3, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                    ],
                    ""finalOutputPosition"": 3
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.positions-not-contiguous"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void PositionsNotStartingAtOne_ReportPositionsNotContiguous()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 2, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                    ],
                    ""finalOutputPosition"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.positions-not-contiguous"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void FinalOutputOnAnOpenLine_ReportsFinalOutputNotFixedInstruction()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } },
                        { ""position"": 2, ""kind"": ""OPEN"" }
                    ],
                    ""finalOutputPosition"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.final-output-not-fixed-instruction"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void FinalOutputOnAFixedStructureLine_ReportsFinalOutputNotFixedInstruction()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""PARITY CHECK Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""OPEN"" },
                        {
                            ""position"": 2,
                            ""kind"": ""FIXED_STRUCTURE"",
                            ""predicate"": { ""register"": ""VALUE"", ""comparison"": ""IS_EVEN"", ""operand"": 0 },
                            ""contained"": { ""position"": 0, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                        }
                    ],
                    ""finalOutputPosition"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.final-output-not-fixed-instruction"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void FinalOutputBeyondTheLineCount_ReportsFinalOutputNotFixedInstruction()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } }
                    ],
                    ""finalOutputPosition"": 9
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.final-output-not-fixed-instruction"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AFixedStructureLineWithoutAPredicate_ReportsLineShape()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""PARITY CHECK Core"",
                    ""lines"": [
                        {
                            ""position"": 1,
                            ""kind"": ""FIXED_STRUCTURE"",
                            ""contained"": { ""position"": 0, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                        },
                        { ""position"": 2, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } }
                    ],
                    ""finalOutputPosition"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.line-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnOpenLineCarryingAnOperation_ReportsLineShape()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""OPEN"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } },
                        { ""position"": 2, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } }
                    ],
                    ""finalOutputPosition"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.line-shape"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnUnknownLineKind_ReportsUnknownLineKind()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""SOMETHING_ELSE"" },
                        { ""position"": 2, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } }
                    ],
                    ""finalOutputPosition"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "core.unknown-line-kind"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void AnIDOutsideTheCoreNamespace_ReportsIDFormat()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-INS-002"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } }
                    ],
                    ""finalOutputPosition"": 1
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "definition.id-format"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }

        [Test]
        public void ACoreCarryingItemFields_ReportsUnknownField()
        {
            const string cores = @"[
                {
                    ""id"": ""WB-CORE-001"",
                    ""displayName"": ""Tutorial Process 1 Core"",
                    ""rarity"": ""COMMON"",
                    ""lines"": [
                        { ""position"": 1, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } }
                    ],
                    ""finalOutputPosition"": 1
                }
            ]";

            IReadOnlyList<CatalogError> errors = CatalogExtensionFixtures.ValidateWith(
                (CatalogExtensionFixtures.CoresFile, cores)
            );

            Assert.IsTrue(
                CatalogTestFixtures.HasRule(errors, "definition.unknown-field"),
                CatalogExtensionFixtures.Describe(errors)
            );
        }
    }
}
