using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests how a Dependency's BUILD_INTERACTION effects become archive observers. The interpreter is
    /// fail-fast by design: it accepts exactly the shape GARBAGE COLLECTOR declares and throws by name
    /// on any other BUILD_INTERACTION effect, so a Dependency that would silently under-execute is a
    /// content error caught at interpretation rather than a missing Byte a player notices.
    /// </summary>
    public sealed class ArchiveObserverTests
    {
        [Test]
        public void Interpret_TheGarbageCollectorShape_YieldsOneObserver()
        {
            DependencyInstance dependency = Dependency(ArchiveGain(1));

            IReadOnlyList<ArchiveObserver> observers = BuildInteractionEffects.Interpret(dependency);

            Assert.AreEqual(1, observers.Count);
            Assert.AreEqual(new InstanceID(70), observers[0].Origin);
            Assert.AreEqual("WB-DEP-008", observers[0].DefinitionID);
            Assert.AreEqual("BYTES", observers[0].Gain.Resource);
            Assert.AreEqual(1, observers[0].Gain.Amount);
        }

        [Test]
        public void Interpret_AnExecutionOnlyDependency_YieldsNone()
        {
            EffectDefinition execution = new(
                PhaseDomain.Execution,
                null,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                null,
                StackingMode.AdditiveParameter,
                null);

            Assert.AreEqual(0, BuildInteractionEffects.Interpret(Dependency(execution)).Count);
        }

        [Test]
        public void Interpret_ADependencyWithNoEffects_YieldsNone()
        {
            DependencyInstance dependency = new(
                new InstanceID(70),
                ProgressionFixtures.Dependency("WB-DEP-008", 1));

            Assert.AreEqual(0, BuildInteractionEffects.Interpret(dependency).Count);
        }

        [Test]
        public void Interpret_ABuildInteractionEffectWithTheWrongOperation_ThrowsNamingTheDefinition()
        {
            EffectDefinition wrongOperation = new(
                PhaseDomain.BuildInteraction,
                ArchiveTrigger(),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                null,
                StackingMode.IndependentResolution,
                ProcessFrequency());

            ArgumentException thrown = Assert.Throws<ArgumentException>(
                () => BuildInteractionEffects.Interpret(Dependency(wrongOperation)));

            Assert.That(thrown.Message, Does.Contain("WB-DEP-008"));
        }

        [Test]
        public void Interpret_ABuildInteractionEffectWithoutATrigger_Throws()
        {
            EffectDefinition triggerless = new(
                PhaseDomain.BuildInteraction,
                null,
                new ResourceGainOperation("BYTES", 1),
                null,
                null,
                StackingMode.IndependentResolution,
                ProcessFrequency());

            Assert.Throws<ArgumentException>(() => BuildInteractionEffects.Interpret(Dependency(triggerless)));
        }

        [Test]
        public void Interpret_ABuildInteractionEffectOnAnotherEvent_Throws()
        {
            EffectDefinition wrongEvent = new(
                PhaseDomain.BuildInteraction,
                new TriggerDescriptor(EventFamily.ContentLifecycle, "OBJECT_ACQUIRED", Array.Empty<TriggerQualifier>(), null),
                new ResourceGainOperation("BYTES", 1),
                null,
                null,
                StackingMode.IndependentResolution,
                ProcessFrequency());

            Assert.Throws<ArgumentException>(() => BuildInteractionEffects.Interpret(Dependency(wrongEvent)));
        }

        [Test]
        public void Interpret_CarriesTheFrequencyTheEffectDeclares()
        {
            IReadOnlyList<ArchiveObserver> observers = BuildInteractionEffects.Interpret(Dependency(ArchiveGain(1)));

            Assert.AreEqual("FIRST_QUALIFYING_EVENT", observers[0].Frequency.Allowance);
            Assert.AreEqual("PROCESS", observers[0].Frequency.Scope);
        }

        private static DependencyInstance Dependency(EffectDefinition effect)
        {
            DependencyDefinition definition = new(
                new DependencyID("WB-DEP-008"),
                "GARBAGE COLLECTOR",
                "GARBAGE COLLECTOR",
                ContentCategory.Dependency,
                Rarity.Uncommon,
                new[] { "Buffer", "Archive", "Bytes" },
                1,
                new[] { effect });

            return new DependencyInstance(new InstanceID(70), definition);
        }

        private static EffectDefinition ArchiveGain(int amount)
        {
            return new EffectDefinition(
                PhaseDomain.BuildInteraction,
                ArchiveTrigger(),
                new ResourceGainOperation("BYTES", amount),
                null,
                null,
                StackingMode.IndependentResolution,
                ProcessFrequency());
        }

        private static TriggerDescriptor ArchiveTrigger()
        {
            return new TriggerDescriptor(EventFamily.ContentLifecycle, "OBJECT_ARCHIVED", Array.Empty<TriggerQualifier>(), null);
        }

        private static EffectFrequency ProcessFrequency()
        {
            return new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS");
        }
    }
}
