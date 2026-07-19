using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests construction and RAM/effect exposure of <see cref="DependencyDefinition"/>.
    /// </summary>
    public sealed class DependencyDefinitionTests
    {
        [Test]
        public void Constructor_ExposesRAMAndEffects()
        {
            IReadOnlyList<EffectDefinition> effects = new[] { EffectFixtures.QuantityReaction() };

            DependencyDefinition dependency = new(
                new DependencyID("WB-DEP-001"),
                "The first eligible fixed-number Value or Signal addition each execution resolves +1 higher.",
                "STANDARD LIBRARY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new[] { "Value", "Add", "Fixed" },
                0,
                effects
            );

            Assert.AreEqual(new DependencyID("WB-DEP-001"), dependency.ID);
            Assert.AreEqual(0, dependency.RAM);
            Assert.AreEqual(1, dependency.Effects.Count);
        }
    }
}
