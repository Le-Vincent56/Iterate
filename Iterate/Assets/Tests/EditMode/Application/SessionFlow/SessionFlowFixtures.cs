using System;
using System.Collections.Generic;
using Iterate.Application.Content;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// The fixture catalog the Session Flow suites play, built from the Domain's public API alone,
    /// because this test assembly cannot see the Domain suite's fixtures.
    ///
    /// Its main System has the authored shape of the vertical slice: a tutorial Process, a shop, a
    /// second tutorial Process, a choice between two routes (each a shop and then a Process), a shop,
    /// and the Critical Process. The tutorials declare no Branch and enter straight into Build; the
    /// later Processes declare one and enter through Branch configuration. Smaller Systems start on
    /// one particular kind of step, so a suite can reach that stage at begin without playing the
    /// stages before it.
    /// </summary>
    public static class SessionFlowFixtures
    {
        /// <summary>
        /// The revision every fixture catalog claims.
        /// </summary>
        public const string Revision = "fixture-0.0.1";

        /// <summary>
        /// The Instruction the starter Repository holds twice.
        /// </summary>
        public const string Instruction = "WB-INS-002";

        /// <summary>
        /// The Instruction the shops offer.
        /// </summary>
        public const string OfferedInstruction = "WB-INS-003";

        /// <summary>
        /// The Structure the starter Repository holds, which the route Processes require.
        /// </summary>
        public const string Structure = "WB-STR-001";

        /// <summary>
        /// The zero-RAM starter Dependency.
        /// </summary>
        public const string StarterDependency = "WB-DEP-001";

        /// <summary>
        /// The free-compilation Process rule the first route Process names.
        /// </summary>
        public const string ProcessRule = "WB-PRC-002";

        private static readonly string[] _tags = { "Value", "Add", "Fixed" };

        /// <summary>
        /// The vertical-slice-shaped System.
        /// </summary>
        public static SystemID MainSystem => new("WB-SYS-001");

        /// <summary>
        /// A System whose first step is a shop, followed by the second tutorial Process.
        /// </summary>
        public static SystemID ShopFirstSystem => new("WB-SYS-SHOP");

        /// <summary>
        /// A System whose first step is the route choice, followed by the Critical Process.
        /// </summary>
        public static SystemID RouteFirstSystem => new("WB-SYS-ROUTE");

        /// <summary>
        /// A System whose first step is the first route Process, which declares a Branch and names a
        /// Process rule, followed by the Critical Process.
        /// </summary>
        public static SystemID BranchFirstSystem => new("WB-SYS-BRANCH");

        /// <summary>
        /// A System whose only Process declares a Branch and names a Core the catalog lacks, so its
        /// creation is refused.
        /// </summary>
        public static SystemID MissingCoreSystem => new("WB-SYS-BADCORE");

        /// <summary>
        /// The first tutorial Process.
        /// </summary>
        public static ProcessID TutorialOne => new("WB-PROC-001");

        /// <summary>
        /// The second tutorial Process.
        /// </summary>
        public static ProcessID TutorialTwo => new("WB-PROC-002");

        /// <summary>
        /// The first route's Process, which names the Process rule.
        /// </summary>
        public static ProcessID RouteOneProcess => new("WB-PROC-003");

        /// <summary>
        /// The second route's Process.
        /// </summary>
        public static ProcessID RouteTwoProcess => new("WB-PROC-004");

        /// <summary>
        /// The Critical Process.
        /// </summary>
        public static ProcessID CriticalProcess => new("WB-PROC-005");

        /// <summary>
        /// The Process whose Core is missing.
        /// </summary>
        public static ProcessID MissingCoreProcess => new("WB-PROC-404");

        /// <summary>
        /// The shop after the first tutorial.
        /// </summary>
        public static ShopID FirstShop => new("WB-SHOP-001");

        /// <summary>
        /// The first route's shop.
        /// </summary>
        public static ShopID RouteOneShop => new("WB-SHOP-002");

        /// <summary>
        /// The second route's shop.
        /// </summary>
        public static ShopID RouteTwoShop => new("WB-SHOP-003");

        /// <summary>
        /// The shop before the Critical Process.
        /// </summary>
        public static ShopID LastShop => new("WB-SHOP-004");

        /// <summary>
        /// The first route.
        /// </summary>
        public static RouteID RouteOne => new("WB-ROUTE-001");

        /// <summary>
        /// The second route.
        /// </summary>
        public static RouteID RouteTwo => new("WB-ROUTE-002");

        /// <summary>
        /// Builds the fixture catalog.
        /// </summary>
        /// <returns>The catalog.</returns>
        public static ContentCatalog Catalog()
        {
            return new ContentCatalog(
                Revision,
                Parameters(),
                new[] { InstructionOf(Instruction), InstructionOf(OfferedInstruction), SessionFlowPlayContent.OverwriteDefinition() },
                new[] { StructureOf(Structure) },
                new[] { SessionFlowPlayContent.DirectiveDefinition() },
                new[] { DependencyOf(StarterDependency, 0), SessionFlowPlayContent.CostlyDependencyDefinition(), SessionFlowPlayContent.GarbageCollectorDefinition() },
                new[] { SessionFlowPlayContent.PatchDefinition() },
                new[] { SessionFlowPlayContent.UtilityDefinition() },
                new[] { FreeCompilationRule() },
                new[] { TutorialCore(), SessionFlowPlayContent.ScoringCore() },
                Join(Configurations(), SessionFlowPlayContent.Configurations()),
                Join(Shops(), new[] { SessionFlowPlayContent.Shop() }),
                SessionFlowPlayContent.Pools(),
                SessionFlowPlayContent.Packages(),
                new[]
                {
                    new RouteDefinition(RouteOne, "Route One", RouteOneProcess, RouteOneShop),
                    new RouteDefinition(RouteTwo, "Route Two", RouteTwoProcess, RouteTwoShop),
                },
                new[] { Archetype(), SessionFlowPlayContent.Archetype(), SessionFlowPlayContent.GarbageCollectorArchetype() },
                Join(Systems(), SessionFlowPlayContent.Systems())
            );
        }

        /// <summary>
        /// Joins two arrays in order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="first">The first array.</param>
        /// <param name="second">The second array.</param>
        /// <returns>A new array holding both.</returns>
        public static T[] Join<T>(T[] first, T[] second)
        {
            T[] joined = new T[first.Length + second.Length];
            Array.Copy(first, joined, first.Length);
            Array.Copy(second, 0, joined, first.Length, second.Length);
            return joined;
        }

        /// <summary>
        /// A catalog holder with the fixture catalog loaded.
        /// </summary>
        /// <returns>The loaded holder.</returns>
        public static CatalogHolder LoadedHolder()
        {
            CatalogHolder holder = new();
            holder.MarkLoaded(Catalog());
            return holder;
        }

        /// <summary>
        /// The Starter Archetype: two copies of one Instruction and the Structure, with the zero-RAM
        /// starter Dependency.
        /// </summary>
        /// <returns>The archetype.</returns>
        public static StarterArchetypeDefinition Archetype()
        {
            return new StarterArchetypeDefinition(
                new StarterArchetypeID("WB-ARCH-001"),
                "Direct Output",
                new[] { Instruction, Instruction, Structure },
                new DependencyID(StarterDependency)
            );
        }

        /// <summary>
        /// The Process configurations, one per fixture Process.
        /// </summary>
        /// <returns>The configurations.</returns>
        public static ProcessConfigurationDefinition[] Configurations()
        {
            return new[]
            {
                Configuration(TutorialOne, ProcessRole.Tutorial1, "WB-CORE-002", null, null),
                Configuration(TutorialTwo, ProcessRole.Tutorial2, "WB-CORE-002", null, null),
                Configuration(RouteOneProcess, ProcessRole.RouteProcess3, "WB-CORE-002", ProcessRule, Branch()),
                Configuration(RouteTwoProcess, ProcessRole.RouteProcess3, "WB-CORE-002", null, Branch()),
                Configuration(CriticalProcess, ProcessRole.CriticalProcess, "WB-CORE-002", null, Branch()),
                Configuration(MissingCoreProcess, ProcessRole.RouteProcess3, "WB-CORE-404", null, Branch()),
            };
        }

        /// <summary>
        /// The Branch every Branch-declaring Process uses: room for two items, the Structure
        /// required. The three-item starter Repository seeds an over-capacity draft.
        /// </summary>
        /// <returns>The Branch spec.</returns>
        public static ActiveBranchSpec Branch()
        {
            return new ActiveBranchSpec(
                2,
                new[] { Structure },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }

        /// <summary>
        /// Builds one Process configuration over a scripted, empty Buffer load.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <param name="role">Its role.</param>
        /// <param name="core">The Core it names.</param>
        /// <param name="rule">The Process rule it names, or null.</param>
        /// <param name="branch">Its Branch spec, or null for a tutorial.</param>
        /// <returns>The configuration.</returns>
        public static ProcessConfigurationDefinition Configuration(
            ProcessID process,
            ProcessRole role,
            string core,
            string rule,
            ActiveBranchSpec branch
        )
        {
            return new ProcessConfigurationDefinition(
                process,
                process.Value,
                role,
                new CoreID(core),
                rule == null ? null : new ProcessRuleID(rule),
                new ProcessThresholdSpec(18, 32, 80),
                4,
                false,
                6,
                8,
                5,
                Array.Empty<InitialSourceSpec>(),
                new BufferLoadSpec(
                    BufferLoadPolicy.Scripted,
                    Array.Empty<string>(),
                    Array.Empty<ArrivalLoadSpec>(),
                    0,
                    Array.Empty<int>()
                ),
                null,
                branch,
                new RewardPackageID("WB-RWD-001"),
                null
            );
        }

        /// <summary>
        /// The four shops, each offering the same Instruction in one fixed slot.
        /// </summary>
        /// <returns>The shops.</returns>
        public static ShopDefinition[] Shops()
        {
            return new[] { ShopOf(FirstShop), ShopOf(RouteOneShop), ShopOf(RouteTwoShop), ShopOf(LastShop) };
        }

        /// <summary>
        /// The fixture Systems.
        /// </summary>
        /// <returns>The Systems.</returns>
        public static SystemDefinition[] Systems()
        {
            return new[]
            {
                new SystemDefinition(
                    MainSystem,
                    "Main",
                    new[]
                    {
                        ProcessStage(TutorialOne),
                        ShopStage(FirstShop),
                        ProcessStage(TutorialTwo),
                        RouteStage(),
                        ShopStage(LastShop),
                        ProcessStage(CriticalProcess),
                    }
                ),
                new SystemDefinition(ShopFirstSystem, "Shop first", new[] { ShopStage(FirstShop), ProcessStage(TutorialTwo) }),
                new SystemDefinition(RouteFirstSystem, "Route first", new[] { RouteStage(), ProcessStage(CriticalProcess) }),
                new SystemDefinition(BranchFirstSystem, "Branch first", new[] { ProcessStage(RouteOneProcess), ProcessStage(CriticalProcess) }),
                new SystemDefinition(MissingCoreSystem, "Missing Core", new[] { ProcessStage(MissingCoreProcess) }),
            };
        }

        /// <summary>
        /// An authored Process stage.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <returns>The stage.</returns>
        public static SystemStage ProcessStage(ProcessID process)
        {
            return new SystemStage(SystemStageKind.Process, process, null, Array.Empty<RouteID>());
        }

        /// <summary>
        /// An authored shop stage.
        /// </summary>
        /// <param name="shop">The shop.</param>
        /// <returns>The stage.</returns>
        public static SystemStage ShopStage(ShopID shop)
        {
            return new SystemStage(SystemStageKind.Shop, null, shop, Array.Empty<RouteID>());
        }

        /// <summary>
        /// The authored choice between the two routes.
        /// </summary>
        /// <returns>The stage.</returns>
        public static SystemStage RouteStage()
        {
            return new SystemStage(SystemStageKind.RouteSelection, null, null, new[] { RouteOne, RouteTwo });
        }

        /// <summary>
        /// The eight-line tutorial Core: two fixed assignments, five open positions, and a fixed
        /// scoring output at position eight.
        /// </summary>
        /// <returns>The Core.</returns>
        public static CoreDefinition TutorialCore()
        {
            List<CoreLineSpec> lines = new()
            {
                FixedLine(1),
                FixedLine(2),
                OpenLine(3),
                OpenLine(4),
                OpenLine(5),
                OpenLine(6),
                OpenLine(7),
                FixedLine(8),
            };

            return new CoreDefinition(new CoreID("WB-CORE-002"), "Tutorial Core", lines, new SourcePosition(8));
        }

        /// <summary>
        /// The free-compilation Process rule: one COMPILATION-domain cost modification to zero Bytes.
        /// </summary>
        /// <returns>The Process rule.</returns>
        public static ProcessRuleDefinition FreeCompilationRule()
        {
            EffectDefinition effect = new(
                PhaseDomain.Compilation,
                new TriggerDescriptor(
                    EventFamily.Lifecycle,
                    "COMPILATION_COMMITTED",
                    new[] { new TriggerQualifier("OPERATION_CLASS", "EDITED_COMPILATION") },
                    null
                ),
                new CostModificationOperation("COMPILATION", true, 0, 0, true),
                new TargetingRule("NO_TARGET", string.Empty),
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "COMPILATION")
            );

            return new ProcessRuleDefinition(
                new ProcessRuleID(ProcessRule),
                "Every edited compilation this Process costs 0 Bytes.",
                "TUTORIAL FREE COMPILATION",
                ContentCategory.ProcessRule,
                Rarity.Starter,
                new[] { "ProcessRule", "Bytes" },
                new[] { effect }
            );
        }

        /// <summary>
        /// The complete locked parameter register the catalog requires.
        /// </summary>
        /// <returns>The parameter set.</returns>
        public static ParameterSet Parameters()
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
                { "WB-PAR-037", 3 }, { "WB-PAR-038", 7 }, { "WB-PAR-039", 3 },
            });
        }

        /// <summary>
        /// Builds a shop with one fixed slot offering the shops' Instruction.
        /// </summary>
        /// <param name="shop">The shop.</param>
        /// <returns>The shop definition.</returns>
        private static ShopDefinition ShopOf(ShopID shop)
        {
            return new ShopDefinition(
                shop,
                shop.Value,
                1,
                false,
                true,
                false,
                false,
                new[] { new ShopOffer($"OFF-{shop.Value}", OfferedInstruction, 1) },
                null
            );
        }

        /// <summary>
        /// Builds an Instruction definition.
        /// </summary>
        /// <param name="id">The Instruction's identity.</param>
        /// <returns>The definition.</returns>
        private static InstructionDefinition InstructionOf(string id)
        {
            return new InstructionDefinition(
                new InstructionID(id),
                id,
                id,
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
        /// Builds a Repeat Structure definition.
        /// </summary>
        /// <param name="id">The Structure's identity.</param>
        /// <returns>The definition.</returns>
        private static StructureDefinition StructureOf(string id)
        {
            return new StructureDefinition(
                new StructureID(id),
                id,
                id,
                ContentCategory.Structure,
                Rarity.Common,
                new[] { "Structure", "Repeat" },
                2,
                StructureKind.Repeat,
                2,
                null
            );
        }

        /// <summary>
        /// Builds a Dependency definition.
        /// </summary>
        /// <param name="id">The Dependency's identity.</param>
        /// <param name="ram">Its RAM cost.</param>
        /// <returns>The definition.</returns>
        private static DependencyDefinition DependencyOf(string id, int ram)
        {
            return new DependencyDefinition(
                new DependencyID(id),
                id,
                id,
                ContentCategory.Dependency,
                Rarity.Starter,
                new[] { "Dependency" },
                ram,
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// A fixed Core line assigning one to Value.
        /// </summary>
        /// <param name="position">The one-based position.</param>
        /// <returns>The line.</returns>
        private static CoreLineSpec FixedLine(int position)
        {
            return new CoreLineSpec(
                position,
                CoreLineKind.FixedInstruction,
                new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)),
                null,
                null
            );
        }

        /// <summary>
        /// An open Core line.
        /// </summary>
        /// <param name="position">The one-based position.</param>
        /// <returns>The line.</returns>
        private static CoreLineSpec OpenLine(int position)
        {
            return new CoreLineSpec(position, CoreLineKind.Open, null, null, null);
        }
    }
}
