using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests construction and effect exposure of <see cref="DirectiveDefinition"/>.
    /// </summary>
    public sealed class DirectiveDefinitionTests
    {
        [Test]
        public void Constructor_ExposesIDAndEffects()
        {
            IReadOnlyList<EffectDefinition> effects = new[] { EffectFixtures.QuantityReaction() };

            DirectiveDefinition directive = new(
                new DirectiveID("WB-DIR-001"),
                "OVERCLOCK effect text",
                "OVERCLOCK",
                ContentCategory.Directive,
                Rarity.Uncommon,
                new[] { "Directive" },
                effects
            );

            Assert.AreEqual(new DirectiveID("WB-DIR-001"), directive.ID);
            Assert.AreEqual(1, directive.Effects.Count);
        }
    }
}
