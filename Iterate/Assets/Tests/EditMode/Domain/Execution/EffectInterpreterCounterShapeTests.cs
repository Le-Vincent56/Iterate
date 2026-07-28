using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the interpreter's admission of the two THERMAL THROTTLE counter shapes and, just as
    /// importantly, that every counter-operation placement outside those two keeps its named
    /// rejection.
    /// The widening is deliberately narrow. A <c>COUNTER_REQUEST</c> is interpretable in exactly two
    /// positions: on a pending primary operation at the pre-operation intervention band, yielding the
    /// new counter-intervention kind; and on a finalized quantity change at the immediate-result
    /// reaction band, yielding a reaction that carries a counter request instead of a register write.
    /// Everywhere else — the modification band, the resolved-operation band, the post-unit band, a
    /// named boundary, or any host-socketed declaration — it still throws, because nothing in canon
    /// or the slice's content authorizes a counter there and a silent admission would let unwritable
    /// content load.
    /// The pre-operation entry also needs the <c>MULTIPLY</c> operation class, which the qualifier
    /// vocabulary did not previously contain; the existing three classes must keep working unchanged.
    /// </summary>
    public sealed class EffectInterpreterCounterShapeTests
    {
        [Test]
        public void HeatGainShape_YieldsACounterInterventionEffect()
        {
            DirectiveInstance rule = DirectiveOver(90, HeatGainEffect());

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(rule);

            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(ActiveEffectKind.CounterIntervention, effects[0].Kind);
            Assert.IsFalse(effects[0].IsModification);
        }

        [Test]
        public void HeatGainShape_CarriesTheCounterRequestNotAQuantityChange()
        {
            ActiveEffect effect = EffectInterpreter.Interpret(DirectiveOver(90, HeatGainEffect()))[0];

            Assert.IsNull(effect.Operation);
            Assert.IsNotNull(effect.CounterRequest);
            Assert.AreEqual("HEAT", effect.CounterRequest.Counter);
            Assert.AreEqual(1, effect.CounterRequest.Delta);
            Assert.AreEqual(0, effect.CounterRequest.Floor);
            Assert.AreEqual(3, effect.CounterRequest.Ceiling);
            Assert.IsTrue(effect.CounterRequest.HasFloor);
            Assert.IsTrue(effect.CounterRequest.HasCeiling);
        }

        [Test]
        public void HeatGainShape_RetainsItsTriggerAndFrequency()
        {
            ActiveEffect effect = EffectInterpreter.Interpret(DirectiveOver(90, HeatGainEffect()))[0];

            Assert.AreEqual(EventFamily.Operation, effect.Trigger.EventFamily);
            Assert.AreEqual("PRIMARY_OPERATION_PENDING", effect.Trigger.EventSubtype);
            Assert.AreEqual("MULTIPLY", effect.Trigger.Qualifiers[0].Value);
            Assert.AreEqual("EVERY_QUALIFYING_EVENT", effect.Frequency.Allowance);
            Assert.AreEqual("DECLARED_SCOPE", effect.Frequency.Scope);
            Assert.IsNull(effect.HostInstance);
        }

        [Test]
        public void CoolingShape_YieldsAReactionCarryingACounterRequest()
        {
            DirectiveInstance rule = DirectiveOver(91, CoolingEffect());

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(rule);

            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(ActiveEffectKind.Reaction, effects[0].Kind);
            Assert.IsNull(effects[0].Operation);
            Assert.IsNotNull(effects[0].CounterRequest);
        }

        [Test]
        public void CoolingShape_CarriesTheFlooredNegativeDelta()
        {
            ActiveEffect effect = EffectInterpreter.Interpret(DirectiveOver(91, CoolingEffect()))[0];

            Assert.AreEqual("HEAT", effect.CounterRequest.Counter);
            Assert.AreEqual(-1, effect.CounterRequest.Delta);
            Assert.AreEqual(0, effect.CounterRequest.Floor);
            Assert.IsTrue(effect.CounterRequest.HasFloor);
            Assert.IsFalse(effect.CounterRequest.HasCeiling);
        }

        [Test]
        public void BothShapesTogether_InterpretAsOnePairInDeclaredOrder()
        {
            DirectiveInstance rule = DirectiveOver(92, HeatGainEffect(), CoolingEffect());

            IReadOnlyList<ActiveEffect> effects = EffectInterpreter.Interpret(rule);

            Assert.AreEqual(2, effects.Count);
            Assert.AreEqual(ActiveEffectKind.CounterIntervention, effects[0].Kind);
            Assert.AreEqual(ActiveEffectKind.Reaction, effects[1].Kind);
            Assert.AreEqual(0, effects[0].EffectIndex);
            Assert.AreEqual(1, effects[1].EffectIndex);
        }

        [Test]
        public void CounterRequestAtTheModificationBand_IsRejected()
        {
            EffectDefinition effect = CounterEffect(
                OperationTrigger("MULTIPLY", "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"),
                GainRequest());

            Assert.Throws<System.ArgumentException>(() => EffectInterpreter.Interpret(DirectiveOver(93, effect)));
        }

        [Test]
        public void CounterRequestOnAResolvedOperation_IsRejected()
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_RESOLVED",
                new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", "MULTIPLY") },
                Band("IMMEDIATE_RESULT_REACTION"));

            Assert.Throws<System.ArgumentException>(
                () => EffectInterpreter.Interpret(DirectiveOver(94, CounterEffect(trigger, GainRequest()))));
        }

        [Test]
        public void CounterRequestAtAPostUnitBand_IsRejected()
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier>(),
                Band("POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));

            Assert.Throws<System.ArgumentException>(
                () => EffectInterpreter.Interpret(DirectiveOver(95, CounterEffect(trigger, GainRequest()))));
        }

        [Test]
        public void CoolingShapeAtTheWrongBand_IsRejected()
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("REGISTER", "SCORE"),
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE")
                },
                Band("QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"));

            Assert.Throws<System.ArgumentException>(
                () => EffectInterpreter.Interpret(DirectiveOver(96, CounterEffect(trigger, CoolRequest()))));
        }

        [Test]
        public void ExistingQuantityChangeReaction_StillInterpretsUnchanged()
        {
            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                new TriggerDescriptor(
                    EventFamily.Quantity,
                    "QUANTITY_CHANGED",
                    new List<TriggerQualifier>
                    {
                        new TriggerQualifier("REGISTER", "SCORE"),
                        new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE")
                    },
                    Band("IMMEDIATE_RESULT_REACTION")),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("NO_TARGET", string.Empty),
                Band("IMMEDIATE_RESULT_REACTION"),
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));

            ActiveEffect interpreted = EffectInterpreter.Interpret(DirectiveOver(97, effect))[0];

            Assert.AreEqual(ActiveEffectKind.Reaction, interpreted.Kind);
            Assert.IsNotNull(interpreted.Operation);
            Assert.IsNull(interpreted.CounterRequest);
        }

        [Test]
        public void ExistingModificationShape_StillInterpretsUnchanged()
        {
            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                OperationTrigger("FIXED_ADDITION", "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("SAME_REGISTER_AS_TRIGGER", string.Empty),
                Band("OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"),
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));

            ActiveEffect interpreted = EffectInterpreter.Interpret(DirectiveOver(98, effect))[0];

            Assert.AreEqual(ActiveEffectKind.Modification, interpreted.Kind);
            Assert.IsTrue(interpreted.IsModification);
            Assert.IsNull(interpreted.CounterRequest);
        }

        [Test]
        public void UnknownOperationClass_IsStillRejected()
        {
            EffectDefinition effect = CounterEffect(
                OperationTrigger("DIVIDE", "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"),
                GainRequest());

            Assert.Throws<System.ArgumentException>(() => EffectInterpreter.Interpret(DirectiveOver(99, effect)));
        }

        /// <summary>
        /// The THERMAL THROTTLE entry (i) shape: a pending multiplication at the pre-operation
        /// intervention band requesting one Heat, clamped to zero through three.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition HeatGainEffect()
        {
            return CounterEffect(
                OperationTrigger("MULTIPLY", "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"),
                GainRequest());
        }

        /// <summary>
        /// The THERMAL THROTTLE entry (ii) shape: a positive Score change at the immediate-result
        /// reaction band releasing one Heat, floored at zero.
        /// </summary>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition CoolingEffect()
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("REGISTER", "SCORE"),
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE")
                },
                Band("IMMEDIATE_RESULT_REACTION"));

            return CounterEffect(trigger, CoolRequest());
        }

        /// <summary>
        /// The plus-one Heat request clamped to zero through three.
        /// </summary>
        /// <returns>The counter-request operation.</returns>
        private static CounterRequestOperation GainRequest()
        {
            return new CounterRequestOperation("HEAT", 1, 0, 3, true, true);
        }

        /// <summary>
        /// The minus-one Heat request floored at zero with no ceiling.
        /// </summary>
        /// <returns>The counter-request operation.</returns>
        private static CounterRequestOperation CoolRequest()
        {
            return new CounterRequestOperation("HEAT", -1, 0, 0, true, false);
        }

        /// <summary>
        /// Wraps a trigger and counter request into an EXECUTION-domain effect definition.
        /// </summary>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="request">The counter-request operation.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition CounterEffect(TriggerDescriptor trigger, CounterRequestOperation request)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                request,
                new TargetingRule("NO_TARGET", string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// A pending-primary-operation trigger carrying one operation-class qualifier at the band.
        /// </summary>
        /// <param name="operationClass">The operation-class qualifier value.</param>
        /// <param name="band">The timing band name.</param>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor OperationTrigger(string operationClass, string band)
        {
            return new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", operationClass) },
                Band(band));
        }

        /// <summary>
        /// A band timing with the given name.
        /// </summary>
        /// <param name="name">The band name.</param>
        /// <returns>The effect timing.</returns>
        private static EffectTiming Band(string name)
        {
            return new EffectTiming(TimingKind.Band, name);
        }

        /// <summary>
        /// Wraps effects in a Directive definition and instance.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="effects">The declared effects.</param>
        /// <returns>The Directive instance.</returns>
        private static DirectiveInstance DirectiveOver(int instance, params EffectDefinition[] effects)
        {
            DirectiveDefinition definition = new DirectiveDefinition(
                new DirectiveID("WB-PRC-901"),
                "Test rules.",
                "TEST PROCESS RULE",
                ContentCategory.Directive,
                Rarity.Starter,
                new List<string>(),
                new List<EffectDefinition>(effects));

            return new DirectiveInstance(new InstanceID(instance), definition);
        }
    }
}
