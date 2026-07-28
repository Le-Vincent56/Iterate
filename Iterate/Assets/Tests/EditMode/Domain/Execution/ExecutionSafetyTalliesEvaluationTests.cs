using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="ExecutionSafetyTallies"/>' multi-limit evaluation and its at-ceiling query.
    /// The evaluators answer "would the next occurrence exceed a ceiling", returning null on the
    /// clear path so no allocation happens per preflight, and a registry-ordered
    /// <see cref="BreachedLimit"/> list carrying every simultaneously violated limit on a breach —
    /// several at once for one created descendant. <c>LimitsAtCeiling</c> is a pure query over the
    /// same registry: reaching a ceiling is permitted and is what it reports; exceeding it is what
    /// the evaluators refuse. The per-pending-operation transformation flag clears with its count on
    /// a new pending operation — first-contact memory belongs to the scheduler, not the tallies.
    /// </summary>
    public sealed class ExecutionSafetyTalliesEvaluationTests
    {
        [Test]
        public void EvaluateUnitOpening_BelowCeiling_ReturnsNull()
        {
            ExecutionSafetyTallies tallies = new();

            tallies.RecordUnitOpened();

            Assert.IsNull(tallies.EvaluateUnitOpening());
        }

        [Test]
        public void EvaluateUnitOpening_AtCeiling_ReturnsTheUnitLimitOnly()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution; i++)
                tallies.RecordUnitOpened();

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateUnitOpening();

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                breached[0].LimitName);
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, breached[0].Ceiling);
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, breached[0].CountAtBreach);
        }

        [Test]
        public void EvaluateUnitOpening_OneBelowCeiling_StillClear()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution - 1; i++)
                tallies.RecordUnitOpened();

            Assert.IsNull(tallies.EvaluateUnitOpening());
        }

        [Test]
        public void EvaluateReaction_AtCeiling_ReturnsTheReactionLimit()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.EffectReactionsPerExecution; i++)
                tallies.RecordReaction();

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateReaction();

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.EffectReactions),
                breached[0].LimitName);
            Assert.AreEqual(SafetyCeilings.EffectReactionsPerExecution, breached[0].CountAtBreach);
        }

        [Test]
        public void EvaluateReaction_OneBelowCeiling_ReturnsNull()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.EffectReactionsPerExecution - 1; i++)
                tallies.RecordReaction();

            Assert.IsNull(tallies.EvaluateReaction());
        }

        [Test]
        public void EvaluateTransformation_AtCeiling_ReturnsTheTransformationLimit()
        {
            ExecutionSafetyTallies tallies = new();
            tallies.BeginPendingOperation();

            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation; i++)
                tallies.RecordTransformation();

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateTransformation();

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.TransformationsOnPendingOperation),
                breached[0].LimitName);
            Assert.AreEqual(SafetyCeilings.TransformationsPerPendingOperation, breached[0].Ceiling);
        }

        [Test]
        public void EvaluateTransformation_NewPendingOperation_ClearsTheBreach()
        {
            ExecutionSafetyTallies tallies = new();
            tallies.BeginPendingOperation();

            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation; i++)
                tallies.RecordTransformation();

            Assert.IsNotNull(tallies.EvaluateTransformation());

            tallies.BeginPendingOperation();

            Assert.IsNull(tallies.EvaluateTransformation());
        }

        [Test]
        public void EvaluateDescendant_WithinEveryCeiling_ReturnsNull()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            Assert.IsNull(tallies.EvaluateDescendant(SafetyCeilings.AddedExecutionLineageDepth, root));
        }

        [Test]
        public void EvaluateDescendant_BeyondDepthCeiling_ReturnsTheDepthLimitCarryingTheProposedDepth()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);
            int proposedDepth = SafetyCeilings.AddedExecutionLineageDepth + 1;

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateDescendant(proposedDepth, root);

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionLineageDepth),
                breached[0].LimitName);
            Assert.AreEqual(SafetyCeilings.AddedExecutionLineageDepth, breached[0].Ceiling);
            Assert.AreEqual(proposedDepth, breached[0].CountAtBreach);
        }

        [Test]
        public void EvaluateDescendant_RootAtItsCeiling_ReturnsThePerActivationLimit()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation; i++)
                tallies.RecordDescendant(1, root);

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateDescendant(1, root);

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionsPerActivation),
                breached[0].LimitName);
            Assert.AreEqual(SafetyCeilings.AddedExecutionsPerActivation, breached[0].CountAtBreach);
        }

        [Test]
        public void EvaluateDescendant_SeparateRoot_StaysClear()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID exhausted = new RuntimeUnitID(1);
            RuntimeUnitID fresh = new RuntimeUnitID(2);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation; i++)
                tallies.RecordDescendant(1, exhausted);

            Assert.IsNotNull(tallies.EvaluateDescendant(1, exhausted));
            Assert.IsNull(tallies.EvaluateDescendant(1, fresh));
        }

        [Test]
        public void EvaluateDescendant_ThreeCeilingsAtOnce_RecordsEveryLimitInRegistryOrder()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation; i++)
                tallies.RecordDescendant(1, root);

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution; i++)
                tallies.RecordUnitOpened();

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateDescendant(
                SafetyCeilings.AddedExecutionLineageDepth + 1,
                root);

            Assert.IsNotNull(breached);
            Assert.AreEqual(3, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionLineageDepth),
                breached[0].LimitName);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionsPerActivation),
                breached[1].LimitName);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                breached[2].LimitName);
        }

        [Test]
        public void EvaluateDescendant_UnitCeilingAloneReached_StillBreachesForTheDescendantUnit()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution; i++)
                tallies.RecordUnitOpened();

            IReadOnlyList<BreachedLimit> breached = tallies.EvaluateDescendant(1, root);

            Assert.IsNotNull(breached);
            Assert.AreEqual(1, breached.Count);
            Assert.AreEqual(
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                breached[0].LimitName);
        }

        [Test]
        public void LimitsAtCeiling_Fresh_IsNone()
        {
            ExecutionSafetyTallies tallies = new();

            Assert.AreEqual(SafetyLimitFlags.None, tallies.LimitsAtCeiling);
        }

        [Test]
        public void LimitsAtCeiling_UnitsExactlyAtCeiling_FlagsUnitsOnly()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution - 1; i++)
                tallies.RecordUnitOpened();

            Assert.AreEqual(SafetyLimitFlags.None, tallies.LimitsAtCeiling);

            tallies.RecordUnitOpened();

            Assert.AreEqual(SafetyLimitFlags.SourceExecutionUnits, tallies.LimitsAtCeiling);
        }

        [Test]
        public void LimitsAtCeiling_ReactionsExactlyAtCeiling_FlagsReactions()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.EffectReactionsPerExecution; i++)
                tallies.RecordReaction();

            Assert.AreEqual(SafetyLimitFlags.EffectReactions, tallies.LimitsAtCeiling);
        }

        [Test]
        public void LimitsAtCeiling_DepthHighWaterAtCeiling_FlagsLineageDepth()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            tallies.RecordDescendant(SafetyCeilings.AddedExecutionLineageDepth - 1, root);

            Assert.AreEqual(SafetyLimitFlags.None, tallies.LimitsAtCeiling);

            tallies.RecordDescendant(SafetyCeilings.AddedExecutionLineageDepth, root);

            Assert.AreEqual(SafetyLimitFlags.AddedExecutionLineageDepth, tallies.LimitsAtCeiling);
        }

        [Test]
        public void LimitsAtCeiling_OneRootAtItsCeiling_FlagsPerActivation()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation; i++)
                tallies.RecordDescendant(1, root);

            Assert.AreEqual(SafetyLimitFlags.AddedExecutionsPerActivation, tallies.LimitsAtCeiling);
        }

        [Test]
        public void LimitsAtCeiling_TransformationsAtCeiling_ClearsOnANewPendingOperation()
        {
            ExecutionSafetyTallies tallies = new();
            tallies.BeginPendingOperation();

            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation; i++)
                tallies.RecordTransformation();

            Assert.AreEqual(SafetyLimitFlags.TransformationsOnPendingOperation, tallies.LimitsAtCeiling);

            tallies.BeginPendingOperation();

            Assert.AreEqual(SafetyLimitFlags.None, tallies.LimitsAtCeiling);
        }

        [Test]
        public void LimitsAtCeiling_TwoLimitsAtOnce_CombinesTheFlags()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution; i++)
                tallies.RecordUnitOpened();

            tallies.RecordDescendant(SafetyCeilings.AddedExecutionLineageDepth, root);

            Assert.AreEqual(
                SafetyLimitFlags.AddedExecutionLineageDepth | SafetyLimitFlags.SourceExecutionUnits,
                tallies.LimitsAtCeiling);
        }

        [Test]
        public void SafetyLimitFlags_AreRegistryOrderedSingleBits()
        {
            Assert.AreEqual(0, (int)SafetyLimitFlags.None);
            Assert.AreEqual(1, (int)SafetyLimitFlags.AddedExecutionLineageDepth);
            Assert.AreEqual(2, (int)SafetyLimitFlags.AddedExecutionsPerActivation);
            Assert.AreEqual(4, (int)SafetyLimitFlags.SourceExecutionUnits);
            Assert.AreEqual(8, (int)SafetyLimitFlags.EffectReactions);
            Assert.AreEqual(16, (int)SafetyLimitFlags.TransformationsOnPendingOperation);
        }
    }
}
