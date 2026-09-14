using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Hand-built fixtures for the economy suites: Utility definitions carrying process-setup effects,
    /// Dependency definitions at chosen RAM costs, and Sessions opened at a chosen Token balance.
    /// Mirrors <see cref="ProgressionFixtures"/>, which owns the content the Session itself seeds from;
    /// this one owns what the economy transacts over.
    /// </summary>
    public static class EconomyFixtures
    {
        /// <summary>
        /// The one-RAM Dependency ID the rack suites install.
        /// </summary>
        public const string OneRAMDependency = "WB-DEP-201";

        /// <summary>
        /// The two-RAM Dependency ID the rack suites install.
        /// </summary>
        public const string TwoRAMDependency = "WB-DEP-202";

        /// <summary>
        /// The Utility ID the commitment suites buy.
        /// </summary>
        public const string StartingBytesUtility = "WB-UTL-201";

        /// <summary>
        /// Builds a Session over the fixture catalog with its Token ledger credited to the given
        /// balance. A Session always starts at zero Tokens, so the balance is credited rather than
        /// constructed, and the credit is visible in the ledger like any other.
        /// </summary>
        /// <param name="tokens">The Tokens to open with.</param>
        /// <returns>The seeded Session state.</returns>
        public static SessionState SessionWithTokens(int tokens)
        {
            SessionState session = ProgressionFixtures.Session();
            if (tokens > 0)
            {
                session.Economy.Tokens.Credit(new TokenAmount(tokens), TokenBasis.RewardTokens, "fixture");
            }

            return session;
        }

        /// <summary>
        /// Builds an Instruction definition whose primary operation adds Value to Score. Needed because
        /// <see cref="ProgressionFixtures.Instruction"/> gives every identity the same Value += 2
        /// operation, so its "Score" identity is a name rather than a register.
        /// </summary>
        /// <param name="id">The Instruction's surrogate-key identity.</param>
        /// <returns>The Instruction definition.</returns>
        public static InstructionDefinition ScoreInstruction(string id)
        {
            return new InstructionDefinition(
                new InstructionID(id),
                id,
                id,
                ContentCategory.Instruction,
                Rarity.Common,
                new[] { "Score", "Add" },
                1,
                new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value)),
                null,
                Array.Empty<string>()
            );
        }

        /// <summary>
        /// Builds a Patch definition declaring a chosen host-eligibility rule. Needed because
        /// <see cref="ProgressionFixtures.Patch"/> hardcodes ORDINARY_INSTRUCTION_HOSTS, under which
        /// every Instruction is eligible — so a suite testing eligibility must state the rule it means
        /// rather than infer it from the Patch identity.
        /// </summary>
        /// <param name="id">The Patch's surrogate-key identity.</param>
        /// <param name="rule">The host-eligibility rule token.</param>
        /// <returns>The Patch definition.</returns>
        public static PatchDefinition PatchWithRule(string id, string rule)
        {
            return new PatchDefinition(
                new PatchID(id),
                id,
                id,
                ContentCategory.Patch,
                Rarity.Common,
                Array.Empty<string>(),
                new PatchHostEligibility(rule),
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a Dependency definition at the given RAM cost.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="ram">The RAM the installed Dependency consumes.</param>
        /// <returns>The Dependency definition.</returns>
        public static DependencyDefinition Dependency(string id, int ram)
        {
            return ProgressionFixtures.Dependency(id, ram);
        }

        /// <summary>
        /// Builds a Utility definition declaring one process-setup effect.
        /// </summary>
        /// <param name="id">The Utility's surrogate-key identity.</param>
        /// <param name="setting">The configuration setting the effect modifies.</param>
        /// <param name="amount">The signed adjustment.</param>
        /// <returns>The Utility definition.</returns>
        public static UtilityDefinition Utility(string id, string setting, int amount)
        {
            return new UtilityDefinition(
                new UtilityID(id),
                id,
                id,
                ContentCategory.Utility,
                Rarity.Common,
                new[] { "Utility" },
                new[] { SetupEffect(setting, amount) }
            );
        }

        /// <summary>
        /// Builds a Utility definition declaring no effects, for the commitments that only need an
        /// identity.
        /// </summary>
        /// <param name="id">The Utility's surrogate-key identity.</param>
        /// <returns>The Utility definition.</returns>
        public static UtilityDefinition UtilityWithoutEffects(string id)
        {
            return new UtilityDefinition(
                new UtilityID(id),
                id,
                id,
                ContentCategory.Utility,
                Rarity.Common,
                new[] { "Utility" },
                Array.Empty<EffectDefinition>()
            );
        }

        /// <summary>
        /// Builds a process-setup configuration modification.
        /// </summary>
        /// <param name="setting">The configuration setting the effect modifies.</param>
        /// <param name="amount">The signed adjustment.</param>
        /// <returns>The effect definition.</returns>
        public static EffectDefinition SetupEffect(string setting, int amount)
        {
            return new EffectDefinition(
                PhaseDomain.ProcessSetup,
                null,
                new ConfigurationModificationOperation(setting, amount, false),
                null,
                null,
                StackingMode.AdditiveParameter,
                null
            );
        }

        /// <summary>
        /// Builds a shop definition. Every flag is explicit because a shop's legality gates — rerolls,
        /// pinning, Dependencies, Services — are exactly what the shop suites vary.
        /// </summary>
        /// <param name="id">The shop's surrogate-key identity.</param>
        /// <param name="slots">How many numbered slots it opens with.</param>
        /// <param name="rerolls">Whether rerolls are offered.</param>
        /// <param name="pinning">Whether pinning is offered.</param>
        /// <param name="dependencies">Whether Dependency offers and destruction are offered.</param>
        /// <param name="services">Whether Repository Services are offered.</param>
        /// <param name="offers">The fixed offers, in slot order.</param>
        /// <param name="rerollPool">The pool rerolls draw from, or null.</param>
        /// <returns>The shop definition.</returns>
        public static ShopDefinition Shop(
            string id,
            int slots,
            bool rerolls,
            bool pinning,
            bool dependencies,
            bool services,
            IReadOnlyList<ShopOffer> offers,
            PoolID? rerollPool
        )
        {
            return new ShopDefinition(
                new ShopID(id),
                id,
                slots,
                rerolls,
                pinning,
                dependencies,
                services,
                offers,
                rerollPool
            );
        }

        /// <summary>
        /// Builds one fixed shop offer.
        /// </summary>
        /// <param name="offerID">The offer's identity.</param>
        /// <param name="content">The content ID it sells.</param>
        /// <param name="price">The Token price.</param>
        /// <returns>The offer.</returns>
        public static ShopOffer Offer(string offerID, string content, int price)
        {
            return new ShopOffer(offerID, content, price);
        }

        /// <summary>
        /// Builds a reroll pool over the given content IDs, each at the given price.
        /// </summary>
        /// <param name="id">The pool's surrogate-key identity.</param>
        /// <param name="price">The price every member carries.</param>
        /// <param name="contentIDs">The member content IDs.</param>
        /// <returns>The pool definition.</returns>
        public static PoolDefinition Pool(string id, int price, params string[] contentIDs)
        {
            List<PoolMember> members = new List<PoolMember>(contentIDs.Length);
            for (int i = 0; i < contentIDs.Length; i++)
            {
                members.Add(new PoolMember(contentIDs[i], price));
            }

            return new PoolDefinition(new PoolID(id), id, "UNIFORM_WITHOUT_REPLACEMENT", null, members);
        }

        /// <summary>
        /// Builds a catalog carrying the fixture content plus the given shops and pools, so a shop
        /// suite resolves its offers through a real catalog rather than a stub.
        /// </summary>
        /// <param name="shops">The shop definitions.</param>
        /// <param name="pools">The pool definitions.</param>
        /// <returns>The hand-built catalog.</returns>
        public static ContentCatalog ShopCatalog(IReadOnlyList<ShopDefinition> shops, IReadOnlyList<PoolDefinition> pools)
        {
            return new ContentCatalog(
                "fixture-0.0.1",
                ProgressionFixtures.Parameters(),
                new[]
                {
                    ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusTwo),
                    ProgressionFixtures.Instruction(ProgressionFixtures.ValuePlusThree),
                    ScoreInstruction(ProgressionFixtures.ScorePlusValue)
                },
                new[]
                {
                    ProgressionFixtures.Structure(ProgressionFixtures.RepeatTwo),
                    ProgressionFixtures.Condition(ProgressionFixtures.ConditionValueEven)
                },
                new[] { ProgressionFixtures.Directive(ProgressionFixtures.Overclock) },
                new[]
                {
                    ProgressionFixtures.Dependency(ProgressionFixtures.StandardLibrary, 0),
                    ProgressionFixtures.Dependency(ProgressionFixtures.CleanBuild, 1),
                    Dependency(OneRAMDependency, 1),
                    Dependency(TwoRAMDependency, 2)
                },
                new[] { PatchWithRule("WB-PAT-001", "FIXED_NUMBER_ADDITION_HOSTS") },
                new[] { Utility(StartingBytesUtility, "STARTING_BYTES", 1) },
                Array.Empty<ProcessRuleDefinition>(),
                Array.Empty<CoreDefinition>(),
                Array.Empty<ProcessConfigurationDefinition>(),
                shops,
                pools,
                Array.Empty<RewardPackageDefinition>(),
                Array.Empty<RouteDefinition>(),
                Array.Empty<StarterArchetypeDefinition>(),
                Array.Empty<SystemDefinition>()
            );
        }

        /// <summary>
        /// Builds a catalog carrying the fixture content plus the given pools, for the reward suites,
        /// which resolve pool choices and guaranteed content through a real catalog.
        /// </summary>
        /// <param name="pools">The pool definitions.</param>
        /// <returns>The hand-built catalog.</returns>
        public static ContentCatalog RewardCatalog(IReadOnlyList<PoolDefinition> pools)
        {
            return ShopCatalog(Array.Empty<ShopDefinition>(), pools);
        }

        /// <summary>
        /// Builds a Session over a shop-carrying catalog, credited to the given Token balance.
        /// </summary>
        /// <param name="catalog">The catalog to seed from.</param>
        /// <param name="tokens">The Tokens to open with.</param>
        /// <returns>The seeded Session state.</returns>
        public static SessionState SessionOver(ContentCatalog catalog, int tokens)
        {
            SessionState session = SessionState.Create(
                catalog,
                ProgressionFixtures.StandardArchetype(),
                "WB-SYS-001",
                "session-1",
                "seed-1");

            if (tokens > 0)
            {
                session.Economy.Tokens.Credit(new TokenAmount(tokens), TokenBasis.RewardTokens, "fixture");
            }

            return session;
        }

        /// <summary>
        /// Builds the GARBAGE COLLECTOR shape: one BUILD_INTERACTION effect gaining a Byte on the first
        /// archive of each Process. Hand-built rather than read from the shipped catalog so the
        /// Progression suites stay engine-and-file free.
        /// </summary>
        /// <returns>The Dependency definition.</returns>
        public static DependencyDefinition GarbageCollector()
        {
            EffectDefinition gain = new(
                PhaseDomain.BuildInteraction,
                new TriggerDescriptor(EventFamily.ContentLifecycle, "OBJECT_ARCHIVED", Array.Empty<TriggerQualifier>(), null),
                new ResourceGainOperation("BYTES", 1),
                null,
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS"));

            return new DependencyDefinition(
                new DependencyID("WB-DEP-008"),
                "GARBAGE COLLECTOR",
                "GARBAGE COLLECTOR",
                ContentCategory.Dependency,
                Rarity.Uncommon,
                new[] { "Buffer", "Archive", "Bytes" },
                1,
                new[] { gain }
            );
        }

        /// <summary>
        /// Builds a Dependency instance over a fixture definition.
        /// </summary>
        /// <param name="id">The Dependency's surrogate-key identity.</param>
        /// <param name="ram">The RAM the installed Dependency consumes.</param>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        public static DependencyInstance Instance(string id, int ram, int instance)
        {
            return new DependencyInstance(new InstanceID(instance), Dependency(id, ram));
        }

        /// <summary>
        /// The Process identity the commitment suites target.
        /// </summary>
        /// <param name="id">The Process configuration's surrogate-key identity.</param>
        /// <returns>The Process identity.</returns>
        public static ProcessID Process(string id)
        {
            return new ProcessID(id);
        }

        /// <summary>
        /// Collects a read-only list into a plain list, so a suite can compare it without Linq.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source list.</param>
        /// <returns>The copied list.</returns>
        public static List<T> Copy<T>(IReadOnlyList<T> source)
        {
            List<T> copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return copy;
        }
    }
}
