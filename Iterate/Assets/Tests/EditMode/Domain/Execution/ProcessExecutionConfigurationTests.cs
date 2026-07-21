using System;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that <see cref="ProcessExecutionConfiguration"/> requires all seven header identities
    /// non-empty and a non-null thresholds record, and round-trips every property.
    /// </summary>
    public sealed class ProcessExecutionConfigurationTests
    {
        private static ProcessThresholds ValidThresholds()
        {
            return new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));
        }

        [Test]
        public void Constructor_AllPresent_RoundTrips()
        {
            ProcessThresholds thresholds = ValidThresholds();

            ProcessExecutionConfiguration configuration = new(
                "exec",
                "compilation",
                "source-rev",
                "process",
                "core",
                "rule-config",
                "session-seed",
                thresholds);

            Assert.AreEqual("exec", configuration.ExecutionIdentity);
            Assert.AreEqual("compilation", configuration.CompilationIdentity);
            Assert.AreEqual("source-rev", configuration.CompiledSourceRevision);
            Assert.AreEqual("process", configuration.ProcessIdentity);
            Assert.AreEqual("core", configuration.CoreIdentity);
            Assert.AreEqual("rule-config", configuration.ProcessRuleConfigurationIdentity);
            Assert.AreEqual("session-seed", configuration.SessionSeedIdentity);
            Assert.AreEqual(thresholds, configuration.Thresholds);
        }

        [Test]
        public void Constructor_EmptyExecutionIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                string.Empty, "compilation", "source-rev", "process", "core", "rule-config", "session-seed", ValidThresholds()));
        }

        [Test]
        public void Constructor_EmptyCompilationIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", string.Empty, "source-rev", "process", "core", "rule-config", "session-seed", ValidThresholds()));
        }

        [Test]
        public void Constructor_EmptyCompiledSourceRevision_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", "compilation", string.Empty, "process", "core", "rule-config", "session-seed", ValidThresholds()));
        }

        [Test]
        public void Constructor_EmptyProcessIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", "compilation", "source-rev", string.Empty, "core", "rule-config", "session-seed", ValidThresholds()));
        }

        [Test]
        public void Constructor_EmptyCoreIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", "compilation", "source-rev", "process", string.Empty, "rule-config", "session-seed", ValidThresholds()));
        }

        [Test]
        public void Constructor_EmptyProcessRuleConfigurationIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", "compilation", "source-rev", "process", "core", string.Empty, "session-seed", ValidThresholds()));
        }

        [Test]
        public void Constructor_EmptySessionSeedIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", "compilation", "source-rev", "process", "core", "rule-config", string.Empty, ValidThresholds()));
        }

        [Test]
        public void Constructor_NullThresholds_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessExecutionConfiguration(
                "exec", "compilation", "source-rev", "process", "core", "rule-config", "session-seed", null));
        }
    }
}
