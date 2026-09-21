using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Catalog and Session fixtures for the capture and restore suites, built from the Domain's public
    /// API alone.
    ///
    /// They duplicate part of the Domain suite's own fixtures on purpose. Each test assembly carries its
    /// own: <c>Iterate.Application.Tests</c> references <c>Iterate.Application</c> and
    /// <c>Iterate.Domain</c> but not <c>Iterate.Domain.Tests</c>, and neither does
    /// <c>Iterate.Infrastructure.Tests</c> — which is the same reason the shipped-catalog driver is a
    /// deliberate twin of the Domain one rather than a shared type. Much of what these suites need —
    /// routes, and Process configurations differing only in their role — has no Domain-side equivalent
    /// in any case.
    /// </summary>
    public static class PersistenceFixtures
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
        /// A second Instruction, for the cases needing two definitions.
        /// </summary>
        public const string SecondInstruction = "WB-INS-003";

        /// <summary>
        /// The Structure, which carries no Patch sockets.
        /// </summary>
        public const string Structure = "WB-STR-001";

        /// <summary>
        /// The Directive.
        /// </summary>
        public const string Directive = "WB-DIR-001";

        /// <summary>
        /// The zero-RAM starter Dependency.
        /// </summary>
        public const string StarterDependency = "WB-DEP-001";

        /// <summary>
        /// A one-RAM Dependency, which a starter may not be.
        /// </summary>
        public const string CostlyDependency = "WB-DEP-002";

        /// <summary>
        /// The Patch the attachment cases socket.
        /// </summary>
        public const string Patch = "WB-PAT-001";

        /// <summary>
        /// The Utility the commitment cases buy.
        /// </summary>
        public const string Utility = "WB-UTL-201";

        /// <summary>
        /// The Process a checkpoint is taken at.
        /// </summary>
        public const string Process = "WB-PROC-004";

        /// <summary>
        /// The route leading to that Process.
        /// </summary>
        public const string Route = "WB-ROUTE-002";

        private static readonly string[] _tags = { "Value", "Add", "Fixed" };

        /// <summary>
        /// Builds the fixture catalog, optionally carrying a Process configuration of the given role and
        /// the route that leads to it.
        /// </summary>
        /// <param name="role">The role the Process configuration declares.</param>
        /// <returns>The catalog.</returns>
        public static ContentCatalog Catalog(ProcessRole role = ProcessRole.RouteProcess3)
        {
            return new ContentCatalog(
                Revision,
                Parameters(),
                new[] { InstructionOf(Instruction), InstructionOf(SecondInstruction) },
                new[] { StructureOf(Structure) },
                new[] { DirectiveOf(Directive) },
                new[] { DependencyOf(StarterDependency, 0), DependencyOf(CostlyDependency, 1) },
                new[] { PatchOf(Patch) },
                new[] { UtilityOf(Utility) },
                Array.Empty<ProcessRuleDefinition>(),
                Array.Empty<CoreDefinition>(),
                new[] { Configuration(role) },
                Array.Empty<ShopDefinition>(),
                Array.Empty<PoolDefinition>(),
                Array.Empty<RewardPackageDefinition>(),
                new[] { new RouteDefinition(new RouteID(Route), "Route", new ProcessID(Process), new ShopID("WB-SHOP-003")) },
                Array.Empty<StarterArchetypeDefinition>(),
                Array.Empty<SystemDefinition>()
            );
        }

        /// <summary>
        /// Builds a Session over the fixture catalog, credited to the given Token balance.
        /// </summary>
        /// <param name="catalog">The catalog to seed from.</param>
        /// <param name="tokens">The Tokens to open with.</param>
        /// <returns>The Session.</returns>
        public static SessionState Session(ContentCatalog catalog, int tokens = 9)
        {
            SessionState session = SessionState.Create(
                catalog,
                Archetype(),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );

            if (tokens > 0)
                session.Economy.Tokens.Credit(new TokenAmount(tokens), TokenBasis.RewardTokens, "fixture");

            return session;
        }

        /// <summary>
        /// The Starter Archetype: two copies of one Instruction, one Structure, and the zero-RAM starter
        /// Dependency.
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
        /// Builds a Process configuration declaring the given role. Only the role varies: the
        /// destination cases turn on it and on nothing else.
        /// </summary>
        /// <param name="role">The role to declare.</param>
        /// <returns>The configuration.</returns>
        public static ProcessConfigurationDefinition Configuration(ProcessRole role)
        {
            return new ProcessConfigurationDefinition(
                new ProcessID(Process),
                "Process",
                role,
                new CoreID("WB-CORE-002"),
                null,
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
                null,
                new RewardPackageID("WB-RWD-003"),
                null
            );
        }

        /// <summary>
        /// Builds an Instruction definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Instruction's identity.</param>
        /// <returns>The definition.</returns>
        public static InstructionDefinition InstructionOf(string id)
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
        /// Builds a Repeat Structure definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Structure's identity.</param>
        /// <returns>The definition.</returns>
        public static StructureDefinition StructureOf(string id)
        {
            return new StructureDefinition(
                new StructureID(id),
                id,
                id,
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                1,
                StructureKind.Repeat,
                2,
                null
            );
        }

        /// <summary>
        /// Builds a Directive definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Directive's identity.</param>
        /// <returns>The definition.</returns>
        public static DirectiveDefinition DirectiveOf(string id)
        {
            return new DirectiveDefinition(
                new DirectiveID(id),
                id,
                id,
                ContentCategory.Directive,
                Rarity.Common,
                _tags,
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a Dependency definition carrying the given ID and RAM cost.
        /// </summary>
        /// <param name="id">The Dependency's identity.</param>
        /// <param name="ram">The RAM it consumes.</param>
        /// <returns>The definition.</returns>
        public static DependencyDefinition DependencyOf(string id, int ram)
        {
            return new DependencyDefinition(
                new DependencyID(id),
                id,
                id,
                ContentCategory.Dependency,
                Rarity.Common,
                _tags,
                ram,
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a Patch definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Patch's identity.</param>
        /// <returns>The definition.</returns>
        public static PatchDefinition PatchOf(string id)
        {
            return new PatchDefinition(
                new PatchID(id),
                id,
                id,
                ContentCategory.Patch,
                Rarity.Common,
                _tags,
                new PatchHostEligibility("FIXED_NUMBER_ADDITION_HOSTS"),
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a Utility definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Utility's identity.</param>
        /// <returns>The definition.</returns>
        public static UtilityDefinition UtilityOf(string id)
        {
            return new UtilityDefinition(
                new UtilityID(id),
                id,
                id,
                ContentCategory.Utility,
                Rarity.Common,
                _tags,
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// The parameter register the fixture catalog locks.
        /// </summary>
        /// <returns>The register.</returns>
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
    }
}
