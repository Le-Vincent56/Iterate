using System.Collections.Generic;
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
    /// Guards the shipped Dependency triggers against family/subtype mismatches in the real frozen
    /// catalog: WB-DEP-001's trigger must observe the OPERATION family with the player-Instruction
    /// scope qualifier (CAB §7.7/§7.21), and every EXECUTION-domain trigger across all loaded
    /// Dependencies must pair its subtype with that subtype's own family so no second mismatch
    /// ships silently.
    /// </summary>
    public sealed class ShippedCatalogDependencyTriggerTests
    {
        private ContentCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            _catalog = loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void StandardLibraryTrigger_ObservesOperationFamily()
        {
            TriggerDescriptor trigger = StandardLibraryTrigger();

            Assert.AreEqual(EventFamily.Operation, trigger.EventFamily);
            Assert.AreEqual("PRIMARY_OPERATION_PENDING", trigger.EventSubtype);
        }

        [Test]
        public void StandardLibraryTrigger_CarriesPlayerInstructionQualifier()
        {
            TriggerDescriptor trigger = StandardLibraryTrigger();

            AssertHasQualifier(trigger, "OPERATION_CLASS", "PLAYER_INSTRUCTION");
            AssertHasQualifier(trigger, "OPERATION_CLASS", "FIXED_ADDITION");
            AssertHasQualifier(trigger, "REGISTER", "VALUE");
        }

        [Test]
        public void EveryExecutionTrigger_PairsSubtypeWithOwnFamily()
        {
            int swept = 0;
            for (int i = 0; i < _catalog.Dependencies.Count; i++)
            {
                DependencyDefinition definition = _catalog.Dependencies[i];
                for (int j = 0; j < definition.Effects.Count; j++)
                {
                    EffectDefinition effect = definition.Effects[j];
                    if (effect.PhaseDomain != PhaseDomain.Execution || effect.Trigger == null)
                        continue;

                    string subtype = effect.Trigger.EventSubtype;
                    EventFamily expected = ExpectedFamily(definition.ID.Value, subtype);
                    Assert.AreEqual(
                        expected,
                        effect.Trigger.EventFamily,
                        $"{definition.ID.Value} effect {j}: subtype {subtype} belongs to family {expected}.");
                    swept++;
                }
            }

            Assert.GreaterOrEqual(swept, 5, "expected at least the five quantity-change-reaction Dependency triggers");
        }

        /// <summary>
        /// Resolves WB-DEP-001's single EXECUTION effect trigger from the loaded catalog.
        /// </summary>
        /// <returns>The STANDARD LIBRARY trigger descriptor.</returns>
        private TriggerDescriptor StandardLibraryTrigger()
        {
            Assert.IsTrue(_catalog.TryGetDependency(new DependencyID("WB-DEP-001"), out DependencyDefinition standardLibrary));
            Assert.AreEqual(1, standardLibrary.Effects.Count);
            EffectDefinition effect = standardLibrary.Effects[0];
            Assert.AreEqual(PhaseDomain.Execution, effect.PhaseDomain);
            Assert.IsNotNull(effect.Trigger);
            return effect.Trigger;
        }

        /// <summary>
        /// Maps an EXECUTION-domain trigger subtype to the event family that owns it in the CAB
        /// registry, failing the sweep on any subtype outside the shipped closed set.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity, for the failure message.</param>
        /// <param name="subtype">The trigger's event-subtype token.</param>
        /// <returns>The family that owns the subtype.</returns>
        private static EventFamily ExpectedFamily(string definitionID, string subtype)
        {
            switch (subtype)
            {
                case "PRIMARY_OPERATION_PENDING":
                case "PRIMARY_OPERATION_RESOLVED":
                    return EventFamily.Operation;

                case "QUANTITY_CHANGED":
                    return EventFamily.Quantity;

                case "SOURCE_EXECUTION_SKIPPED":
                    return EventFamily.Disposition;

                case "CONDITION_TRUE":
                    return EventFamily.Structure;

                case "RUNTIME_UNIT_COMPLETED":
                    return EventFamily.Lifecycle;

                default:
                    Assert.Fail($"{definitionID}: EXECUTION trigger subtype {subtype} is outside the shipped closed set.");
                    return EventFamily.Lifecycle;
            }
        }

        /// <summary>
        /// Asserts that the trigger's qualifier list contains the given kind/value pair.
        /// </summary>
        /// <param name="trigger">The trigger under test.</param>
        /// <param name="kind">The expected qualifier kind token.</param>
        /// <param name="value">The expected qualifier value token.</param>
        private static void AssertHasQualifier(
            TriggerDescriptor trigger,
            string kind,
            string value)
        {
            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                if (qualifier.Kind == kind && qualifier.Value == value)
                    return;
            }

            Assert.Fail($"Trigger carries no qualifier ({kind}, {value}).");
        }
    }
}
