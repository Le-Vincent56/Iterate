using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests that the freezer turns the eight catalog-extension kinds into immutable definitions and
    /// that the frozen catalog exposes them, driven end to end through the read → parse → validate →
    /// freeze pipeline so the manifest, the rule sets and the freezer are all exercised together.
    /// </summary>
    public sealed class CatalogFreezerExtensionTests
    {
        [Test]
        public void Freeze_ProducesOneDefinitionOfEachExtensionKind()
        {
            ContentCatalog catalog = Freeze();

            Assert.AreEqual(1, catalog.Cores.Count);
            Assert.AreEqual(1, catalog.ProcessConfigurations.Count);
            Assert.AreEqual(1, catalog.Shops.Count);
            Assert.AreEqual(1, catalog.Pools.Count);
            Assert.AreEqual(1, catalog.RewardPackages.Count);
            Assert.AreEqual(1, catalog.Routes.Count);
            Assert.AreEqual(1, catalog.StarterArchetypes.Count);
            Assert.AreEqual(1, catalog.Systems.Count);
        }

        [Test]
        public void Freeze_CountsTheExtensionKindsInDefinitionCount()
        {
            ContentCatalog catalog = Freeze();

            int items = catalog.Instructions.Count
                        + catalog.Structures.Count
                        + catalog.Directives.Count
                        + catalog.Dependencies.Count
                        + catalog.Patches.Count
                        + catalog.Utilities.Count
                        + catalog.ProcessRules.Count;
            int packages = catalog.Cores.Count
                           + catalog.ProcessConfigurations.Count
                           + catalog.Shops.Count
                           + catalog.Pools.Count
                           + catalog.RewardPackages.Count
                           + catalog.Routes.Count
                           + catalog.StarterArchetypes.Count
                           + catalog.Systems.Count;

            Assert.AreEqual(items + packages, catalog.DefinitionCount);
        }

        [Test]
        public void Freeze_CoreCarriesItsLinesOpenCountAndFinalOutput()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetCore(new CoreID("WB-CORE-001"), out CoreDefinition core));
            Assert.AreEqual(3, core.Lines.Count);
            Assert.AreEqual(1, core.OpenCount);
            Assert.AreEqual(3, core.FinalOutputPosition.LineNumber);
            Assert.AreEqual(CoreLineKind.FixedInstruction, core.Lines[0].Kind);
            Assert.AreEqual(CoreLineKind.Open, core.Lines[1].Kind);
        }

        [Test]
        public void Freeze_CoreLineOperationIsTypedNotText()
        {
            ContentCatalog catalog = Freeze();

            catalog.TryGetCore(new CoreID("WB-CORE-001"), out CoreDefinition core);

            Assert.AreEqual(CoreLineOperator.Assign, core.Lines[0].Operation.Operator);
            Assert.AreEqual(CoreRegister.Value, core.Lines[0].Operation.Target);
            Assert.AreEqual(OperandSource.Constant, core.Lines[0].Operation.Operand.Source);
            Assert.AreEqual(1, core.Lines[0].Operation.Operand.Constant);
            Assert.AreEqual(CoreLineOperator.Add, core.Lines[2].Operation.Operator);
            Assert.AreEqual(CoreRegister.Score, core.Lines[2].Operation.Target);
        }

        [Test]
        public void Freeze_FixedStructureCoreLineCarriesItsPredicateAndContainedInstruction()
        {
            ContentCatalog catalog = Freeze(
                (CatalogExtensionFixtures.CoresFile, CatalogExtensionFixtures.FixedStructureCoresFile)
            );

            Assert.IsTrue(catalog.TryGetCore(new CoreID("WB-CORE-003"), out CoreDefinition core));

            CoreLineSpec structureLine = core.Lines[1];
            Assert.AreEqual(CoreLineKind.FixedStructure, structureLine.Kind);
            Assert.AreEqual(CoreRegister.Value, structureLine.Predicate.Register);
            Assert.AreEqual(PredicateComparison.IsEven, structureLine.Predicate.Comparison);
            Assert.AreEqual(CoreLineKind.FixedInstruction, structureLine.Contained.Kind);
            Assert.AreEqual(CoreLineOperator.Add, structureLine.Contained.Operation.Operator);
            Assert.AreEqual(1, core.OpenCount);
        }

        [Test]
        public void Freeze_ProcessConfigurationCarriesItsScriptedLoadAndOptionalReferences()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-001"), out ProcessConfigurationDefinition configuration));
            Assert.AreEqual(ProcessRole.Tutorial1, configuration.Role);
            Assert.AreEqual("WB-CORE-001", configuration.Core.Value);
            Assert.AreEqual("WB-PRC-001", configuration.ProcessRule.Value.Value);
            Assert.AreEqual(20, configuration.Thresholds.Pass);
            Assert.AreEqual(36, configuration.Thresholds.Benchmark);
            Assert.AreEqual(3, configuration.Executions);
            Assert.AreEqual(1, configuration.SourceCapacity);
            Assert.AreEqual(BufferLoadPolicy.Scripted, configuration.BufferLoad.Policy);
            Assert.AreEqual(1, configuration.BufferLoad.Arrivals.Count);
            Assert.AreEqual(1, configuration.BufferLoad.Arrivals[0].AfterExecution);
            Assert.AreEqual("WB-INS-003", configuration.BufferLoad.Arrivals[0].Items[0]);
            Assert.AreEqual(1, configuration.InitialSource.Count);
            Assert.AreEqual(2, configuration.InitialSource[0].Position);
            Assert.AreEqual("WB-INS-002", configuration.InitialSource[0].Content);
            Assert.AreEqual("WB-RWD-001", configuration.RewardPackage.Value);
        }

        [Test]
        public void Freeze_AbsentOptionalProcessFieldsAreNull()
        {
            ContentCatalog catalog = Freeze();

            catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-001"), out ProcessConfigurationDefinition configuration);

            Assert.IsNull(configuration.Exposure);
            Assert.IsNull(configuration.ActiveBranch);
            Assert.IsNull(configuration.PrecedingShop);
        }

        [Test]
        public void Freeze_ShopCarriesItsSwitchesAndOffers()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetShop(new ShopID("WB-SHOP-001"), out ShopDefinition shop));
            Assert.AreEqual(3, shop.Slots);
            Assert.IsFalse(shop.RerollsEnabled);
            Assert.IsNull(shop.RerollPool);
            Assert.AreEqual(1, shop.FixedOffers.Count);
            Assert.AreEqual("OFF-001", shop.FixedOffers[0].OfferID);
            Assert.AreEqual("WB-INS-002", shop.FixedOffers[0].Content);
            Assert.AreEqual(4, shop.FixedOffers[0].Price);
        }

        [Test]
        public void Freeze_PoolCarriesItsMethodCountAndMembers()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetPool(new PoolID("WB-POOL-001"), out PoolDefinition pool));
            Assert.AreEqual("PLAYER_CHOICE", pool.SelectionMethod);
            Assert.AreEqual(1, pool.SelectionCount);
            Assert.AreEqual(2, pool.Members.Count);
            Assert.AreEqual("WB-INS-002", pool.Members[0].Content);
            Assert.IsNull(pool.Members[0].Price);
        }

        [Test]
        public void Freeze_RewardPackageKeepsComponentOrder()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetRewardPackage(new RewardPackageID("WB-RWD-001"), out RewardPackageDefinition package));
            Assert.AreEqual(3, package.Components.Count);
            Assert.AreEqual(RewardComponentKind.Tokens, package.Components[0].Kind);
            Assert.AreEqual(4, package.Components[0].Amount);
            Assert.AreEqual(RewardComponentKind.PoolChoice, package.Components[1].Kind);
            Assert.AreEqual("WB-POOL-001", package.Components[1].Reference);
            Assert.AreEqual(RewardTier.Optimize, package.Components[2].Tier);
            Assert.AreEqual(RewardComponentKind.CachedPatch, package.Components[2].Kind);
        }

        [Test]
        public void Freeze_RouteArchetypeAndSystemCarryTheirReferences()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetRoute(new RouteID("WB-ROUTE-001"), out RouteDefinition route));
            Assert.AreEqual("WB-PROC-001", route.Process.Value);
            Assert.AreEqual("WB-SHOP-001", route.Shop.Value);

            Assert.IsTrue(catalog.TryGetStarterArchetype(new StarterArchetypeID("WB-ARCH-001"), out StarterArchetypeDefinition archetype));
            Assert.AreEqual(3, archetype.StartingRepository.Count);
            Assert.AreEqual("WB-INS-002", archetype.StartingRepository[1]);
            Assert.AreEqual("WB-DEP-001", archetype.StarterDependency.Value);

            Assert.IsTrue(catalog.TryGetSystem(new SystemID("WB-SYS-001"), out SystemDefinition system));
            Assert.AreEqual(3, system.Stages.Count);
            Assert.AreEqual(SystemStageKind.Process, system.Stages[0].Kind);
            Assert.AreEqual("WB-PROC-001", system.Stages[0].Process.Value.Value);
            Assert.AreEqual(SystemStageKind.Shop, system.Stages[1].Kind);
            Assert.AreEqual(SystemStageKind.RouteSelection, system.Stages[2].Kind);
            Assert.AreEqual("WB-ROUTE-001", system.Stages[2].Routes[0].Value);
        }

        [Test]
        public void Freeze_UnknownExtensionIDsDoNotResolve()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsFalse(catalog.TryGetCore(new CoreID("WB-CORE-404"), out _));
            Assert.IsFalse(catalog.TryGetShop(new ShopID("WB-SHOP-404"), out _));
            Assert.IsFalse(catalog.TryGetSystem(new SystemID("WB-SYS-404"), out _));
        }

        /// <summary>
        /// Loads the freezable extension file set end to end, with zero or more files replaced.
        /// </summary>
        /// <param name="overrides">The files to replace, as (name, json) pairs.</param>
        /// <returns>The frozen catalog.</returns>
        private static ContentCatalog Freeze(params (string Name, string Json)[] overrides)
        {
            Dictionary<string, string> files = CatalogExtensionFixtures.FreezableFiles(overrides);
            FakeCatalogFileSource source = new(CatalogExtensionFixtures.FreezableManifest, files);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
