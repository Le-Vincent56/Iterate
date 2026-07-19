using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests construction, field exposure, and value equality of <see cref="InstructionDefinition"/>,
    /// including the position-qualifier and ineligibility-tag variants.
    /// </summary>
    public sealed class InstructionDefinitionTests
    {
        private static readonly string[] _tags = { "Value", "Add", "Fixed" };

        private static readonly QuantityChangeOperation _addTwo =
            new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2));

        [Test]
        public void Constructor_PlainInstruction_ExposesFields()
        {
            InstructionDefinition instruction = new(
                new InstructionID("WB-INS-002"),
                "Value += 2",
                "Value += 2",
                ContentCategory.Instruction,
                Rarity.Common,
                _tags,
                1,
                _addTwo,
                null,
                Array.Empty<string>()
            );

            Assert.AreEqual(new InstructionID("WB-INS-002"), instruction.ID);
            Assert.AreEqual(1, instruction.SourceFootprint);
            Assert.AreEqual(_addTwo, instruction.PrimaryOperation);
            Assert.IsNull(instruction.PositionQualifier);
        }

        [Test]
        public void Constructor_PositionalInstruction_CarriesQualifier()
        {
            TriggerQualifier qualifier = new("POSITIONAL", "FINAL_PLAYER_LINE");

            InstructionDefinition instruction = new(
                new InstructionID("WB-INS-014"),
                "If this is the final player-controlled line, Score += Value",
                "If this is the final player-controlled line, Score += Value",
                ContentCategory.Instruction,
                Rarity.Uncommon,
                _tags,
                1,
                new QuantityChangeOperation(CoreRegister.Score, QuantityOperator.Add, OperandSpec.FromRegister(CoreRegister.Value)),
                qualifier,
                Array.Empty<string>()
            );

            Assert.AreEqual(qualifier, instruction.PositionQualifier);
        }

        [Test]
        public void Constructor_IneligibleInstruction_CarriesIneligibilityTags()
        {
            IReadOnlyList<string> ineligibility = new[] { "STANDARD LIBRARY", "CONSTANT PATCH" };

            InstructionDefinition instruction = new(
                new InstructionID("WB-INS-015"),
                "Value += this line's number",
                "Value += this line's number",
                ContentCategory.Instruction,
                Rarity.Uncommon,
                _tags,
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromLineNumber()),
                null,
                ineligibility
            );

            Assert.AreEqual(ineligibility, instruction.IneligibilityTags);
        }

        [Test]
        public void Equality_SameComponents_AreEqual()
        {
            InstructionDefinition first = new(
                new InstructionID("WB-INS-002"),
                "Value += 2",
                "Value += 2",
                ContentCategory.Instruction,
                Rarity.Common,
                _tags,
                1,
                _addTwo,
                null,
                Array.Empty<string>()
            );
            InstructionDefinition second = first with { };

            Assert.AreEqual(first, second);
        }
    }
}
