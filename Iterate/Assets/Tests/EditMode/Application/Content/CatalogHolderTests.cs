using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the thread-safe <see cref="CatalogHolder"/> state machine: the Unloaded → Loaded | Failed
    /// transitions, the double-mark guard, and the non-throwing <see cref="CatalogHolder.TryMarkFailed"/>
    /// fallback.
    /// </summary>
    public sealed class CatalogHolderTests
    {
        private static ContentCatalog EmptyCatalog()
        {
            Dictionary<string, double> parameters = new(StringComparer.Ordinal);
            string[] ids =
            {
                "WB-PAR-001", "WB-PAR-002", "WB-PAR-003", "WB-PAR-004", "WB-PAR-005",
                "WB-PAR-006", "WB-PAR-007", "WB-PAR-008", "WB-PAR-009", "WB-PAR-010",
                "WB-PAR-011", "WB-PAR-012", "WB-PAR-013", "WB-PAR-014", "WB-PAR-015",
                "WB-PAR-016", "WB-PAR-017", "WB-PAR-018", "WB-PAR-019", "WB-PAR-020",
                "WB-PAR-021", "WB-PAR-026", "WB-PAR-028", "WB-PAR-029", "WB-PAR-030", "WB-PAR-036"
            };
            foreach (string id in ids)
            {
                parameters[id] = 1;
            }

            parameters["WB-PAR-022"] = 1.0;
            parameters["WB-PAR-023"] = 1.75;
            parameters["WB-PAR-024"] = 3.0;
            parameters["WB-PAR-035"] = 0.5;

            return new ContentCatalog(
                "0.1.0",
                new ParameterSet(parameters),
                new List<InstructionDefinition>(),
                new List<StructureDefinition>(),
                new List<DirectiveDefinition>(),
                new List<DependencyDefinition>(),
                new List<PatchDefinition>(),
                new List<UtilityDefinition>()
            );
        }

        private static IReadOnlyList<CatalogError> OneError()
        {
            return new[] { new CatalogError("manifest.json", "$", "manifest.not-object", "broken.") };
        }

        [Test]
        public void NewHolder_IsUnloaded()
        {
            CatalogHolder holder = new();

            Assert.AreEqual(CatalogState.Unloaded, holder.State);
            Assert.IsFalse(holder.TryGetCatalog(out ContentCatalog catalog));
            Assert.IsNull(catalog);
            Assert.AreEqual(0, holder.Errors.Count);
        }

        [Test]
        public void MarkLoaded_TransitionsToLoadedAndExposesCatalog()
        {
            CatalogHolder holder = new();
            ContentCatalog catalog = EmptyCatalog();

            holder.MarkLoaded(catalog);

            Assert.AreEqual(CatalogState.Loaded, holder.State);
            Assert.IsTrue(holder.TryGetCatalog(out ContentCatalog resolved));
            Assert.AreSame(catalog, resolved);
        }

        [Test]
        public void MarkFailed_TransitionsToFailedAndExposesErrors()
        {
            CatalogHolder holder = new();

            holder.MarkFailed(OneError());

            Assert.AreEqual(CatalogState.Failed, holder.State);
            Assert.IsFalse(holder.TryGetCatalog(out ContentCatalog catalog));
            Assert.IsNull(catalog);
            Assert.AreEqual(1, holder.Errors.Count);
        }

        [Test]
        public void MarkLoaded_Twice_Throws()
        {
            CatalogHolder holder = new();
            holder.MarkLoaded(EmptyCatalog());

            Assert.Throws<InvalidOperationException>(() => holder.MarkLoaded(EmptyCatalog()));
        }

        [Test]
        public void MarkFailed_AfterLoaded_Throws()
        {
            CatalogHolder holder = new();
            holder.MarkLoaded(EmptyCatalog());

            Assert.Throws<InvalidOperationException>(() => holder.MarkFailed(OneError()));
        }

        [Test]
        public void TryMarkFailed_FromUnloaded_ReturnsTrueAndFails()
        {
            CatalogHolder holder = new();

            Assert.IsTrue(holder.TryMarkFailed(OneError()));
            Assert.AreEqual(CatalogState.Failed, holder.State);
        }

        [Test]
        public void TryMarkFailed_AfterLoaded_ReturnsFalseWithoutThrowing()
        {
            CatalogHolder holder = new();
            holder.MarkLoaded(EmptyCatalog());

            Assert.IsFalse(holder.TryMarkFailed(OneError()));
            Assert.AreEqual(CatalogState.Loaded, holder.State);
        }

        [Test]
        public void TryMarkFailed_AfterFailed_ReturnsFalseWithoutThrowing()
        {
            CatalogHolder holder = new();
            holder.MarkFailed(OneError());

            Assert.IsFalse(holder.TryMarkFailed(OneError()));
            Assert.AreEqual(CatalogState.Failed, holder.State);
        }
    }
}
