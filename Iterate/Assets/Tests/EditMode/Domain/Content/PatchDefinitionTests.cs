using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Tests construction and host-eligibility/effect exposure of <see cref="PatchDefinition"/>.
    /// </summary>
    public sealed class PatchDefinitionTests
    {
        [Test]
        public void Constructor_ExposesHostEligibilityAndEffects()
        {
            PatchHostEligibility eligibility = new("FIXED_NUMBER_ADDITION_HOSTS");
            IReadOnlyList<EffectDefinition> effects = new[] { EffectFixtures.QuantityReaction() };

            PatchDefinition patch = new(
                new PatchID("WB-PAT-001"),
                "CONSTANT PATCH effect text",
                "CONSTANT PATCH",
                ContentCategory.Patch,
                Rarity.Common,
                new[] { "Patch" },
                eligibility,
                effects
            );

            Assert.AreEqual(new PatchID("WB-PAT-001"), patch.ID);
            Assert.AreEqual(eligibility, patch.HostEligibility);
            Assert.AreEqual(1, patch.Effects.Count);
        }
    }
}
