using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="ExecutionEvidenceHeader"/> and <see cref="RevisionStamp"/>: every
    /// reproduction identity is required, the revision-stamp list is non-empty, and a revision stamp
    /// requires both a name and a revision identity.
    /// </summary>
    public sealed class ExecutionEvidenceHeaderTests
    {
        [Test]
        public void FullHeader_RoundTrips()
        {
            // Arrange
            List<RevisionStamp> stamps = new() { new RevisionStamp("engine", "1") };
            List<InstanceID> directives = new() { new InstanceID(1) };
            List<InstanceID> dependencies = new() { new InstanceID(2) };
            List<InstanceID> patches = new() { new InstanceID(3) };
            InitialExecutionState initialState = new(new ValueAmount(5), new SignalValue(6), new ScoreValue(7));

            // Act
            ExecutionEvidenceHeader header = new(
                "EXEC:1",
                "COMP:1",
                "REV:1",
                "PROC:1",
                "CORE:1",
                "RULECFG:1",
                "SEED:1",
                stamps,
                directives,
                dependencies,
                patches,
                initialState);

            // Assert
            Assert.That(header.ExecutionIdentity, Is.EqualTo("EXEC:1"));
            Assert.That(header.CompilationIdentity, Is.EqualTo("COMP:1"));
            Assert.That(header.CompiledSourceRevision, Is.EqualTo("REV:1"));
            Assert.That(header.ProcessIdentity, Is.EqualTo("PROC:1"));
            Assert.That(header.CoreIdentity, Is.EqualTo("CORE:1"));
            Assert.That(header.ProcessRuleConfigurationIdentity, Is.EqualTo("RULECFG:1"));
            Assert.That(header.SessionSeedIdentity, Is.EqualTo("SEED:1"));
            Assert.That(header.RevisionStamps, Is.EqualTo(stamps));
            Assert.That(header.ActiveDirectiveInstances, Is.EqualTo(directives));
            Assert.That(header.InstalledDependencyInstances, Is.EqualTo(dependencies));
            Assert.That(header.RelevantPatchInstances, Is.EqualTo(patches));
            Assert.That(header.InitialState, Is.EqualTo(initialState));
        }

        [Test]
        public void EmptyRequiredString_Throws(
            [Values("execution", "compilation", "revision", "process", "core", "ruleConfig", "seed")] string which)
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionEvidenceHeader(
                which == "execution" ? "" : "EXEC:1",
                which == "compilation" ? "" : "COMP:1",
                which == "revision" ? "" : "REV:1",
                which == "process" ? "" : "PROC:1",
                which == "core" ? "" : "CORE:1",
                which == "ruleConfig" ? "" : "RULECFG:1",
                which == "seed" ? "" : "SEED:1",
                new List<RevisionStamp> { new RevisionStamp("engine", "1") },
                new List<InstanceID>(),
                new List<InstanceID>(),
                new List<InstanceID>(),
                MinimalInitialState()));
        }

        [Test]
        public void EmptyRevisionStampList_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ExecutionEvidenceHeader(
                "EXEC:1",
                "COMP:1",
                "REV:1",
                "PROC:1",
                "CORE:1",
                "RULECFG:1",
                "SEED:1",
                new List<RevisionStamp>(),
                new List<InstanceID>(),
                new List<InstanceID>(),
                new List<InstanceID>(),
                MinimalInitialState()));
        }

        [Test]
        public void RevisionStamp_EmptyNameOrRevision_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RevisionStamp("", "1"));
            Assert.Throws<ArgumentException>(() => _ = new RevisionStamp("engine", ""));
        }

        private static InitialExecutionState MinimalInitialState()
        {
            return new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0));
        }
    }
}
