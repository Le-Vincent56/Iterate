using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests <see cref="ContentCatalog"/> per-category lookups, ordered-list preservation, the total
    /// definition count, and the defensive duplicate-ID and null-list guards.
    /// </summary>
    public sealed class ContentCatalogTests
    {
        [Test]
        public void TryGetInstruction_PresentID_ReturnsTrueAndDefinition()
        {
            ContentCatalog catalog = BuildCatalog(MakeInstruction("WB-INS-002"), MakeInstruction("WB-INS-003"));

            bool found = catalog.TryGetInstruction(new InstructionID("WB-INS-003"), out InstructionDefinition definition);

            Assert.IsTrue(found);
            Assert.AreEqual(new InstructionID("WB-INS-003"), definition.ID);
        }

        [Test]
        public void TryGetInstruction_AbsentID_ReturnsFalse()
        {
            ContentCatalog catalog = BuildCatalog(MakeInstruction("WB-INS-002"));

            bool found = catalog.TryGetInstruction(new InstructionID("WB-INS-099"), out InstructionDefinition definition);

            Assert.IsFalse(found);
            Assert.IsNull(definition);
        }

        [Test]
        public void Instructions_PreserveAuthoredOrder()
        {
            ContentCatalog catalog = BuildCatalog(MakeInstruction("WB-INS-004"), MakeInstruction("WB-INS-001"));

            Assert.AreEqual(new InstructionID("WB-INS-004"), catalog.Instructions[0].ID);
            Assert.AreEqual(new InstructionID("WB-INS-001"), catalog.Instructions[1].ID);
        }

        [Test]
        public void DefinitionCount_CountsEveryCategory()
        {
            ContentCatalog catalog = BuildCatalog(MakeInstruction("WB-INS-002"), MakeInstruction("WB-INS-003"));

            Assert.AreEqual(2, catalog.DefinitionCount);
        }

        [Test]
        public void Revision_IsExposed()
        {
            ContentCatalog catalog = BuildCatalog(MakeInstruction("WB-INS-002"));

            Assert.AreEqual("0.1.0", catalog.Revision);
        }

        [Test]
        public void Constructor_DuplicateInstructionId_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _ = BuildCatalog(MakeInstruction("WB-INS-002"), MakeInstruction("WB-INS-002"))
            );
        }

        [Test]
        public void Constructor_NullInstructionsList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ContentCatalog(
                "0.1.0",
                BuildParameters(),
                null,
                Array.Empty<StructureDefinition>(),
                Array.Empty<DirectiveDefinition>(),
                Array.Empty<DependencyDefinition>(),
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>()
            ));
        }

        /// <summary>
        /// Builds a catalog whose only populated category is the given Instructions.
        /// </summary>
        /// <param name="instructions">The Instruction definitions to place in the catalog.</param>
        /// <returns>The constructed catalog.</returns>
        private static ContentCatalog BuildCatalog(params InstructionDefinition[] instructions)
        {
            return new ContentCatalog(
                "0.1.0",
                BuildParameters(),
                instructions,
                Array.Empty<StructureDefinition>(),
                Array.Empty<DirectiveDefinition>(),
                Array.Empty<DependencyDefinition>(),
                Array.Empty<PatchDefinition>(),
                Array.Empty<UtilityDefinition>()
            );
        }

        /// <summary>
        /// Builds a minimal Instruction definition carrying the given ID.
        /// </summary>
        /// <param name="id">The Instruction's surrogate-key ID.</param>
        /// <returns>The constructed definition.</returns>
        private static InstructionDefinition MakeInstruction(string id)
        {
            return new InstructionDefinition(
                new InstructionID(id),
                "Value += 2",
                "Value += 2",
                ContentCategory.Instruction,
                Rarity.Common,
                new[] { "Value", "Add", "Fixed" },
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2)),
                null,
                Array.Empty<string>()
            );
        }

        /// <summary>
        /// Builds a complete, canonical WB-PAR register (Balance section 4).
        /// </summary>
        /// <returns>The parameter set.</returns>
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
                { "WB-PAR-035", 0.5 }, { "WB-PAR-036", 2 }
            });
        }
    }
}
