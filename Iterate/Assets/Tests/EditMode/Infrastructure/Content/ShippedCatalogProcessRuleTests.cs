using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Loads the shipped catalog through the real pipeline and pins the frozen
    /// <c>WB-PRC-001 THERMAL THROTTLE</c> record field by field — the standing content-layer
    /// conformance evidence for the Process-rule category.
    /// The record is a two-entry pair, not one effect: entry (i) raises Heat by one on a pending
    /// multiplication primary at the pre-operation intervention band, clamped to zero through three;
    /// entry (ii) lowers Heat by one on each positive Score change at the immediate-result-reaction
    /// band, floored at zero with no ceiling. The Technical Architecture source's conformance
    /// register fixes that decomposition and Balance registers the delegated record.
    /// This card is content-layer only. The interpreter does not yet admit either shape, so nothing
    /// here executes the rule — end-to-end Heat evidence arrives with the interpreter and scheduler
    /// work. What is pinned is that the authored bytes freeze into exactly the intended record.
    /// </summary>
    public sealed class ShippedCatalogProcessRuleTests
    {
        [Test]
        public void ShippedCatalog_CarriesExactlyOneProcessRule()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(1, catalog.ProcessRules.Count);
            Assert.AreEqual("WB-PRC-001", catalog.ProcessRules[0].ID.Value);
            Assert.AreEqual("THERMAL THROTTLE", catalog.ProcessRules[0].DisplayName);
        }

        [Test]
        public void ThermalThrottle_ResolvesByID()
        {
            ContentCatalog catalog = Load();

            bool found = catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-001"), out ProcessRuleDefinition definition);

            Assert.IsTrue(found);
            Assert.AreEqual(ContentCategory.ProcessRule, definition.Category);
            Assert.AreEqual(Rarity.Starter, definition.Rarity);
        }

        [Test]
        public void ThermalThrottle_DeclaresExactlyTwoEffects()
        {
            Assert.AreEqual(2, ThermalThrottle().Effects.Count);
        }

        [Test]
        public void HeatGainEntry_TriggersOnPendingMultiplicationAtTheInterventionBand()
        {
            EffectDefinition gain = ThermalThrottle().Effects[0];

            Assert.AreEqual(PhaseDomain.Execution, gain.PhaseDomain);
            Assert.AreEqual(EventFamily.Operation, gain.Trigger.EventFamily);
            Assert.AreEqual("PRIMARY_OPERATION_PENDING", gain.Trigger.EventSubtype);
            Assert.AreEqual(TimingKind.Band, gain.Trigger.Timing.Kind);
            Assert.AreEqual("QUALIFICATION_AND_PRE_OPERATION_INTERVENTION", gain.Trigger.Timing.Name);
        }

        [Test]
        public void HeatGainEntry_QualifiesOnTheMultiplicationOperationClass()
        {
            EffectDefinition gain = ThermalThrottle().Effects[0];

            Assert.AreEqual(1, gain.Trigger.Qualifiers.Count);
            Assert.AreEqual("OPERATION_CLASS", gain.Trigger.Qualifiers[0].Kind);
            Assert.AreEqual("MULTIPLY", gain.Trigger.Qualifiers[0].Value);
        }

        [Test]
        public void HeatGainEntry_RequestsPlusOneHeatClampedToZeroThroughThree()
        {
            CounterRequestOperation operation = (CounterRequestOperation)ThermalThrottle().Effects[0].Operation;

            Assert.AreEqual("HEAT", operation.Counter);
            Assert.AreEqual(1, operation.Delta);
            Assert.AreEqual(0, operation.Floor);
            Assert.AreEqual(3, operation.Ceiling);
            Assert.IsTrue(operation.HasFloor);
            Assert.IsTrue(operation.HasCeiling);
        }

        [Test]
        public void HeatGainEntry_ResolvesOnEveryQualifyingEvent()
        {
            EffectDefinition gain = ThermalThrottle().Effects[0];

            Assert.AreEqual("EVERY_QUALIFYING_EVENT", gain.Frequency.Allowance);
            Assert.AreEqual("DECLARED_SCOPE", gain.Frequency.Scope);
            Assert.AreEqual(StackingMode.IndependentResolution, gain.Stacking);
        }

        [Test]
        public void CoolingEntry_TriggersOnQuantityChangedAtTheReactionBand()
        {
            EffectDefinition cooling = ThermalThrottle().Effects[1];

            Assert.AreEqual(PhaseDomain.Execution, cooling.PhaseDomain);
            Assert.AreEqual(EventFamily.Quantity, cooling.Trigger.EventFamily);
            Assert.AreEqual("QUANTITY_CHANGED", cooling.Trigger.EventSubtype);
            Assert.AreEqual(TimingKind.Band, cooling.Trigger.Timing.Kind);
            Assert.AreEqual("IMMEDIATE_RESULT_REACTION", cooling.Trigger.Timing.Name);
        }

        [Test]
        public void CoolingEntry_QualifiesOnPositiveScoreChangesOnly()
        {
            EffectDefinition cooling = ThermalThrottle().Effects[1];

            Assert.AreEqual(2, cooling.Trigger.Qualifiers.Count);
            Assert.AreEqual("REGISTER", cooling.Trigger.Qualifiers[0].Kind);
            Assert.AreEqual("SCORE", cooling.Trigger.Qualifiers[0].Value);
            Assert.AreEqual("ACTUAL_DELTA_SIGN", cooling.Trigger.Qualifiers[1].Kind);
            Assert.AreEqual("POSITIVE", cooling.Trigger.Qualifiers[1].Value);
        }

        [Test]
        public void CoolingEntry_RequestsMinusOneHeatFlooredAtZeroWithNoCeiling()
        {
            CounterRequestOperation operation = (CounterRequestOperation)ThermalThrottle().Effects[1].Operation;

            Assert.AreEqual("HEAT", operation.Counter);
            Assert.AreEqual(-1, operation.Delta);
            Assert.AreEqual(0, operation.Floor);
            Assert.IsTrue(operation.HasFloor);
            Assert.IsFalse(operation.HasCeiling);
        }

        [Test]
        public void CoolingEntry_ResolvesOnEveryQualifyingEvent()
        {
            EffectDefinition cooling = ThermalThrottle().Effects[1];

            Assert.AreEqual("EVERY_QUALIFYING_EVENT", cooling.Frequency.Allowance);
            Assert.AreEqual("DECLARED_SCOPE", cooling.Frequency.Scope);
            Assert.AreEqual(StackingMode.IndependentResolution, cooling.Stacking);
        }

        [Test]
        public void BothEntries_TargetNothing()
        {
            ProcessRuleDefinition rule = ThermalThrottle();

            Assert.AreEqual("NO_TARGET", rule.Effects[0].Targeting.Kind);
            Assert.AreEqual("NO_TARGET", rule.Effects[1].Targeting.Kind);
        }

        /// <summary>
        /// Loads the shipped catalog through the real pipeline.
        /// </summary>
        /// <returns>The frozen catalog.</returns>
        private static ContentCatalog Load()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// The frozen THERMAL THROTTLE definition from the shipped catalog.
        /// </summary>
        /// <returns>The Process-rule definition.</returns>
        private static ProcessRuleDefinition ThermalThrottle()
        {
            return Load().ProcessRules[0];
        }
    }
}
