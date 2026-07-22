using System;
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
    /// Tests the per-execution <see cref="EffectEngine"/> driven directly with interpreted hand-built
    /// Dependencies: identity-sorted registration, the three boundary matchers with their qualifier
    /// semantics and first-failing-qualifier near-misses, the CAB-pinned OUTPUT CACHE → OUTPUT
    /// PIPELINE precedence over instance order, and commitment-driven eligibility.
    /// </summary>
    public sealed class EffectEngineTests
    {
        [Test]
        public void Constructor_NullEffects_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EffectEngine(null, new FrequencyLedger()));
        }

        [Test]
        public void Constructor_NullLedger_Throws()
        {
            Assert.Throws<ArgumentException>(() => new EffectEngine(new List<ActiveEffect>(), null));
        }

        [Test]
        public void Registration_OutOfIdentityOrder_EnumeratesSorted()
        {
            List<ActiveEffect> effects = Effects(
                ParallelChannel(5),
                ParallelChannel(2),
                ParallelChannel(9));

            EffectEngine engine = new EffectEngine(effects, new FrequencyLedger());

            Assert.AreEqual(3, engine.RegisteredEffects.Count);
            Assert.AreEqual(new InstanceID(2), engine.RegisteredEffects[0].Origin);
            Assert.AreEqual(new InstanceID(5), engine.RegisteredEffects[1].Origin);
            Assert.AreEqual(new InstanceID(9), engine.RegisteredEffects[2].Origin);
        }

        [Test]
        public void Match_EmptyRegistry_ReturnsSharedEmptyBatch()
        {
            EffectEngine engine = new EffectEngine(new List<ActiveEffect>(), new FrequencyLedger());

            EffectMatchBatch pending = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            EffectMatchBatch resolved = engine.MatchResolvedOperation(ResolvedValueAddSignal(OwnershipClassification.PlayerOwned));
            EffectMatchBatch quantity = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Signal, 1, OwnershipClassification.PlayerOwned));

            Assert.AreSame(EffectMatchBatch.Empty, pending);
            Assert.AreSame(EffectMatchBatch.Empty, resolved);
            Assert.AreSame(EffectMatchBatch.Empty, quantity);
        }

        [Test]
        public void MatchPendingOperation_PlayerFixedAddValue_Qualifies()
        {
            EffectEngine engine = Engine(StandardLibrary(1));

            EffectMatchBatch batch = engine.MatchPendingOperation(PlayerFixedAddValuePending());

            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.AreEqual(new InstanceID(1), batch.Qualified[0].Origin);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchPendingOperation_CoreOwned_NearMissesPlayerInstruction()
        {
            EffectEngine engine = Engine(StandardLibrary(1));

            EffectMatchBatch batch = engine.MatchPendingOperation(Pending(
                CoreRegister.Value, CoreLineOperator.Add, OperandSource.Constant, null, OwnershipClassification.CoreOwned));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchPendingOperation_AssignOperator_NearMissesFixedAddition()
        {
            EffectEngine engine = Engine(StandardLibrary(1));

            EffectMatchBatch batch = engine.MatchPendingOperation(Pending(
                CoreRegister.Value, CoreLineOperator.Assign, OperandSource.Constant, null, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:FIXED_ADDITION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchPendingOperation_RegisterOperand_NearMissesFixedAddition()
        {
            EffectEngine engine = Engine(StandardLibrary(1));

            EffectMatchBatch batch = engine.MatchPendingOperation(Pending(
                CoreRegister.Value, CoreLineOperator.Add, OperandSource.Register, CoreRegister.Signal, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:FIXED_ADDITION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchPendingOperation_SignalTarget_NearMissesRegisterValue()
        {
            EffectEngine engine = Engine(StandardLibrary(1));

            EffectMatchBatch batch = engine.MatchPendingOperation(Pending(
                CoreRegister.Signal, CoreLineOperator.Add, OperandSource.Constant, null, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("REGISTER:VALUE", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchPendingOperation_Consumed_SilentlyAbsent()
        {
            EffectEngine engine = Engine(StandardLibrary(1));
            engine.Commit(engine.RegisteredEffects[0]);

            EffectMatchBatch batch = engine.MatchPendingOperation(PlayerFixedAddValuePending());

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchQuantityChange_PositiveSignalDeltas_PrimaryAndReactionQualify()
        {
            EffectEngine engine = Engine(ParallelChannel(3));

            EffectMatchBatch primary = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Signal, 2, OwnershipClassification.CoreOwned));
            EffectMatchBatch reaction = engine.MatchQuantityChange(ReactionQuantity(CoreRegister.Signal, 1, 9));

            Assert.AreEqual(1, primary.Qualified.Count);
            Assert.AreEqual(1, reaction.Qualified.Count);
        }

        [Test]
        public void MatchQuantityChange_ZeroDelta_NearMissesPositive()
        {
            EffectEngine engine = Engine(ParallelChannel(3));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Signal, 0, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("ACTUAL_DELTA_SIGN:POSITIVE", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchQuantityChange_CoreOwnedScoreGain_NearMissesPlayerInstruction()
        {
            EffectEngine engine = Engine(OutputCache(4));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Score, 6, OwnershipClassification.CoreOwned));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchQuantityChange_ReactionCausedScoreGain_NearMissesPlayerInstruction()
        {
            EffectEngine engine = Engine(OutputCache(4));

            EffectMatchBatch batch = engine.MatchQuantityChange(ReactionQuantity(CoreRegister.Score, 2, 9));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchResolvedOperation_ValueAddSignal_QualifiesRegardlessOfDelta()
        {
            EffectEngine engine = Engine(SignalRouter(6));

            EffectMatchBatch batch = engine.MatchResolvedOperation(ResolvedValueAddSignal(OwnershipClassification.PlayerOwned));

            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.AreEqual(new InstanceID(6), batch.Qualified[0].Origin);
        }

        [Test]
        public void MatchResolvedOperation_FixedAddition_NearMissesValueAddSignal()
        {
            EffectEngine engine = Engine(SignalRouter(6));

            EffectMatchBatch batch = engine.MatchResolvedOperation(Resolved(
                CoreRegister.Value, CoreLineOperator.Add, OperandSource.Constant, null, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:VALUE_ADD_SIGNAL", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchQuantityChange_CacheAndPipeline_CacheFirstDespiteInstanceOrder()
        {
            EffectEngine engine = Engine(OutputPipeline(1), OutputCache(2));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Score, 6, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(2, batch.Qualified.Count);
            Assert.AreEqual("WB-DEP-005", batch.Qualified[0].DefinitionID);
            Assert.AreEqual("WB-DEP-011", batch.Qualified[1].DefinitionID);
        }

        [Test]
        public void MatchQuantityChange_TwoParallelChannels_OrderByInstanceIdentity()
        {
            EffectEngine engine = Engine(ParallelChannel(4), ParallelChannel(2));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Signal, 1, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(2, batch.Qualified.Count);
            Assert.AreEqual(new InstanceID(2), batch.Qualified[0].Origin);
            Assert.AreEqual(new InstanceID(4), batch.Qualified[1].Origin);
        }

        [Test]
        public void Commit_FirstQualifyingStops_EveryQualifyingKeepsMatching()
        {
            EffectEngine engine = Engine(OutputCache(1), ParallelChannel(2));
            QuantityOccurrence scoreGain = PrimaryQuantity(CoreRegister.Score, 6, OwnershipClassification.PlayerOwned);
            QuantityOccurrence signalGain = PrimaryQuantity(CoreRegister.Signal, 1, OwnershipClassification.PlayerOwned);

            EffectMatchBatch cacheFirst = engine.MatchQuantityChange(scoreGain);
            engine.Commit(cacheFirst.Qualified[0]);
            EffectMatchBatch channelFirst = engine.MatchQuantityChange(signalGain);
            engine.Commit(channelFirst.Qualified[0]);

            EffectMatchBatch cacheSecond = engine.MatchQuantityChange(scoreGain);
            EffectMatchBatch channelSecond = engine.MatchQuantityChange(signalGain);

            Assert.AreEqual(0, cacheSecond.Qualified.Count);
            Assert.AreEqual(1, cacheSecond.NearMisses.Count);
            Assert.AreEqual("WB-DEP-004", cacheSecond.NearMisses[0].Effect.DefinitionID);
            Assert.AreEqual(1, channelSecond.Qualified.Count);
        }

        /// <summary>
        /// Interprets the given Dependency instances and assembles an engine over a fresh ledger.
        /// </summary>
        /// <param name="dependencies">The installed Dependency instances.</param>
        /// <returns>The assembled engine.</returns>
        private static EffectEngine Engine(params DependencyInstance[] dependencies)
        {
            return new EffectEngine(Effects(dependencies), new FrequencyLedger());
        }

        /// <summary>
        /// Interprets the given Dependency instances into one flattened effect list.
        /// </summary>
        /// <param name="dependencies">The installed Dependency instances.</param>
        /// <returns>The interpreted effects.</returns>
        private static List<ActiveEffect> Effects(params DependencyInstance[] dependencies)
        {
            List<ActiveEffect> effects = new List<ActiveEffect>();
            for (int i = 0; i < dependencies.Length; i++)
            {
                IReadOnlyList<ActiveEffect> interpreted = EffectInterpreter.Interpret(dependencies[i]);
                for (int j = 0; j < interpreted.Count; j++)
                {
                    effects.Add(interpreted[j]);
                }
            }

            return effects;
        }

        /// <summary>
        /// The standard player fixed <c>Value += 2</c> pending occurrence.
        /// </summary>
        /// <returns>The pending occurrence.</returns>
        private static OperationOccurrence PlayerFixedAddValuePending()
        {
            return Pending(CoreRegister.Value, CoreLineOperator.Add, OperandSource.Constant, null, OwnershipClassification.PlayerOwned);
        }

        /// <summary>
        /// Builds a pending-operation occurrence at depth zero.
        /// </summary>
        /// <param name="register">The target register.</param>
        /// <param name="op">The operator.</param>
        /// <param name="operandSource">The operand source.</param>
        /// <param name="operandRegister">The operand register when the source is a register.</param>
        /// <param name="ownership">The unit's ownership.</param>
        /// <returns>The occurrence.</returns>
        private static OperationOccurrence Pending(
            CoreRegister register,
            CoreLineOperator op,
            OperandSource operandSource,
            CoreRegister? operandRegister,
            OwnershipClassification ownership)
        {
            return new OperationOccurrence(new RuntimeUnitID(1), new TraceEventID(10), 0, register, op, operandSource, operandRegister, ownership);
        }

        /// <summary>
        /// Builds a resolved-operation occurrence at depth zero.
        /// </summary>
        /// <param name="register">The target register.</param>
        /// <param name="op">The operator.</param>
        /// <param name="operandSource">The operand source.</param>
        /// <param name="operandRegister">The operand register when the source is a register.</param>
        /// <param name="ownership">The unit's ownership.</param>
        /// <returns>The occurrence.</returns>
        private static OperationOccurrence Resolved(
            CoreRegister register,
            CoreLineOperator op,
            OperandSource operandSource,
            CoreRegister? operandRegister,
            OwnershipClassification ownership)
        {
            return new OperationOccurrence(new RuntimeUnitID(1), new TraceEventID(12), 0, register, op, operandSource, operandRegister, ownership);
        }

        /// <summary>
        /// The resolved <c>Value += Signal</c> occurrence.
        /// </summary>
        /// <param name="ownership">The unit's ownership.</param>
        /// <returns>The occurrence.</returns>
        private static OperationOccurrence ResolvedValueAddSignal(OwnershipClassification ownership)
        {
            return Resolved(CoreRegister.Value, CoreLineOperator.Add, OperandSource.Register, CoreRegister.Signal, ownership);
        }

        /// <summary>
        /// Builds a primary-operation quantity occurrence at depth zero.
        /// </summary>
        /// <param name="register">The changed register.</param>
        /// <param name="delta">The actual delta.</param>
        /// <param name="ownership">The source ownership.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence PrimaryQuantity(
            CoreRegister register,
            int delta,
            OwnershipClassification ownership)
        {
            return new QuantityOccurrence(new RuntimeUnitID(1), new TraceEventID(14), 0, register, delta, ownership, null, true);
        }

        /// <summary>
        /// Builds a reaction-caused quantity occurrence at depth two.
        /// </summary>
        /// <param name="register">The changed register.</param>
        /// <param name="delta">The actual delta.</param>
        /// <param name="origin">The causing effect instance's identity value.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence ReactionQuantity(
            CoreRegister register,
            int delta,
            int origin)
        {
            return new QuantityOccurrence(new RuntimeUnitID(1), new TraceEventID(16), 2, register, delta, null, new InstanceID(origin), false);
        }

        /// <summary>
        /// A STANDARD-LIBRARY-shaped instance (WB-DEP-001): first player fixed Value addition gains +1
        /// at the modification band.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance StandardLibrary(int instance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier>
                {
                    new TriggerQualifier("OPERATION_CLASS", "FIXED_ADDITION"),
                    new TriggerQualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"),
                    new TriggerQualifier("REGISTER", "VALUE")
                },
                timing);

            return Instance(instance, "WB-DEP-001", Effect(trigger, CoreRegister.Value, 1, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A PARALLEL-CHANNEL-shaped instance (WB-DEP-004): every positive Signal delta adds 1 to Value.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance ParallelChannel(int instance)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "SIGNAL"));

            return Instance(instance, "WB-DEP-004", Effect(trigger, CoreRegister.Value, 1, "EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// An OUTPUT-CACHE-shaped instance (WB-DEP-005): first positive player-Instruction Score delta
        /// adds 2 to Score.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance OutputCache(int instance)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "SCORE"),
                Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"));

            return Instance(instance, "WB-DEP-005", Effect(trigger, CoreRegister.Score, 2, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A SIGNAL-ROUTER-shaped instance (WB-DEP-006): first resolved <c>Value += Signal</c> adds 1
        /// to Signal, delta-insensitive.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance SignalRouter(int instance)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Operation,
                "PRIMARY_OPERATION_RESOLVED",
                Qualifier("OPERATION_CLASS", "VALUE_ADD_SIGNAL"));

            return Instance(instance, "WB-DEP-006", Effect(trigger, CoreRegister.Signal, 1, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// An OUTPUT-PIPELINE-shaped instance (WB-DEP-011): first positive player-Instruction Score
        /// delta adds 2 to Value.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance OutputPipeline(int instance)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "SCORE"),
                Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"));

            return Instance(instance, "WB-DEP-011", Effect(trigger, CoreRegister.Value, 2, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// Builds a reaction-band trigger.
        /// </summary>
        /// <param name="family">The event family.</param>
        /// <param name="subtype">The event-subtype token.</param>
        /// <param name="qualifiers">The trigger qualifiers.</param>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor ReactionTrigger(
            EventFamily family,
            string subtype,
            params TriggerQualifier[] qualifiers)
        {
            return new TriggerDescriptor(family, subtype, qualifiers, new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION"));
        }

        /// <summary>
        /// Builds one trigger qualifier.
        /// </summary>
        /// <param name="kind">The qualifier kind token.</param>
        /// <param name="value">The qualifier value token.</param>
        /// <returns>The qualifier.</returns>
        private static TriggerQualifier Qualifier(string kind, string value)
        {
            return new TriggerQualifier(kind, value);
        }

        /// <summary>
        /// Builds an EXECUTION-domain constant-operand quantity-change effect.
        /// </summary>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="register">The register the operation writes.</param>
        /// <param name="constant">The constant operand.</param>
        /// <param name="allowance">The frequency-allowance token.</param>
        /// <param name="scope">The frequency-scope token.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition Effect(
            TriggerDescriptor trigger,
            CoreRegister register,
            int constant,
            string allowance,
            string scope)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(register, QuantityOperator.Add, OperandSpec.FromConstant(constant)),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency(allowance, scope));
        }

        /// <summary>
        /// Wraps one effect in a Dependency definition and instance.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <param name="effect">The declared effect.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance Instance(int instance, string definitionID, EffectDefinition effect)
        {
            DependencyDefinition definition = new DependencyDefinition(
                new DependencyID(definitionID),
                "Test rules.",
                "TEST DEPENDENCY",
                ContentCategory.Dependency,
                Rarity.Starter,
                new List<string>(),
                0,
                new List<EffectDefinition> { effect });

            return new DependencyInstance(new InstanceID(instance), definition);
        }
    }
}
