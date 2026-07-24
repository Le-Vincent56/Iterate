using System;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that <see cref="ExecutionSafetyTallies"/> begins at zero, counts opened units, effect
    /// reactions, and per-pending-operation transformations with a retained high-water, projects them
    /// to <see cref="SafetyCounts"/>, and enforces every ceiling as preflight-permits-reaching /
    /// prohibits-exceeding.
    /// </summary>
    public sealed class ExecutionSafetyTalliesTests
    {
        [Test]
        public void Fresh_ZeroUnits_AllZeroCounts()
        {
            ExecutionSafetyTallies tallies = new();

            Assert.AreEqual(0, tallies.SourceExecutionUnits);
            Assert.AreEqual(new SafetyCounts(0, 0, 0, 0, 0), tallies.ToCounts());
        }

        [Test]
        public void RecordUnitOpened_ThreeTimes_CountsThree()
        {
            ExecutionSafetyTallies tallies = new();

            tallies.RecordUnitOpened();
            tallies.RecordUnitOpened();
            tallies.RecordUnitOpened();

            Assert.AreEqual(3, tallies.SourceExecutionUnits);
            Assert.AreEqual(new SafetyCounts(0, 0, 3, 0, 0), tallies.ToCounts());
        }

        [Test]
        public void PreflightUnitOpening_TrueAtZeroAndAfterSevenOpens()
        {
            ExecutionSafetyTallies tallies = new();

            Assert.IsTrue(tallies.PreflightUnitOpening());

            for (int i = 0; i < 7; i++)
                tallies.RecordUnitOpened();

            Assert.IsTrue(tallies.PreflightUnitOpening());
        }

        [Test]
        public void RecordUnitOpened_ReachingCeiling_IsLegalThenPreflightFalse()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution - 1; i++)
                tallies.RecordUnitOpened();

            Assert.IsTrue(tallies.PreflightUnitOpening());
            Assert.DoesNotThrow(() => tallies.RecordUnitOpened());
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, tallies.SourceExecutionUnits);
            Assert.IsFalse(tallies.PreflightUnitOpening());
        }

        [Test]
        public void RecordUnitOpened_BeyondCeiling_Throws()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution; i++)
                tallies.RecordUnitOpened();

            Assert.Throws<InvalidOperationException>(() => tallies.RecordUnitOpened());
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, tallies.SourceExecutionUnits);
        }

        [Test]
        public void Fresh_ReactionAndTransformationCounts_AreZero()
        {
            ExecutionSafetyTallies tallies = new();

            Assert.AreEqual(0, tallies.EffectReactions);
            Assert.AreEqual(0, tallies.TransformationsOnPendingOperation);
            Assert.AreEqual(0, tallies.TransformationHighWater);
            Assert.AreEqual(new SafetyCounts(0, 0, 0, 0, 0), tallies.ToCounts());
        }

        [Test]
        public void RecordReaction_ThreeTimes_LandsInCounts()
        {
            ExecutionSafetyTallies tallies = new();

            tallies.RecordReaction();
            tallies.RecordReaction();
            tallies.RecordReaction();

            Assert.AreEqual(3, tallies.EffectReactions);
            Assert.AreEqual(new SafetyCounts(0, 0, 0, 3, 0), tallies.ToCounts());
        }

        [Test]
        public void RecordTransformation_TwoOnOneOperation_HighWaterTwo()
        {
            ExecutionSafetyTallies tallies = new();

            tallies.BeginPendingOperation();
            tallies.RecordTransformation();
            tallies.RecordTransformation();

            Assert.AreEqual(2, tallies.TransformationsOnPendingOperation);
            Assert.AreEqual(2, tallies.TransformationHighWater);
        }

        [Test]
        public void BeginPendingOperation_ResetsPerOperation_HighWaterIsMaxNotSum()
        {
            ExecutionSafetyTallies tallies = new();

            tallies.BeginPendingOperation();
            tallies.RecordTransformation();
            tallies.RecordTransformation();
            tallies.BeginPendingOperation();

            Assert.AreEqual(0, tallies.TransformationsOnPendingOperation);
            Assert.AreEqual(2, tallies.TransformationHighWater);

            tallies.RecordTransformation();

            Assert.AreEqual(1, tallies.TransformationsOnPendingOperation);
            Assert.AreEqual(2, tallies.TransformationHighWater);
            Assert.AreEqual(new SafetyCounts(0, 0, 0, 0, 2), tallies.ToCounts());
        }

        [Test]
        public void RecordReaction_ReachingCeiling_IsLegalThenPreflightFalse()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.EffectReactionsPerExecution - 1; i++)
                tallies.RecordReaction();

            Assert.IsTrue(tallies.PreflightReaction());
            Assert.DoesNotThrow(() => tallies.RecordReaction());
            Assert.AreEqual(SafetyCeilings.EffectReactionsPerExecution, tallies.EffectReactions);
            Assert.IsFalse(tallies.PreflightReaction());
            Assert.Throws<InvalidOperationException>(() => tallies.RecordReaction());
            Assert.AreEqual(SafetyCeilings.EffectReactionsPerExecution, tallies.EffectReactions);
        }

        [Test]
        public void RecordTransformation_ReachingCeiling_IsLegalThenPreflightFalse()
        {
            ExecutionSafetyTallies tallies = new();
            tallies.BeginPendingOperation();

            for (int i = 0; i < SafetyCeilings.TransformationsPerPendingOperation - 1; i++)
                tallies.RecordTransformation();

            Assert.IsTrue(tallies.PreflightTransformation());
            Assert.DoesNotThrow(() => tallies.RecordTransformation());
            Assert.AreEqual(SafetyCeilings.TransformationsPerPendingOperation, tallies.TransformationsOnPendingOperation);
            Assert.IsFalse(tallies.PreflightTransformation());
            Assert.Throws<InvalidOperationException>(() => tallies.RecordTransformation());
            Assert.AreEqual(SafetyCeilings.TransformationsPerPendingOperation, tallies.TransformationsOnPendingOperation);
        }

        [Test]
        public void PreflightDescendant_DepthAtCeiling_TrueAndFalseBeyond()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            Assert.IsTrue(tallies.PreflightDescendant(SafetyCeilings.AddedExecutionLineageDepth, root));
            Assert.IsFalse(tallies.PreflightDescendant(SafetyCeilings.AddedExecutionLineageDepth + 1, root));
        }

        [Test]
        public void PreflightDescendant_PerRootCeiling_FalseExactlyAtTheLimit()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation - 1; i++)
                tallies.RecordDescendant(1, root);

            Assert.IsTrue(tallies.PreflightDescendant(1, root));
            tallies.RecordDescendant(1, root);
            Assert.IsFalse(tallies.PreflightDescendant(1, root));
        }

        [Test]
        public void RecordDescendant_BeyondDepthBound_Throws()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            Assert.Throws<InvalidOperationException>(
                () => tallies.RecordDescendant(SafetyCeilings.AddedExecutionLineageDepth + 1, root));
            Assert.AreEqual(0, tallies.AddedDescendants);
        }

        [Test]
        public void RecordDescendant_BeyondPerRootBound_Throws()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation; i++)
                tallies.RecordDescendant(1, root);

            Assert.Throws<InvalidOperationException>(() => tallies.RecordDescendant(1, root));
            Assert.AreEqual(SafetyCeilings.AddedExecutionsPerActivation, tallies.AddedDescendants);
        }

        [Test]
        public void RecordDescendant_IndependentRoots_CountSeparately()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID rootA = new RuntimeUnitID(1);
            RuntimeUnitID rootB = new RuntimeUnitID(2);

            for (int i = 0; i < SafetyCeilings.AddedExecutionsPerActivation; i++)
                tallies.RecordDescendant(1, rootA);

            Assert.IsFalse(tallies.PreflightDescendant(1, rootA));
            Assert.IsTrue(tallies.PreflightDescendant(1, rootB));
            Assert.DoesNotThrow(() => tallies.RecordDescendant(1, rootB));
            Assert.AreEqual(SafetyCeilings.AddedExecutionsPerActivation + 1, tallies.AddedDescendants);
        }

        [Test]
        public void RecordDescendant_RaisesDepthHighWaterToMax()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            tallies.RecordDescendant(2, root);
            tallies.RecordDescendant(1, root);

            Assert.AreEqual(2, tallies.LineageDepthHighWater);
            Assert.AreEqual(2, tallies.AddedDescendants);
        }

        [Test]
        public void ToCounts_CarriesBothAddedExecutionHighWaters()
        {
            ExecutionSafetyTallies tallies = new();
            RuntimeUnitID root = new RuntimeUnitID(1);

            tallies.RecordUnitOpened();
            tallies.RecordDescendant(1, root);
            tallies.RecordDescendant(2, root);

            Assert.AreEqual(new SafetyCounts(2, 2, 1, 0, 0), tallies.ToCounts());
        }
    }
}
