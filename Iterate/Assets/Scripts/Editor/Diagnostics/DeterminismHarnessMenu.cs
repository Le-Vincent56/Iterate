using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Iterate.Application.Content;
using Iterate.Application.Diagnostics;
using Iterate.Domain.Content;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Editor.Content;

namespace Iterate.Editor.Diagnostics
{
    /// <summary>
    /// The determinism harness: writes a baseline document per registered fixture, and compares the
    /// committed baselines against what this build produces now.
    ///
    /// This is the cross-revision drift detection that was impossible until records could persist. A
    /// baseline is a record produced by an earlier build; comparing it against a freshly built request
    /// answers a question no in-memory test can — whether *this* build still reproduces what an earlier
    /// one did. A catalog revision bump is expected to report exact reproduction unavailable rather than
    /// a divergence, which is the distinction that keeps the report useful.
    ///
    /// Editor tooling logs through UnityEngine.Debug directly: there is no composition root in Edit
    /// mode, the sanctioned exception to the runtime single-sink rule.
    /// </summary>
    public static class DeterminismHarnessMenu
    {
        /// <summary>
        /// The baselines' directory, relative to the project root.
        /// </summary>
        public const string BaselineFolder = "Diagnostics/Baselines";

        private const string Prefix = "[Harness] ";

        /// <summary>
        /// The absolute baseline directory, which sits beside Assets/ rather than inside it: these are
        /// tracked build artifacts, not content the player ever loads.
        /// </summary>
        /// <returns>The absolute path.</returns>
        public static string BaselineDirectory()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Diagnostics", "Baselines");
        }

        /// <summary>
        /// The file name a fixture's baseline is written under: lower-cased, spaces hyphenated and
        /// parentheses dropped, so "F5 Align Parity (odd)" becomes "f5-align-parity-odd.json".
        /// </summary>
        /// <param name="fixtureName">The registered fixture name.</param>
        /// <returns>The file name.</returns>
        public static string FileNameFor(string fixtureName)
        {
            StringBuilder slug = new(fixtureName.Length + 5);
            for (int index = 0; index < fixtureName.Length; index++)
            {
                char current = fixtureName[index];
                if (current == '(' || current == ')')
                    continue;

                slug.Append(current == ' ' ? '-' : char.ToLowerInvariant(current));
            }

            return slug.ToString().Replace("--", "-") + ".json";
        }

        /// <summary>
        /// Rebuilds every fixture's baseline from this build and writes it to disk.
        /// </summary>
        [MenuItem("Iterate/Determinism Harness/Refresh Baselines")]
        public static void RefreshBaselines()
        {
            ContentCatalog catalog = LoadCatalogOrReport();
            if (catalog == null)
                return;

            string directory = BaselineDirectory();
            Directory.CreateDirectory(directory);
            Debug.Log(Prefix + "writing baselines to " + directory);

            IReadOnlyList<string> names = DiagnosticFixtureRegistry.Names();
            int written = 0;
            for (int index = 0; index < names.Count; index++)
            {
                string name = names[index];
                try
                {
                    ExecutionRequest request = DiagnosticFixtureRegistry.Build(name, catalog);
                    ExecutionRecord record = new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
                    string fileName = FileNameFor(name);
                    File.WriteAllText(
                        Path.Combine(directory, fileName),
                        ExecutionRecordDocument.Write(record),
                        new UTF8Encoding(false)
                    );

                    Debug.Log(Prefix + "wrote " + fileName);
                    written++;
                }
                catch (ArgumentException exception)
                {
                    Debug.LogError(Prefix + "fixture defect in '" + name + "': " + exception.Message);
                }
            }

            Debug.Log(Prefix + "Refresh Baselines: " + Count(written, names.Count) + " written.");
        }

