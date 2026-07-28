using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the Process-counter state: the single counter a Process rule owns, its explicit
    /// initialization, and the bound-clamped commitment that reports everything a counter change must
    /// record — prior value, requested delta, the delta actually applied, and the final value.
    /// The type is deliberately one counter, not a registry. Heat is the only Process counter the
    /// current content declares, and a general framework with named counters and reset-scope
    /// registration would be structure invented ahead of any content needing it. A second counter is
    /// a later change, made when a second counter exists.
    /// Clamping is where the interesting behaviour lives: a request that would cross a declared bound
    /// is not refused, it is reduced, and the difference between what was asked and what was applied
    /// is the evidence a bound was reached. A request that lands exactly on a bound applies in full
    /// and is therefore not a clamp.
    /// </summary>
    public sealed class ProcessCounterStateTests
    {
        [Test]
        public void NewState_InitializesToZero()
        {
            ProcessCounterState state = new ProcessCounterState();

            Assert.AreEqual(0, state.Value);
        }

        [Test]
        public void Gain_RaisesTheValueByTheRequestedDelta()
        {
            ProcessCounterState state = new ProcessCounterState();

            ProcessCounterCommit commit = state.Apply(Gain());

            Assert.AreEqual(1, state.Value);
            Assert.AreEqual(0, commit.PriorValue);
            Assert.AreEqual(1, commit.RequestedDelta);
            Assert.AreEqual(1, commit.FinalDelta);
            Assert.AreEqual(1, commit.FinalValue);
            Assert.IsFalse(commit.BoundApplied);
        }

        [Test]
        public void RepeatedGains_AccumulateToTheCeiling()
        {
            ProcessCounterState state = new ProcessCounterState();

            state.Apply(Gain());
            state.Apply(Gain());
            ProcessCounterCommit third = state.Apply(Gain());

            Assert.AreEqual(3, state.Value);
            Assert.AreEqual(3, third.FinalValue);
            Assert.IsFalse(third.BoundApplied);
        }

        [Test]
        public void GainAtTheCeiling_ClampsAndReportsTheBound()
        {
            ProcessCounterState state = new ProcessCounterState();
            state.Apply(Gain());
            state.Apply(Gain());
            state.Apply(Gain());

            ProcessCounterCommit clamped = state.Apply(Gain());

            Assert.AreEqual(3, state.Value);
            Assert.AreEqual(3, clamped.PriorValue);
            Assert.AreEqual(1, clamped.RequestedDelta);
            Assert.AreEqual(0, clamped.FinalDelta);
            Assert.AreEqual(3, clamped.FinalValue);
            Assert.IsTrue(clamped.BoundApplied);
        }

        [Test]
        public void Cooling_LowersTheValue()
        {
            ProcessCounterState state = new ProcessCounterState();
            state.Apply(Gain());
            state.Apply(Gain());

            ProcessCounterCommit commit = state.Apply(Cool());

            Assert.AreEqual(1, state.Value);
            Assert.AreEqual(2, commit.PriorValue);
            Assert.AreEqual(-1, commit.RequestedDelta);
            Assert.AreEqual(-1, commit.FinalDelta);
            Assert.IsFalse(commit.BoundApplied);
        }

        [Test]
        public void CoolingAtTheFloor_ClampsAndReportsTheBound()
        {
            ProcessCounterState state = new ProcessCounterState();

            ProcessCounterCommit clamped = state.Apply(Cool());

            Assert.AreEqual(0, state.Value);
            Assert.AreEqual(0, clamped.PriorValue);
            Assert.AreEqual(-1, clamped.RequestedDelta);
            Assert.AreEqual(0, clamped.FinalDelta);
            Assert.AreEqual(0, clamped.FinalValue);
            Assert.IsTrue(clamped.BoundApplied);
        }

        [Test]
        public void CoolingFromTheCeiling_PermitsALaterGain()
        {
            ProcessCounterState state = new ProcessCounterState();
            state.Apply(Gain());
            state.Apply(Gain());
            state.Apply(Gain());

            state.Apply(Cool());
            ProcessCounterCommit regained = state.Apply(Gain());

            Assert.AreEqual(3, state.Value);
            Assert.AreEqual(2, regained.PriorValue);
            Assert.IsFalse(regained.BoundApplied);
        }

        [Test]
        public void ARequestWithNoCeiling_IsNotClampedAbove()
        {
            ProcessCounterState state = new ProcessCounterState();

            state.Apply(new CounterRequestOperation("HEAT", 5, 0, 0, true, false));

            Assert.AreEqual(5, state.Value);
        }

        [Test]
        public void ARequestWithNoFloor_IsNotClampedBelow()
        {
            ProcessCounterState state = new ProcessCounterState();

            state.Apply(new CounterRequestOperation("HEAT", -2, 0, 0, false, false));

            Assert.AreEqual(-2, state.Value);
        }

        [Test]
        public void Reset_ReturnsTheCounterToZero()
        {
            ProcessCounterState state = new ProcessCounterState();
            state.Apply(Gain());
            state.Apply(Gain());

            state.Reset();

            Assert.AreEqual(0, state.Value);
        }

        /// <summary>
        /// The THERMAL THROTTLE gain request: plus one Heat clamped to zero through three.
        /// </summary>
        /// <returns>The counter-request operation.</returns>
        private static CounterRequestOperation Gain()
        {
            return new CounterRequestOperation("HEAT", 1, 0, 3, true, true);
        }

        /// <summary>
        /// The THERMAL THROTTLE cooling request: minus one Heat floored at zero.
        /// </summary>
        /// <returns>The counter-request operation.</returns>
        private static CounterRequestOperation Cool()
        {
            return new CounterRequestOperation("HEAT", -1, 0, 0, true, false);
        }
    }
}
