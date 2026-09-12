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
    /// Verifies the shipped catalog at revision 0.2.0: the eight new files load through the real
    /// pipeline, carry the authored counts, and every ID they reference resolves through the catalog's
    /// own lookups. The authored values themselves are checked against the Design and Balance lines
    /// the proposal cites, so a transcription slip fails here rather than at the first playtest.
    /// </summary>
    public sealed class ShippedCatalogExtensionTests
    {
        [Test]
        public void ShippedCatalog_LoadsAtRevisionZeroTwoZero()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual("0.2.0", catalog.Revision);
        }

        [Test]
        public void ShippedCatalog_CarriesTheAuthoredExtensionCounts()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(5, catalog.Cores.Count, "Cores");
            Assert.AreEqual(5, catalog.ProcessConfigurations.Count, "Process configurations");
            Assert.AreEqual(4, catalog.Shops.Count, "shops");
            Assert.AreEqual(9, catalog.Pools.Count, "pools");
            Assert.AreEqual(5, catalog.RewardPackages.Count, "reward packages");
            Assert.AreEqual(2, catalog.Routes.Count, "routes");
            Assert.AreEqual(1, catalog.StarterArchetypes.Count, "Starter Archetypes");
            Assert.AreEqual(1, catalog.Systems.Count, "Systems");
        }

        [Test]
        public void ShippedCatalog_CarriesBothProcessRules()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(2, catalog.ProcessRules.Count);
            Assert.IsTrue(catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-001"), out _));
            Assert.IsTrue(catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-002"), out ProcessRuleDefinition free));
            Assert.AreEqual("TUTORIAL FREE COMPILATION", free.DisplayName);
        }

        [Test]
        public void TutorialFreeCompilation_IsACompilationDomainCostModificationToZero()
        {
            ContentCatalog catalog = Load();
            catalog.TryGetProcessRule(new ProcessRuleID("WB-PRC-002"), out ProcessRuleDefinition rule);

            Assert.AreEqual(1, rule.Effects.Count);
            EffectDefinition effect = rule.Effects[0];
            Assert.AreEqual(PhaseDomain.Compilation, effect.PhaseDomain);

            CostModificationOperation operation = effect.Operation as CostModificationOperation;
            Assert.IsNotNull(operation, "WB-PRC-002 must carry a cost modification.");
            Assert.IsTrue(operation.SetsAbsolute);
            Assert.AreEqual(0, operation.Amount);
            Assert.AreEqual(0, operation.Floor);
        }

        [Test]
        public void EveryCore_HasContiguousLinesAndAFixedInstructionFinalOutput()
        {
            ContentCatalog catalog = Load();

            foreach (CoreDefinition core in catalog.Cores)
            {
                for (int index = 0; index < core.Lines.Count; index++)
                {
                    Assert.AreEqual(index + 1, core.Lines[index].Position, core.ID.Value + " line positions");
                }

                int finalOutput = core.FinalOutputPosition.LineNumber;
                Assert.LessOrEqual(finalOutput, core.Lines.Count, core.ID.Value + " final output");
                Assert.AreEqual(
                    CoreLineKind.FixedInstruction,
                    core.Lines[finalOutput - 1].Kind,
                    core.ID.Value + " final output kind"
                );
            }
        }

        [Test]
        public void EveryProcessConfiguration_SourceCapacityMatchesItsCoresOpenCount()
        {
            ContentCatalog catalog = Load();

            foreach (ProcessConfigurationDefinition configuration in catalog.ProcessConfigurations)
            {
                Assert.IsTrue(catalog.TryGetCore(configuration.Core, out CoreDefinition core), configuration.ID.Value);
                Assert.AreEqual(
                    core.OpenCount,
                    configuration.SourceCapacity,
                    configuration.ID.Value + " source capacity versus " + core.ID.Value + " open lines"
                );
            }
        }

        [Test]
        public void TutorialProcessOne_CarriesItsAuthoredShape()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-001"), out ProcessConfigurationDefinition configuration));
            Assert.AreEqual(ProcessRole.Tutorial1, configuration.Role);
            Assert.AreEqual(3, configuration.Executions);
            Assert.AreEqual(5, configuration.SourceCapacity);
            Assert.AreEqual(5, configuration.BufferCapacity);
            Assert.AreEqual(0, configuration.StartingBytes);
            Assert.AreEqual(20, configuration.Thresholds.Pass);
            Assert.AreEqual(30, configuration.Thresholds.Optimize);
            Assert.AreEqual(36, configuration.Thresholds.Benchmark);
            Assert.AreEqual("WB-PRC-002", configuration.ProcessRule.Value.Value);
            Assert.IsNull(configuration.ActiveBranch);
            Assert.IsNull(configuration.Exposure);
        }

        [Test]
        public void TutorialProcessOne_PreInstallsItsThreeStartingItems()
        {
            ContentCatalog catalog = Load();
            catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-001"), out ProcessConfigurationDefinition configuration);

            Assert.AreEqual(3, configuration.InitialSource.Count);
            Assert.AreEqual("WB-INS-002", configuration.InitialSource[0].Content);
            Assert.AreEqual("WB-INS-002", configuration.InitialSource[1].Content);
            Assert.AreEqual("WB-INS-012", configuration.InitialSource[2].Content);
            Assert.AreEqual(0, configuration.BufferLoad.Initial.Count);
        }

        [Test]
        public void TutorialProcessOne_ScriptsItsTwoArrivals()
        {
            ContentCatalog catalog = Load();
            catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-001"), out ProcessConfigurationDefinition configuration);

            Assert.AreEqual(BufferLoadPolicy.Scripted, configuration.BufferLoad.Policy);
            Assert.AreEqual(2, configuration.BufferLoad.Arrivals.Count);
            Assert.AreEqual(1, configuration.BufferLoad.Arrivals[0].AfterExecution);
            Assert.AreEqual("WB-INS-005", configuration.BufferLoad.Arrivals[0].Items[0]);
            Assert.AreEqual(2, configuration.BufferLoad.Arrivals[1].AfterExecution);
            Assert.AreEqual("WB-INS-003", configuration.BufferLoad.Arrivals[1].Items[0]);
        }

        [Test]
        public void TutorialProcessTwo_OverflowsAfterExecutionThreeInAuthoredOrder()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-002"), out ProcessConfigurationDefinition configuration));
            Assert.AreEqual(3, configuration.BufferCapacity);
            Assert.AreEqual(4, configuration.Executions);
            Assert.IsTrue(configuration.MandatoryExecutions);
            Assert.AreEqual(3, configuration.StartingBytes);

            ArrivalLoadSpec third = configuration.BufferLoad.Arrivals[2];
            Assert.AreEqual(3, third.AfterExecution);
            Assert.AreEqual(2, third.Items.Count);
            Assert.AreEqual("WB-DIR-001", third.Items[0], "OVERCLOCK enters before the Instruction.");
            Assert.AreEqual("WB-INS-003", third.Items[1]);
        }

        [Test]
        public void BothProcessThreeRoutes_DrawSixOfNineWithTheConditionGuaranteed()
        {
            ContentCatalog catalog = Load();

            foreach (string id in new[] { "WB-PROC-003", "WB-PROC-004" })
            {
                catalog.TryGetProcessConfiguration(new ProcessID(id), out ProcessConfigurationDefinition configuration);

                Assert.AreEqual(BufferLoadPolicy.Drawn, configuration.BufferLoad.Policy, id);
                Assert.AreEqual(3, configuration.BufferLoad.InitialCount, id);
                Assert.AreEqual(3, configuration.BufferLoad.ArrivalsAfterExecutions.Count, id);
                Assert.AreEqual(9, configuration.ActiveBranch.Capacity, id);
                Assert.AreEqual(1, configuration.Exposure.Guaranteed.Count, id);
                Assert.AreEqual("WB-STR-002", configuration.Exposure.Guaranteed[0], id);
                Assert.AreEqual(1, configuration.ActiveBranch.Required.Count, id);
                Assert.AreEqual("WB-STR-002", configuration.ActiveBranch.Required[0], id);
            }
        }

        [Test]
        public void ThermalThrottle_CarriesItsAuthoredShapeAndHeatRule()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-005"), out ProcessConfigurationDefinition configuration));
            Assert.AreEqual(ProcessRole.CriticalProcess, configuration.Role);
            Assert.AreEqual(4, configuration.BufferCapacity);
            Assert.AreEqual(6, configuration.SourceCapacity);
            Assert.AreEqual(64, configuration.Thresholds.Pass);
            Assert.AreEqual(112, configuration.Thresholds.Optimize);
            Assert.AreEqual(192, configuration.Thresholds.Benchmark);
            Assert.AreEqual("WB-PRC-001", configuration.ProcessRule.Value.Value);
            Assert.AreEqual(0, configuration.Exposure.Guaranteed.Count, "six-of-nine exposure guarantees nothing.");
        }

        [Test]
        public void EveryProcessReference_Resolves()
        {
            ContentCatalog catalog = Load();

            foreach (ProcessConfigurationDefinition configuration in catalog.ProcessConfigurations)
            {
                string id = configuration.ID.Value;
                Assert.IsTrue(catalog.TryGetCore(configuration.Core, out _), id + " core");
                Assert.IsTrue(catalog.TryGetRewardPackage(configuration.RewardPackage, out _), id + " reward package");

                if (configuration.ProcessRule.HasValue)
                    Assert.IsTrue(catalog.TryGetProcessRule(configuration.ProcessRule.Value, out _), id + " process rule");

                if (configuration.PrecedingShop.HasValue)
                    Assert.IsTrue(catalog.TryGetShop(configuration.PrecedingShop.Value, out _), id + " preceding shop");

                AssertItemsResolve(catalog, InitialSourceContent(configuration), id + " initial source");
                AssertItemsResolve(catalog, configuration.BufferLoad.Initial, id + " initial Buffer");

                foreach (ArrivalLoadSpec arrival in configuration.BufferLoad.Arrivals)
                {
                    AssertItemsResolve(catalog, arrival.Items, id + " arrival " + arrival.AfterExecution);
                }

                if (configuration.Exposure != null)
                    AssertItemsResolve(catalog, configuration.Exposure.Guaranteed, id + " guaranteed exposure");

                if (configuration.ActiveBranch != null)
                {
                    AssertItemsResolve(catalog, configuration.ActiveBranch.Required, id + " required");
                    AssertItemsResolve(catalog, configuration.ActiveBranch.Quarantined, id + " quarantined");
                }
            }
        }

        [Test]
        public void EveryShopAndPoolReference_Resolves()
        {
            ContentCatalog catalog = Load();

            foreach (ShopDefinition shop in catalog.Shops)
            {
                foreach (ShopOffer offer in shop.FixedOffers)
                {
                    Assert.IsTrue(ResolvesAsContent(catalog, offer.Content), shop.ID.Value + " offer " + offer.OfferID);
                }

                if (shop.RerollPool.HasValue)
                    Assert.IsTrue(catalog.TryGetPool(shop.RerollPool.Value, out _), shop.ID.Value + " reroll pool");
                else
                    Assert.IsFalse(shop.RerollsEnabled, shop.ID.Value + " rerolls without a pool");
            }

            foreach (PoolDefinition pool in catalog.Pools)
            {
                foreach (PoolMember member in pool.Members)
                {
                    Assert.IsTrue(ResolvesAsContent(catalog, member.Content), pool.ID.Value + " member " + member.Content);
                }
            }
        }

        [Test]
        public void EveryRewardRouteArchetypeAndSystemReference_Resolves()
        {
            ContentCatalog catalog = Load();

            foreach (RewardPackageDefinition package in catalog.RewardPackages)
            {
                foreach (RewardComponent component in package.Components)
                {
                    if (component.Kind == RewardComponentKind.PoolChoice)
                        Assert.IsTrue(catalog.TryGetPool(new PoolID(component.Reference), out _), package.ID.Value);
                    else if (component.Kind == RewardComponentKind.Patch || component.Kind == RewardComponentKind.CachedPatch)
                        Assert.IsTrue(catalog.TryGetPatch(new PatchID(component.Reference), out _), package.ID.Value);
                    else if (component.Kind == RewardComponentKind.GuaranteedContent)
                        Assert.IsTrue(ResolvesAsContent(catalog, component.Reference), package.ID.Value);
                }
            }

            foreach (RouteDefinition route in catalog.Routes)
            {
                Assert.IsTrue(catalog.TryGetProcessConfiguration(route.Process, out _), route.ID.Value + " process");
                Assert.IsTrue(catalog.TryGetShop(route.Shop, out _), route.ID.Value + " shop");
            }

            foreach (StarterArchetypeDefinition archetype in catalog.StarterArchetypes)
            {
                AssertItemsResolve(catalog, archetype.StartingRepository, archetype.ID.Value);
                Assert.IsTrue(catalog.TryGetDependency(archetype.StarterDependency, out _), archetype.ID.Value + " starter Dependency");
            }

            foreach (SystemDefinition system in catalog.Systems)
            {
                foreach (SystemStage stage in system.Stages)
                {
                    if (stage.Process.HasValue)
                        Assert.IsTrue(catalog.TryGetProcessConfiguration(stage.Process.Value, out _), system.ID.Value);

                    if (stage.Shop.HasValue)
                        Assert.IsTrue(catalog.TryGetShop(stage.Shop.Value, out _), system.ID.Value);

                    foreach (RouteID route in stage.Routes)
                    {
                        Assert.IsTrue(catalog.TryGetRoute(route, out _), system.ID.Value + " route " + route.Value);
                    }
                }
            }
        }

        [Test]
        public void DirectOutput_SeedsNineItemsAndAZeroRAMStarterDependency()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetStarterArchetype(new StarterArchetypeID("WB-ARCH-001"), out StarterArchetypeDefinition archetype));
            Assert.AreEqual("Direct Output", archetype.DisplayName);
            Assert.AreEqual(9, archetype.StartingRepository.Count);
            Assert.AreEqual("WB-DEP-001", archetype.StarterDependency.Value);

            Assert.IsTrue(catalog.TryGetDependency(archetype.StarterDependency, out DependencyDefinition dependency));
            Assert.AreEqual(0, dependency.RAM, "the starter Dependency must consume no RAM.");
        }

        [Test]
        public void DirectOutput_ListsValuePlusTwoTwice()
        {
            ContentCatalog catalog = Load();
            catalog.TryGetStarterArchetype(new StarterArchetypeID("WB-ARCH-001"), out StarterArchetypeDefinition archetype);

            int count = 0;
            foreach (string id in archetype.StartingRepository)
            {
                if (id == "WB-INS-002")
                    count++;
            }

            Assert.AreEqual(2, count);
        }

        [Test]
        public void SystemOne_WalksTheOnboardingStagesInOrder()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetSystem(new SystemID("WB-SYS-001"), out SystemDefinition system));

            Assert.AreEqual(SystemStageKind.Process, system.Stages[0].Kind);
            Assert.AreEqual("WB-PROC-001", system.Stages[0].Process.Value.Value);
            Assert.AreEqual(SystemStageKind.Shop, system.Stages[1].Kind);
            Assert.AreEqual("WB-SHOP-001", system.Stages[1].Shop.Value.Value);
            Assert.AreEqual(SystemStageKind.Process, system.Stages[2].Kind);
            Assert.AreEqual("WB-PROC-002", system.Stages[2].Process.Value.Value);
            Assert.AreEqual(SystemStageKind.RouteSelection, system.Stages[3].Kind);
            Assert.AreEqual(2, system.Stages[3].Routes.Count);
            Assert.AreEqual(SystemStageKind.Shop, system.Stages[4].Kind);
            Assert.AreEqual("WB-SHOP-004", system.Stages[4].Shop.Value.Value);
            Assert.AreEqual(SystemStageKind.Process, system.Stages[5].Kind);
            Assert.AreEqual("WB-PROC-005", system.Stages[5].Process.Value.Value);
            Assert.AreEqual(6, system.Stages.Count);
        }

        [Test]
        public void ParityCheckCore_CarriesItsFixedStructureAsOneLine()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetCore(new CoreID("WB-CORE-003"), out CoreDefinition core));
            Assert.AreEqual(11, core.Lines.Count, "lines 10 and 11 collapse into one fixed Structure.");
            Assert.AreEqual(6, core.OpenCount);
            Assert.AreEqual(11, core.FinalOutputPosition.LineNumber);

            CoreLineSpec structureLine = core.Lines[9];
            Assert.AreEqual(CoreLineKind.FixedStructure, structureLine.Kind);
            Assert.AreEqual(CoreRegister.Value, structureLine.Predicate.Register);
            Assert.AreEqual(PredicateComparison.IsEven, structureLine.Predicate.Comparison);
            Assert.AreEqual(CoreLineKind.FixedInstruction, structureLine.Contained.Kind);
        }

        [Test]
        public void OnlyParityCheckCore_CarriesAFixedStructureLine()
        {
            ContentCatalog catalog = Load();

            foreach (CoreDefinition core in catalog.Cores)
            {
                foreach (CoreLineSpec line in core.Lines)
                {
                    if (line.Kind == CoreLineKind.FixedStructure)
                        Assert.AreEqual("WB-CORE-003", core.ID.Value, "only PARITY CHECK's Core owns a Structure.");
                }
            }
        }

        [Test]
        public void ThePreCriticalShop_EnablesDependenciesAndServices()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetShop(new ShopID("WB-SHOP-004"), out ShopDefinition shop));
            Assert.AreEqual(4, shop.Slots);
            Assert.IsTrue(shop.RerollsEnabled);
            Assert.IsTrue(shop.PinningEnabled);
            Assert.IsTrue(shop.DependenciesEnabled);
            Assert.IsTrue(shop.ServicesEnabled);
            Assert.AreEqual("WB-POOL-005", shop.RerollPool.Value.Value);
        }

        [Test]
        public void TheSimplifiedShop_DisablesEverythingAndOffersThree()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetShop(new ShopID("WB-SHOP-001"), out ShopDefinition shop));
            Assert.AreEqual(3, shop.Slots);
            Assert.IsFalse(shop.RerollsEnabled);
            Assert.IsFalse(shop.PinningEnabled);
            Assert.IsFalse(shop.DependenciesEnabled);
            Assert.IsFalse(shop.ServicesEnabled);
            Assert.IsNull(shop.RerollPool);
            Assert.AreEqual(3, shop.FixedOffers.Count);
        }

        [Test]
        public void ThermalThrottleRewards_ResolveInBalanceOrder()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetRewardPackage(new RewardPackageID("WB-RWD-005"), out RewardPackageDefinition package));
            Assert.AreEqual(6, package.Components.Count);
            Assert.AreEqual(RewardComponentKind.Tokens, package.Components[0].Kind);
            Assert.AreEqual(6, package.Components[0].Amount);
            Assert.AreEqual(RewardComponentKind.PoolChoice, package.Components[1].Kind);
            Assert.AreEqual("WB-POOL-008", package.Components[1].Reference);
            Assert.AreEqual(RewardComponentKind.RAMSet, package.Components[2].Kind);
            Assert.AreEqual(5, package.Components[2].Amount);
            Assert.AreEqual(RewardComponentKind.PoolChoice, package.Components[3].Kind);
            Assert.AreEqual("WB-POOL-009", package.Components[3].Reference);
            Assert.AreEqual(RewardComponentKind.Patch, package.Components[4].Kind);
            Assert.AreEqual(RewardTier.Benchmark, package.Components[5].Tier);
        }

        [Test]
        public void PowerSurgeRewards_KeepTokensComponentised()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetRewardPackage(new RewardPackageID("WB-RWD-004"), out RewardPackageDefinition package));

            bool hasBaseTokens = false;
            bool hasRouteBonus = false;
            bool hasBenchmarkTokens = false;
            foreach (RewardComponent component in package.Components)
            {
                if (component.Kind == RewardComponentKind.Tokens && component.Tier == RewardTier.Pass)
                    hasBaseTokens = true;

                if (component.Kind == RewardComponentKind.RouteTokenBonus)
                    hasRouteBonus = true;

                if (component.Kind == RewardComponentKind.Tokens && component.Tier == RewardTier.Benchmark)
                    hasBenchmarkTokens = true;
            }

            Assert.IsTrue(hasBaseTokens, "base success Tokens");
            Assert.IsTrue(hasRouteBonus, "route bonus stays separate");
            Assert.IsTrue(hasBenchmarkTokens, "Benchmark bonus stays separate");
        }

        [Test]
        public void EveryDefinitionCount_MatchesTheLoadedCatalog()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(79, catalog.DefinitionCount);
        }

        /// <summary>
        /// Collects a configuration's pre-installed content IDs.
        /// </summary>
        /// <param name="configuration">The Process configuration.</param>
        /// <returns>The pre-installed content IDs in authored order.</returns>
        private static IReadOnlyList<string> InitialSourceContent(ProcessConfigurationDefinition configuration)
        {
            List<string> content = new(configuration.InitialSource.Count);
            foreach (InitialSourceSpec entry in configuration.InitialSource)
            {
                content.Add(entry.Content);
            }

            return content;
        }

        /// <summary>
        /// Asserts that every ID in the list resolves to content the catalog defines.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="ids">The content IDs to resolve.</param>
        /// <param name="label">The assertion label naming the referencing site.</param>
        private static void AssertItemsResolve(ContentCatalog catalog, IReadOnlyList<string> ids, string label)
        {
            foreach (string id in ids)
            {
                Assert.IsTrue(ResolvesAsContent(catalog, id), label + ": " + id);
            }
        }

        /// <summary>
        /// Whether an authored content ID resolves through the catalog — as a Repository item through
        /// the single item seam, or as one of the three installable kinds a shop or pool may offer.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="id">The content ID.</param>
        /// <returns>True when the ID names content the catalog defines.</returns>
        private static bool ResolvesAsContent(ContentCatalog catalog, string id)
        {
            if (catalog.TryGetItem(id, out _))
                return true;

            if (id.StartsWith("WB-DEP-", System.StringComparison.Ordinal))
                return catalog.TryGetDependency(new DependencyID(id), out _);

            if (id.StartsWith("WB-PAT-", System.StringComparison.Ordinal))
                return catalog.TryGetPatch(new PatchID(id), out _);

            if (id.StartsWith("WB-UTL-", System.StringComparison.Ordinal))
                return catalog.TryGetUtility(new UtilityID(id), out _);

            return false;
        }

        /// <summary>
        /// Loads the real shipped catalog from the streaming-assets path through the whole pipeline.
        /// </summary>
        /// <returns>The frozen shipped catalog.</returns>
        private static ContentCatalog Load()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
