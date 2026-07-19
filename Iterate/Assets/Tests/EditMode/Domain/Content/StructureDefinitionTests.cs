using System;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests that <see cref="StructureDefinition"/> enforces the Repeat/Condition field-pairing shape:
    /// a Repeat carries a positive repeat count and no predicate; a Condition carries a predicate and
    /// no repeat count.
    /// </summary>
    public sealed class StructureDefinitionTests
    {
        private static readonly string[] _tags = { "Structure", "Repeat" };

        [Test]
        public void Repeat_WithCountAndNoPredicate_Constructs()
        {
            StructureDefinition structure = new(
                new StructureID("WB-STR-001"),
                "Repeat 2",
                "Repeat 2",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Repeat,
                2,
                null
            );

            Assert.AreEqual(StructureKind.Repeat, structure.StructureKind);
            Assert.AreEqual(2, structure.RepeatCount);
            Assert.IsNull(structure.Predicate);
        }

        [Test]
        public void Condition_WithPredicateAndZeroCount_Constructs()
        {
            StructurePredicate predicate = new(CoreRegister.Value, PredicateComparison.IsEven, 0);

            StructureDefinition structure = new(
                new StructureID("WB-STR-002"),
                "If Value is even: [1 instruction]",
                "If Value is even: [1 instruction]",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Condition,
                0,
                predicate
            );

            Assert.AreEqual(StructureKind.Condition, structure.StructureKind);
            Assert.AreEqual(predicate, structure.Predicate);
        }

        [Test]
        public void Repeat_WithPredicate_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureDefinition(
                new StructureID("WB-STR-001"),
                "Repeat 2",
                "Repeat 2",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Repeat,
                2,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0)
            ));
        }

        [Test]
        public void Repeat_WithZeroCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureDefinition(
                new StructureID("WB-STR-001"),
                "Repeat 2",
                "Repeat 2",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Repeat,
                0,
                null
            ));
        }

        [Test]
        public void Condition_WithNullPredicate_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureDefinition(
                new StructureID("WB-STR-002"),
                "If Value is even: [1 instruction]",
                "If Value is even: [1 instruction]",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Condition,
                0,
                null
            ));
        }

        [Test]
        public void Condition_WithNonZeroCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureDefinition(
                new StructureID("WB-STR-002"),
                "If Value is even: [1 instruction]",
                "If Value is even: [1 instruction]",
                ContentCategory.Structure,
                Rarity.Common,
                _tags,
                2,
                StructureKind.Condition,
                3,
                new StructurePredicate(CoreRegister.Value, PredicateComparison.IsEven, 0)
            ));
        }
    }
}
