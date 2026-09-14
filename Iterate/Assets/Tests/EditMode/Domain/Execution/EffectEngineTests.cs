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
    /// Dependencies: identity-sorted registration, the four boundary matchers with their qualifier
    /// semantics and first-failing-qualifier near-misses, the rescue-only silent skip boundary, the
    /// CAB-pinned OUTPUT CACHE → OUTPUT PIPELINE precedence over instance order, and
    /// commitment-driven eligibility.
    /// </summary>
    public sealed class EffectEngineTests
    {
        /// <summary>
        /// The one wired named scheduling boundary token.
        /// </summary>
        private const string PlayerTraversalBoundary = "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL";

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
        public void MatchSkip_RescuablePlayerSkip_QualifiesRescueOnly()
        {
            EffectEngine engine = Engine(SafeMode(3), StandardLibrary(1), ParallelChannel(2));

            EffectMatchBatch batch = engine.MatchSkip(Skip(true));

            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.AreEqual(new InstanceID(3), batch.Qualified[0].Origin);
            Assert.AreEqual("WB-DEP-007", batch.Qualified[0].DefinitionID);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchSkip_NonRescuable_YieldsEmptyBatch()
        {
            EffectEngine engine = Engine(SafeMode(3));

            EffectMatchBatch batch = engine.MatchSkip(Skip(false));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchSkip_AfterCommit_ExhaustedIsSilent()
        {
            EffectEngine engine = Engine(SafeMode(3));
            EffectMatchBatch first = engine.MatchSkip(Skip(true));
            engine.Commit(first.Qualified[0]);

            EffectMatchBatch second = engine.MatchSkip(Skip(true));

            Assert.AreEqual(0, second.Qualified.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void MatchSkip_NullOccurrence_Throws()
        {
            EffectEngine engine = Engine(SafeMode(3));

            Assert.Throws<ArgumentException>(() => engine.MatchSkip(null));
        }

        [Test]
        public void MatchSkip_RescueEffect_NeverMatchesOtherBoundaries()
        {
            EffectEngine engine = Engine(SafeMode(3));

            EffectMatchBatch pending = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            EffectMatchBatch resolved = engine.MatchResolvedOperation(ResolvedValueAddSignal(OwnershipClassification.PlayerOwned));
            EffectMatchBatch quantity = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Signal, 1, OwnershipClassification.PlayerOwned));

            Assert.AreSame(EffectMatchBatch.Empty, pending);
            Assert.AreSame(EffectMatchBatch.Empty, resolved);
            Assert.AreSame(EffectMatchBatch.Empty, quantity);
        }

        [Test]
        public void CommitModification_SameHostOffer_Reapplies()
        {
            EffectEngine engine = Engine(StandardLibrary(1));
            EffectMatchBatch first = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            engine.CommitModification(first.Qualified[0], new InstanceID(50));

            EffectMatchBatch second = engine.MatchPendingOperation(PlayerFixedAddValuePending());

            Assert.AreEqual(0, second.Qualified.Count);
            Assert.AreEqual(1, second.Reapplications.Count);
            Assert.AreEqual("WB-DEP-001", second.Reapplications[0].DefinitionID);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void CommitModification_DifferentHostOffer_YieldsNothing()
        {
            EffectEngine engine = Engine(StandardLibrary(1));
            EffectMatchBatch first = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            engine.CommitModification(first.Qualified[0], new InstanceID(50));

            EffectMatchBatch second = engine.MatchPendingOperation(PendingWithHost(99));

            Assert.AreEqual(0, second.Qualified.Count);
            Assert.AreEqual(0, second.Reapplications.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void CommitModification_NonSelectedHostEffect_StaysPlainIneligible()
        {
            EffectEngine engine = Engine(ModificationShaped(1, "WB-DEP-902"));
            EffectMatchBatch first = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            engine.CommitModification(first.Qualified[0], new InstanceID(50));

            EffectMatchBatch second = engine.MatchPendingOperation(PlayerFixedAddValuePending());

            Assert.AreEqual(0, second.Qualified.Count);
            Assert.AreEqual(0, second.Reapplications.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void ReapplicationOffer_QualifierFailure_IsSilentOnBothLists()
        {
            EffectEngine engine = Engine(StandardLibrary(1));
            EffectMatchBatch first = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            engine.CommitModification(first.Qualified[0], new InstanceID(50));

            EffectMatchBatch second = engine.MatchPendingOperation(Pending(
                CoreRegister.Value, CoreLineOperator.Assign, OperandSource.Constant, null, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(0, second.Qualified.Count);
            Assert.AreEqual(0, second.Reapplications.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void Clear_ForgetsSelectedHosts()
        {
            EffectEngine engine = Engine(StandardLibrary(1));
            EffectMatchBatch first = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            engine.CommitModification(first.Qualified[0], new InstanceID(50));

            engine.Clear();
            EffectMatchBatch afterClear = engine.MatchPendingOperation(PlayerFixedAddValuePending());

            Assert.AreSame(EffectMatchBatch.Empty, afterClear);
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

        [Test]
        public void MatchQuantityChange_QualifyingPlayerValueGain_PutsCreatorInCreators()
        {
            EffectEngine engine = Engine(Overclock(7), ParallelChannel(2));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Value, 2, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(1, batch.Creators.Count);
            Assert.AreEqual("WB-DIR-001", batch.Creators[0].DefinitionID);
            Assert.AreEqual(new InstanceID(7), batch.Creators[0].Origin);
            Assert.AreEqual(0, batch.Qualified.Count);
        }

        [Test]
        public void MatchQuantityChange_ReactionCausedGain_IsStructurallySilentForCreators()
        {
            EffectEngine engine = Engine(Overclock(7), ParallelChannel(2));

            EffectMatchBatch batch = engine.MatchQuantityChange(ReactionQuantity(CoreRegister.Signal, 1, 9));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.AreEqual("WB-DEP-004", batch.Qualified[0].DefinitionID);
        }

        [Test]
        public void MatchQuantityChange_CoreOwnedValueGain_NearMissesPlayerInstruction()
        {
            EffectEngine engine = Engine(Overclock(7));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Value, 2, OwnershipClassification.CoreOwned));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("OPERATION_CLASS:PLAYER_INSTRUCTION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchQuantityChange_ConsumedCreatorInOwnLineage_IsSilent()
        {
            EffectEngine engine = Engine(Overclock(7));
            EffectMatchBatch first = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Value, 2, OwnershipClassification.PlayerOwned));
            engine.Commit(first.Creators[0]);

            EffectMatchBatch second = engine.MatchQuantityChange(PrimaryQuantityInLineage(
                CoreRegister.Value, 2, OwnershipClassification.PlayerOwned, EffectOriginLineage.Empty.Append(new InstanceID(7))));

            Assert.AreEqual(0, second.Creators.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void MatchQuantityChange_EligibleCreatorInLineage_NearMissesOriginLock()
        {
            EffectEngine engine = Engine(EveryQualifyingCreator(7));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityInLineage(
                CoreRegister.Value, 2, OwnershipClassification.PlayerOwned, EffectOriginLineage.Empty.Append(new InstanceID(7))));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("ORIGIN_LOCK:" + new InstanceID(7).ToString(), batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchQuantityChange_OriginLockBlock_LeavesLedgerUntouched()
        {
            EffectEngine engine = Engine(EveryQualifyingCreator(7));
            engine.MatchQuantityChange(PrimaryQuantityInLineage(
                CoreRegister.Value, 2, OwnershipClassification.PlayerOwned, EffectOriginLineage.Empty.Append(new InstanceID(7))));

            EffectMatchBatch clean = engine.MatchQuantityChange(PrimaryQuantity(CoreRegister.Value, 2, OwnershipClassification.PlayerOwned));

            Assert.AreEqual(1, clean.Creators.Count);
            Assert.AreEqual(0, clean.NearMisses.Count);
        }

        [Test]
        public void MatchPostUnit_SuccessInRepeatContext_QualifiesCreator()
        {
            EffectEngine engine = Engine(LoopUnroller(8));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnit(EventDisposition.Resolved, RepeatContext()));

            Assert.AreEqual(1, batch.Creators.Count);
            Assert.AreEqual("WB-DEP-009", batch.Creators[0].DefinitionID);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchPostUnit_RescuedClosure_QualifiesCreator()
        {
            EffectEngine engine = Engine(LoopUnroller(8));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnit(EventDisposition.Rescued, RepeatContext()));

            Assert.AreEqual(1, batch.Creators.Count);
        }

        [Test]
        public void MatchPostUnit_NonRepeatContext_NearMissesStructureContext()
        {
            EffectEngine engine = Engine(LoopUnroller(8));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnit(EventDisposition.Resolved, null));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("STRUCTURE_CONTEXT:INSIDE_REPEAT", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchPostUnit_SkippedClosure_IsStructurallySilent()
        {
            EffectEngine engine = Engine(LoopUnroller(8));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnit(EventDisposition.Skipped, RepeatContext()));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchPostUnit_Consumed_IsSilent()
        {
            EffectEngine engine = Engine(LoopUnroller(8));
            EffectMatchBatch first = engine.MatchPostUnit(PostUnit(EventDisposition.Resolved, RepeatContext()));
            engine.Commit(first.Creators[0]);

            EffectMatchBatch second = engine.MatchPostUnit(PostUnit(EventDisposition.Resolved, RepeatContext()));

            Assert.AreEqual(0, second.Creators.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void MatchPostUnit_NullOccurrence_Throws()
        {
            EffectEngine engine = Engine(LoopUnroller(8));

            Assert.Throws<ArgumentException>(() => engine.MatchPostUnit(null));
        }

        [Test]
        public void MatchConditionSuccess_OccupiedChild_QualifiesCreator()
        {
            EffectEngine engine = Engine(BranchPredictor(9));

            EffectMatchBatch batch = engine.MatchConditionSuccess(ConditionSuccess(new InstanceID(60)));

            Assert.AreEqual(1, batch.Creators.Count);
            Assert.AreEqual("WB-DEP-010", batch.Creators[0].DefinitionID);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchConditionSuccess_NoOccupiedChild_IsStructurallySilent()
        {
            EffectEngine engine = Engine(BranchPredictor(9));

            EffectMatchBatch batch = engine.MatchConditionSuccess(ConditionSuccess(null));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchConditionSuccess_Consumed_IsSilent()
        {
            EffectEngine engine = Engine(BranchPredictor(9));
            EffectMatchBatch first = engine.MatchConditionSuccess(ConditionSuccess(new InstanceID(60)));
            engine.Commit(first.Creators[0]);

            EffectMatchBatch second = engine.MatchConditionSuccess(ConditionSuccess(new InstanceID(61)));

            Assert.AreEqual(0, second.Creators.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void MatchConditionSuccess_NullOccurrence_Throws()
        {
            EffectEngine engine = Engine(BranchPredictor(9));

            Assert.Throws<ArgumentException>(() => engine.MatchConditionSuccess(null));
        }

        [Test]
        public void MatchBoundary_OddValue_QualifiesBoundaryEffect()
        {
            EffectEngine engine = Engine(Align(10));

            EffectMatchBatch batch = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 7));

            Assert.AreEqual(1, batch.Qualified.Count);
            Assert.AreEqual("WB-DIR-002", batch.Qualified[0].DefinitionID);
            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchBoundary_EvenValue_NearMissesParity()
        {
            EffectEngine engine = Engine(Align(10));

            EffectMatchBatch batch = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 6));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("PARITY:ODD", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void MatchBoundary_UnrelatedBoundaryName_YieldsEmptyBatch()
        {
            EffectEngine engine = Engine(Align(10));

            EffectMatchBatch batch = engine.MatchBoundary(Boundary("END_OF_COMPLETE_SOURCE_TRAVERSAL", 7));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void MatchBoundary_Consumed_IsSilent()
        {
            EffectEngine engine = Engine(Align(10));
            EffectMatchBatch first = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 7));
            engine.Commit(first.Qualified[0]);

            EffectMatchBatch second = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 9));

            Assert.AreEqual(0, second.Qualified.Count);
            Assert.AreEqual(0, second.NearMisses.Count);
        }

        [Test]
        public void MatchBoundary_NullOccurrence_Throws()
        {
            EffectEngine engine = Engine(Align(10));

            Assert.Throws<ArgumentException>(() => engine.MatchBoundary(null));
        }

        [Test]
        public void Creators_NeverMatchTheChildBoundaries()
        {
            EffectEngine engine = Engine(Overclock(7), LoopUnroller(8), BranchPredictor(9), Align(10));

            EffectMatchBatch pending = engine.MatchPendingOperation(PlayerFixedAddValuePending());
            EffectMatchBatch resolved = engine.MatchResolvedOperation(ResolvedValueAddSignal(OwnershipClassification.PlayerOwned));
            EffectMatchBatch skip = engine.MatchSkip(Skip(true));

            Assert.AreSame(EffectMatchBatch.Empty, pending);
            Assert.AreSame(EffectMatchBatch.Empty, resolved);
            Assert.AreSame(EffectMatchBatch.Empty, skip);
        }

        [Test]
        public void ChildBoundaryEffects_NeverReachTheNewBoundaries()
        {
            EffectEngine engine = Engine(StandardLibrary(1), ParallelChannel(2), SafeMode(3));

            EffectMatchBatch postUnit = engine.MatchPostUnit(PostUnit(EventDisposition.Resolved, RepeatContext()));
            EffectMatchBatch condition = engine.MatchConditionSuccess(ConditionSuccess(new InstanceID(60)));
            EffectMatchBatch boundary = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 7));

            Assert.AreSame(EffectMatchBatch.Empty, postUnit);
            Assert.AreSame(EffectMatchBatch.Empty, condition);
            Assert.AreSame(EffectMatchBatch.Empty, boundary);
        }

        [Test]
        public void TargetLockUpdate_PrimaryPlayerScoreGain_CarriesLockCandidate()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityHosted(CoreRegister.Score, 4, OwnershipClassification.PlayerOwned, 60));

            Assert.AreEqual(1, batch.TargetLockUpdates.Count);
            Assert.AreEqual(new InstanceID(70), batch.TargetLockUpdates[0].Origin);
            Assert.AreEqual(0, batch.Qualified.Count);
        }

        [Test]
        public void TargetLockUpdate_SourceLessChange_IsStructurallySilent()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));

            EffectMatchBatch batch = engine.MatchQuantityChange(SourceLessQuantity(CoreRegister.Score, 4));

            Assert.AreEqual(0, batch.TargetLockUpdates.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void CommitTargetLock_WritesLockReadableByOrigin()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));
            ActiveEffect update = engine.RegisteredEffects[0];

            engine.CommitTargetLock(update, Lock(60));

            TargetLock stored = engine.TargetLockFor(new InstanceID(70));
            Assert.IsNotNull(stored);
            Assert.AreEqual(new InstanceID(60), stored.HostInstance);
        }

        [Test]
        public void CommitTargetLock_SecondCommit_OverwritesWithMostRecent()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));
            ActiveEffect update = engine.RegisteredEffects[0];

            engine.CommitTargetLock(update, Lock(60));
            engine.CommitTargetLock(update, Lock(62));

            Assert.AreEqual(new InstanceID(62), engine.TargetLockFor(new InstanceID(70)).HostInstance);
        }

        [Test]
        public void TargetLockFor_NeverWritten_ReturnsNull()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));

            Assert.IsNull(engine.TargetLockFor(new InstanceID(70)));
        }

        [Test]
        public void TargetLockFor_ReadTwice_DoesNotConsumeTheLock()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));
            ActiveEffect update = engine.RegisteredEffects[0];
            engine.CommitTargetLock(update, Lock(60));

            Assert.IsNotNull(engine.TargetLockFor(new InstanceID(70)));
            Assert.IsNotNull(engine.TargetLockFor(new InstanceID(70)));
        }

        [Test]
        public void TwoBurstInstances_HoldIndependentLocks()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70), BurstLockEffect(71));
            ActiveEffect first = engine.RegisteredEffects[0];
            ActiveEffect second = engine.RegisteredEffects[1];

            engine.CommitTargetLock(first, Lock(60));
            engine.CommitTargetLock(second, Lock(62));

            Assert.AreEqual(new InstanceID(60), engine.TargetLockFor(new InstanceID(70)).HostInstance);
            Assert.AreEqual(new InstanceID(62), engine.TargetLockFor(new InstanceID(71)).HostInstance);
        }

        [Test]
        public void CloseObservationWindow_ThenPlayerScoreGain_LockUpdateIsStructurallySilent()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70));
            engine.CloseObservationWindow();

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityHosted(CoreRegister.Score, 4, OwnershipClassification.PlayerOwned, 60));

            Assert.AreEqual(0, batch.TargetLockUpdates.Count, "a closed window silences the lock update — no candidate");
            Assert.AreEqual(0, batch.NearMisses.Count, "the closed window is structural ineligibility, not a requirement failure");
        }

        [Test]
        public void CloseObservationWindow_LeavesReactionKindsUnaffected()
        {
            EffectEngine engine = EngineOf(FeedbackPatchEffect(70, 60));
            engine.CloseObservationWindow();

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityHosted(CoreRegister.Score, 3, OwnershipClassification.PlayerOwned, 60));

            Assert.AreEqual(1, batch.Qualified.Count, "the window close silences only TargetLock-kind candidates");
        }

        [Test]
        public void FeedbackPatch_OwnHostScoreGain_Qualifies()
        {
            EffectEngine engine = EngineOf(FeedbackPatchEffect(70, 60));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityHosted(CoreRegister.Score, 3, OwnershipClassification.PlayerOwned, 60));

            Assert.AreEqual(1, batch.Qualified.Count);
        }

        [Test]
        public void FeedbackPatch_ForeignHostScoreGain_IsStructurallySilent()
        {
            EffectEngine engine = EngineOf(FeedbackPatchEffect(70, 60));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityHosted(CoreRegister.Score, 3, OwnershipClassification.PlayerOwned, 61));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void FeedbackPatch_OwnHostNegativeDelta_NearMissesDeltaSign()
        {
            EffectEngine engine = EngineOf(FeedbackPatchEffect(70, 60));

            EffectMatchBatch batch = engine.MatchQuantityChange(PrimaryQuantityHosted(CoreRegister.Score, -1, OwnershipClassification.PlayerOwned, 60));

            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("ACTUAL_DELTA_SIGN:POSITIVE", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void EchoPatch_EvenLineOwnHost_Qualifies()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "POSITIONAL", "EVEN_NUMBERED_LINE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, null, false));

            Assert.AreEqual(1, batch.Creators.Count);
        }

        [Test]
        public void EchoPatch_OddLine_NearMissesEvenLine()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "POSITIONAL", "EVEN_NUMBERED_LINE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 3, false, null, false));

            Assert.AreEqual(1, batch.NearMisses.Count);
            Assert.AreEqual("POSITIONAL:EVEN_NUMBERED_LINE", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void EchoPatch_ForeignHost_IsStructurallySilent()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "POSITIONAL", "EVEN_NUMBERED_LINE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(61, 4, false, null, false));

            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void TerminalPatch_FinalPlayerLine_Qualifies()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "POSITIONAL", "FINAL_OCCUPIED_PLAYER_LINE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 5, true, null, false));

            Assert.AreEqual(1, batch.Creators.Count);
        }

        [Test]
        public void TerminalPatch_NonFinalLine_NearMissesFinalLine()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "POSITIONAL", "FINAL_OCCUPIED_PLAYER_LINE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 5, false, null, false));

            Assert.AreEqual("POSITIONAL:FINAL_OCCUPIED_PLAYER_LINE", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void TruePatch_InsideSucceedingCondition_Qualifies()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "STRUCTURE_CONTEXT", "INSIDE_SUCCEEDING_CONDITION"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, ConditionOutcome.True, false));

            Assert.AreEqual(1, batch.Creators.Count);
        }

        [Test]
        public void TruePatch_RescuedFromFalse_NearMissesSucceedingCondition()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "STRUCTURE_CONTEXT", "INSIDE_SUCCEEDING_CONDITION"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, ConditionOutcome.False, false));

            Assert.AreEqual("STRUCTURE_CONTEXT:INSIDE_SUCCEEDING_CONDITION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void TruePatch_NotInsideCondition_NearMissesSucceedingCondition()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "STRUCTURE_CONTEXT", "INSIDE_SUCCEEDING_CONDITION"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, null, false));

            Assert.AreEqual("STRUCTURE_CONTEXT:INSIDE_SUCCEEDING_CONDITION", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void PipelinePatch_AdjacentAfterSuccessfulScore_Qualifies()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "STRUCTURE_CONTEXT", "ADJACENT_AFTER_SUCCESSFUL_SCORE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, null, true));

            Assert.AreEqual(1, batch.Creators.Count);
        }

        [Test]
        public void PipelinePatch_NotAdjacent_NearMissesAdjacency()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "STRUCTURE_CONTEXT", "ADJACENT_AFTER_SUCCESSFUL_SCORE"));

            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, null, false));

            Assert.AreEqual("STRUCTURE_CONTEXT:ADJACENT_AFTER_SUCCESSFUL_SCORE", batch.NearMisses[0].FailedRequirement);
        }

        [Test]
        public void BoundaryCreator_NoLock_IsSilentWithLedgerUntouched()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70), BurstBoundaryEffect(70));

            EffectMatchBatch batch = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 7));

            Assert.AreEqual(0, batch.Creators.Count);
        }

        [Test]
        public void BoundaryCreator_WithLock_Qualifies()
        {
            EffectEngine engine = EngineOf(BurstLockEffect(70), BurstBoundaryEffect(70));
            engine.CommitTargetLock(engine.RegisteredEffects[0], Lock(60));

            EffectMatchBatch batch = engine.MatchBoundary(Boundary(PlayerTraversalBoundary, 7));

            Assert.AreEqual(1, batch.Creators.Count);
            Assert.AreEqual(new InstanceID(70), batch.Creators[0].Origin);
        }

        [Test]
        public void PendingOperationBatch_OrdersOperationModificationBeforeQuantityChange()
        {
            // The OperationModification effect carries the *higher* instance identity, so identity
            // order alone would place it second; only the CAB-EVT-543 rule puts it first.
            EffectEngine engine = EngineOf(StandardLibrarySelectedHost(70), ConstantPatchEffect(80, 60));

            EffectMatchBatch batch = engine.MatchPendingOperation(PendingHosted(60));

            Assert.AreEqual(2, batch.Qualified.Count);
            Assert.IsNotNull(batch.Qualified[0].OperationModification);
            Assert.AreEqual(new InstanceID(80), batch.Qualified[0].Origin);
            Assert.IsNull(batch.Qualified[1].OperationModification);
            Assert.AreEqual(new InstanceID(70), batch.Qualified[1].Origin);
        }

        [Test]
        public void CommitUnderUnit_SourceExecutionCreator_ConsumesThatUnitKey()
        {
            EffectEngine engine = EngineOf(PostUnitPatchEffect(70, 60, "POSITIONAL", "EVEN_NUMBERED_LINE"));
            ActiveEffect echo = engine.RegisteredEffects[0];

            engine.Commit(echo, new RuntimeUnitID(1));

            // Re-offering the same unit's even-line closure finds the allowance consumed: silent, no near-miss.
            EffectMatchBatch batch = engine.MatchPostUnit(PostUnitFacts(60, 4, false, null, false));
            Assert.AreEqual(0, batch.Creators.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
        }

        [Test]
        public void ConstantPatch_ForeignHostPending_IsStructurallySilent()
        {
            EffectEngine engine = EngineOf(ConstantPatchEffect(70, 60));

            EffectMatchBatch batch = engine.MatchPendingOperation(PendingHosted(61));

            Assert.AreEqual(0, batch.Qualified.Count);
            Assert.AreEqual(0, batch.NearMisses.Count);
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
        /// Assembles an engine directly over hand-built active effects and a fresh ledger, for the
        /// host-socketed Patch and target-lock shapes the interpreter produces from an attached Patch.
        /// </summary>
        /// <param name="effects">The registered effects.</param>
        /// <returns>The assembled engine.</returns>
        private static EffectEngine EngineOf(params ActiveEffect[] effects)
        {
            return new EffectEngine(new List<ActiveEffect>(effects), new FrequencyLedger());
        }

        /// <summary>
        /// A BURST-OUTPUT-shaped target-lock effect: every positive player Score gain updates the
        /// tracked most-recent qualifying unit.
        /// </summary>
        /// <param name="origin">The owning instance's identity value.</param>
        /// <returns>The target-lock effect.</returns>
        private static ActiveEffect BurstLockEffect(int origin)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "SCORE"),
                Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"));

            return ActiveEffect.ForTargetLock(
                new InstanceID(origin),
                "WB-DIR-003",
                0,
                trigger,
                new TargetLockUpdateOperation(new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty)),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// A BURST-OUTPUT-shaped boundary creator: at the player-traversal boundary, one added
        /// execution of the locked target.
        /// </summary>
        /// <param name="origin">The owning instance's identity value.</param>
        /// <returns>The boundary creator effect.</returns>
        private static ActiveEffect BurstBoundaryEffect(int origin)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Reaction,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.NamedBoundary, PlayerTraversalBoundary));

            return ActiveEffect.ForBoundaryCreator(
                new InstanceID(origin),
                "WB-DIR-003",
                1,
                trigger,
                new AddedExecutionRequestOperation(new TargetingRule("LOCKED_TARGET", string.Empty), true),
                PlayerTraversalBoundary,
                new EffectFrequency("ONCE", "EXECUTION"));
        }

        /// <summary>
        /// A FEEDBACK-PATCH-shaped socketed reaction: the host's own positive Score gain adds 1 to
        /// Value.
        /// </summary>
        /// <param name="origin">The Patch instance's identity value.</param>
        /// <param name="host">The socketed host instance's identity value.</param>
        /// <returns>The host-socketed reaction effect.</returns>
        private static ActiveEffect FeedbackPatchEffect(int origin, int host)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "SCORE"),
                Qualifier("OPERATION_CLASS", "HOST_INSTRUCTION"));

            return ActiveEffect.ForReaction(
                new InstanceID(origin),
                "WB-PAT-005",
                0,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE")).WithHostInstance(new InstanceID(host));
        }

        /// <summary>
        /// A CONSTANT-PATCH-shaped socketed operand modification on the host's fixed-addition operation.
        /// </summary>
        /// <param name="origin">The Patch instance's identity value.</param>
        /// <param name="host">The socketed host instance's identity value.</param>
        /// <returns>The host-socketed operation-modification effect.</returns>
        private static ActiveEffect ConstantPatchEffect(int origin, int host)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier> { Qualifier("OPERATION_CLASS", "FIXED_ADDITION") },
                new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"));

            return ActiveEffect.ForOperationModification(
                new InstanceID(origin),
                "WB-PAT-001",
                0,
                trigger,
                new OperationModificationOperation(1),
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE")).WithHostInstance(new InstanceID(host));
        }

        /// <summary>
        /// A post-unit Patch creator socketed to a host, carrying one qualifier and own-host targeting.
        /// </summary>
        /// <param name="origin">The Patch instance's identity value.</param>
        /// <param name="host">The socketed host instance's identity value.</param>
        /// <param name="qualifierKind">The post-unit qualifier kind token.</param>
        /// <param name="qualifierValue">The post-unit qualifier value token.</param>
        /// <returns>The host-socketed creator effect.</returns>
        private static ActiveEffect PostUnitPatchEffect(
            int origin,
            int host,
            string qualifierKind,
            string qualifierValue)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier> { Qualifier(qualifierKind, qualifierValue) },
                new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));

            return ActiveEffect.ForAddedExecution(
                new InstanceID(origin),
                "WB-PAT-002",
                0,
                trigger,
                new AddedExecutionRequestOperation(new TargetingRule("OWN_HOST", string.Empty), false),
                new EffectFrequency("ONCE", "SOURCE_EXECUTION")).WithHostInstance(new InstanceID(host));
        }

        /// <summary>
        /// A STANDARD-LIBRARY-shaped selected-host modification (a quantity-change modification, not an
        /// operand adjustment), for the composition-order pin.
        /// </summary>
        /// <param name="origin">The owning instance's identity value.</param>
        /// <returns>The modification effect.</returns>
        private static ActiveEffect StandardLibrarySelectedHost(int origin)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier>
                {
                    Qualifier("OPERATION_CLASS", "FIXED_ADDITION"),
                    Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"),
                    Qualifier("REGISTER", "VALUE")
                },
                new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"));

            return ActiveEffect.ForModification(
                new InstanceID(origin),
                "WB-DEP-001",
                0,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A player fixed <c>Value += n</c> pending occurrence hosted by a specific Instruction instance.
        /// </summary>
        /// <param name="host">The hosting instance's identity value.</param>
        /// <returns>The pending occurrence.</returns>
        private static OperationOccurrence PendingHosted(int host)
        {
            return new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(10),
                0,
                new InstanceID(host),
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                null,
                OwnershipClassification.PlayerOwned);
        }

        /// <summary>
        /// A target-lock snapshot locking the given host instance's slot.
        /// </summary>
        /// <param name="host">The locked host instance's identity value.</param>
        /// <returns>The target lock.</returns>
        private static TargetLock Lock(int host)
        {
            return new TargetLock(
                InstructionSlot(host),
                new InstanceID(host),
                null,
                new TraceEventID(14),
                new RuntimeUnitID(1),
                null);
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
            return new OperationOccurrence(new RuntimeUnitID(1), new TraceEventID(10), 0, HostFor(ownership), register, op, operandSource, operandRegister, ownership);
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
            return new OperationOccurrence(new RuntimeUnitID(1), new TraceEventID(12), 0, HostFor(ownership), register, op, operandSource, operandRegister, ownership);
        }

        /// <summary>
        /// The standing host instance for an occurrence's ownership: a player-owned occurrence carries
        /// the fixture host, a Core-owned occurrence none.
        /// </summary>
        /// <param name="ownership">The occurrence's ownership.</param>
        /// <returns>The host instance, or null for Core ownership.</returns>
        private static InstanceID? HostFor(OwnershipClassification ownership)
        {
            return ownership == OwnershipClassification.PlayerOwned ? new InstanceID(50) : (InstanceID?)null;
        }

        /// <summary>
        /// The standard player fixed <c>Value += 2</c> pending occurrence hosted by a specific
        /// Instruction instance.
        /// </summary>
        /// <param name="host">The hosting instance's identity value.</param>
        /// <returns>The pending occurrence.</returns>
        private static OperationOccurrence PendingWithHost(int host)
        {
            return new OperationOccurrence(
                new RuntimeUnitID(2),
                new TraceEventID(11),
                0,
                new InstanceID(host),
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                null,
                OwnershipClassification.PlayerOwned);
        }

        /// <summary>
        /// A STANDARD-LIBRARY-shaped modification effect under a non-selected-host definition
        /// identity, for pinning plain first-qualifying ineligibility.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance ModificationShaped(int instance, string definitionID)
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

            return Instance(instance, definitionID, Effect(trigger, CoreRegister.Value, 1, "FIRST_QUALIFYING_EVENT", "EXECUTION"));
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
            SourceSlot hostSlot = ownership == OwnershipClassification.PlayerOwned ? InstructionSlot(50) : null;
            return new QuantityOccurrence(new RuntimeUnitID(1), new TraceEventID(14), 0, register, delta, ownership, null, true, EffectOriginLineage.Empty, hostSlot, null);
        }

        /// <summary>
        /// Builds a primary-operation quantity occurrence hosted by a specific Instruction slot, for
        /// the target-lock and host-gate cases that key on the offering unit's host.
        /// </summary>
        /// <param name="register">The changed register.</param>
        /// <param name="delta">The actual delta.</param>
        /// <param name="ownership">The source ownership.</param>
        /// <param name="host">The hosting instance's identity value.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence PrimaryQuantityHosted(
            CoreRegister register,
            int delta,
            OwnershipClassification ownership,
            int host)
        {
            return new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(14),
                0,
                register,
                delta,
                ownership,
                null,
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(host),
                null);
        }

        /// <summary>
        /// A source-less boundary-caused quantity occurrence, whose null host slot leaves a
        /// target-lock candidate structurally ineligible.
        /// </summary>
        /// <param name="register">The changed register.</param>
        /// <param name="delta">The actual delta.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence SourceLessQuantity(CoreRegister register, int delta)
        {
            return new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(14),
                0,
                register,
                delta,
                null,
                new InstanceID(80),
                false,
                EffectOriginLineage.Empty,
                null,
                null);
        }

        /// <summary>
        /// Builds a top-level Instruction slot carrying an instance with the given identity.
        /// </summary>
        /// <param name="instanceID">The occupying instance's identity value.</param>
        /// <returns>The Instruction slot.</returns>
        private static SourceSlot InstructionSlot(int instanceID)
        {
            InstructionDefinition definition = new InstructionDefinition(
                new InstructionID("WB-INS-001"),
                "rules",
                "TEST INSTRUCTION",
                ContentCategory.Instruction,
                Rarity.Starter,
                new List<string>(),
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                new List<string>());

            return SourceSlot.ForInstruction(
                new SourcePosition(2),
                new InstructionInstance(new InstanceID(instanceID), definition, Array.Empty<PatchAttachment>()));
        }

        /// <summary>
        /// Builds a primary-operation quantity occurrence carrying a branch lineage.
        /// </summary>
        /// <param name="register">The changed register.</param>
        /// <param name="delta">The actual delta.</param>
        /// <param name="ownership">The source ownership.</param>
        /// <param name="lineage">The offering branch's effect-origin lineage.</param>
        /// <returns>The occurrence.</returns>
        private static QuantityOccurrence PrimaryQuantityInLineage(
            CoreRegister register,
            int delta,
            OwnershipClassification ownership,
            EffectOriginLineage lineage)
        {
            SourceSlot hostSlot = ownership == OwnershipClassification.PlayerOwned ? InstructionSlot(50) : null;
            return new QuantityOccurrence(new RuntimeUnitID(2), new TraceEventID(15), 0, register, delta, ownership, null, true, lineage, hostSlot, null);
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
            return new QuantityOccurrence(new RuntimeUnitID(1), new TraceEventID(16), 2, register, delta, null, new InstanceID(origin), false, EffectOriginLineage.Empty, null, null);
        }

        /// <summary>
        /// Builds a post-unit occurrence for a player-owned unit with the given closure disposition
        /// and Structure context.
        /// </summary>
        /// <param name="disposition">The unit's final disposition.</param>
        /// <param name="context">The unit's Structure context, or null when top-level.</param>
        /// <returns>The post-unit occurrence.</returns>
        private static PostUnitOccurrence PostUnit(EventDisposition disposition, StructureContext context)
        {
            return new PostUnitOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(20),
                disposition,
                OwnershipClassification.PlayerOwned,
                new InstanceID(50),
                context,
                EffectOriginLineage.Empty,
                new SourcePosition(2),
                false,
                null,
                false);
        }

        /// <summary>
        /// Builds a player-owned resolved post-unit occurrence hosted by a specific instance and
        /// carrying the four Patch facts, for the host-gate and qualifier cases.
        /// </summary>
        /// <param name="host">The hosting instance's identity value.</param>
        /// <param name="position">The unit's source position.</param>
        /// <param name="isFinalOccupiedPlayerLine">Whether the unit is the final occupied player line.</param>
        /// <param name="conditionResult">The retained Condition result, or null when not inside one.</param>
        /// <param name="adjacentAfterSuccessfulScore">Whether the captured adjacency snapshot qualified.</param>
        /// <returns>The post-unit occurrence.</returns>
        private static PostUnitOccurrence PostUnitFacts(
            int host,
            int position,
            bool isFinalOccupiedPlayerLine,
            ConditionOutcome? conditionResult,
            bool adjacentAfterSuccessfulScore)
        {
            return new PostUnitOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(20),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(host),
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(position),
                isFinalOccupiedPlayerLine,
                conditionResult,
                adjacentAfterSuccessfulScore);
        }

        /// <summary>
        /// Builds a Condition-success occurrence with or without an occupied first child.
        /// </summary>
        /// <param name="firstOccupiedChild">The first occupied contained child, or null when none.</param>
        /// <returns>The Condition-success occurrence.</returns>
        private static ConditionSuccessOccurrence ConditionSuccess(InstanceID? firstOccupiedChild)
        {
            return new ConditionSuccessOccurrence(
                new TraceEventID(22),
                "#5@3#1/eval-1",
                firstOccupiedChild,
                firstOccupiedChild == null ? (SourcePosition?)null : new SourcePosition(4),
                EvaluationContext(),
                EffectOriginLineage.Empty);
        }

        /// <summary>
        /// Builds a boundary occurrence with the given name and Value snapshot.
        /// </summary>
        /// <param name="boundaryName">The reached boundary's token.</param>
        /// <param name="value">The Value register's snapshot.</param>
        /// <returns>The boundary occurrence.</returns>
        private static BoundaryOccurrence Boundary(string boundaryName, int value)
        {
            return new BoundaryOccurrence(boundaryName, value, 0, 0);
        }

        /// <summary>
        /// A Structure context carrying an active Repeat-iteration identity.
        /// </summary>
        /// <returns>The context.</returns>
        private static StructureContext RepeatContext()
        {
            return new StructureContext(
                new List<InstanceID> { new InstanceID(5) },
                "#5@3",
                "#5@3/iter-1",
                null);
        }

        /// <summary>
        /// A Structure context carrying a Condition-evaluation identity.
        /// </summary>
        /// <returns>The context.</returns>
        private static StructureContext EvaluationContext()
        {
            return new StructureContext(
                new List<InstanceID> { new InstanceID(5) },
                "#5@3",
                null,
                "#5@3/eval-1");
        }

        /// <summary>
        /// An OVERCLOCK-shaped instance (WB-DIR-001, post-fix): the first positive player Value gain
        /// requests one added execution of the triggering unit.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance carrying the creator shape.</returns>
        private static DependencyInstance Overclock(int instance)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "VALUE"),
                Qualifier("OPERATION_CLASS", "PLAYER_INSTRUCTION"));

            return Instance(instance, "WB-DIR-001", CreatorEffect(trigger, "TRIGGERING_UNIT", "ONCE", "EXECUTION"));
        }

        /// <summary>
        /// An every-qualifying creator shape: interpreter-legal, never consumed, so the origin lock is
        /// the only thing that can block it — the direct probe for the lock's evidence.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance carrying the creator shape.</returns>
        private static DependencyInstance EveryQualifyingCreator(int instance)
        {
            TriggerDescriptor trigger = ReactionTrigger(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Qualifier("ACTUAL_DELTA_SIGN", "POSITIVE"),
                Qualifier("REGISTER", "VALUE"));

            return Instance(instance, "WB-DIR-901", CreatorEffect(trigger, "TRIGGERING_UNIT", "EVERY_QUALIFYING_EVENT", "DECLARED_SCOPE"));
        }

        /// <summary>
        /// A LOOP-UNROLLER-shaped instance (WB-DEP-009): the first successful Repeat-context unit
        /// closure requests one added execution of the triggering unit.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance LoopUnroller(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier> { Qualifier("STRUCTURE_CONTEXT", "INSIDE_REPEAT") },
                new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));

            return Instance(instance, "WB-DEP-009", CreatorEffect(trigger, "TRIGGERING_UNIT", "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// A BRANCH-PREDICTOR-shaped instance (WB-DEP-010): the first successful Condition evaluation
        /// requests one added execution of its first contained Instruction.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance BranchPredictor(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Structure,
                "CONDITION_TRUE",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));

            return Instance(instance, "WB-DEP-010", CreatorEffect(trigger, "FIRST_CONTAINED_INSTRUCTION", "FIRST_QUALIFYING_EVENT", "EXECUTION"));
        }

        /// <summary>
        /// An ALIGN-shaped instance (WB-DIR-002, post-fix): at the player-traversal boundary, an odd
        /// Value gains 1.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance carrying the boundary shape.</returns>
        private static DependencyInstance Align(int instance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.NamedBoundary, PlayerTraversalBoundary);
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Reaction,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier> { Qualifier("PARITY", "ODD"), Qualifier("REGISTER", "VALUE") },
                timing);

            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new TargetingRule("NO_TARGET", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "EXECUTION"));

            return Instance(instance, "WB-DIR-002", effect);
        }

        /// <summary>
        /// Builds an EXECUTION-domain added-execution-request effect.
        /// </summary>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="targeting">The added-execution targeting token.</param>
        /// <param name="allowance">The frequency-allowance token.</param>
        /// <param name="scope">The frequency-scope token.</param>
        /// <returns>The effect definition.</returns>
        private static EffectDefinition CreatorEffect(
            TriggerDescriptor trigger,
            string targeting,
            string allowance,
            string scope)
        {
            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new AddedExecutionRequestOperation(new TargetingRule(targeting, string.Empty), false),
                new TargetingRule(targeting, string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency(allowance, scope));
        }

        /// <summary>
        /// A rescuable player-owned skip occurrence at the pre-operation band.
        /// </summary>
        /// <param name="rescuable">Whether the skip is structurally rescuable.</param>
        /// <returns>The skip occurrence.</returns>
        private static SkipOccurrence Skip(bool rescuable)
        {
            return new SkipOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(18),
                1,
                OwnershipClassification.PlayerOwned,
                new InstanceID(50),
                "CONDITION_FALSE:#5@3#1/eval-1",
                rescuable);
        }

        /// <summary>
        /// A SAFE-MODE-shaped instance (WB-DEP-007): the first skipped source execution each execution
        /// is rescued to resolve normally.
        /// </summary>
        /// <param name="instance">The instance identity value.</param>
        /// <returns>The Dependency instance.</returns>
        private static DependencyInstance SafeMode(int instance)
        {
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Disposition,
                "SOURCE_EXECUTION_SKIPPED",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"));

            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                new RescueOperation("RESCUED"),
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                trigger.Timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));

            return Instance(instance, "WB-DEP-007", effect);
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
