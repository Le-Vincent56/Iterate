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
            IReadOnlyList<UtilityDefinition> utilities
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

            _instructionsByID = BuildIndex(instructions, definition => definition.ID);
            _structuresByID = BuildIndex(structures, definition => definition.ID);
            _directivesByID = BuildIndex(directives, definition => definition.ID);
            _dependenciesByID = BuildIndex(dependencies, definition => definition.ID);
            _patchesByID = BuildIndex(patches, definition => definition.ID);
            _utilitiesByID = BuildIndex(utilities, definition => definition.ID);

            DefinitionCount = instructions.Count
                + structures.Count
                + directives.Count
                + dependencies.Count
                + patches.Count
                + utilities.Count;
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