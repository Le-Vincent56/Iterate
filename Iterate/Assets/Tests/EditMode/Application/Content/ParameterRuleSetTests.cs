using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the parameter rule set: the 30-ID completeness contract, unknown and duplicate IDs, the
    /// integer/ratio value discipline, and the file-shape and row-shape rules.
    /// </summary>
    public sealed class ParameterRuleSetTests
    {
        private const string ManifestFileName = "parameters.json";

        private const string ParametersManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""parameters.json"", ""category"": ""PARAMETERS"" } ]
        }";

        private static IReadOnlyList<CatalogError> ValidateParameters(string parametersJson)
        {
            return Validate(ParametersManifest, (ManifestFileName, parametersJson));
        }

        [Test]
        public void Validate_AllThirtyParameters_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = ValidateParameters(ValidParametersFile);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_FileNotArray_ReportsFileNotArray()
        {
            IReadOnlyList<CatalogError> errors = ValidateParameters(@"{ ""WB-PAR-001"": 3 }");

            Assert.IsTrue(HasError(errors, ManifestFileName, "parameters.file-not-array"));
        }

        [Test]
        public void Validate_RowMissingValue_ReportsRowInvalid()
        {
            string json = @"[ { ""id"": ""WB-PAR-001"", ""name"": ""Systems per Session"", ""unit"": ""count"", ""scope"": ""GLOBAL"" } ]";

            IReadOnlyList<CatalogError> errors = ValidateParameters(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "parameters.row-invalid"));
        }

        [Test]
        public void Validate_MissingRequiredID_ReportsMissingID()
        {
            // The full register with WB-PAR-036 removed.
            string json = @"[
                { ""id"": ""WB-PAR-001"", ""name"": ""a"", ""value"": 3, ""unit"": ""count"", ""scope"": ""GLOBAL"" }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateParameters(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "parameters.missing-id"));
        }

        [Test]
        public void Validate_UnknownID_ReportsUnknownID()
        {
            string json = ValidParametersFile.Replace(
                @"{ ""id"": ""WB-PAR-036"", ""name"": ""Patch sockets per Repository instance"", ""value"": 2, ""unit"": ""count"", ""scope"": ""GLOBAL"" }",
                @"{ ""id"": ""WB-PAR-036"", ""name"": ""Patch sockets per Repository instance"", ""value"": 2, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
                { ""id"": ""WB-PAR-099"", ""name"": ""Mystery"", ""value"": 1, ""unit"": ""count"", ""scope"": ""GLOBAL"" }");

            IReadOnlyList<CatalogError> errors = ValidateParameters(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "parameters.unknown-id"));
        }

        [Test]
        public void Validate_DuplicateID_ReportsDuplicateID()
        {
            string json = ValidParametersFile.Replace(
                @"{ ""id"": ""WB-PAR-002"", ""name"": ""Processes per Session"", ""value"": 12, ""unit"": ""count"", ""scope"": ""GLOBAL"" }",
                @"{ ""id"": ""WB-PAR-001"", ""name"": ""Processes per Session"", ""value"": 12, ""unit"": ""count"", ""scope"": ""GLOBAL"" }");

            IReadOnlyList<CatalogError> errors = ValidateParameters(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "parameters.duplicate-id"));
        }

        [Test]
        public void Validate_FractionalIntegerParameter_ReportsNonInteger()
        {
            string json = ValidParametersFile.Replace(
                @"{ ""id"": ""WB-PAR-005"", ""name"": ""Starting RAM"", ""value"": 4, ""unit"": ""count"", ""scope"": ""GLOBAL"" }",
                @"{ ""id"": ""WB-PAR-005"", ""name"": ""Starting RAM"", ""value"": 4.5, ""unit"": ""count"", ""scope"": ""GLOBAL"" }");

            IReadOnlyList<CatalogError> errors = ValidateParameters(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "parameters.non-integer"));
        }
    }
}
