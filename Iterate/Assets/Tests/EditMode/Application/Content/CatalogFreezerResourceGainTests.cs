using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests that a RESOURCE_GAIN operation survives freezing as a typed
    /// <see cref="ResourceGainOperation"/>. Freezing is where an authored token becomes a runtime
    /// primitive, so a kind the freezer does not know throws rather than producing a null operation.
    /// </summary>
    public sealed class CatalogFreezerResourceGainTests
    {
        [Test]
        public void AByteGainOnArchive_FreezesToAResourceGainOperation()
        {
            ContentCatalog catalog = Freeze();

            Assert.IsTrue(catalog.TryGetDependency(new DependencyID("WB-DEP-008"), out DependencyDefinition dependency));
            Assert.AreEqual(1, dependency.Effects.Count);
            Assert.AreEqual(PhaseDomain.BuildInteraction, dependency.Effects[0].PhaseDomain);
            Assert.IsInstanceOf<ResourceGainOperation>(dependency.Effects[0].Operation);

            ResourceGainOperation gain = (ResourceGainOperation)dependency.Effects[0].Operation;
            Assert.AreEqual("BYTES", gain.Resource);
            Assert.AreEqual(1, gain.Amount);
            Assert.AreEqual(OperationKind.ResourceGain, gain.Kind);
        }

        [Test]
        public void TheFrozenEffect_KeepsItsTriggerAndFrequency()
        {
            ContentCatalog catalog = Freeze();
            catalog.TryGetDependency(new DependencyID("WB-DEP-008"), out DependencyDefinition dependency);

            EffectDefinition effect = dependency.Effects[0];

            Assert.AreEqual(EventFamily.ContentLifecycle, effect.Trigger.EventFamily);
            Assert.AreEqual("OBJECT_ARCHIVED", effect.Trigger.EventSubtype);
            Assert.AreEqual("FIRST_QUALIFYING_EVENT", effect.Frequency.Allowance);
            Assert.AreEqual("PROCESS", effect.Frequency.Scope);
        }

        private static ContentCatalog Freeze()
        {
            // Appended to the valid fixture rather than replacing it: archetypes.json names
            // WB-DEP-001 as its starter Dependency, so a dependencies file without it fails the
            // cross-file reference check before the freezer is ever reached.
            string dependencies = CatalogTestFixtures.ValidDependenciesFile.TrimEnd().TrimEnd(']') + @",
                {
                    ""id"": ""WB-DEP-008"", ""rulesText"": ""x"", ""displayName"": ""GARBAGE COLLECTOR"",
                    ""category"": ""DEPENDENCY"", ""rarity"": ""UNCOMMON"", ""tags"": [""Archive""],
                    ""ram"": 1,
                    ""effects"": [
                        {
                            ""phaseDomain"": ""BUILD_INTERACTION"",
                            ""trigger"": { ""eventFamily"": ""CONTENT_LIFECYCLE"", ""eventSubtype"": ""OBJECT_ARCHIVED"", ""qualifiers"": [] },
                            ""operation"": { ""kind"": ""RESOURCE_GAIN"", ""resource"": ""BYTES"", ""amount"": 1 },
                            ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                            ""stacking"": ""INDEPENDENT_RESOLUTION"",
                            ""frequency"": { ""allowance"": ""FIRST_QUALIFYING_EVENT"", ""scope"": ""PROCESS"" }
                        }
                    ]
                }
            ]";

            Dictionary<string, string> files = CatalogExtensionFixtures.FreezableFiles(
                ("dependencies.json", dependencies));
            FakeCatalogFileSource source = new(CatalogExtensionFixtures.FreezableManifest, files);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
