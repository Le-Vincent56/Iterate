using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests construction and effect exposure of <see cref="UtilityDefinition"/>.
    /// </summary>
    public sealed class UtilityDefinitionTests
    {
        [Test]
        public void Constructor_ExposesIDAndEffects()
        {
            IReadOnlyList<EffectDefinition> effects = new[] { EffectFixtures.ConfigurationSetup() };

            UtilityDefinition utility = new(
                new UtilityID("WB-UTL-001"),
                "BYTE CACHE effect text",
                "BYTE CACHE",
                ContentCategory.Utility,
                Rarity.Common,
                new[] { "Utility" },
                effects
            );

            Assert.AreEqual(new UtilityID("WB-UTL-001"), utility.ID);
            Assert.AreEqual(1, utility.Effects.Count);
            Assert.AreEqual(PhaseDomain.ProcessSetup, utility.Effects[0].PhaseDomain);
        }
    }
}
