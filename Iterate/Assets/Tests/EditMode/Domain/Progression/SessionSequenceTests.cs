using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Drives the whole child through <see cref="SessionSequenceDriver"/> over hand-built fixtures:
    /// Session to Branch to Process to Buffer to Build to Compilation to Execution, with the Bytes
    /// ledger debited from the breakdown and the counters advanced by the run. These are the seam
    /// tests — each unit is already covered by its own suite, so what is asserted here is that the
    /// pieces compose.
    /// </summary>
    public sealed class SessionSequenceTests
    {
        [Test]
        public void AScriptedProcess_RunsEndToEndFromSessionToExecutionRecord()
        {
            SessionSequenceDriver driver = Driver();
            ProcessConfigurationDefinition configuration = TutorialConfiguration();

            Assert.IsTrue(driver.CreateProcess(configuration).Succeeded);
            CompilationAttempt attempt = driver.Compile();

            Assert.IsTrue(attempt.Committed, attempt.BlockReason.ToString());
            ExecutionRecord record = driver.Execute(attempt.Source);

            Assert.IsNotNull(record.Header);
            Assert.AreEqual(1, driver.Process.Counters.ExecutionsRun, "the run advanced the counters.");
        }

        [Test]
        public void TheCompilationBreakdown_IsDebitedFromTheProcessLedger()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration());
            driver.Compile();
            int before = driver.Process.Bytes.Balance.Value;

            driver.Install(new InstanceID(4), 6);
            CompilationAttempt attempt = driver.Compile();

            Assert.IsTrue(attempt.Committed, attempt.BlockReason.ToString());
            Assert.AreEqual(CompilationClassification.OrdinaryEdited, attempt.Breakdown.Classification);
            Assert.Greater(attempt.Breakdown.FinalCost, 0, "an edited compilation without a rule costs Bytes.");
            Assert.AreEqual(
                before - attempt.Breakdown.FinalCost,
                driver.Process.Bytes.Balance.Value,
                "the caller debits the breakdown, which is Compilation's standing contract."
            );
        }

        [Test]
        public void AnEditedCompilation_UnderAFreeCompilationRule_CostsNothing()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration(withProcessRule: true));
            driver.Compile();
            int before = driver.Process.Bytes.Balance.Value;

            driver.Install(new InstanceID(4), 6);
            CompilationAttempt attempt = driver.Compile();

            Assert.IsTrue(attempt.Committed, attempt.BlockReason.ToString());
            Assert.AreEqual(CompilationClassification.OrdinaryEdited, attempt.Breakdown.Classification);
            Assert.AreEqual(0, attempt.Breakdown.FinalCost);
            Assert.AreEqual(before, driver.Process.Bytes.Balance.Value, "a free compilation debits nothing.");
        }

        [Test]
        public void ACompilationOnlyProcessRule_IsNotCarriedOnTheExecutionRequest()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration(withProcessRule: true));
            CompilationAttempt attempt = driver.Compile();

            ExecutionRequest request = driver.BuildRequest(attempt.Source);

            Assert.IsNotNull(driver.Process.ProcessRule, "the Process state exposes the rule unconditionally.");
            Assert.Greater(driver.Process.CompilationEffects.Count, 0, "and its compilation effects.");
            Assert.IsNull(
                request.Configuration.ProcessRule,
                "but a COMPILATION-only rule must not reach the engine, which reads mere presence as counter evidence."
            );
        }

        [Test]
        public void ACompilationOnlyProcessRule_ProducesNoProcessCounterEvidence()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration(withProcessRule: true));
            CompilationAttempt attempt = driver.Compile();

            ExecutionRecord record = driver.Execute(attempt.Source);

            Assert.AreEqual(0, record.CounterHistory.Count, "no counter exists, so none is stamped.");
        }

        [Test]
        public void ArrivalsAndInstallsCompose_AcrossTwoExecutions()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration());

            CompilationAttempt first = driver.Compile();
            driver.Execute(first.Source);
            ArrivalResult arrived = driver.Arrive(1);

            Assert.IsTrue(arrived.Succeeded, "the after-one moment fires.");
            InstanceID admitted = arrived.Arrived[0].InstanceID;
            Assert.IsTrue(driver.Install(admitted, 6).Succeeded, "the arrival is installable.");

            CompilationAttempt second = driver.Compile();

            Assert.IsTrue(second.Committed, second.BlockReason.ToString());
            driver.Execute(second.Source);
            Assert.AreEqual(2, driver.Process.Counters.ExecutionsRun);
        }

        [Test]
        public void ARemovedInstruction_ReturnsToTheBufferAndIsInstallableAgain()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration());
            driver.Install(new InstanceID(4), 6);

            Assert.IsTrue(driver.Remove(6).Succeeded);
            Assert.AreEqual(
                BufferItemState.Present,
                driver.Process.Buffer.StateOf(new InstanceID(4)),
                "a removal returns the instance to the Buffer."
            );
            Assert.IsTrue(driver.Install(new InstanceID(4), 7).Succeeded, "and it installs again elsewhere.");
        }

        [Test]
        public void TheExecutionRequest_CarriesBothRevisionStampsAndTheStarterDependency()
        {
            SessionSequenceDriver driver = Driver();
            driver.CreateProcess(TutorialConfiguration());
            CompilationAttempt attempt = driver.Compile();

            ExecutionRequest request = driver.BuildRequest(attempt.Source);

            Assert.AreEqual(2, request.RevisionStamps.Count, "the engine requires at least one stamp.");
            Assert.AreEqual(1, request.InstalledDependencies.Count);
            Assert.AreEqual(
                driver.Session.StarterDependency.InstanceID,
                request.InstalledDependencies[0].InstanceID
            );
        }

        /// <summary>
        /// Builds a driver over the fixture catalog and a Repository holding the Buffer content the
        /// Tutorial configuration scripts.
        /// </summary>
        /// <returns>The driver.</returns>
        private static SessionSequenceDriver Driver()
        {
            return new SessionSequenceDriver(
                ProgressionFixtures.ProcessCatalog(),
                ProgressionFixtures.Archetype(
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ValuePlusTwo,
                    ProgressionFixtures.ScorePlusValue,
                    ProgressionFixtures.ValuePlusThree,
                    ProgressionFixtures.ValuePlusThree
                ),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );
        }

        /// <summary>
        /// A scripted Tutorial-shaped configuration over the fixture Core: three items pre-installed,
        /// one arriving after execution one, and two open positions left to edit into.
        /// </summary>
        /// <param name="withProcessRule">Whether the configuration names the free-compilation rule.</param>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition TutorialConfiguration(bool withProcessRule = false)
        {
            BufferLoadSpec load = new(
                BufferLoadPolicy.Scripted,
                new[] { ProgressionFixtures.ValuePlusThree },
                new ArrivalLoadSpec[] { new(1, new[] { ProgressionFixtures.ValuePlusThree }) },
                0,
                new int[0]
            );

            return new ProcessConfigurationDefinition(
                new ProcessID("WB-PROC-002"),
                "Tutorial Process 2",
                ProcessRole.Tutorial2,
                new CoreID("WB-CORE-002"),
                withProcessRule ? new ProcessRuleID("WB-PRC-002") : null,
                new ProcessThresholdSpec(18, 32, 80),
                4,
                true,
                3,
                3,
                5,
                new InitialSourceSpec[]
                {
                    new(3, ProgressionFixtures.ValuePlusTwo),
                    new(4, ProgressionFixtures.ValuePlusTwo),
                    new(5, ProgressionFixtures.ScorePlusValue)
                },
                load,
                null,
                null,
                new RewardPackageID("WB-RWD-002"),
                null
            );
        }
    }
}
