using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests <see cref="ParameterSet"/> completeness validation and the typed WB-PAR accessors.
    /// </summary>
    public sealed class ParameterSetTests
    {
        [Test]
        public void Constructor_FullRegister_Constructs()
        {
            ParameterSet parameters = new(BuildFullRegister());

            Assert.IsNotNull(parameters);
        }

        [Test]
        public void StartingRAM_FullRegister_ReadsFour()
        {
            ParameterSet parameters = new(BuildFullRegister());

            Assert.AreEqual(4, parameters.StartingRAM);
        }

        [Test]
        public void PassRatio_FullRegister_ReadsOne()
        {
            ParameterSet parameters = new(BuildFullRegister());

            Assert.AreEqual(1.0, parameters.PassRatio);
        }

        [Test]
        public void BenchmarkRatio_FullRegister_ReadsThree()
        {
            ParameterSet parameters = new(BuildFullRegister());

            Assert.AreEqual(3.0, parameters.BenchmarkRatio);
        }

        [Test]
        public void PatchSocketsPerRepositoryInstance_FullRegister_ReadsTwo()
        {
            ParameterSet parameters = new(BuildFullRegister());

            Assert.AreEqual(2, parameters.PatchSocketsPerRepositoryInstance);
        }

        [Test]
        public void Constructor_MissingRegisterID_ThrowsNamingID()
        {
            Dictionary<string, double> values = BuildFullRegister();
            values.Remove("WB-PAR-005");

            ArgumentException exception = Assert.Throws<ArgumentException>(() => _ = new ParameterSet(values));
            Assert.That(exception.Message, Does.Contain("WB-PAR-005"));
        }

        [Test]
        public void Constructor_UnknownRegisterID_ThrowsNamingID()
        {
            Dictionary<string, double> values = BuildFullRegister();
            values["WB-PAR-999"] = 1.0;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => _ = new ParameterSet(values));
            Assert.That(exception.Message, Does.Contain("WB-PAR-999"));
        }

        [Test]
        public void Constructor_FractionalIntegerParameter_ThrowsNamingID()
        {
            Dictionary<string, double> values = BuildFullRegister();
            values["WB-PAR-001"] = 3.5;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => _ = new ParameterSet(values));
            Assert.That(exception.Message, Does.Contain("WB-PAR-001"));
        }

        [Test]
        public void Constructor_NullValues_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ParameterSet(null));
        }

        /// <summary>
        /// Builds a complete, canonical WB-PAR register (Balance section 4) as a mutable dictionary so
        /// individual tests can perturb single entries.
        /// </summary>
        /// <returns>The 30 register entries keyed by WB-PAR ID.</returns>
        private static Dictionary<string, double> BuildFullRegister()
        {
            return new Dictionary<string, double>
            {
                { "WB-PAR-001", 3 },
                { "WB-PAR-002", 12 },
                { "WB-PAR-003", 20 },
                { "WB-PAR-004", 30 },
                { "WB-PAR-005", 4 },
                { "WB-PAR-006", 5 },
                { "WB-PAR-007", 6 },
                { "WB-PAR-008", 6 },
                { "WB-PAR-009", 6 },
                { "WB-PAR-010", 5 },
                { "WB-PAR-011", 4 },
                { "WB-PAR-012", 3 },
                { "WB-PAR-013", 3 },
                { "WB-PAR-014", 3 },
                { "WB-PAR-015", 9 },
                { "WB-PAR-016", 6 },
                { "WB-PAR-017", 0 },
                { "WB-PAR-018", 0 },
                { "WB-PAR-019", 1 },
                { "WB-PAR-020", 2 },
                { "WB-PAR-021", 3 },
                { "WB-PAR-022", 1.0 },
                { "WB-PAR-023", 1.75 },
                { "WB-PAR-024", 3.0 },
                { "WB-PAR-026", 2 },
                { "WB-PAR-028", 1 },
                { "WB-PAR-029", 2 },
                { "WB-PAR-030", 3 },
                { "WB-PAR-035", 0.5 },
                { "WB-PAR-036", 2 }
            };
        }
    }
}
