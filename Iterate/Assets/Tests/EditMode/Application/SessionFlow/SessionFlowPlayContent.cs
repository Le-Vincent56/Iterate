using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// The fixture content a Process is played with: a scoring Core, an archetype whose whole starter
    /// Repository is loaded into the Buffer, a full shop, and three reward packages.
    ///
    /// The scoring Core sets Value to one and adds Value to Score on its last line. An empty source
    /// therefore outputs 1 against thresholds of 2, 3 and 5: below Pass. One installed Instruction
    /// (Value + 2) outputs 3, which is Optimize. Two output 5, which is Benchmark. That is what lets a
    /// suite pick the tier a result reaches.
    /// </summary>
    public static class SessionFlowPlayContent
    {
        /// <summary>
        /// An Instruction tagged Overwrite, which may replace an installed line.
        /// </summary>
        public const string OverwriteInstruction = "WB-INS-020";

        /// <summary>
        /// The Directive the play archetype holds.
        /// </summary>
        public const string Directive = "WB-DIR-001";

        /// <summary>
        /// The Patch the play shop offers and the Patch reward grants.
        /// </summary>
        public const string Patch = "WB-PAT-001";

        /// <summary>
        /// A one-RAM Dependency the play shop offers.
        /// </summary>
        public const string CostlyDependency = "WB-DEP-002";

        /// <summary>
        /// A Utility with no effects, which the play shop offers.
        /// </summary>
        public const string Utility = "WB-UTL-201";

        /// <summary>
        /// The GARBAGE COLLECTOR shape.
        /// </summary>
        public const string GarbageCollector = "WB-DEP-008";

        /// <summary>
        /// The pool the choice reward draws from.
        /// </summary>
        public const string ChoicePool = "WB-POOL-001";

        /// <summary>
        /// The pool the play shop rerolls from.
        /// </summary>
        public const string RerollPool = "WB-POOL-002";

        /// <summary>
        /// The package granting Tokens at Pass.
        /// </summary>
        public const string TokenPackage = "WB-RWD-001";

        /// <summary>
        /// The package offering a choice from a pool at Pass.
        /// </summary>
        public const string ChoicePackage = "WB-RWD-010";

        /// <summary>
        /// The package granting Tokens at Pass and a Patch at Optimize.
        /// </summary>
        public const string PatchPackage = "WB-RWD-011";

        /// <summary>
        /// The play System: a Process, the full shop, and a last Process.
        /// </summary>
        public static SystemID PlaySystem => new("WB-SYS-PLAY");

        /// <summary>
        /// A System of one Process whose executions are mandatory.
        /// </summary>
        public static SystemID MandatorySystem => new("WB-SYS-MANDATORY");

        /// <summary>
        /// A System of one Process rewarding a choice from a pool.
        /// </summary>
        public static SystemID ChoiceSystem => new("WB-SYS-CHOICE");

        /// <summary>
        /// A System of one Process rewarding a Patch at Optimize.
        /// </summary>
        public static SystemID PatchSystem => new("WB-SYS-PATCH");

        /// <summary>
        /// A System of one Process that starts with no Bytes.
        /// </summary>
        public static SystemID BrokeSystem => new("WB-SYS-BROKE");

        /// <summary>
        /// The play System's first Process, rewarding Tokens.
        /// </summary>
        public static ProcessID PlayFirst => new("WB-PROC-101");

        /// <summary>
        /// The mandatory-executions Process.
        /// </summary>
        public static ProcessID Mandatory => new("WB-PROC-102");

        /// <summary>
        /// The play System's last Process, rewarding a Patch at Optimize.
        /// </summary>
        public static ProcessID PlayLast => new("WB-PROC-103");

        /// <summary>
        /// The Process rewarding a choice.
        /// </summary>
        public static ProcessID Choice => new("WB-PROC-104");

        /// <summary>
        /// The Process with no Bytes.
        /// </summary>
        public static ProcessID Broke => new("WB-PROC-105");

        /// <summary>
        /// The Process whose second Instruction arrives after the first execution.
        /// </summary>
        public static ProcessID Arrival => new("WB-PROC-106");

        /// <summary>
        /// A System of one Process with a scripted arrival.
        /// </summary>
        public static SystemID ArrivalSystem => new("WB-SYS-ARRIVAL");

        /// <summary>
        /// The full shop.
        /// </summary>
        public static ShopID PlayShop => new("WB-SHOP-101");

        /// <summary>
        /// The play archetype: two scoring Instructions, the Structure, the Directive and the Overwrite
        /// Instruction.
        /// </summary>
        /// <returns>The archetype.</returns>
        public static StarterArchetypeDefinition Archetype()
        {
            return new StarterArchetypeDefinition(
                new StarterArchetypeID("WB-ARCH-002"),
                "Play",
                StarterItems(),
                new DependencyID(SessionFlowFixtures.StarterDependency)
            );
        }

        /// <summary>
        /// The play archetype's items, which every play Process loads into its Buffer.
        /// </summary>
        /// <returns>The content IDs in acquisition order.</returns>
        public static string[] StarterItems()
        {
            return new[]
            {
                SessionFlowFixtures.Instruction,
                SessionFlowFixtures.Instruction,
                SessionFlowFixtures.Structure,
                Directive,
                OverwriteInstruction,
            };
        }

        /// <summary>
        /// The play archetype with the GARBAGE COLLECTOR shape as its starter Dependency, so every play
        /// Process it enters carries an archive observer gaining a Byte on its first archive. The
        /// fixture gives the shape zero RAM, which a starter requires.
        /// </summary>
        /// <returns>The archetype.</returns>
        public static StarterArchetypeDefinition GarbageCollectorArchetype()
        {
            return new StarterArchetypeDefinition(
                new StarterArchetypeID("WB-ARCH-003"),
                "Play with GARBAGE COLLECTOR",
                StarterItems(),
                new DependencyID(GarbageCollector)
            );
        }

        /// <summary>
        /// The GARBAGE COLLECTOR shape: one build-interaction effect gaining a Byte on the first archive of
        /// each Process.
        /// </summary>
        /// <returns>The definition.</returns>
        public static DependencyDefinition GarbageCollectorDefinition()
        {
            EffectDefinition gain = new(
                PhaseDomain.BuildInteraction,
                new TriggerDescriptor(EventFamily.ContentLifecycle, "OBJECT_ARCHIVED", Array.Empty<TriggerQualifier>(), null),
                new ResourceGainOperation("BYTES", 1),
                null,
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS")
            );

            return new DependencyDefinition(
                new DependencyID(GarbageCollector),
                "GARBAGE COLLECTOR",
                "GARBAGE COLLECTOR",
                ContentCategory.Dependency,
                Rarity.Starter,
                new[] { "Buffer", "Archive", "Bytes" },
                0,
                new[] { gain }
            );
        }

        /// <summary>
        /// The Overwrite Instruction's definition.
        /// </summary>
        /// <returns>The definition.</returns>
        public static InstructionDefinition OverwriteDefinition()
        {
            return new InstructionDefinition(
                new InstructionID(OverwriteInstruction),
                OverwriteInstruction,
                OverwriteInstruction,
                ContentCategory.Instruction,
                Rarity.Common,
                new[] { "Value", "Add", "Overwrite" },
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                Array.Empty<string>()
            );
        }

        /// <summary>
        /// The Directive's definition.
        /// </summary>
        /// <returns>The definition.</returns>
        public static DirectiveDefinition DirectiveDefinition()
        {
            return new DirectiveDefinition(
                new DirectiveID(Directive),
                Directive,
                Directive,
                ContentCategory.Directive,
                Rarity.Uncommon,
                new[] { "Directive" },
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// The Patch's definition: eligible for ordinary Instruction hosts, with no effects.
        /// </summary>
        /// <returns>The definition.</returns>
        public static PatchDefinition PatchDefinition()
        {
            return new PatchDefinition(
                new PatchID(Patch),
                Patch,
                Patch,
                ContentCategory.Patch,
                Rarity.Common,
                Array.Empty<string>(),
                new PatchHostEligibility("ORDINARY_INSTRUCTION_HOSTS"),
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// The one-RAM Dependency's definition.
        /// </summary>
        /// <returns>The definition.</returns>
        public static DependencyDefinition CostlyDependencyDefinition()
        {
            return new DependencyDefinition(
                new DependencyID(CostlyDependency),
                CostlyDependency,
                CostlyDependency,
                ContentCategory.Dependency,
                Rarity.Common,
                new[] { "Dependency" },
                1,
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// The Utility's definition.
        /// </summary>
        /// <returns>The definition.</returns>
        public static UtilityDefinition UtilityDefinition()
        {
            return new UtilityDefinition(
                new UtilityID(Utility),
                Utility,
                Utility,
                ContentCategory.Utility,
                Rarity.Common,
                new[] { "Utility" },
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// The scoring Core: Value set to one on line 1, five open lines, and Score plus Value as the
        /// final output on line 7.
        /// </summary>
        /// <returns>The Core.</returns>
        public static CoreDefinition ScoringCore()
        {
            List<CoreLineSpec> lines = new()
            {
                new CoreLineSpec(
                    1,
                    CoreLineKind.FixedInstruction,
                    new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)),
                    null,
                    null
                ),
                new CoreLineSpec(2, CoreLineKind.Open, null, null, null),
                new CoreLineSpec(3, CoreLineKind.Open, null, null, null),
                new CoreLineSpec(4, CoreLineKind.Open, null, null, null),
                new CoreLineSpec(5, CoreLineKind.Open, null, null, null),
                new CoreLineSpec(6, CoreLineKind.Open, null, null, null),
                new CoreLineSpec(
                    7,
                    CoreLineKind.FixedInstruction,
                    new CoreLineOperation(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value)),
                    null,
                    null
                ),
            };

            return new CoreDefinition(new CoreID("WB-CORE-003"), "Scoring Core", lines, new SourcePosition(7));
        }

        /// <summary>
        /// The play Process configurations.
        /// </summary>
        /// <returns>The configurations.</returns>
        public static ProcessConfigurationDefinition[] Configurations()
        {
            return new[]
            {
                Configuration(PlayFirst, ProcessRole.Tutorial1, false, 30, TokenPackage),
                Configuration(Mandatory, ProcessRole.Tutorial2, true, 30, TokenPackage),
                Configuration(PlayLast, ProcessRole.RouteProcess3, false, 30, PatchPackage),
                Configuration(Choice, ProcessRole.RouteProcess3, false, 30, ChoicePackage),
                Configuration(Broke, ProcessRole.Tutorial1, false, 0, TokenPackage),
                Configuration(Arrival, ProcessRole.Tutorial1, false, 30, TokenPackage) with
                {
                    BufferLoad = new BufferLoadSpec(
                        BufferLoadPolicy.Scripted,
                        new[] { SessionFlowFixtures.Instruction, SessionFlowFixtures.Structure },
                        new[] { new ArrivalLoadSpec(1, new[] { SessionFlowFixtures.Instruction }) },
                        0,
                        Array.Empty<int>()
                    ),
                },
            };
        }

        /// <summary>
        /// Builds a play configuration: the scoring Core, thresholds of 2, 3 and 5, two executions, and
        /// the whole starter Repository loaded into the Buffer.
        /// </summary>
        /// <param name="process">The Process.</param>
        /// <param name="role">Its role.</param>
        /// <param name="mandatory">Whether its executions are mandatory.</param>
        /// <param name="bytes">Its starting Bytes.</param>
        /// <param name="package">Its reward package.</param>
        /// <returns>The configuration.</returns>
        public static ProcessConfigurationDefinition Configuration(
            ProcessID process,
            ProcessRole role,
            bool mandatory,
            int bytes,
            string package
        )
        {
            return new ProcessConfigurationDefinition(
                process,
                process.Value,
                role,
                new CoreID("WB-CORE-003"),
                null,
                new ProcessThresholdSpec(2, 3, 5),
                2,
                mandatory,
                bytes,
                8,
                5,
                Array.Empty<InitialSourceSpec>(),
                new BufferLoadSpec(
                    BufferLoadPolicy.Scripted,
                    StarterItems(),
                    Array.Empty<ArrivalLoadSpec>(),
                    0,
                    Array.Empty<int>()
                ),
                null,
                null,
                new RewardPackageID(package),
                null
            );
        }

        /// <summary>
        /// The full shop: an Instruction, a Utility, a Patch and a Dependency, with rerolls, pinning,
        /// Dependencies and Services all enabled.
        /// </summary>
        /// <returns>The shop.</returns>
        public static ShopDefinition Shop()
        {
            return new ShopDefinition(
                PlayShop,
                "Play shop",
                4,
                true,
                true,
                true,
                true,
                new[]
                {
                    new ShopOffer("OFF-PLAY-INS", SessionFlowFixtures.OfferedInstruction, 1),
                    new ShopOffer("OFF-PLAY-UTL", Utility, 1),
                    new ShopOffer("OFF-PLAY-PAT", Patch, 1),
                    new ShopOffer("OFF-PLAY-DEP", CostlyDependency, 1),
                },
                new PoolID(RerollPool)
            );
        }

        /// <summary>
        /// The two pools.
        /// </summary>
        /// <returns>The pools.</returns>
        public static PoolDefinition[] Pools()
        {
            return new[]
            {
                new PoolDefinition(
                    new PoolID(ChoicePool),
                    ChoicePool,
                    "UNIFORM_WITHOUT_REPLACEMENT",
                    null,
                    new[] { new PoolMember(SessionFlowFixtures.OfferedInstruction, null), new PoolMember(SessionFlowFixtures.Structure, null) }
                ),
                new PoolDefinition(
                    new PoolID(RerollPool),
                    RerollPool,
                    "UNIFORM_WITHOUT_REPLACEMENT",
                    null,
                    new[]
                    {
                        new PoolMember(SessionFlowFixtures.Instruction, 1),
                        new PoolMember(SessionFlowFixtures.Structure, 1),
                        new PoolMember(OverwriteInstruction, 1),
                        new PoolMember(Directive, 1),
                    }
                ),
            };
        }

        /// <summary>
        /// The three reward packages.
        /// </summary>
        /// <returns>The packages.</returns>
        public static RewardPackageDefinition[] Packages()
        {
            return new[]
            {
                new RewardPackageDefinition(
                    new RewardPackageID(TokenPackage),
                    "Tokens",
                    new[] { new RewardComponent(RewardTier.Pass, RewardComponentKind.Tokens, 10, null) }
                ),
                new RewardPackageDefinition(
                    new RewardPackageID(ChoicePackage),
                    "Choice",
                    new[] { new RewardComponent(RewardTier.Pass, RewardComponentKind.PoolChoice, null, ChoicePool) }
                ),
                new RewardPackageDefinition(
                    new RewardPackageID(PatchPackage),
                    "Patch",
                    new[]
                    {
                        new RewardComponent(RewardTier.Pass, RewardComponentKind.Tokens, 2, null),
                        new RewardComponent(RewardTier.Optimize, RewardComponentKind.Patch, null, Patch),
                    }
                ),
            };
        }

        /// <summary>
        /// The play Systems.
        /// </summary>
        /// <returns>The Systems.</returns>
        public static SystemDefinition[] Systems()
        {
            return new[]
            {
                new SystemDefinition(
                    PlaySystem,
                    "Play",
                    new[] { SessionFlowFixtures.ProcessStage(PlayFirst), SessionFlowFixtures.ShopStage(PlayShop), SessionFlowFixtures.ProcessStage(PlayLast) }
                ),
                new SystemDefinition(MandatorySystem, "Mandatory", new[] { SessionFlowFixtures.ProcessStage(Mandatory) }),
                new SystemDefinition(ChoiceSystem, "Choice", new[] { SessionFlowFixtures.ProcessStage(Choice) }),
                new SystemDefinition(PatchSystem, "Patch", new[] { SessionFlowFixtures.ProcessStage(PlayLast) }),
                new SystemDefinition(BrokeSystem, "Broke", new[] { SessionFlowFixtures.ProcessStage(Broke) }),
                new SystemDefinition(ArrivalSystem, "Arrival", new[] { SessionFlowFixtures.ProcessStage(Arrival) }),
            };
        }
    }
}
