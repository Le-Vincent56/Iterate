using System.Text.RegularExpressions;
using NUnit.Framework;
using Iterate.Infrastructure.Telemetry;

namespace Iterate.Infrastructure.Telemetry.Tests
{
    /// <summary>
    /// Tests <see cref="GuidTelemetryRunIdentitySource"/>: each identity is fresh and has the fixed shape.
    /// </summary>
    public sealed class GuidTelemetryRunIdentitySourceTests
    {
        [Test]
        public void Next_TwoIdentities_Differ()
        {
            GuidTelemetryRunIdentitySource source = new();

            Assert.AreNotEqual(source.Next(), source.Next());
        }

        [Test]
        public void Next_HasTheRunPrefixAndThirtyTwoHexDigits()
        {
            GuidTelemetryRunIdentitySource source = new();

            StringAssert.IsMatch("^run-[0-9a-f]{32}$", source.Next());
            Assert.IsTrue(Regex.IsMatch(source.Next(), "^run-[0-9a-f]{32}$"));
        }
    }
}
