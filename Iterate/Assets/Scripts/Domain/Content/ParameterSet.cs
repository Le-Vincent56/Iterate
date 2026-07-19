using System;
using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// The locked WB-PAR parameter register as typed accessors. Construction requires exactly the 30
    /// canonical register IDs; a missing ID, an unknown ID, or a fractional value for an integer
    /// parameter is a construction failure naming the offending ID (honoring missing-is-not-zero).
    /// </summary>
    public sealed class ParameterSet
    {
        private static readonly HashSet<string> _requiredIDs = new(StringComparer.Ordinal)
        {
            "WB-PAR-001", "WB-PAR-002", "WB-PAR-003", "WB-PAR-004", "WB-PAR-005",
            "WB-PAR-006", "WB-PAR-007", "WB-PAR-008", "WB-PAR-009", "WB-PAR-010",
            "WB-PAR-011", "WB-PAR-012", "WB-PAR-013", "WB-PAR-014", "WB-PAR-015",
            "WB-PAR-016", "WB-PAR-017", "WB-PAR-018", "WB-PAR-019", "WB-PAR-020",
            "WB-PAR-021", "WB-PAR-022", "WB-PAR-023", "WB-PAR-024", "WB-PAR-026",
            "WB-PAR-028", "WB-PAR-029", "WB-PAR-030", "WB-PAR-035", "WB-PAR-036"
        };

        private static readonly HashSet<string> _ratioIDs = new(StringComparer.Ordinal)
        {
            "WB-PAR-022", "WB-PAR-023", "WB-PAR-024", "WB-PAR-035"
        };

        private readonly IReadOnlyDictionary<string, double> _values;

        /// <summary>
        /// The number of Systems per Session.
        /// </summary>
        public int SystemsPerSession => Integer("WB-PAR-001");

        /// <summary>
        /// The number of Processes per Session.
        /// </summary>
        public int ProcessesPerSession => Integer("WB-PAR-002");

        /// <summary>
        /// The minimum target Session duration in minutes.
        /// </summary>
        public int TargetSessionDurationMinimumMinutes => Integer("WB-PAR-003");

        /// <summary>
        /// The maximum target Session duration in minutes.
        /// </summary>
        public int TargetSessionDurationMaximumMinutes => Integer("WB-PAR-004");

        /// <summary>
        /// The starting RAM.
        /// </summary>
        public int StartingRAM => Integer("WB-PAR-005");

        /// <summary>
        /// The RAM after System 1.
        /// </summary>
        public int RAMAfterSystem1 => Integer("WB-PAR-006");

        /// <summary>
        /// The RAM after System 2.
        /// </summary>
        public int RAMAfterSystem2 => Integer("WB-PAR-007");

        /// <summary>
        /// The final RAM.
        /// </summary>
        public int FinalRAM => Integer("WB-PAR-008");

        /// <summary>
        /// The standard source-position capacity.
        /// </summary>
        public int StandardSourceCapacity => Integer("WB-PAR-009");

        /// <summary>
        /// The standard Instruction Buffer capacity.
        /// </summary>
        public int StandardBufferCapacity => Integer("WB-PAR-010");

        /// <summary>
        /// The standard number of Process executions.
        /// </summary>
        public int StandardProcessExecutions => Integer("WB-PAR-011");

        /// <summary>
        /// The standard starting Bytes.
        /// </summary>
        public int StandardStartingBytes => Integer("WB-PAR-012");

        /// <summary>
        /// The standard initial Instruction Buffer items.
        /// </summary>
        public int StandardInitialBufferItems => Integer("WB-PAR-013");

        /// <summary>
        /// The standard number of later Instruction Buffer arrivals.
        /// </summary>
        public int StandardLaterArrivals => Integer("WB-PAR-014");

        /// <summary>
        /// The standard Active Branch capacity.
        /// </summary>
        public int StandardActiveBranchCapacity => Integer("WB-PAR-015");

        /// <summary>
        /// The standard natural exposure count.
        /// </summary>
        public int StandardNaturalExposure => Integer("WB-PAR-016");

        /// <summary>
        /// The initial compilation cost in Bytes.
        /// </summary>
        public int InitialCompilationCost => Integer("WB-PAR-017");

        /// <summary>
        /// The unchanged-source compilation cost in Bytes.
        /// </summary>
        public int UnchangedCompilationCost => Integer("WB-PAR-018");

        /// <summary>
        /// The first edited-compilation cost in Bytes.
        /// </summary>
        public int FirstEditedCompilationCost => Integer("WB-PAR-019");

        /// <summary>
        /// The second edited-compilation cost in Bytes.
        /// </summary>
        public int SecondEditedCompilationCost => Integer("WB-PAR-020");

        /// <summary>
        /// The third edited-compilation cost in Bytes.
        /// </summary>
        public int ThirdEditedCompilationCost => Integer("WB-PAR-021");

        /// <summary>
        /// The Pass threshold ratio.
        /// </summary>
        public double PassRatio => _values["WB-PAR-022"];

        /// <summary>
        /// The Optimize threshold ratio.
        /// </summary>
        public double OptimizeRatio => _values["WB-PAR-023"];

        /// <summary>
        /// The Benchmark threshold ratio.
        /// </summary>
        public double BenchmarkRatio => _values["WB-PAR-024"];

        /// <summary>
        /// The standard Benchmark Token bonus.
        /// </summary>
        public int StandardBenchmarkTokenBonus => Integer("WB-PAR-026");

        /// <summary>
        /// The first reroll cost in Tokens.
        /// </summary>
        public int FirstRerollCost => Integer("WB-PAR-028");

        /// <summary>
        /// The second reroll cost in Tokens.
        /// </summary>
        public int SecondRerollCost => Integer("WB-PAR-029");

        /// <summary>
        /// The third reroll cost in Tokens.
        /// </summary>
        public int ThirdRerollCost => Integer("WB-PAR-030");

        /// <summary>
        /// The Dependency-destruction refund ratio.
        /// </summary>
        public double DependencyDestructionRefundRatio => _values["WB-PAR-035"];

        /// <summary>
        /// The number of Patch sockets per Repository instance.
        /// </summary>
        public int PatchSocketsPerRepositoryInstance => Integer("WB-PAR-036");

        public ParameterSet(IReadOnlyDictionary<string, double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            foreach (string requiredID in _requiredIDs)
            {
                if (!values.ContainsKey(requiredID))
                    throw new ArgumentException($"The parameter register is missing required ID {requiredID}.", nameof(values));
            }

            Dictionary<string, double> copy = new(values.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, double> entry in values)
            {
                if (!_requiredIDs.Contains(entry.Key))
                {
                    throw new ArgumentException($"The parameter register carries unknown ID {entry.Key}.", nameof(values));
                }

                if (!_ratioIDs.Contains(entry.Key) && Math.Floor(entry.Value) != entry.Value)
                {
                    throw new ArgumentException($"The parameter register carries a fractional value for integer ID {entry.Key}.", nameof(values));
                }

                copy[entry.Key] = entry.Value;
            }

            _values = copy;
        }

        /// <summary>
        /// Reads an integer-valued parameter, narrowing the stored value validated integral at
        /// construction.
        /// </summary>
        /// <param name="id">The register ID to read.</param>
        /// <returns>The parameter value as an integer.</returns>
        private int Integer(string id) => (int)_values[id];
    }
}