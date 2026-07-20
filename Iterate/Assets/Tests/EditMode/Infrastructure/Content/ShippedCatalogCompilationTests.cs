using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// End-to-end Compilation verification against the real frozen 45-definition catalog (no fixture effect
    /// data): the evaluator-support sweep guarding against catalog/evaluator drift, catalog-versus-fixture
    /// parity for CLEAN BUILD and COMPILE AHEAD, and one full Process-shaped scenario over the standard Core
    /// shape. It loads through the same pipeline as the standing shipped-catalog conformance test.
    /// </summary>
    public sealed class ShippedCatalogCompilationTests
    {
        private static readonly CoreLine _coreOne = new CoreLine("core-01", "Value = 1");
        private static readonly CoreLine _coreTwo = new CoreLine("core-02", "Signal = 0");
        private static readonly CoreLine _coreNine = new CoreLine("core-09", "Score += Value");

        [Test]
        public void EveryCompilationEffect_ResolvesWithoutException()
        {
            ContentCatalog catalog = Load();
            ParameterSet parameters = catalog.Parameters;
            int swept = 0;

            for (int i = 0; i < catalog.Dependencies.Count; i++)
            {
                DependencyDefinition definition = catalog.Dependencies[i];
                swept += SweepCompilationEffects(definition.ID.Value, definition.DisplayName, definition.Effects, parameters);
            }

            for (int i = 0; i < catalog.Directives.Count; i++)
            {
                DirectiveDefinition definition = catalog.Directives[i];
                swept += SweepCompilationEffects(definition.ID.Value, definition.DisplayName, definition.Effects, parameters);
            }

            for (int i = 0; i < catalog.Patches.Count; i++)
            {
                PatchDefinition definition = catalog.Patches[i];
                swept += SweepCompilationEffects(definition.ID.Value, definition.DisplayName, definition.Effects, parameters);
            }

            for (int i = 0; i < catalog.Utilities.Count; i++)
            {
                UtilityDefinition definition = catalog.Utilities[i];
                swept += SweepCompilationEffects(definition.ID.Value, definition.DisplayName, definition.Effects, parameters);
            }

            Assert.GreaterOrEqual(swept, 2, "expected at least CLEAN BUILD and COMPILE AHEAD");
        }

        [Test]
        public void CleanBuildFromCatalog_ZeroesOrdinaryFirstEdited()
        {
            ContentCatalog catalog = Load();
            Assert.IsTrue(catalog.TryGetDependency(new DependencyID("WB-DEP-002"), out DependencyDefinition cleanBuild));
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(1), cleanBuild.DisplayName, cleanBuild.Effects[0]);

            CompilationCostBreakdown breakdown = CompilationCostResolver.Resolve(
                CompilationClassification.OrdinaryEdited,
                1,
                catalog.Parameters,
                new[] { active },
                new CompilationEffectLedger());

            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.AreEqual(1, breakdown.Modifiers.Count);
            Assert.AreEqual(1, breakdown.Modifiers[0].CostBefore);
            Assert.AreEqual(0, breakdown.Modifiers[0].CostAfter);
            Assert.IsFalse(breakdown.Modifiers[0].NumericallyRedundant);
        }

        [Test]
        public void CompileAheadFromCatalog_ReducesOrdinaryByOne()
        {
            ContentCatalog catalog = Load();
            Assert.IsTrue(catalog.TryGetDirective(new DirectiveID("WB-DIR-004"), out DirectiveDefinition compileAhead));
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-DIR-004", 0, new InstanceID(1), compileAhead.DisplayName, compileAhead.Effects[0]);

            CompilationCostBreakdown breakdown = CompilationCostResolver.Resolve(
                CompilationClassification.OrdinaryEdited,
                2,
                catalog.Parameters,
                new[] { active },
                new CompilationEffectLedger());

            Assert.AreEqual(1, breakdown.FinalCost);
            Assert.AreEqual(1, breakdown.Modifiers.Count);
            Assert.AreEqual(CostModifierStage.Additive, breakdown.Modifiers[0].Stage);
            Assert.AreEqual(2, breakdown.Modifiers[0].CostBefore);
            Assert.AreEqual(1, breakdown.Modifiers[0].CostAfter);
        }

        [Test]
        public void CleanBuildAndCompileAheadFromCatalog_CombineToZero()
        {
            ContentCatalog catalog = Load();
            Assert.IsTrue(catalog.TryGetDependency(new DependencyID("WB-DEP-002"), out DependencyDefinition cleanBuild));
            Assert.IsTrue(catalog.TryGetDirective(new DirectiveID("WB-DIR-004"), out DirectiveDefinition compileAhead));
            ActiveCompilationEffect cleanBuildEffect = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(1), cleanBuild.DisplayName, cleanBuild.Effects[0]);
            ActiveCompilationEffect compileAheadEffect = ActiveCompilationEffect.For("WB-DIR-004", 0, new InstanceID(2), compileAhead.DisplayName, compileAhead.Effects[0]);

            CompilationCostBreakdown breakdown = CompilationCostResolver.Resolve(
                CompilationClassification.OrdinaryEdited,
                1,
                catalog.Parameters,
                new[] { cleanBuildEffect, compileAheadEffect },
                new CompilationEffectLedger());

            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.AreEqual(2, breakdown.Modifiers.Count);
            Assert.AreEqual(CostModifierStage.SetOrReplacement, breakdown.Modifiers[0].Stage);
            Assert.AreEqual(CostModifierStage.Additive, breakdown.Modifiers[1].Stage);
            Assert.IsTrue(breakdown.Modifiers[1].NumericallyRedundant);
        }

        [Test]
        public void ProcessScenario_FreeInitialThroughAffordabilityBlock()
        {
            ContentCatalog catalog = Load();
            ParameterSet parameters = catalog.Parameters;
            Assert.IsTrue(catalog.TryGetDependency(new DependencyID("WB-DEP-002"), out DependencyDefinition cleanBuildDefinition));
            Assert.IsTrue(catalog.TryGetDirective(new DirectiveID("WB-DIR-004"), out DirectiveDefinition compileAheadDefinition));

            InstructionInstance firstEdit = new InstructionInstance(new InstanceID(1), catalog.Instructions[0], null);
            InstructionInstance secondEdit = new InstructionInstance(new InstanceID(2), catalog.Instructions[1], null);
            DirectiveInstance compileAhead = new DirectiveInstance(new InstanceID(50), compileAheadDefinition);

            CatalogBuffer buffer = new CatalogBuffer();
            buffer.AddInstruction(firstEdit);
            buffer.AddInstruction(secondEdit);
            buffer.AddDirective(compileAhead);

            BuildState state = new BuildState(StandardCore(), buffer, parameters);
            ActiveCompilationEffect cleanBuild = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(100), cleanBuildDefinition.DisplayName, cleanBuildDefinition.Effects[0]);
            IReadOnlyList<ActiveCompilationEffect> installed = new[] { cleanBuild };

            CompilationAttempt initial = state.Compile(new ByteAmount(10), installed);
            Assert.IsTrue(initial.Committed);
            Assert.AreEqual(CompilationClassification.Initial, initial.Breakdown.Classification);
            Assert.AreEqual(0, initial.Breakdown.FinalCost);
            Assert.AreEqual(0, state.EditedCompilationCount);

            CompilationAttempt unchanged = state.Compile(new ByteAmount(10), installed);
            Assert.AreEqual(CompilationClassification.Unchanged, unchanged.Breakdown.Classification);
            Assert.AreEqual(0, unchanged.Breakdown.FinalCost);

            state.TryInstall(new InstanceID(1), new SourcePosition(3));
            state.TryActivateDirective(new InstanceID(50));
            CompilationAttempt combined = state.Compile(new ByteAmount(10), installed);

            Assert.IsTrue(combined.Committed);
            Assert.AreEqual(CompilationClassification.OrdinaryEdited, combined.Breakdown.Classification);
            Assert.AreEqual(0, combined.Breakdown.FinalCost);
            Assert.IsTrue(combined.Breakdown.AdvancesProgression);
            Assert.AreEqual(1, state.EditedCompilationCount);
            Assert.AreEqual(2, combined.Breakdown.Modifiers.Count);
            Assert.IsTrue(combined.Breakdown.Modifiers[1].NumericallyRedundant);
            CollectionAssert.AreEqual(new[] { compileAhead }, combined.Source.Pragmas);
            CollectionAssert.IsEmpty(state.PendingPragmas);

            state.TryInstall(new InstanceID(2), new SourcePosition(4));
            CompilationAttempt blocked = state.Compile(new ByteAmount(1), installed);

            Assert.IsFalse(blocked.Committed);
            Assert.AreEqual(CompilationBlockReason.InsufficientBytes, blocked.BlockReason);
            Assert.AreEqual(2, blocked.Breakdown.FinalCost);
            Assert.AreEqual(1, state.EditedCompilationCount);
            CollectionAssert.IsEmpty(state.ArchivedInstances);
        }

        private static int SweepCompilationEffects(
            string definitionID,
            string displayName,
            IReadOnlyList<EffectDefinition> effects,
            ParameterSet parameters)
        {
            int swept = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].PhaseDomain != PhaseDomain.Compilation)
                    continue;

                ActiveCompilationEffect active = ActiveCompilationEffect.For(definitionID, i, new InstanceID(1), displayName, effects[i]);
                CompilationCostResolver.Resolve(
                    CompilationClassification.OrdinaryEdited,
                    1,
                    parameters,
                    new[] { active },
                    new CompilationEffectLedger());
                swept++;
            }

            return swept;
        }

        private static SourceArrangement StandardCore()
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), _coreOne),
                SourceSlot.ForCore(new SourcePosition(2), _coreTwo),
                SourceSlot.ForEmpty(new SourcePosition(3)),
                SourceSlot.ForEmpty(new SourcePosition(4)),
                SourceSlot.ForEmpty(new SourcePosition(5)),
                SourceSlot.ForEmpty(new SourcePosition(6)),
                SourceSlot.ForEmpty(new SourcePosition(7)),
                SourceSlot.ForEmpty(new SourcePosition(8)),
                SourceSlot.ForCore(new SourcePosition(9), _coreNine)
            });
        }

        private static ContentCatalog Load()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// A minimal in-assembly <see cref="IBuildBuffer"/> for the Process scenario, holding instructions
        /// and Directives seeded from the loaded catalog. The Domain test double lives in another assembly,
        /// so the scenario carries its own.
        /// </summary>
        private sealed class CatalogBuffer : IBuildBuffer
        {
            private readonly List<InstructionInstance> _instructions = new List<InstructionInstance>();
            private readonly List<DirectiveInstance> _directives = new List<DirectiveInstance>();

            public bool HasRemovalCapacity => true;

            public void AddInstruction(InstructionInstance instruction)
            {
                _instructions.Add(instruction);
            }

            public void AddDirective(DirectiveInstance directive)
            {
                _directives.Add(directive);
            }

            public bool TryPeekInstruction(InstanceID instanceID, out InstructionInstance instance)
            {
                for (int i = 0; i < _instructions.Count; i++)
                {
                    if (_instructions[i].InstanceID == instanceID)
                    {
                        instance = _instructions[i];
                        return true;
                    }
                }

                instance = null;
                return false;
            }

            public bool TryPeekStructure(InstanceID instanceID, out StructureInstance instance)
            {
                instance = null;
                return false;
            }

            public bool TryPeekDirective(InstanceID instanceID, out DirectiveInstance instance)
            {
                for (int i = 0; i < _directives.Count; i++)
                {
                    if (_directives[i].InstanceID == instanceID)
                    {
                        instance = _directives[i];
                        return true;
                    }
                }

                instance = null;
                return false;
            }

            public void Take(InstanceID instanceID)
            {
                for (int i = 0; i < _instructions.Count; i++)
                {
                    if (_instructions[i].InstanceID == instanceID)
                    {
                        _instructions.RemoveAt(i);
                        return;
                    }
                }

                for (int i = 0; i < _directives.Count; i++)
                {
                    if (_directives[i].InstanceID == instanceID)
                    {
                        _directives.RemoveAt(i);
                        return;
                    }
                }
            }

            public void AcceptRemoved(InstructionInstance removed)
            {
                _instructions.Add(removed);
            }

            public void AcceptRemoved(StructureInstance removed)
            {
            }
        }
    }
}
