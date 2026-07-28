using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the engine's two counter behaviours: the pre-operation Heat pre-check, and the
    /// window gate on cooling.
    /// The pre-check reads the Heat snapshot the occurrence carries and returns the same effect down
    /// one of two arms — at the ceiling it is an intervention candidate, below it a deferred gain
    /// candidate. Both arms are candidates only; neither commits anything here. Deciding both arms
    /// from a single match is what keeps one effect from being matched twice against two different
    /// Heat values: matching again after the operation resolves would read a Heat that the first
    /// match's own outcome could have changed.
    /// The snapshot is carried on the occurrence rather than read from engine state because the
    /// engine holds no counter — the scheduler owns Heat, and the occurrence is the complete boundary
    /// snapshot the engine matches against.
    /// Cooling is gated by a window the scheduler closes when the designated final Core output's unit
    /// activates. After the close, a cooling candidate is tier-1 silent: not qualified, and not a
    /// near-miss either, because it was never structurally a candidate — the same posture the
    /// observation window already takes for target locks.
    /// </summary>
    public sealed class EffectEngineCounterMatchTests
    {
        [Test]
        public void SnapshotBelowCeiling_YieldsADeferredGainNotAnIntervention()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            EffectMatchBatch batch = engine.MatchPendingOperation(Multiplication(0));

            Assert.AreEqual(1, batch.DeferredCounterGains.Count);
            Assert.AreEqual(0, batch.CounterInterventions.Count);
        }

        [Test]
        public void SnapshotJustBelowCeiling_StillYieldsADeferredGain()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            EffectMatchBatch batch = engine.MatchPendingOperation(Multiplication(2));

            Assert.AreEqual(1, batch.DeferredCounterGains.Count);
            Assert.AreEqual(0, batch.CounterInterventions.Count);
        }

        [Test]
        public void SnapshotAtCeiling_YieldsAnInterventionNotAGain()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            EffectMatchBatch batch = engine.MatchPendingOperation(Multiplication(3));

            Assert.AreEqual(1, batch.CounterInterventions.Count);
            Assert.AreEqual(0, batch.DeferredCounterGains.Count);
        }

        [Test]
        public void BothArms_ReturnTheSameEffectIdentity()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            ActiveEffect below = engine.MatchPendingOperation(Multiplication(1)).DeferredCounterGains[0];
            ActiveEffect atCeiling = engine.MatchPendingOperation(Multiplication(3)).CounterInterventions[0];

            Assert.AreEqual(below.FrequencyKey, atCeiling.FrequencyKey);
            Assert.AreEqual(ActiveEffectKind.CounterIntervention, below.Kind);
            Assert.AreEqual(ActiveEffectKind.CounterIntervention, atCeiling.Kind);
        }

        [Test]
        public void NonMultiplication_NeverReachesEitherArm()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            EffectMatchBatch batch = engine.MatchPendingOperation(Addition(3));

            Assert.AreEqual(0, batch.CounterInterventions.Count);
            Assert.AreEqual(0, batch.DeferredCounterGains.Count);
        }

        [Test]
        public void AbsentSnapshot_LeavesTheCounterEffectStructurallyIneligible()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            EffectMatchBatch batch = engine.MatchPendingOperation(Multiplication(null));

            Assert.AreEqual(0, batch.CounterInterventions.Count);
            Assert.AreEqual(0, batch.DeferredCounterGains.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void CounterInterventionEffect_NeverAppearsAsAnOrdinaryModification()
        {
            EffectEngine engine = EngineWith(HeatGain(90));

            EffectMatchBatch batch = engine.MatchPendingOperation(Multiplication(0));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(0, batch.Reapplications.Count);
        }

        [Test]
        public void CoolingCandidate_QualifiesWhileTheWindowIsOpen()
        {
            EffectEngine engine = EngineWith(Cooling(91));

            EffectMatchBatch batch = engine.MatchQuantityChange(ScoreGain());

            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.AreEqual(ActiveEffectKind.Reaction, batch.Qualified[0].Kind);
            Assert.IsNotNull(batch.Qualified[0].CounterRequest);
        }

        [Test]
        public void CoolingCandidate_IsSilentAfterTheWindowCloses()
        {
            EffectEngine engine = EngineWith(Cooling(91));
            engine.CloseCoolingWindow();

            EffectMatchBatch batch = engine.MatchQuantityChange(ScoreGain());

            Assert.AreEqual(0, batch.Qualified.Count);
        }

        [Test]
        public void ClosedCoolingWindow_ProducesNoNearMissNoise()
        {
            EffectEngine engine = EngineWith(Cooling(91));
            engine.CloseCoolingWindow();

            EffectMatchBatch batch = engine.MatchQuantityChange(ScoreGain());

            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void ClosedCoolingWindow_LeavesOrdinaryReactionsUntouched()
        {
            EffectEngine engine = EngineWith(Cooling(91), PlainScoreReaction(92));
            engine.CloseCoolingWindow();

            EffectMatchBatch batch = engine.MatchQuantityChange(ScoreGain());

            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.IsNull(batch.Qualified[0].CounterRequest);
        }

        [Test]
        public void CoolingCandidate_DoesNotObserveANegativeScoreChange()
        {
            EffectEngine engine = EngineWith(Cooling(91));

            EffectMatchBatch batch = engine.MatchQuantityChange(ScoreChange(-1));

            Assert.AreEqual(0, batch.Qualified.Count);
        }

        [Test]
        public void CoolingCandidate_DoesNotObserveAValueChange()
        {
            EffectEngine engine = EngineWith(Cooling(91));

            EffectMatchBatch batch = engine.MatchQuantityChange(RegisterChange(CoreRegister.Value, 1));

            Assert.AreEqual(0, batch.Qualified.Count);
        }

        /// <summary>
        /// An engine registered over the given effects with a fresh ledger.
        /// </summary>
        /// <param name="effects">The interpreted effects to register.</param>
        /// <returns>The engine.</returns>
        private static EffectEngine EngineWith(params ActiveEffect[] effects)
        {
            return new EffectEngine(new List<ActiveEffect>(effects), new FrequencyLedger());
        }

        /// <summary>
        /// The interpreted THERMAL THROTTLE gain entry.
        /// </summary>
        /// <param name="instance">The origin instance identity value.</param>
        /// <returns>The interpreted effect.</returns>
        private static ActiveEffect HeatGain(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier> { new TriggerQualifier("OPERATION_CLASS", "MULTIPLY") },
                new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"));

            return ActiveEffect.ForCounterIntervention(
                new InstanceID(instance),
                "WB-PRC-001",
                0,
                trigger,
                new CounterRequestOperation("HEAT", 1, 0, 3, true, true),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// The interpreted THERMAL THROTTLE cooling entry.
        /// </summary>
        /// <param name="instance">The origin instance identity value.</param>
        /// <returns>The interpreted effect.</returns>
        private static ActiveEffect Cooling(int instance)
        {
            return ActiveEffect.ForCounterReaction(
                new InstanceID(instance),
                "WB-PRC-001",
                1,
                ScoreTrigger(),
                new CounterRequestOperation("HEAT", -1, 0, 0, true, false),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// An ordinary register-writing reaction on the same trigger, for the isolation case.
        /// </summary>
        /// <param name="instance">The origin instance identity value.</param>
        /// <returns>The interpreted effect.</returns>
        private static ActiveEffect PlainScoreReaction(int instance)
        {
            return ActiveEffect.ForReaction(
                new InstanceID(instance),
                "WB-DIR-905",
                0,
                ScoreTrigger(),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// The positive-Score reaction trigger both cooling and the plain reaction observe.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor ScoreTrigger()
        {
            return new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("REGISTER", "SCORE"),
                    new TriggerQualifier("ACTUAL_DELTA_SIGN", "POSITIVE")
                },
                new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION"));
        }

        /// <summary>
        /// A pending player multiplication carrying the given Heat snapshot.
        /// </summary>
        /// <param name="heat">The Heat snapshot, or null when no Process rule is configured.</param>
        /// <returns>The occurrence.</returns>
        private static OperationOccurrence Multiplication(int? heat)
        {
            return PendingOperation(CoreLineOperator.Multiply, heat);
        }

        /// <summary>
        /// A pending player addition carrying the given Heat snapshot.
        /// </summary>
        /// <param name="heat">The Heat snapshot.</param>
        /// <returns>The occurrence.</returns>
        private static OperationOccurrence Addition(int? heat)
        {
            return PendingOperation(CoreLineOperator.Add, heat);
        }

        /// <summary>
        /// A pending player-owned operation with the given operator and Heat snapshot.
        /// </summary>
        /// <param name="lineOperator">The operator the operation applies.</param>
        /// <param name="heat">The Heat snapshot, or null when absent.</param>
        /// <returns>The occurrence.</returns>
        private static OperationOccurrence PendingOperation(CoreLineOperator lineOperator, int? heat)
        {
            return new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                new InstanceID(50),
                CoreRegister.Value,
                lineOperator,
                OperandSource.Constant,
                null,
                OwnershipClassification.PlayerOwned,
                heat);
        }

        /// <summary>
        /// A finalized positive Score change from a primary operation.
        /// </summary>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence ScoreGain()
        {
            return ScoreChange(1);
        }

        /// <summary>
        /// A finalized Score change with the given delta.
        /// </summary>
        /// <param name="delta">The actual delta.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence ScoreChange(int delta)
        {
            return RegisterChange(CoreRegister.Score, delta);
        }

        /// <summary>
        /// A finalized primary-operation quantity change on the given register, from a player-owned
        /// Instruction. The host slot is supplied because a player-owned primary-operation change is
        /// lockable and the occurrence requires its slot — and because a player Instruction's own
        /// Score increase is the canonical thing cooling observes.
        /// </summary>
        /// <param name="register">The register written.</param>
        /// <param name="delta">The actual delta.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence RegisterChange(CoreRegister register, int delta)
        {
            SourceSlot host = SourceSlot.ForInstruction(
                new SourcePosition(1),
                SchedulerFixtures.ValueAddInstance(50, 1));

            return new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(2),
                1,
                register,
                delta,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                host,
                null);
        }
    }
}
