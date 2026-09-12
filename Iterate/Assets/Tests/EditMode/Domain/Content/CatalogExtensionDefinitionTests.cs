using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests the eight catalog-extension definition kinds and their element records: the validating
    /// property initializers that reject a malformed authored shape at construction (defense-in-depth
    /// behind the validator), the derived Core open-line count, the widened
    /// <see cref="CatalogFileKind"/> vocabulary, and the pool selection-method registry.
    /// </summary>
    public sealed class CatalogExtensionDefinitionTests
    {
        private static readonly SourcePosition _finalOutput = new(3);

        [Test]
        public void CoreID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreID(string.Empty));
        }

        [Test]
        public void ShopID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ShopID(string.Empty));
        }

        [Test]
        public void PoolID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PoolID(string.Empty));
        }

        [Test]
        public void RewardPackageID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RewardPackageID(string.Empty));
        }

        [Test]
        public void RouteID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RouteID(string.Empty));
        }

        [Test]
        public void StarterArchetypeID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StarterArchetypeID(string.Empty));
        }

        [Test]
        public void SystemID_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SystemID(string.Empty));
        }

        [Test]
        public void CoreID_ToString_ReturnsIdentity()
        {
            CoreID id = new("WB-CORE-001");

            Assert.AreEqual("WB-CORE-001", id.ToString());
        }

        [Test]
        public void SystemID_ToString_ReturnsIdentity()
        {
            SystemID id = new("WB-SYS-001");

            Assert.AreEqual("WB-SYS-001", id.ToString());
        }

        [Test]
        public void FixedInstructionLine_WithOperation_Constructs()
        {
            CoreLineSpec line = FixedInstruction(1);

            Assert.AreEqual(CoreLineKind.FixedInstruction, line.Kind);
            Assert.IsNotNull(line.Operation);
            Assert.IsNull(line.Predicate);
            Assert.IsNull(line.Contained);
        }

        [Test]
        public void FixedInstructionLine_WithoutOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLineSpec(
                1,
                CoreLineKind.FixedInstruction,
                null,
                null,
                null
            ));
        }

        [Test]
        public void OpenLine_WithoutOperation_Constructs()
        {
            CoreLineSpec line = Open(2);

            Assert.AreEqual(CoreLineKind.Open, line.Kind);
            Assert.IsNull(line.Operation);
        }

        [Test]
        public void OpenLine_WithOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLineSpec(
                2,
                CoreLineKind.Open,
                Assignment(),
                null,
                null
            ));
        }

        [Test]
        public void FixedStructureLine_WithPredicateAndContained_Constructs()
        {
            CoreLineSpec line = FixedStructure(1);

            Assert.AreEqual(CoreLineKind.FixedStructure, line.Kind);
            Assert.IsNotNull(line.Predicate);
            Assert.AreEqual(CoreLineKind.FixedInstruction, line.Contained.Kind);
        }

        [Test]
        public void FixedStructureLine_WithoutPredicate_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLineSpec(
                1,
                CoreLineKind.FixedStructure,
                null,
                null,
                FixedInstruction(0)
            ));
        }

        [Test]
        public void FixedStructureLine_WithoutContained_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLineSpec(
                1,
                CoreLineKind.FixedStructure,
                null,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0),
                null
            ));
        }

        [Test]
        public void FixedStructureLine_ContainingAnOpenLine_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLineSpec(
                1,
                CoreLineKind.FixedStructure,
                null,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0),
                Open(0)
            ));
        }

        [Test]
        public void CoreDefinition_WithContiguousPositions_Constructs()
        {
            CoreDefinition core = new(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                new CoreLineSpec[] { FixedInstruction(1), Open(2), FixedInstruction(3) },
                _finalOutput
            );

            Assert.AreEqual(3, core.Lines.Count);
            Assert.AreEqual(3, core.FinalOutputPosition.LineNumber);
        }

        [Test]
        public void CoreDefinition_OpenCount_CountsOpenLinesOnly()
        {
            CoreDefinition core = new(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                new CoreLineSpec[] { FixedInstruction(1), Open(2), Open(3), FixedInstruction(4) },
                new SourcePosition(4)
            );

            Assert.AreEqual(2, core.OpenCount);
        }

        [Test]
        public void CoreDefinition_WithNonContiguousPositions_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreDefinition(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                new CoreLineSpec[] { FixedInstruction(1), Open(3), FixedInstruction(4) },
                new SourcePosition(4)
            ));
        }

        [Test]
        public void CoreDefinition_NotStartingAtPositionOne_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreDefinition(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                new CoreLineSpec[] { FixedInstruction(2), Open(3) },
                new SourcePosition(2)
            ));
        }

        [Test]
        public void CoreDefinition_WithNoLines_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreDefinition(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                Array.Empty<CoreLineSpec>(),
                new SourcePosition(1)
            ));
        }

        [Test]
        public void CoreDefinition_FinalOutputOnAnOpenLine_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreDefinition(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                new CoreLineSpec[] { FixedInstruction(1), Open(2) },
                new SourcePosition(2)
            ));
        }

        [Test]
        public void CoreDefinition_FinalOutputOnAFixedStructureLine_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreDefinition(
                new CoreID("WB-CORE-003"),
                "PARITY CHECK Core",
                new CoreLineSpec[] { FixedInstruction(1), FixedStructure(2) },
                new SourcePosition(2)
            ));
        }

        [Test]
        public void CoreDefinition_FinalOutputBeyondTheLineCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreDefinition(
                new CoreID("WB-CORE-001"),
                "Tutorial Process 1 Core",
                new CoreLineSpec[] { FixedInstruction(1), Open(2) },
                new SourcePosition(9)
            ));
        }

        [Test]
        public void ProcessThresholdSpec_Ascending_Constructs()
        {
            ProcessThresholdSpec thresholds = new(20, 30, 36);

            Assert.AreEqual(20, thresholds.Pass);
            Assert.AreEqual(30, thresholds.Optimize);
            Assert.AreEqual(36, thresholds.Benchmark);
        }

        [Test]
        public void ProcessThresholdSpec_EqualPassAndOptimize_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessThresholdSpec(20, 20, 36));
        }

        [Test]
        public void ProcessThresholdSpec_DescendingBenchmark_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessThresholdSpec(20, 30, 25));
        }

        [Test]
        public void ProcessConfigurationDefinition_Scripted_Constructs()
        {
            ProcessConfigurationDefinition configuration = TutorialOne();

            Assert.AreEqual(ProcessRole.Tutorial1, configuration.Role);
            Assert.AreEqual(BufferLoadPolicy.Scripted, configuration.BufferLoad.Policy);
            Assert.IsNull(configuration.ActiveBranch);
            Assert.IsNull(configuration.PrecedingShop);
            Assert.AreEqual("WB-PRC-002", configuration.ProcessRule.Value.Value);
        }

        [Test]
        public void ArrivalLoadSpec_WithItems_Constructs()
        {
            ArrivalLoadSpec arrival = new(1, new[] { "WB-INS-005" });

            Assert.AreEqual(1, arrival.AfterExecution);
            Assert.AreEqual(1, arrival.Items.Count);
        }

        [Test]
        public void ShopDefinition_Constructs()
        {
            ShopDefinition shop = new(
                new ShopID("WB-SHOP-001"),
                "Simplified post-Tutorial-1 shop",
                3,
                false,
                false,
                false,
                false,
                new[] { new ShopOffer("OFF-001", "WB-INS-001", 4) },
                null
            );

            Assert.AreEqual(3, shop.Slots);
            Assert.IsFalse(shop.RerollsEnabled);
            Assert.IsNull(shop.RerollPool);
            Assert.AreEqual("OFF-001", shop.FixedOffers[0].OfferID);
        }

        [Test]
        public void PoolDefinition_WithSelectionMethod_Constructs()
        {
            PoolDefinition pool = new(
                new PoolID("WB-POOL-001"),
                "Process 1 fixed reward pool",
                "PLAYER_CHOICE",
                1,
                new[] { new PoolMember("WB-INS-004", null) }
            );

            Assert.AreEqual("PLAYER_CHOICE", pool.SelectionMethod);
            Assert.AreEqual(1, pool.SelectionCount);
            Assert.IsNull(pool.Members[0].Price);
        }

        [Test]
        public void PoolDefinition_WithEmptySelectionMethod_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PoolDefinition(
                new PoolID("WB-POOL-001"),
                "Process 1 fixed reward pool",
                string.Empty,
                1,
                new[] { new PoolMember("WB-INS-004", null) }
            ));
        }

        [Test]
        public void PoolDefinition_UniformWithoutReplacementAndNoCount_Constructs()
        {
            PoolDefinition pool = new(
                new PoolID("WB-POOL-002"),
                "Shared Process 3 reroll pool",
                "UNIFORM_WITHOUT_REPLACEMENT",
                null,
                new[] { new PoolMember("WB-INS-002", 4) }
            );

            Assert.IsNull(pool.SelectionCount);
            Assert.AreEqual(4, pool.Members[0].Price);
        }

        [Test]
        public void RewardComponent_TokensWithAmount_Constructs()
        {
            RewardComponent component = new(RewardTier.Pass, RewardComponentKind.Tokens, 4, null);

            Assert.AreEqual(4, component.Amount);
            Assert.IsNull(component.Reference);
        }

        [Test]
        public void RewardComponent_TokensWithoutAmount_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new RewardComponent(RewardTier.Pass, RewardComponentKind.Tokens, null, null));
        }

        [Test]
        public void RewardComponent_RAMSetWithoutAmount_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new RewardComponent(RewardTier.Pass, RewardComponentKind.RAMSet, null, null));
        }

        [Test]
        public void RewardComponent_TokensWithReference_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new RewardComponent(RewardTier.Pass, RewardComponentKind.Tokens, 4, "WB-POOL-001"));
        }

        [Test]
        public void RewardComponent_PoolChoiceWithReference_Constructs()
        {
            RewardComponent component = new(RewardTier.Pass, RewardComponentKind.PoolChoice, null, "WB-POOL-001");

            Assert.AreEqual("WB-POOL-001", component.Reference);
            Assert.IsNull(component.Amount);
        }

        [Test]
        public void RewardComponent_PoolChoiceWithoutReference_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new RewardComponent(RewardTier.Pass, RewardComponentKind.PoolChoice, null, null));
        }

        [Test]
        public void RewardComponent_PatchWithAmount_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new RewardComponent(RewardTier.Optimize, RewardComponentKind.Patch, 1, "WB-PAT-002"));
        }

        [Test]
        public void RewardPackageDefinition_KeepsComponentOrder()
        {
            RewardPackageDefinition package = new(
                new RewardPackageID("WB-RWD-001"),
                "Tutorial Process 1 rewards",
                new[]
                {
                    new RewardComponent(RewardTier.Pass, RewardComponentKind.Tokens, 4, null),
                    new RewardComponent(RewardTier.Pass, RewardComponentKind.PoolChoice, null, "WB-POOL-001"),
                    new RewardComponent(RewardTier.Benchmark, RewardComponentKind.Tokens, 2, null)
                }
            );

            Assert.AreEqual(3, package.Components.Count);
            Assert.AreEqual(RewardComponentKind.PoolChoice, package.Components[1].Kind);
        }

        [Test]
        public void RouteDefinition_Constructs()
        {
            RouteDefinition route = new(
                new RouteID("WB-ROUTE-001"),
                "PARITY CHECK",
                new ProcessID("WB-PROC-003"),
                new ShopID("WB-SHOP-002")
            );

            Assert.AreEqual("WB-PROC-003", route.Process.Value);
            Assert.AreEqual("WB-SHOP-002", route.Shop.Value);
        }

        [Test]
        public void StarterArchetypeDefinition_AllowsDuplicateContent()
        {
            StarterArchetypeDefinition archetype = new(
                new StarterArchetypeID("WB-ARCH-001"),
                "Direct Output",
                new[] { "WB-INS-002", "WB-INS-002", "WB-INS-012" },
                new DependencyID("WB-DEP-001")
            );

            Assert.AreEqual(3, archetype.StartingRepository.Count);
            Assert.AreEqual("WB-INS-002", archetype.StartingRepository[1]);
            Assert.AreEqual("WB-DEP-001", archetype.StarterDependency.Value);
        }

        [Test]
        public void StarterArchetypeDefinition_WithEmptyRepository_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StarterArchetypeDefinition(
                new StarterArchetypeID("WB-ARCH-001"),
                "Direct Output",
                Array.Empty<string>(),
                new DependencyID("WB-DEP-001")
            ));
        }

        [Test]
        public void SystemStage_Process_Constructs()
        {
            SystemStage stage = new(SystemStageKind.Process, new ProcessID("WB-PROC-001"), null, null);

            Assert.AreEqual(SystemStageKind.Process, stage.Kind);
            Assert.AreEqual("WB-PROC-001", stage.Process.Value.Value);
        }

        [Test]
        public void SystemStage_ProcessWithoutProcess_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new SystemStage(SystemStageKind.Process, null, null, null));
        }

        [Test]
        public void SystemStage_ShopWithoutShop_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new SystemStage(SystemStageKind.Shop, null, null, null));
        }

        [Test]
        public void SystemStage_RouteSelectionWithRoutes_Constructs()
        {
            SystemStage stage = new(
                SystemStageKind.RouteSelection,
                null,
                null,
                new[] { new RouteID("WB-ROUTE-001"), new RouteID("WB-ROUTE-002") }
            );

            Assert.AreEqual(2, stage.Routes.Count);
        }

        [Test]
        public void SystemStage_RouteSelectionWithoutRoutes_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = new SystemStage(SystemStageKind.RouteSelection, null, null, Array.Empty<RouteID>()));
        }

        [Test]
        public void SystemStage_ProcessCarryingAShop_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SystemStage(
                SystemStageKind.Process,
                new ProcessID("WB-PROC-001"),
                new ShopID("WB-SHOP-001"),
                null
            ));
        }

        [Test]
        public void SystemDefinition_KeepsStageOrder()
        {
            SystemDefinition system = new(
                new SystemID("WB-SYS-001"),
                "System 1",
                new[]
                {
                    new SystemStage(SystemStageKind.Process, new ProcessID("WB-PROC-001"), null, null),
                    new SystemStage(SystemStageKind.Shop, null, new ShopID("WB-SHOP-001"), null)
                }
            );

            Assert.AreEqual(2, system.Stages.Count);
            Assert.AreEqual(SystemStageKind.Shop, system.Stages[1].Kind);
        }

        [Test]
        public void SystemDefinition_WithNoStages_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SystemDefinition(
                new SystemID("WB-SYS-001"),
                "System 1",
                Array.Empty<SystemStage>()
            ));
        }

        [Test]
        public void CatalogFileKind_CarriesSixteenMembers()
        {
            Assert.AreEqual(16, Enum.GetValues(typeof(CatalogFileKind)).Length);
        }

        [Test]
        public void CatalogFileKind_CarriesTheEightExtensionMembers()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.Core));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.ProcessConfiguration));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.Shop));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.Pool));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.RewardPackage));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.Route));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.StarterArchetype));
            Assert.IsTrue(Enum.IsDefined(typeof(CatalogFileKind), CatalogFileKind.System));
        }

        [Test]
        public void PoolSelectionMethods_HoldsExactlyTheTwoBalanceMethods()
        {
            Assert.AreEqual(2, CatalogVocabulary.PoolSelectionMethods.Count);
            Assert.IsTrue(CatalogVocabulary.PoolSelectionMethods.Contains("PLAYER_CHOICE"));
            Assert.IsTrue(CatalogVocabulary.PoolSelectionMethods.Contains("UNIFORM_WITHOUT_REPLACEMENT"));
        }

        /// <summary>
        /// Builds an assignment operation used as a fixed Core line's payload.
        /// </summary>
        /// <returns>An assignment of the constant one to Value.</returns>
        private static CoreLineOperation Assignment()
        {
            return new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));
        }

        /// <summary>
        /// Builds a fixed-instruction Core line at the given position.
        /// </summary>
        /// <param name="position">The one-based Core position; zero for a contained line.</param>
        /// <returns>The fixed-instruction line spec.</returns>
        private static CoreLineSpec FixedInstruction(int position)
        {
            return new CoreLineSpec(position, CoreLineKind.FixedInstruction, Assignment(), null, null);
        }

        /// <summary>
        /// Builds an open Core line at the given position.
        /// </summary>
        /// <param name="position">The one-based Core position; zero for a contained line.</param>
        /// <returns>The open line spec.</returns>
        private static CoreLineSpec Open(int position)
        {
            return new CoreLineSpec(position, CoreLineKind.Open, null, null, null);
        }

        /// <summary>
        /// Builds a fixed-Structure Core line containing a fixed instruction.
        /// </summary>
        /// <param name="position">The one-based Core position.</param>
        /// <returns>The fixed-Structure line spec.</returns>
        private static CoreLineSpec FixedStructure(int position)
        {
            return new CoreLineSpec(
                position,
                CoreLineKind.FixedStructure,
                null,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0),
                FixedInstruction(0)
            );
        }

        /// <summary>
        /// Builds the Tutorial Process 1 configuration shape used by the scripted-load tests.
        /// </summary>
        /// <returns>A scripted Process configuration with no Active Branch and no preceding shop.</returns>
        private static ProcessConfigurationDefinition TutorialOne()
        {
            BufferLoadSpec bufferLoad = new(
                BufferLoadPolicy.Scripted,
                Array.Empty<string>(),
                new[]
                {
                    new ArrivalLoadSpec(1, new[] { "WB-INS-005" }),
                    new ArrivalLoadSpec(2, new[] { "WB-INS-003" })
                },
                0,
                Array.Empty<int>()
            );

            return new ProcessConfigurationDefinition(
                new ProcessID("WB-PROC-001"),
                "Tutorial Process 1",
                ProcessRole.Tutorial1,
                new CoreID("WB-CORE-001"),
                new ProcessRuleID("WB-PRC-002"),
                new ProcessThresholdSpec(20, 30, 36),
                3,
                false,
                0,
                0,
                5,
                new[] { new InitialSourceSpec(1, "WB-INS-002") },
                bufferLoad,
                null,
                null,
                new RewardPackageID("WB-RWD-001"),
                null
            );
        }
    }
}
