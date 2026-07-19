using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Domain.Values;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Loads the shipped catalog under StreamingAssets/Catalog through the real pipeline and pins its
    /// conformance: it loads without errors at revision 0.1.0, carries all 45 definitions with the
    /// per-category counts, exposes the locked parameters, and matches the slice's spot-check items.
    /// This is the standing TA-DAT-004 conformance evidence; it is red until the catalog files exist.
    /// </summary>
    public sealed class ShippedCatalogTests
    {
        private static ContentCatalog Load()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void ShippedCatalog_LoadsAtRevision010()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual("0.1.0", catalog.Revision);
        }

        [Test]
        public void ShippedCatalog_HasForty5DefinitionsWithPerCategoryCounts()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(15, catalog.Instructions.Count, "Instructions");
            Assert.AreEqual(6, catalog.Structures.Count, "Structures");
            Assert.AreEqual(4, catalog.Directives.Count, "Directives");
            Assert.AreEqual(11, catalog.Dependencies.Count, "Dependencies");
            Assert.AreEqual(6, catalog.Patches.Count, "Patches");
            Assert.AreEqual(3, catalog.Utilities.Count, "Utilities");
            Assert.AreEqual(45, catalog.DefinitionCount, "total");
        }

        [Test]
        public void ShippedCatalog_LocksStartingRAMToFour()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(4, catalog.Parameters.StartingRAM);
        }

        [Test]
        public void ShippedCatalog_FinalLineInstructionCarriesPositionQualifier()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetInstruction(new InstructionID("WB-INS-014"), out InstructionDefinition instruction));
            Assert.IsNotNull(instruction.PositionQualifier);
        }

        [Test]
        public void ShippedCatalog_SafeModeResolvesToRescueOperation()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetDependency(new DependencyID("WB-DEP-007"), out DependencyDefinition dependency));
            Assert.AreEqual("SAFE MODE", dependency.DisplayName);
            Assert.GreaterOrEqual(dependency.Effects.Count, 1);
            Assert.IsInstanceOf<RescueOperation>(dependency.Effects[0].Operation);
        }

        [Test]
        public void ShippedCatalog_BurstOutputCarriesTwoEffects()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetDirective(new DirectiveID("WB-DIR-003"), out DirectiveDefinition directive));
            Assert.AreEqual("BURST OUTPUT", directive.DisplayName);
            Assert.AreEqual(2, directive.Effects.Count);
        }

        [Test]
        public void ShippedCatalog_CompileAheadIsCompilationDomain()
        {
            ContentCatalog catalog = Load();

            Assert.IsTrue(catalog.TryGetDirective(new DirectiveID("WB-DIR-004"), out DirectiveDefinition directive));
            Assert.AreEqual("COMPILE AHEAD", directive.DisplayName);
            Assert.AreEqual(PhaseDomain.Compilation, directive.Effects[0].PhaseDomain);
        }

        [Test]
        public void ShippedCatalog_UtilitiesAreProcessSetup()
        {
            ContentCatalog catalog = Load();

            Assert.AreEqual(3, catalog.Utilities.Count);
            for (int index = 0; index < catalog.Utilities.Count; index++)
            {
                UtilityDefinition utility = catalog.Utilities[index];
                for (int effectIndex = 0; effectIndex < utility.Effects.Count; effectIndex++)
                {
                    Assert.AreEqual(PhaseDomain.ProcessSetup, utility.Effects[effectIndex].PhaseDomain, utility.DisplayName);
                }
            }
        }
    }
}
