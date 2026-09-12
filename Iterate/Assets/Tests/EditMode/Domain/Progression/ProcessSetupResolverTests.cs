using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Process setup resolver: the configuration's authored baseline, then process-setup
    /// effects folded over it — absolute settings first, then additive adjustments — with floors that
    /// keep a resolved Process playable. Runs before Branch configuration, because a Utility may widen
    /// the Branch the player is about to fill.
    /// </summary>
    public sealed class ProcessSetupResolverTests
    {
        [Test]
        public void Resolve_WithNoEffects_ReturnsTheAuthoredBaseline()
        {
            ProcessSetup setup = Resolve(Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9));

            Assert.AreEqual(5, setup.BufferCapacity);
            Assert.AreEqual(3, setup.StartingBytes);
            Assert.AreEqual(9, setup.BranchCapacity);
            Assert.AreEqual(4, setup.Executions);
            Assert.IsFalse(setup.MandatoryExecutions);
        }

        [Test]
        public void Resolve_WithoutAnActiveBranch_TakesBranchCapacityFromTheParameters()
        {
            ProcessSetup setup = Resolve(Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: null));

            Assert.AreEqual(
                ProgressionFixtures.Parameters().StandardActiveBranchCapacity,
                setup.BranchCapacity,
                "a tutorial Process has no authored Branch, so the standard capacity applies."
            );
        }

        [Test]
        public void Resolve_AnAdditiveEffect_AdjustsTheBaseline()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("STARTING_BYTES", 1, false)
            );

            Assert.AreEqual(4, setup.StartingBytes);
        }

        [Test]
        public void Resolve_AnAbsoluteEffect_ReplacesTheBaseline()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("STARTING_BYTES", 7, true)
            );

            Assert.AreEqual(7, setup.StartingBytes);
        }

        [Test]
        public void Resolve_AppliesEveryAbsoluteBeforeAnyAdditive()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("STARTING_BYTES", 1, false),
                Effect("STARTING_BYTES", 7, true)
            );

            Assert.AreEqual(8, setup.StartingBytes, "the absolute set lands first, then the addition.");
        }

        [Test]
        public void Resolve_AppliesAbsolutesInListOrder()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("STARTING_BYTES", 7, true),
                Effect("STARTING_BYTES", 2, true)
            );

            Assert.AreEqual(2, setup.StartingBytes, "the last absolute in list order wins.");
        }

        [Test]
        public void Resolve_WidensTheInstructionBuffer()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("INSTRUCTION_BUFFER_CAPACITY", 1, false)
            );

            Assert.AreEqual(6, setup.BufferCapacity);
        }

        [Test]
        public void Resolve_WidensTheActiveBranch()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("ACTIVE_BRANCH_CAPACITY", 1, false)
            );

            Assert.AreEqual(10, setup.BranchCapacity);
        }

        [Test]
        public void Resolve_FloorsBytesAtZero()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("STARTING_BYTES", -9, false)
            );

            Assert.AreEqual(0, setup.StartingBytes, "a Process can start with no Bytes, never with negative Bytes.");
        }

        [Test]
        public void Resolve_FloorsCapacitiesAtOne()
        {
            ProcessSetup setup = Resolve(
                Configuration(bufferCapacity: 5, startingBytes: 3, branchCapacity: 9),
                Effect("INSTRUCTION_BUFFER_CAPACITY", -9, false),
                Effect("ACTIVE_BRANCH_CAPACITY", -9, false)
            );

            Assert.AreEqual(1, setup.BufferCapacity, "a Buffer always has at least one slot.");
            Assert.AreEqual(1, setup.BranchCapacity);
        }

        [Test]
        public void Resolve_CarriesExecutionsAndMandatoryThrough()
        {
            ProcessConfigurationDefinition configuration = Configuration(
                bufferCapacity: 3,
                startingBytes: 3,
                branchCapacity: null,
                executions: 4,
                mandatoryExecutions: true
            );

            ProcessSetup setup = Resolve(configuration);

            Assert.AreEqual(4, setup.Executions);
            Assert.IsTrue(setup.MandatoryExecutions);
        }

        [Test]
        public void Resolve_AnEffectOutsideTheProcessSetupDomain_Throws()
        {
            EffectDefinition execution = new(
                PhaseDomain.Execution,
                null,
                new ConfigurationModificationOperation("STARTING_BYTES", 1, false),
                null,
                null,
                StackingMode.AdditiveParameter,
                null
            );

            Assert.Throws<ArgumentException>(() => _ = new ActiveSetupEffect("k", "n", execution));
        }

        [Test]
        public void Resolve_AnEffectThatIsNotAConfigurationModification_Throws()
        {
            EffectDefinition wrong = new(
                PhaseDomain.ProcessSetup,
                null,
                new OperationModificationOperation(1),
                null,
                null,
                StackingMode.AdditiveParameter,
                null
            );

            Assert.Throws<ArgumentException>(() => _ = new ActiveSetupEffect("k", "n", wrong));
        }

        /// <summary>
        /// Resolves a configuration against the fixture parameters and the given setup effects.
        /// </summary>
        /// <param name="configuration">The Process configuration.</param>
        /// <param name="effects">The active process-setup effects.</param>
        /// <returns>The resolved setup.</returns>
        private static ProcessSetup Resolve(
            ProcessConfigurationDefinition configuration,
            params ActiveSetupEffect[] effects
        )
        {
            return ProcessSetupResolver.Resolve(configuration, ProgressionFixtures.Parameters(), effects);
        }

        /// <summary>
        /// Builds a process-setup effect adjusting one configuration setting.
        /// </summary>
        /// <param name="setting">The configuration-setting token.</param>
        /// <param name="amount">The signed adjustment or absolute value.</param>
        /// <param name="setsAbsolute">Whether the amount sets the value absolutely.</param>
        /// <returns>The active setup effect.</returns>
        private static ActiveSetupEffect Effect(string setting, int amount, bool setsAbsolute)
        {
            EffectDefinition definition = new(
                PhaseDomain.ProcessSetup,
                null,
                new ConfigurationModificationOperation(setting, amount, setsAbsolute),
                null,
                null,
                StackingMode.AdditiveParameter,
                null
            );

            return new ActiveSetupEffect("WB-UTL-001:0#1", "BYTE CACHE", definition);
        }

        /// <summary>
        /// Builds a Process configuration with the given setup baseline.
        /// </summary>
        /// <param name="bufferCapacity">The authored Buffer capacity.</param>
        /// <param name="startingBytes">The authored starting Bytes.</param>
        /// <param name="branchCapacity">The authored Branch capacity, or null for no Branch.</param>
        /// <param name="executions">The authored execution allowance.</param>
        /// <param name="mandatoryExecutions">Whether every execution must be run.</param>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition Configuration(
            int bufferCapacity,
            int startingBytes,
            int? branchCapacity,
            int executions = 4,
            bool mandatoryExecutions = false
        )
        {
            ActiveBranchSpec branch = branchCapacity.HasValue
                ? new ActiveBranchSpec(
                    branchCapacity.Value,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>())
                : null;

            BufferLoadSpec load = new(
                BufferLoadPolicy.Scripted,
                Array.Empty<string>(),
                Array.Empty<ArrivalLoadSpec>(),
                0,
                Array.Empty<int>()
            );

            return new ProcessConfigurationDefinition(
                new ProcessID("WB-PROC-001"),
                "Fixture Process",
                ProcessRole.Tutorial1,
                new CoreID("WB-CORE-001"),
                null,
                new ProcessThresholdSpec(10, 20, 30),
                executions,
                mandatoryExecutions,
                startingBytes,
                bufferCapacity,
                1,
                Array.Empty<InitialSourceSpec>(),
                load,
                null,
                branch,
                new RewardPackageID("WB-RWD-001"),
                null
            );
        }
    }
}
