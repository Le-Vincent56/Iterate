using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests the catalog's extension surface: the constructor overload carrying the eight package and
    /// configuration kinds, the existing constructor still delegating with empty lists, and
    /// <see cref="ContentCatalog.TryGetItem"/> — the single seam every Progression site resolves a
    /// content ID string through, and the only place that knows the prefix-to-kind mapping.
    /// </summary>
    public sealed class ContentCatalogExtensionTests
    {
        private static readonly string[] _tags = { "Value", "Add" };

        [Test]
        public void ExistingConstructor_LeavesEveryExtensionListEmpty()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.AreEqual(0, catalog.Cores.Count);
            Assert.AreEqual(0, catalog.ProcessConfigurations.Count);
            Assert.AreEqual(0, catalog.Shops.Count);
            Assert.AreEqual(0, catalog.Pools.Count);
            Assert.AreEqual(0, catalog.RewardPackages.Count);
            Assert.AreEqual(0, catalog.Routes.Count);
            Assert.AreEqual(0, catalog.StarterArchetypes.Count);
            Assert.AreEqual(0, catalog.Systems.Count);
        }

        [Test]
        public void ExistingConstructor_CountsOnlyItsOwnDefinitions()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.AreEqual(3, catalog.DefinitionCount);
        }

        [Test]
        public void Overload_CountsTheExtensionDefinitions()
        {
            ContentCatalog catalog = BuildFullCatalog();

            Assert.AreEqual(5, catalog.DefinitionCount);
        }

        [Test]
        public void Overload_ResolvesEachExtensionKindByID()
        {
            ContentCatalog catalog = BuildFullCatalog();

            Assert.IsTrue(catalog.TryGetCore(new CoreID("WB-CORE-001"), out _));
            Assert.IsTrue(catalog.TryGetSystem(new SystemID("WB-SYS-001"), out _));
            Assert.IsFalse(catalog.TryGetCore(new CoreID("WB-CORE-002"), out _));
        }

        [Test]
        public void Overload_RejectsANullExtensionList()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ContentCatalog(
                "0.2.0",
                BuildParameters(),
                Array.Empty<InstructionDefinition>(),
                Array.Empty<StructureDefinition>(),
                Array.Empty<DirectiveDefinition>(),
                Array.Empty<DependencyDefinition>(),
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>(),
                Array.Empty<ProcessRuleDefinition>(),
                null,
                Array.Empty<ProcessConfigurationDefinition>(),
                Array.Empty<ShopDefinition>(),
                Array.Empty<PoolDefinition>(),
                Array.Empty<RewardPackageDefinition>(),
                Array.Empty<RouteDefinition>(),
                Array.Empty<StarterArchetypeDefinition>(),
                Array.Empty<SystemDefinition>()
            ));
        }

        [Test]
        public void TryGetItem_ResolvesAnInstruction()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.IsTrue(catalog.TryGetItem("WB-INS-002", out ContentDefinition definition));
            Assert.AreEqual(ContentCategory.Instruction, definition.Category);
            Assert.AreEqual("Value += 2", definition.DisplayName);
        }

        [Test]
        public void TryGetItem_ResolvesAStructure()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.IsTrue(catalog.TryGetItem("WB-STR-002", out ContentDefinition definition));
            Assert.AreEqual(ContentCategory.Structure, definition.Category);
        }

        [Test]
        public void TryGetItem_ResolvesADirective()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.IsTrue(catalog.TryGetItem("WB-DIR-001", out ContentDefinition definition));
            Assert.AreEqual(ContentCategory.Directive, definition.Category);
        }

        [Test]
        public void TryGetItem_ReturnsFalseForAnUndefinedItemID()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.IsFalse(catalog.TryGetItem("WB-INS-404", out ContentDefinition definition));
            Assert.IsNull(definition);
        }

        [Test]
        public void TryGetItem_ReturnsFalseForANonItemPrefix()
        {
            ContentCatalog catalog = BuildFullCatalog();

            Assert.IsFalse(catalog.TryGetItem("WB-DEP-001", out _));
            Assert.IsFalse(catalog.TryGetItem("WB-PAT-001", out _));
            Assert.IsFalse(catalog.TryGetItem("WB-UTL-001", out _));
            Assert.IsFalse(catalog.TryGetItem("WB-CORE-001", out _));
            Assert.IsFalse(catalog.TryGetItem("WB-SYS-001", out _));
            Assert.IsFalse(catalog.TryGetItem("WB-PAR-001", out _));
        }

        [Test]
        public void TryGetItem_ReturnsFalseForAnUnrecognisedString()
        {
            ContentCatalog catalog = BuildItemOnlyCatalog();

            Assert.IsFalse(catalog.TryGetItem("Value += 2", out _));
            Assert.IsFalse(catalog.TryGetItem(string.Empty, out _));
        }

        /// <summary>
        /// Builds the locked parameter register the catalog requires.
        /// </summary>
        /// <returns>A parameter set carrying the slice's required rows.</returns>
        private static ParameterSet BuildParameters()
        {
            return new ParameterSet(new Dictionary<string, double>
            {
                { "WB-PAR-001", 3 }, { "WB-PAR-002", 12 }, { "WB-PAR-003", 20 }, { "WB-PAR-004", 30 },
                { "WB-PAR-005", 4 }, { "WB-PAR-006", 5 }, { "WB-PAR-007", 6 }, { "WB-PAR-008", 6 },
                { "WB-PAR-009", 6 }, { "WB-PAR-010", 5 }, { "WB-PAR-011", 4 }, { "WB-PAR-012", 3 },
                { "WB-PAR-013", 3 }, { "WB-PAR-014", 3 }, { "WB-PAR-015", 9 }, { "WB-PAR-016", 6 },
                { "WB-PAR-017", 0 }, { "WB-PAR-018", 0 }, { "WB-PAR-019", 1 }, { "WB-PAR-020", 2 },
                { "WB-PAR-021", 3 }, { "WB-PAR-022", 1.0 }, { "WB-PAR-023", 1.75 }, { "WB-PAR-024", 3.0 },
                { "WB-PAR-026", 2 }, { "WB-PAR-028", 1 }, { "WB-PAR-029", 2 }, { "WB-PAR-030", 3 },
                { "WB-PAR-035", 0.5 }, { "WB-PAR-036", 2 },
                { "WB-PAR-037", 3 }, { "WB-PAR-038", 7 }, { "WB-PAR-039", 3 }
            });
        }

        /// <summary>
        /// Builds a catalog through the existing nine-argument constructor, carrying one Instruction,
        /// one Structure and one Directive.
        /// </summary>
        /// <returns>The item-only catalog.</returns>
        private static ContentCatalog BuildItemOnlyCatalog()
        {
            return new ContentCatalog(
                "0.2.0",
                BuildParameters(),
                new[] { BuildInstruction() },
                new[] { BuildStructure() },
                new[] { BuildDirective() },
                Array.Empty<DependencyDefinition>(),
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>(),
                Array.Empty<ProcessRuleDefinition>()
            );
        }

        /// <summary>
        /// Builds a catalog through the overload, carrying one Instruction, one Structure, one
        /// Directive, one Core and one System.
        /// </summary>
        /// <returns>The extended catalog.</returns>
        private static ContentCatalog BuildFullCatalog()
        {
            return new ContentCatalog(
                "0.2.0",
                BuildParameters(),
                new[] { BuildInstruction() },
                new[] { BuildStructure() },
                new[] { BuildDirective() },
                Array.Empty<DependencyDefinition>(),
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>(),
                Array.Empty<ProcessRuleDefinition>(),
                new[] { BuildCore() },
                Array.Empty<ProcessConfigurationDefinition>(),
                Array.Empty<ShopDefinition>(),
                Array.Empty<PoolDefinition>(),
                Array.Empty<RewardPackageDefinition>(),
                Array.Empty<RouteDefinition>(),
                Array.Empty<StarterArchetypeDefinition>(),
                new[] { BuildSystem() }
            );
        }

        /// <summary>
        /// Builds the Instruction definition the item lookups resolve.
        /// </summary>
        /// <returns>The Instruction definition.</returns>
        private static InstructionDefinition BuildInstruction()
        {
            return new InstructionDefinition(
                new InstructionID("WB-INS-002"),
                "Value += 2",
                "Value += 2",
                ContentCategory.Instruction,
                Rarity.Common,
                _tags,
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2)),
                null,
                Array.Empty<string>()
            );
        }

        /// <summary>
        /// Builds the Structure definition the item lookups resolve.
        /// </summary>
        /// <returns>The Structure definition.</returns>
        private static StructureDefinition BuildStructure()
        {
            return new StructureDefinition(
                new StructureID("WB-STR-002"),
                "If Value is even: [1 instruction]",
                "If Value is even: [1 instruction]",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Condition,
                0,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0)
            );
        }

        /// <summary>
        /// Builds the Directive definition the item lookups resolve.
        /// </summary>
        /// <returns>The Directive definition.</returns>
        private static DirectiveDefinition BuildDirective()
        {
            return new DirectiveDefinition(
                new DirectiveID("WB-DIR-001"),
                "OVERCLOCK",
                "OVERCLOCK",
                ContentCategory.Directive,
                Rarity.Uncommon,
                _tags,
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds the Core definition the extension lookups resolve.
        /// </summary>
        /// <returns>The Core definition.</returns>
        private static CoreDefinition BuildCore()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));
            CoreLineSpec[] lines =
            {
                new CoreLineSpec(1, CoreLineKind.FixedInstruction, operation, null, null),
                new CoreLineSpec(2, CoreLineKind.Open, null, null, null)
            };

            return new CoreDefinition(new CoreID("WB-CORE-001"), "Tutorial Process 1 Core", lines, new SourcePosition(1));
        }

        /// <summary>
        /// Builds the System definition the extension lookups resolve.
        /// </summary>
        /// <returns>The System definition.</returns>
        private static SystemDefinition BuildSystem()
        {
            SystemStage[] stages =
            {
                new SystemStage(SystemStageKind.Process, new ProcessID("WB-PROC-001"), null, null)
            };

            return new SystemDefinition(new SystemID("WB-SYS-001"), "System 1", stages);
        }
    }
}
