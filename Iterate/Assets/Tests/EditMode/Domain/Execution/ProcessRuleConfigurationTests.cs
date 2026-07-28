using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests how a Process rule reaches an execution: carried on the configuration, interpreted with
    /// the rest of the request's content, and appended after the Patches.
    /// Most Processes carry no Process rule, so the configuration's rule is nullable and both new
    /// members default to absent — every existing request shape keeps constructing unchanged. That
    /// default is the point: a Process rule is Critical-Process content, not something every
    /// execution has.
    /// Ordering across content categories is not meaningful here and no priority is invented. The
    /// rule's two effects act at moments nothing else occupies — a pending multiplication and a
    /// positive Score change — so appending them last is an arbitrary but stable choice rather than a
    /// precedence claim.
    /// </summary>
    public sealed class ProcessRuleConfigurationTests
    {
        [Test]
        public void Configuration_DefaultsToNoProcessRule()
        {
            ProcessExecutionConfiguration configuration = SchedulerFixtures.StandardConfiguration();

            Assert.IsNull(configuration.ProcessRule);
            Assert.IsNull(configuration.DesignatedFinalCoreOutputPosition);
        }

        [Test]
        public void Configuration_CarriesAProcessRuleWhenGiven()
        {
            ProcessExecutionConfiguration configuration = ConfigurationWith(ThermalThrottle(80));

            Assert.IsNotNull(configuration.ProcessRule);
            Assert.AreEqual("WB-PRC-001", configuration.ProcessRule.Definition.ID.Value);
            Assert.AreEqual(new InstanceID(80), configuration.ProcessRule.InstanceID);
        }

        [Test]
        public void Configuration_CarriesTheDesignatedFinalCoreOutputPosition()
        {
            ProcessExecutionConfiguration configuration = ConfigurationWith(ThermalThrottle(80), new SourcePosition(9));

            Assert.AreEqual(new SourcePosition(9), configuration.DesignatedFinalCoreOutputPosition);
        }

        [Test]
        public void RequestWithoutAProcessRule_InterpretsNoRuleEffects()
        {
            ExecutionRequest request = RequestWith(SchedulerFixtures.StandardConfiguration());

            for (int i = 0; i < request.InterpretedEffects.Count; i++)
            {
                Assert.AreNotEqual("WB-PRC-001", request.InterpretedEffects[i].DefinitionID);
            }
        }

        [Test]
        public void RequestWithAProcessRule_InterpretsBothRuleEffects()
        {
            ExecutionRequest request = RequestWith(ConfigurationWith(ThermalThrottle(80)));

            int ruleEffects = 0;
            for (int i = 0; i < request.InterpretedEffects.Count; i++)
            {
                if (request.InterpretedEffects[i].DefinitionID == "WB-PRC-001")
                    ruleEffects++;
            }

            Assert.AreEqual(2, ruleEffects);
        }

        [Test]
        public void RuleEffects_AreAppendedAfterTheRestOfTheContent()
        {
            ExecutionRequest request = RequestWith(ConfigurationWith(ThermalThrottle(80)));
            int count = request.InterpretedEffects.Count;

            Assert.AreEqual("WB-PRC-001", request.InterpretedEffects[count - 2].DefinitionID);
            Assert.AreEqual("WB-PRC-001", request.InterpretedEffects[count - 1].DefinitionID);
        }

        [Test]
        public void RuleEffects_CarryTheRuleInstanceAsTheirOrigin()
        {
            ExecutionRequest request = RequestWith(ConfigurationWith(ThermalThrottle(80)));
            int count = request.InterpretedEffects.Count;

            Assert.AreEqual(new InstanceID(80), request.InterpretedEffects[count - 1].Origin);
            Assert.AreEqual(new InstanceID(80), request.InterpretedEffects[count - 2].Origin);
        }

        [Test]
        public void RuleEffects_InterpretToTheInterventionAndReactionPair()
        {
            ExecutionRequest request = RequestWith(ConfigurationWith(ThermalThrottle(80)));
            int count = request.InterpretedEffects.Count;

            Assert.AreEqual(ActiveEffectKind.CounterIntervention, request.InterpretedEffects[count - 2].Kind);
            Assert.AreEqual(ActiveEffectKind.Reaction, request.InterpretedEffects[count - 1].Kind);
            Assert.IsNotNull(request.InterpretedEffects[count - 1].CounterRequest);
        }

        /// <summary>
        /// A standard configuration carrying the given Process rule and no designated output.
        /// </summary>
        /// <param name="rule">The Process-rule instance.</param>
        /// <returns>The configuration.</returns>
        private static ProcessExecutionConfiguration ConfigurationWith(ProcessRuleInstance rule)
        {
            return ConfigurationWith(rule, null);
        }

        /// <summary>
        /// A standard configuration carrying the given Process rule and designated output position.
        /// </summary>
        /// <param name="rule">The Process-rule instance.</param>
        /// <param name="designatedOutput">The designated final Core output position, or null.</param>
        /// <returns>The configuration.</returns>
        private static ProcessExecutionConfiguration ConfigurationWith(
            ProcessRuleInstance rule,
            SourcePosition? designatedOutput)
        {
            return SchedulerFixtures.ConfigurationWithRule(rule, designatedOutput);
        }

        /// <summary>
        /// A request over one <c>Value += 1</c> Instruction under the given configuration.
        /// </summary>
        /// <param name="configuration">The Process configuration.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RequestWith(ProcessExecutionConfiguration configuration)
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(50, 1))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), configuration);
        }

        /// <summary>
        /// A THERMAL-THROTTLE-shaped Process-rule instance: the pre-operation Heat gain followed by
        /// the post-Score cooling request.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Process-rule instance.</returns>
        private static ProcessRuleInstance ThermalThrottle(int instance)
        {
            EffectTiming preOperation = new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION");
            EffectDefinition gain = new EffectDefinition(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Operation,
                    "PRIMARY_OPERATION_PENDING",
                    new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", "MULTIPLY") },
                    preOperation),
                new CounterRequestOperation("HEAT", 1, 0, 3, true, true),
                new TargetingRule("NO_TARGET", string.Empty),
                preOperation,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            EffectTiming reaction = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            EffectDefinition cooling = new EffectDefinition(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Quantity,
                    "QUANTITY_CHANGED",
                    new List<TriggerQualifier>
                    {
                        new TriggerQualifier("REGISTER", "SCORE"),
                        new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE")
                    },
                    reaction),
                new CounterRequestOperation("HEAT", -1, 0, 0, true, false),
                new TargetingRule("NO_TARGET", string.Empty),
                reaction,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            ProcessRuleDefinition definition = new ProcessRuleDefinition(
                new ProcessRuleID("WB-PRC-001"),
                "Multiplication raises Heat; Score increases cool it.",
                "THERMAL THROTTLE",
                ContentCategory.ProcessRule,
                Rarity.Starter,
                new List<string> { "ProcessRule", "Heat" },
                new List<EffectDefinition> { gain, cooling });

            return new ProcessRuleInstance(new InstanceID(instance), definition);
        }
    }
}
