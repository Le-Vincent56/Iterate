using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Hand-built content for the Progression Domain suites. Nothing here reads a catalog file: the
    /// Domain tests own their definitions outright, so a shipped-content edit can never silently change
    /// what a Domain test means. The shipped catalog is exercised by the Infrastructure suites instead.
    /// </summary>
    public static class ProgressionFixtures
    {
        /// <summary>
        /// The Instruction the duplicate-suffix cases use.
        /// </summary>
        public const string ValuePlusTwo = "WB-INS-002";

        /// <summary>
        /// A second Instruction, for the cases that need two distinct definitions.
        /// </summary>
        public const string ValuePlusThree = "WB-INS-003";

        /// <summary>
        /// The scoring Instruction.
        /// </summary>
        public const string ScorePlusValue = "WB-INS-012";

        /// <summary>
        /// The Repeat Structure.
        /// </summary>
        public const string RepeatTwo = "WB-STR-001";

        /// <summary>
        /// The Condition Structure the Branch fixtures mark Required.
        /// </summary>
        public const string ConditionValueEven = "WB-STR-002";

        /// <summary>
        /// The Directive.
        /// </summary>
        public const string Overclock = "WB-DIR-001";

        /// <summary>
        /// The zero-RAM starter Dependency.
        /// </summary>
        public const string StandardLibrary = "WB-DEP-001";

        /// <summary>
        /// A one-RAM Dependency, which is not a legal starter.
        /// </summary>
        public const string CleanBuild = "WB-DEP-002";

        private static readonly string[] _tags = { "Value", "Add", "Fixed" };

        /// <summary>
        /// Builds an Instruction definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Instruction's surrogate-key identity.</param>
        /// <returns>The Instruction definition.</returns>
        public static InstructionDefinition Instruction(string id)
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
        /// Builds a Patch definition carrying the given ID, eligible for ordinary Instruction hosts and
        /// declaring no effects. Economy fixtures socket it to test attachment, not interpretation.
        /// </summary>
        /// <param name="id">The Patch's surrogate-key identity.</param>
        /// <returns>The Patch definition.</returns>
        public static PatchDefinition Patch(string id)
        {
            return new PatchDefinition(
                new PatchID(id),
                id,
                id,
                ContentCategory.Patch,
                Rarity.Common,
                Array.Empty<string>(),
                new PatchHostEligibility("ORDINARY_INSTRUCTION_HOSTS"),
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a Repeat Structure definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Structure's surrogate-key identity.</param>
        /// <returns>The Structure definition.</returns>
        public static StructureDefinition Structure(string id)
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
        /// Builds a Condition Structure definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Structure's surrogate-key identity.</param>
        /// <returns>The Condition Structure definition.</returns>
        public static StructureDefinition Condition(string id)
        {
            return new StructureDefinition(
                new StructureID(id),
                id,
                id,
                ContentCategory.Structure,
                Rarity.Common,
                new[] { "Structure", "Condition" },
                2,
                StructureKind.Condition,
                0,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0)
            );
        }

        /// <summary>
        /// Builds a Directive definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Directive's surrogate-key identity.</param>
        /// <returns>The Directive definition.</returns>
        public static DirectiveDefinition Directive(string id)
        {
            return new DirectiveDefinition(
                new DirectiveID(id),
                id,
                id,
                ContentCategory.Directive,
                Rarity.Uncommon,
                new[] { "Directive" },
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a Dependency definition carrying the given ID and RAM cost.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="ram">The RAM the installed Dependency consumes.</param>
        /// <returns>The Dependency definition.</returns>
        public static DependencyDefinition Dependency(string id, int ram)
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
        /// Builds a Starter Archetype seeding the given content, with the zero-RAM starter Dependency.
        /// </summary>
        /// <param name="startingRepository">The starting content IDs in acquisition order.</param>
        /// <returns>The archetype definition.</returns>
        public static StarterArchetypeDefinition Archetype(params string[] startingRepository)
        {
            return new StarterArchetypeDefinition(
                new StarterArchetypeID("WB-ARCH-001"),
                "Direct Output",
                startingRepository,
                new DependencyID(StandardLibrary)
            );
        }

        /// <summary>
        /// Builds a Starter Archetype whose starter Dependency consumes RAM, which is a content error.
        /// </summary>
        /// <returns>The archetype definition naming a one-RAM starter Dependency.</returns>
        public static StarterArchetypeDefinition ArchetypeWithRAMConsumingStarter()
        {
            return new StarterArchetypeDefinition(
                new StarterArchetypeID("WB-ARCH-001"),
                "Direct Output",
                new[] { ValuePlusTwo },
                new DependencyID(CleanBuild)
            );
        }

        /// <summary>
        /// The archetype the Session fixtures seed from: the same Instruction twice, then the scoring
        /// Instruction, so suffix allocation and duplicate handling are both exercised.
        /// </summary>
        /// <returns>The standard three-item archetype.</returns>
        public static StarterArchetypeDefinition StandardArchetype()
        {
            return Archetype(ValuePlusTwo, ValuePlusTwo, ScorePlusValue);
        }

        /// <summary>
        /// Builds a catalog carrying every fixture definition, through the extension overload.
        /// </summary>
        /// <returns>The hand-built catalog.</returns>
        public static ContentCatalog Catalog()
        {
            return new ContentCatalog(
                "fixture-0.0.1",
                Parameters(),
                new[] { Instruction(ValuePlusTwo), Instruction(ValuePlusThree), Instruction(ScorePlusValue) },
                new[] { Structure(RepeatTwo), Condition(ConditionValueEven) },
                new[] { Directive(Overclock) },
                new[] { Dependency(StandardLibrary, 0), Dependency(CleanBuild, 1) },
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>(),
                Array.Empty<ProcessRuleDefinition>()
            );
        }

        /// <summary>
        /// Builds the complete locked parameter register the catalog requires.
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
                { "WB-PAR-037", 3 }, { "WB-PAR-038", 7 }, { "WB-PAR-039", 3 }
            });
        }

        /// <summary>
        /// Builds a Session over the standard archetype and the fixture catalog.
        /// </summary>
        /// <returns>The seeded Session state.</returns>
        public static SessionState Session()
        {
            return SessionState.Create(Catalog(), StandardArchetype(), "WB-SYS-001", "session-1", "seed-1");
        }

        /// <summary>
        /// Builds a catalog carrying the fixture content plus the Cores and Process rule the Process
        /// suites resolve: an eight-line Tutorial Process 2 Core shape, and optionally a Core whose
        /// second line is a fixed Structure, which this slice cannot materialise.
        /// </summary>
        /// <param name="structureCore">Whether the Cores include the fixed-Structure Core.</param>
        /// <returns>The hand-built catalog.</returns>
        public static ContentCatalog ProcessCatalog(bool structureCore = false)
        {
            List<CoreDefinition> cores = new() { TutorialTwoCore() };
            if (structureCore)
            {
                cores.Add(StructureCore());
            }

            return new ContentCatalog(
                "fixture-0.0.1",
                Parameters(),
                new[] { Instruction(ValuePlusTwo), Instruction(ValuePlusThree), Instruction(ScorePlusValue) },
                new[] { Structure(RepeatTwo), Condition(ConditionValueEven) },
                new[] { Directive(Overclock) },
                new[] { Dependency(StandardLibrary, 0), Dependency(CleanBuild, 1) },
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>(),
                new[] { FreeCompilationRule() },
                cores,
                Array.Empty<ProcessConfigurationDefinition>(),
                Array.Empty<ShopDefinition>(),
                Array.Empty<PoolDefinition>(),
                Array.Empty<RewardPackageDefinition>(),
                Array.Empty<RouteDefinition>(),
                Array.Empty<StarterArchetypeDefinition>(),
                Array.Empty<SystemDefinition>()
            );
        }

        /// <summary>
        /// The Tutorial Process 2 Core shape: two fixed assignments, five player positions, and a
        /// designated scoring output at position eight.
        /// </summary>
        /// <returns>The Core definition.</returns>
        public static CoreDefinition TutorialTwoCore()
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
                FixedLine(8)
            };

            return new CoreDefinition(new CoreID("WB-CORE-002"), "Tutorial Process 2 Core", lines, new SourcePosition(8));
        }

        /// <summary>
        /// A Core whose second line is a fixed Structure, which Progression refuses to materialise.
        /// </summary>
        /// <returns>The Core definition.</returns>
        public static CoreDefinition StructureCore()
        {
            CoreLineSpec contained = new(
                0,
                CoreLineKind.FixedInstruction,
                new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)),
                null,
                null
            );

            List<CoreLineSpec> lines = new()
            {
                OpenLine(1),
                new CoreLineSpec(
                    2,
                    CoreLineKind.FixedStructure,
                    null,
                    new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0),
                    contained
                ),
                FixedLine(3)
            };

            return new CoreDefinition(new CoreID("WB-CORE-003"), "PARITY CHECK Core", lines, new SourcePosition(3));
        }

        /// <summary>
        /// The free-compilation Process rule: one COMPILATION-domain cost modification to zero Bytes.
        /// </summary>
        /// <returns>The Process-rule definition.</returns>
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
                new ProcessRuleID("WB-PRC-002"),
                "Every edited compilation this Process costs 0 Bytes.",
                "TUTORIAL FREE COMPILATION",
                ContentCategory.ProcessRule,
                Rarity.Starter,
                new[] { "ProcessRule", "Bytes" },
                new[] { effect }
            );
        }

        /// <summary>
        /// Builds a fixed-instruction Core line at the given position.
        /// </summary>
        /// <param name="position">The one-based Core position.</param>
        /// <returns>The line spec.</returns>
        public static CoreLineSpec FixedLine(int position)
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
        /// Builds an open Core line at the given position.
        /// </summary>
        /// <param name="position">The one-based Core position.</param>
        /// <returns>The line spec.</returns>
        public static CoreLineSpec OpenLine(int position)
        {
            return new CoreLineSpec(position, CoreLineKind.Open, null, null, null);
        }

        /// <summary>
        /// Builds an empty Repository over a fresh identity source.
        /// </summary>
        /// <returns>The empty Repository.</returns>
        public static Repository EmptyRepository()
        {
            return new Repository(new InstanceIDSource());
        }
    }
}