        /// <summary>
        /// Compares every committed baseline against what this build produces now.
        /// </summary>
        [MenuItem("Iterate/Determinism Harness/Compare Baselines")]
        public static void CompareBaselines()
        {
            ContentCatalog catalog = LoadCatalogOrReport();
            if (catalog == null)
                return;

            string directory = BaselineDirectory();
            IReadOnlyList<string> names = DiagnosticFixtureRegistry.Names();
            int matched = 0;
            for (int index = 0; index < names.Count; index++)
            {
                if (CompareOne(catalog, directory, names[index]))
                    matched++;
            }

            Debug.Log(Prefix + "Compare Baselines: " + Count(matched, names.Count) + " matched.");
        }

        /// <summary>
        /// Compares one fixture's baseline, reporting whichever of the four outcomes applies.
        /// </summary>
        /// <param name="catalog">The loaded catalog.</param>
        /// <param name="directory">The baseline directory.</param>
        /// <param name="name">The fixture name.</param>
        /// <returns>True when the baseline matched.</returns>
        private static bool CompareOne(ContentCatalog catalog, string directory, string name)
        {
            string path = Path.Combine(directory, FileNameFor(name));
            if (!File.Exists(path))
            {
                Debug.LogWarning(Prefix + "no baseline for " + name + " at " + path + ".");
                return false;
            }

            ExecutionRecordReadResult stored = ExecutionRecordDocument.Read(File.ReadAllText(path));
            if (stored.Outcome != ExecutionRecordReadOutcome.Loaded)
            {
                Debug.LogWarning(Prefix + name + ": baseline " + stored.Outcome + " — " + stored.Defect);
                return false;
            }

            ExecutionRequest request;
            try
            {
                request = DiagnosticFixtureRegistry.Build(name, catalog);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError(Prefix + "fixture defect in '" + name + "': " + exception.Message);
                return false;
            }

            ReplayComparisonResult result = new ReplayComparer().Compare(stored.Record, request);
            switch (result.Status)
            {
                case ReplayComparisonStatus.Match:
                    Debug.Log(Prefix + name + ": Match");
                    return true;

                case ReplayComparisonStatus.ExactReproductionUnavailable:
                    Debug.LogWarning(
                        Prefix + name + ": exact reproduction unavailable (" + result.UnavailableComponent + ")"
                    );

                    return false;

                default:
                    Debug.LogWarning(
                        Prefix + name + " diverged at " + Locus(result.Divergence)
                        + ": " + result.Divergence.Description
                    );

                    return false;
            }
        }

        /// <summary>
        /// Generates a string representation of the specified divergence, including its area and element index, if applicable.
        /// </summary>
        /// <param name="divergence">The divergence to represent, containing the area and an optional element index.</param>
        /// <returns>A string representing the divergence area, and if the element index is specified, it will include the index in the format: Area[Index].</returns>
        private static string Locus(ReplayDivergence divergence)
        {
            if (divergence.ElementIndex < 0)
                return divergence.Area.ToString();

            return divergence.Area + "[" + divergence.ElementIndex.ToString(CultureInfo.InvariantCulture) + "]";
        }

        /// <summary>
        /// Loads the shipped catalog, reporting a load failure rather than throwing out of a menu item.
        /// </summary>
        /// <returns>The catalog, or null when it could not be loaded.</returns>
        private static ContentCatalog LoadCatalogOrReport()
        {
            if (!Directory.Exists(EditorCatalog.ShippedRoot()))
            {
                Debug.LogError(Prefix + "the catalog folder was not found at " + EditorCatalog.ShippedRoot() + ".");
                return null;
            }

            try
            {
                return EditorCatalog.LoadShipped();
            }
            catch (CatalogLoadException exception)
            {
                Debug.LogError(Prefix + "the catalog failed to load: " + exception.Errors.Count + " error(s).");
                return null;
            }
        }

        /// <summary>
        /// Renders an "n of m" count invariantly.
        /// </summary>
        /// <param name="reached">How many succeeded.</param>
        /// <param name="total">How many were attempted.</param>
        /// <returns>The rendered count.</returns>
        private static string Count(int reached, int total)
        {
            return reached.ToString(CultureInfo.InvariantCulture)
                + " of "
                + total.ToString(CultureInfo.InvariantCulture);
        }
    }
}