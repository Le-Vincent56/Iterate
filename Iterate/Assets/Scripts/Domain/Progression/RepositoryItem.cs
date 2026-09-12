using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One Repository item: a closed record over the three runtime instance kinds, carrying exactly one
    /// non-null typed instance alongside the identity, definition ID and tags every Progression site
    /// reads without caring which kind it is. <see cref="From"/> is the only place that maps a content
    /// category onto a kind, so no other site switches on category to build one.
    /// </summary>
    /// <param name="Kind">Which of the three item kinds this is.</param>
    /// <param name="InstanceID">The stable instance identity, shared with the typed instance.</param>
    /// <param name="DefinitionID">The definition's stable identity, as a string.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="Instruction">The Instruction instance; null for every other kind.</param>
    /// <param name="Structure">The Structure instance; null for every other kind.</param>
    /// <param name="Directive">The Directive instance; null for every other kind.</param>
    public sealed record RepositoryItem(
        RepositoryItemKind Kind,
        InstanceID InstanceID,
        string DefinitionID,
        IReadOnlyList<string> Tags,
        InstructionInstance Instruction,
        StructureInstance Structure,
        DirectiveInstance Directive
    )
    {
        /// <summary>
        /// The definition's stable identity. Validated against the kind's whole field shape at
        /// construction, so an item with the wrong instance populated cannot exist.
        /// </summary>
        public string DefinitionID { get; } = RequireShape(
            Kind,
            DefinitionID,
            Instruction,
            Structure,
            Directive
        );

        /// <summary>
        /// Builds the Repository item for a content definition. The one switch from content category to
        /// item kind: Instruction, Structure and Directive definitions become items, and every other
        /// category is a contract violation rather than a rejection, because the Repository's own
        /// operations screen categories before they reach here.
        /// </summary>
        /// <param name="definition">The frozen content definition.</param>
        /// <param name="id">The instance identity to give the item.</param>
        /// <returns>The constructed Repository item.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is absent or not an item.</exception>
        public static RepositoryItem From(ContentDefinition definition, InstanceID id)
        {
            if (definition == null)
                throw new ArgumentException("A Repository item requires a definition.", nameof(definition));

            switch (definition.Category)
            {
                case ContentCategory.Instruction:
                    InstructionDefinition instruction = Require<InstructionDefinition>(definition);
                    return new RepositoryItem(
                        RepositoryItemKind.Instruction,
                        id,
                        instruction.ID.Value,
                        instruction.Tags,
                        new InstructionInstance(id, instruction, null),
                        null,
                        null
                    );

                case ContentCategory.Structure:
                    StructureDefinition structure = Require<StructureDefinition>(definition);
                    return new RepositoryItem(
                        RepositoryItemKind.Structure,
                        id,
                        structure.ID.Value,
                        structure.Tags,
                        null,
                        new StructureInstance(id, structure),
                        null
                    );

                case ContentCategory.Directive:
                    DirectiveDefinition directive = Require<DirectiveDefinition>(definition);
                    return new RepositoryItem(
                        RepositoryItemKind.Directive,
                        id,
                        directive.ID.Value,
                        directive.Tags,
                        null,
                        null,
                        new DirectiveInstance(id, directive)
                    );

                default:
                    throw new ArgumentException(
                        "'" + definition.Category + "' content is not a Repository item.",
                        nameof(definition)
                    );
            }
        }

        /// <summary>
        /// Casts a definition to the type its declared category implies, failing loudly when the row's
        /// category and runtime type disagree.
        /// </summary>
        /// <typeparam name="TDefinition">The definition type the category implies.</typeparam>
        /// <param name="definition">The definition to cast.</param>
        /// <returns>The typed definition.</returns>
        /// <exception cref="ArgumentException">Thrown when the category and type disagree.</exception>
        private static TDefinition Require<TDefinition>(ContentDefinition definition) where TDefinition : ContentDefinition
        {
            if (definition is not TDefinition typed)
                throw new ArgumentException(
                    "the definition declares category '" + definition.Category + "' but is not a " + typeof(TDefinition).Name + ".",
                    nameof(definition)
                );

            return typed;
        }

        /// <summary>
        /// Validates that exactly the one instance the kind admits is present, and returns the
        /// definition ID unchanged.
        /// </summary>
        /// <param name="kind">The declared item kind.</param>
        /// <param name="definitionID">The candidate definition ID.</param>
        /// <param name="instruction">The candidate Instruction instance.</param>
        /// <param name="structure">The candidate Structure instance.</param>
        /// <param name="directive">The candidate Directive instance.</param>
        /// <returns>The definition ID unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the ID is empty or the shape is wrong.</exception>
        private static string RequireShape(
            RepositoryItemKind kind,
            string definitionID,
            InstructionInstance instruction,
            StructureInstance structure,
            DirectiveInstance directive
        )
        {
            if (string.IsNullOrEmpty(definitionID))
                throw new ArgumentException("A Repository item requires a definition ID.", nameof(definitionID));

            int present = 0;
            if (instruction != null) present++;
            if (structure != null) present++;
            if (directive != null) present++;

            if (present != 1)
                throw new ArgumentException("A Repository item carries exactly one typed instance.", nameof(definitionID));

            bool matches = kind switch
            {
                RepositoryItemKind.Instruction => instruction != null,
                RepositoryItemKind.Structure => structure != null,
                _ => directive != null
            };

            if (!matches)
                throw new ArgumentException("A Repository item's instance must match its kind.", nameof(kind));

            return definitionID;
        }
    }
}