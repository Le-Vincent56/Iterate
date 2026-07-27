using System;
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
    /// Guards the shipped Directive triggers against family/subtype and qualifier defects in the real
    /// frozen catalog: WB-DIR-001's trigger must carry the player-Instruction operation-class
    /// qualifier (CAB §12.10), WB-DIR-002's trigger must observe the REACTION family that owns
    /// BOUNDARY_EFFECT_REQUESTED (CAB §7.12), and WB-DEP-009/010's already-correct added-execution
    /// trigger pairs are pinned so no mismatch ships silently. WB-DIR-003's two effects are pinned in
    /// full: the target-lock update observing player Score gains, and the boundary request observing
    /// the same REACTION family that owns BOUNDARY_EFFECT_REQUESTED.
    /// </summary>
    public sealed class ShippedCatalogDirectiveTriggerTests
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
        public void OverclockTrigger_ObservesPlayerValueGains()
        {
            TriggerDescriptor trigger = SingleExecutionTrigger("WB-DIR-001");

            Assert.AreEqual(EventFamily.Quantity, trigger.EventFamily);
            Assert.AreEqual("QUANTITY_CHANGED", trigger.EventSubtype);
            Assert.AreEqual(3, trigger.Qualifiers.Count);
            AssertHasQualifier(trigger, "ACTUAL_DELTA_SIGN", "POSITIVE");
            AssertHasQualifier(trigger, "REGISTER", "VALUE");
            AssertHasQualifier(trigger, "OPERATION_CLASS", "PLAYER_INSTRUCTION");
        }

        [Test]
        public void AlignTrigger_ObservesReactionFamily()
        {
            TriggerDescriptor trigger = SingleExecutionTrigger("WB-DIR-002");

            Assert.AreEqual(EventFamily.Reaction, trigger.EventFamily);
            Assert.AreEqual("BOUNDARY_EFFECT_REQUESTED", trigger.EventSubtype);
        }

        [Test]
        public void BurstOutputLockUpdate_ObservesPlayerScoreGains()
        {
            IReadOnlyList<EffectDefinition> effects = DirectiveExecutionEffects("WB-DIR-003", 2);

            EffectDefinition lockUpdate = effects[0];
            Assert.AreEqual(EventFamily.Quantity, lockUpdate.Trigger.EventFamily);
            Assert.AreEqual("QUANTITY_CHANGED", lockUpdate.Trigger.EventSubtype);
            Assert.AreEqual(3, lockUpdate.Trigger.Qualifiers.Count);
            AssertHasQualifier(lockUpdate.Trigger, "ACTUAL_DELTA_SIGN", "POSITIVE");
            AssertHasQualifier(lockUpdate.Trigger, "REGISTER", "SCORE");
            AssertHasQualifier(lockUpdate.Trigger, "OPERATION_CLASS", "PLAYER_INSTRUCTION");

            TargetLockUpdateOperation operation = lockUpdate.Operation as TargetLockUpdateOperation;
            Assert.IsNotNull(operation, "WB-DIR-003 effect 0 is not a target-lock update.");
            Assert.AreEqual("MOST_RECENT_QUALIFYING_UNIT", operation.Selection.Kind);
        }

        [Test]
        public void BurstOutputBoundaryRequest_ObservesReactionFamily()
        {
            IReadOnlyList<EffectDefinition> effects = DirectiveExecutionEffects("WB-DIR-003", 2);

            EffectDefinition boundaryRequest = effects[1];
            Assert.AreEqual(EventFamily.Reaction, boundaryRequest.Trigger.EventFamily);
            Assert.AreEqual("BOUNDARY_EFFECT_REQUESTED", boundaryRequest.Trigger.EventSubtype);
            Assert.AreEqual(TimingKind.NamedBoundary, boundaryRequest.Trigger.Timing.Kind);
            Assert.AreEqual(
                "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
                boundaryRequest.Trigger.Timing.Name);

            AddedExecutionRequestOperation operation =
                boundaryRequest.Operation as AddedExecutionRequestOperation;
            Assert.IsNotNull(operation, "WB-DIR-003 effect 1 is not an added-execution request.");
            Assert.AreEqual("LOCKED_TARGET", operation.Target.Kind);
            Assert.IsTrue(operation.CancelOnInvalid, "WB-DIR-003 effect 1 must cancel on an invalid host.");
        }

        [Test]
        public void LoopUnrollerTrigger_ObservesLifecycleFamily()
        {
            TriggerDescriptor trigger = SingleExecutionTrigger("WB-DEP-009");

            Assert.AreEqual(EventFamily.Lifecycle, trigger.EventFamily);
            Assert.AreEqual("RUNTIME_UNIT_COMPLETED", trigger.EventSubtype);
        }

        [Test]
        public void BranchPredictorTrigger_ObservesStructureFamily()
        {
            TriggerDescriptor trigger = SingleExecutionTrigger("WB-DEP-010");

            Assert.AreEqual(EventFamily.Structure, trigger.EventFamily);
            Assert.AreEqual("CONDITION_TRUE", trigger.EventSubtype);
        }

        /// <summary>
        /// Resolves the named shipped definition (Directive or Dependency by ID prefix), asserts it
        /// declares exactly one EXECUTION-domain effect, and returns that effect's trigger.
        /// </summary>
        /// <param name="id">The definition's surrogate-key identity string.</param>
        /// <returns>The single EXECUTION effect's trigger descriptor.</returns>
        private TriggerDescriptor SingleExecutionTrigger(string id)
        {
            IReadOnlyList<EffectDefinition> effects;
            if (id.StartsWith("WB-DIR", StringComparison.Ordinal))
            {
                Assert.IsTrue(_catalog.TryGetDirective(new DirectiveID(id), out DirectiveDefinition directive));
                effects = directive.Effects;
            }
            else
            {
                Assert.IsTrue(_catalog.TryGetDependency(new DependencyID(id), out DependencyDefinition dependency));
                effects = dependency.Effects;
            }

            TriggerDescriptor found = null;
            int executionEffects = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                EffectDefinition effect = effects[i];
                if (effect.PhaseDomain != PhaseDomain.Execution)
                    continue;

                executionEffects++;
                found = effect.Trigger;
            }

            Assert.AreEqual(1, executionEffects, $"{id}: expected exactly one EXECUTION effect.");
            Assert.IsNotNull(found, $"{id}: EXECUTION effect carries no trigger.");
            return found;
        }

        /// <summary>
        /// Resolves the named shipped Directive and returns its EXECUTION-domain effects in
        /// declaration order, asserting the expected count so a record gaining or losing an effect
        /// fails here rather than shifting an index assertion silently.
        /// </summary>
        /// <param name="id">The Directive's surrogate-key identity string.</param>
        /// <param name="expectedCount">The number of EXECUTION effects the record must declare.</param>
        /// <returns>The EXECUTION effects in declaration order.</returns>
        private IReadOnlyList<EffectDefinition> DirectiveExecutionEffects(string id, int expectedCount)
        {
            Assert.IsTrue(_catalog.TryGetDirective(new DirectiveID(id), out DirectiveDefinition directive));

            List<EffectDefinition> executionEffects = new();
            for (int i = 0; i < directive.Effects.Count; i++)
            {
                EffectDefinition effect = directive.Effects[i];
                if (effect.PhaseDomain == PhaseDomain.Execution)
                    executionEffects.Add(effect);
            }

            Assert.AreEqual(
                expectedCount,
                executionEffects.Count,
                $"{id}: unexpected EXECUTION effect count.");
            return executionEffects;
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
