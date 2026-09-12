using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// The frozen catalog root: the revision identity, the parameter register, and every content
    /// definition indexed for per-category lookup and exposed as ordered lists. Constructed only by
    /// the loader; carries no mutation surface. Guards against null category lists and duplicate IDs
    /// as defense-in-depth behind the validator.
    /// </summary>
    public sealed class ContentCatalog
    {
        private readonly IReadOnlyDictionary<InstructionID, InstructionDefinition> _instructionsByID;
        private readonly IReadOnlyDictionary<StructureID, StructureDefinition> _structuresByID;
        private readonly IReadOnlyDictionary<DirectiveID, DirectiveDefinition> _directivesByID;
        private readonly IReadOnlyDictionary<DependencyID, DependencyDefinition> _dependenciesByID;
        private readonly IReadOnlyDictionary<PatchID, PatchDefinition> _patchesByID;
        private readonly IReadOnlyDictionary<UtilityID, UtilityDefinition> _utilitiesByID;
        private readonly IReadOnlyDictionary<ProcessRuleID, ProcessRuleDefinition> _processRulesByID;
        private readonly IReadOnlyDictionary<CoreID, CoreDefinition> _coresByID;
        private readonly IReadOnlyDictionary<ProcessID, ProcessConfigurationDefinition> _processConfigurationsByID;
        private readonly IReadOnlyDictionary<ShopID, ShopDefinition> _shopsByID;
        private readonly IReadOnlyDictionary<PoolID, PoolDefinition> _poolsByID;
        private readonly IReadOnlyDictionary<RewardPackageID, RewardPackageDefinition> _rewardPackagesByID;
        private readonly IReadOnlyDictionary<RouteID, RouteDefinition> _routesByID;
        private readonly IReadOnlyDictionary<StarterArchetypeID, StarterArchetypeDefinition> _starterArchetypesByID;
        private readonly IReadOnlyDictionary<SystemID, SystemDefinition> _systemsByID;

        /// <summary>
        /// The catalog revision identity string.
        /// </summary>
        public string Revision { get; }

        /// <summary>
        /// The locked parameter register.
        /// </summary>
        public ParameterSet Parameters { get; }

        /// <summary>
        /// The Instruction definitions in authored order.
        /// </summary>
        public IReadOnlyList<InstructionDefinition> Instructions { get; }

        /// <summary>
        /// The Structure definitions in authored order.
        /// </summary>
        public IReadOnlyList<StructureDefinition> Structures { get; }

        /// <summary>
        /// The Directive definitions in authored order.
        /// </summary>
        public IReadOnlyList<DirectiveDefinition> Directives { get; }

        /// <summary>
        /// The Dependency definitions in authored order.
        /// </summary>
        public IReadOnlyList<DependencyDefinition> Dependencies { get; }

        /// <summary>
        /// The Patch definitions in authored order.
        /// </summary>
        public IReadOnlyList<PatchDefinition> Patches { get; }

        /// <summary>
        /// The Utility definitions in authored order.
        /// </summary>
        public IReadOnlyList<UtilityDefinition> Utilities { get; }
        
        /// <summary>
        /// The Process-rule definitions in authored order.
        /// </summary>
        public IReadOnlyList<ProcessRuleDefinition> ProcessRules { get; }
        
        /// <summary>
        /// The Core definitions in authored order.
        /// </summary>
        public IReadOnlyList<CoreDefinition> Cores { get; }

        /// <summary>
        /// The Process-configuration definitions in authored order.
        /// </summary>
        public IReadOnlyList<ProcessConfigurationDefinition> ProcessConfigurations { get; }

        /// <summary>
        /// The shop definitions in authored order.
        /// </summary>
        public IReadOnlyList<ShopDefinition> Shops { get; }

        /// <summary>
        /// The acquisition-pool definitions in authored order.
        /// </summary>
        public IReadOnlyList<PoolDefinition> Pools { get; }

        /// <summary>
        /// The reward-package definitions in authored order.
        /// </summary>
        public IReadOnlyList<RewardPackageDefinition> RewardPackages { get; }

        /// <summary>
        /// The route definitions in authored order.
        /// </summary>
        public IReadOnlyList<RouteDefinition> Routes { get; }

        /// <summary>
        /// The Starter Archetype definitions in authored order.
        /// </summary>
        public IReadOnlyList<StarterArchetypeDefinition> StarterArchetypes { get; }

        /// <summary>
        /// The System definitions in authored order.
        /// </summary>
        public IReadOnlyList<SystemDefinition> Systems { get; }


        /// <summary>
        /// The total number of definitions across every category.
        /// </summary>
        public int DefinitionCount { get; }

        public ContentCatalog(
            string revision,
            ParameterSet parameters,
            IReadOnlyList<InstructionDefinition> instructions,
            IReadOnlyList<StructureDefinition> structures,
            IReadOnlyList<DirectiveDefinition> directives,
            IReadOnlyList<DependencyDefinition> dependencies,
            IReadOnlyList<PatchDefinition> patches,
            IReadOnlyList<UtilityDefinition> utilities,
            IReadOnlyList<ProcessRuleDefinition> processRules
        ) : this(
            revision,
            parameters,
            instructions,
            structures,
            directives,
            dependencies,
            patches,
            utilities,
            processRules,
            Array.Empty<CoreDefinition>(),
            Array.Empty<ProcessConfigurationDefinition>(),
            Array.Empty<ShopDefinition>(),
            Array.Empty<PoolDefinition>(),
            Array.Empty<RewardPackageDefinition>(),
            Array.Empty<RouteDefinition>(),
            Array.Empty<StarterArchetypeDefinition>(),
            Array.Empty<SystemDefinition>()
        ) { }

        public ContentCatalog(
            string revision,
            ParameterSet parameters,
            IReadOnlyList<InstructionDefinition> instructions,
            IReadOnlyList<StructureDefinition> structures,
            IReadOnlyList<DirectiveDefinition> directives,
            IReadOnlyList<DependencyDefinition> dependencies,
            IReadOnlyList<PatchDefinition> patches,
            IReadOnlyList<UtilityDefinition> utilities,
            IReadOnlyList<ProcessRuleDefinition> processRules,
            IReadOnlyList<CoreDefinition> cores,
            IReadOnlyList<ProcessConfigurationDefinition> processConfigurations,
            IReadOnlyList<ShopDefinition> shops,
            IReadOnlyList<PoolDefinition> pools,
            IReadOnlyList<RewardPackageDefinition> rewardPackages,
            IReadOnlyList<RouteDefinition> routes,
            IReadOnlyList<StarterArchetypeDefinition> starterArchetypes,
            IReadOnlyList<SystemDefinition> systems
        )
        {
            Revision = revision ?? throw new ArgumentNullException(nameof(revision));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Instructions = instructions ?? throw new ArgumentNullException(nameof(instructions));
            Structures = structures ?? throw new ArgumentNullException(nameof(structures));
            Directives = directives ?? throw new ArgumentNullException(nameof(directives));
            Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            Patches = patches ?? throw new ArgumentNullException(nameof(patches));
            Utilities = utilities ?? throw new ArgumentNullException(nameof(utilities));
            ProcessRules = processRules ?? throw new ArgumentNullException(nameof(processRules));
            Cores = cores ?? throw new ArgumentNullException(nameof(cores));
            ProcessConfigurations = processConfigurations ?? throw new ArgumentNullException(nameof(processConfigurations));
            Shops = shops ?? throw new ArgumentNullException(nameof(shops));
            Pools = pools ?? throw new ArgumentNullException(nameof(pools));
            RewardPackages = rewardPackages ?? throw new ArgumentNullException(nameof(rewardPackages));
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            StarterArchetypes = starterArchetypes ?? throw new ArgumentNullException(nameof(starterArchetypes));
            Systems = systems ?? throw new ArgumentNullException(nameof(systems));

            _instructionsByID = BuildIndex(instructions, definition => definition.ID);
            _structuresByID = BuildIndex(structures, definition => definition.ID);
            _directivesByID = BuildIndex(directives, definition => definition.ID);
            _dependenciesByID = BuildIndex(dependencies, definition => definition.ID);
            _patchesByID = BuildIndex(patches, definition => definition.ID);
            _utilitiesByID = BuildIndex(utilities, definition => definition.ID);
            _processRulesByID = BuildIndex(processRules, definition => definition.ID);
            _coresByID = BuildIndex(cores, definition => definition.ID);
            _processConfigurationsByID = BuildIndex(processConfigurations, definition => definition.ID);
            _shopsByID = BuildIndex(shops, definition => definition.ID);
            _poolsByID = BuildIndex(pools, definition => definition.ID);
            _rewardPackagesByID = BuildIndex(rewardPackages, definition => definition.ID);
            _routesByID = BuildIndex(routes, definition => definition.ID);
            _starterArchetypesByID = BuildIndex(starterArchetypes, definition => definition.ID);
            _systemsByID = BuildIndex(systems, definition => definition.ID);

            DefinitionCount = instructions.Count
                              + structures.Count
                              + directives.Count
                              + dependencies.Count
                              + patches.Count
                              + utilities.Count
                              + processRules.Count
                              + cores.Count
                              + processConfigurations.Count
                              + shops.Count
                              + pools.Count
                              + rewardPackages.Count
                              + routes.Count
                              + starterArchetypes.Count
                              + systems.Count;
        }

        /// <summary>
        /// Looks up an Instruction definition by ID.
        /// </summary>
        /// <param name="id">The Instruction ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetInstruction(InstructionID id, out InstructionDefinition definition)
        {
            return _instructionsByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Structure definition by ID.
        /// </summary>
        /// <param name="id">The Structure ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetStructure(StructureID id, out StructureDefinition definition)
        {
            return _structuresByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Directive definition by ID.
        /// </summary>
        /// <param name="id">The Directive ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetDirective(DirectiveID id, out DirectiveDefinition definition)
        {
            return _directivesByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Dependency definition by ID.
        /// </summary>
        /// <param name="id">The Dependency ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetDependency(DependencyID id, out DependencyDefinition definition)
        {
            return _dependenciesByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Patch definition by ID.
        /// </summary>
        /// <param name="id">The Patch ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetPatch(PatchID id, out PatchDefinition definition)
        {
            return _patchesByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Utility definition by ID.
        /// </summary>
        /// <param name="id">The Utility ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetUtility(UtilityID id, out UtilityDefinition definition)
        {
            return _utilitiesByID.TryGetValue(id, out definition);
        }
        
        /// <summary>
        /// Looks up a Process-rule definition by ID.
        /// </summary>
        /// <param name="id">The Process-rule ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetProcessRule(ProcessRuleID id, out ProcessRuleDefinition definition)
        {
            return _processRulesByID.TryGetValue(id, out definition);
        }
        
        /// <summary>
        /// Looks up a Core definition by ID.
        /// </summary>
        /// <param name="id">The Core ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetCore(CoreID id, out CoreDefinition definition)
        {
            return _coresByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Process configuration by ID.
        /// </summary>
        /// <param name="id">The Process ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetProcessConfiguration(ProcessID id, out ProcessConfigurationDefinition definition)
        {
            return _processConfigurationsByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a shop definition by ID.
        /// </summary>
        /// <param name="id">The shop ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetShop(ShopID id, out ShopDefinition definition)
        {
            return _shopsByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up an acquisition-pool definition by ID.
        /// </summary>
        /// <param name="id">The pool ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetPool(PoolID id, out PoolDefinition definition)
        {
            return _poolsByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a reward-package definition by ID.
        /// </summary>
        /// <param name="id">The reward-package ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetRewardPackage(RewardPackageID id, out RewardPackageDefinition definition)
        {
            return _rewardPackagesByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a route definition by ID.
        /// </summary>
        /// <param name="id">The route ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetRoute(RouteID id, out RouteDefinition definition)
        {
            return _routesByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a Starter Archetype definition by ID.
        /// </summary>
        /// <param name="id">The archetype ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetStarterArchetype(StarterArchetypeID id, out StarterArchetypeDefinition definition)
        {
            return _starterArchetypesByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a System definition by ID.
        /// </summary>
        /// <param name="id">The System ID to look up.</param>
        /// <param name="definition">The found definition, or null when absent.</param>
        /// <returns>True when the ID resolves to a definition.</returns>
        public bool TryGetSystem(SystemID id, out SystemDefinition definition)
        {
            return _systemsByID.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Resolves a content ID string to the Repository-item definition it names. This is the only
        /// place that maps an ID prefix to a content kind, and it covers exactly the three item
        /// categories a Repository holds: an Instruction, a Structure or a Directive. Every other
        /// prefix — Dependencies, Patches, Utilities, Process rules, parameters and the package kinds —
        /// returns false, because none of them is a Repository item.
        /// </summary>
        /// <param name="id">The content ID string, as authored in a package or configuration.</param>
        /// <param name="definition">The found item definition, or null when the ID names no item.</param>
        /// <returns>True when the ID resolves to an Instruction, Structure or Directive.</returns>
        public bool TryGetItem(string id, out ContentDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;

            if (id.StartsWith("WB-INS-", StringComparison.Ordinal))
            {
                if (!_instructionsByID.TryGetValue(new InstructionID(id), out InstructionDefinition instruction))
                    return false;

                definition = instruction;
                return true;
            }

            if (id.StartsWith("WB-STR-", StringComparison.Ordinal))
            {
                if (!_structuresByID.TryGetValue(new StructureID(id), out StructureDefinition structure))
                    return false;

                definition = structure;
                return true;
            }

            if (id.StartsWith("WB-DIR-", StringComparison.Ordinal))
            {
                if (!_directivesByID.TryGetValue(new DirectiveID(id), out DirectiveDefinition directive))
                    return false;

                definition = directive;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Indexes definitions by ID, throwing on a duplicate ID within a category.
        /// </summary>
        /// <typeparam name="TKey">The ID type.</typeparam>
        /// <typeparam name="TValue">The definition type.</typeparam>
        /// <param name="definitions">The definitions to index.</param>
        /// <param name="selectID">Selects a definition's ID.</param>
        /// <returns>The ID-to-definition index.</returns>
        /// <exception cref="ArgumentException">Thrown when two definitions share an ID.</exception>
        private static IReadOnlyDictionary<TKey, TValue> BuildIndex<TKey, TValue>(
            IReadOnlyList<TValue> definitions,
            Func<TValue, TKey> selectID
        )
        {
            Dictionary<TKey, TValue> index = new(definitions.Count);
            foreach (TValue definition in definitions)
            {
                TKey id = selectID(definition);
                if (!index.TryAdd(id, definition))
                    throw new ArgumentException($"The catalog contains a duplicate definition ID {id}.");
            }

            return index;
        }
    }
}