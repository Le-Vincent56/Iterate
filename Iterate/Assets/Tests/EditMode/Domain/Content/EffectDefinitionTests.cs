using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests value equality across the effect-model records, the closed-enum member counts that
    /// mirror the governing canon registries, and the <see cref="CatalogVocabulary"/> controlled sets.
    /// </summary>
    public sealed class EffectDefinitionTests
    {
        [Test]
        public void StructurePredicate_SameValues_AreEqual()
        {
            StructurePredicate first = new(CoreRegister.Value, PredicateComparison.IsEven, 0);
            StructurePredicate second = new(CoreRegister.Value, PredicateComparison.IsEven, 0);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void StructurePredicate_DifferentComparison_AreNotEqual()
        {
            StructurePredicate first = new(CoreRegister.Value, PredicateComparison.IsEven, 0);
            StructurePredicate second = new(CoreRegister.Value, PredicateComparison.AtLeast, 10);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void EffectTiming_SameValues_AreEqual()
        {
            EffectTiming first = new(TimingKind.Band, "PRIMARY_OPERATION_RESOLUTION");
            EffectTiming second = new(TimingKind.Band, "PRIMARY_OPERATION_RESOLUTION");

            Assert.AreEqual(first, second);
        }

        [Test]
        public void EffectFrequency_SameValues_AreEqual()
        {
            EffectFrequency first = new("FIRST_QUALIFYING_EVENT", "EXECUTION");
            EffectFrequency second = new("FIRST_QUALIFYING_EVENT", "EXECUTION");

            Assert.AreEqual(first, second);
        }

        [Test]
        public void TargetingRule_DifferentKind_AreNotEqual()
        {
            TargetingRule first = new("OWN_HOST", string.Empty);
            TargetingRule second = new("TRIGGERING_UNIT", string.Empty);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void EffectDefinition_SameComponents_AreEqual()
        {
            IReadOnlyList<TriggerQualifier> qualifiers = new[] { new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE") };
            EffectTiming timing = new(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new(EventFamily.Quantity, "QUANTITY_CHANGED", qualifiers, timing);
            EffectOperation operation = new QuantityChangeOperation(
                CoreRegister.Value,
                QuantityOperator.Add,
                OperandSpec.FromConstant(1)
            );
            TargetingRule targeting = new("SAME_REGISTER_AS_TRIGGER", string.Empty);
            EffectFrequency frequency = new("FIRST_QUALIFYING_EVENT", "EXECUTION");

            EffectDefinition first = new(
                PhaseDomain.Execution,
                trigger,
                operation,
                targeting,
                timing,
                StackingMode.IndependentResolution,
                frequency
            );
            EffectDefinition second = new(
                PhaseDomain.Execution,
                trigger,
                operation,
                targeting,
                timing,
                StackingMode.IndependentResolution,
                frequency
            );

            Assert.AreEqual(first, second);
        }

        [Test]
        public void EventFamily_HasFifteenMembers()
        {
            Assert.AreEqual(15, Enum.GetValues(typeof(EventFamily)).Length);
        }

        [Test]
        public void OperationKind_HasTenMembers()
        {
            Assert.AreEqual(10, Enum.GetValues(typeof(OperationKind)).Length);
        }

        [Test]
        public void StackingMode_HasNineControlledModes()
        {
            Assert.AreEqual(9, Enum.GetValues(typeof(StackingMode)).Length);
        }

        [Test]
        public void CatalogVocabulary_TimingBands_ContainsPrimaryOperationResolution()
        {
            Assert.IsTrue(CatalogVocabulary.TimingBands.Contains("PRIMARY_OPERATION_RESOLUTION"));
            Assert.AreEqual(6, CatalogVocabulary.TimingBands.Count);
        }

        [Test]
        public void CatalogVocabulary_TimingBoundaries_ContainsAlignBoundary()
        {
            Assert.IsTrue(CatalogVocabulary.TimingBoundaries.Contains("END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL"));
        }

        [Test]
        public void CatalogVocabulary_FrequencyAllowances_ContainsExpectedForms()
        {
            Assert.IsTrue(CatalogVocabulary.FrequencyAllowances.Contains("ONCE"));
            Assert.IsTrue(CatalogVocabulary.FrequencyAllowances.Contains("FIRST_QUALIFYING_EVENT"));
        }

        [Test]
        public void CatalogVocabulary_FrequencyScopes_ContainsExecution()
        {
            Assert.IsTrue(CatalogVocabulary.FrequencyScopes.Contains("EXECUTION"));
        }

        [Test]
        public void CatalogVocabulary_TargetingKinds_ContainsOwnHost()
        {
            Assert.IsTrue(CatalogVocabulary.TargetingKinds.Contains("OWN_HOST"));
        }

        [Test]
        public void CatalogVocabulary_EventSubtypes_ContainsQuantityChanged()
        {
            Assert.IsTrue(CatalogVocabulary.EventSubtypes.Contains("QUANTITY_CHANGED"));
        }

        [Test]
        public void CatalogVocabulary_AllSets_AreNonEmpty()
        {
            Assert.IsNotEmpty(CatalogVocabulary.EventSubtypes);
            Assert.IsNotEmpty(CatalogVocabulary.TimingBands);
            Assert.IsNotEmpty(CatalogVocabulary.TimingBoundaries);
            Assert.IsNotEmpty(CatalogVocabulary.QualifierKinds);
            Assert.IsNotEmpty(CatalogVocabulary.FrequencyAllowances);
            Assert.IsNotEmpty(CatalogVocabulary.FrequencyScopes);
            Assert.IsNotEmpty(CatalogVocabulary.TargetingKinds);
            Assert.IsNotEmpty(CatalogVocabulary.DispositionNames);
            Assert.IsNotEmpty(CatalogVocabulary.CounterNames);
            Assert.IsNotEmpty(CatalogVocabulary.CostKinds);
            Assert.IsNotEmpty(CatalogVocabulary.ConfigurationSettings);
            Assert.IsNotEmpty(CatalogVocabulary.PredictionProjections);
        }
    }
}
