using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="SafetyAbortSignal"/> — the sealed unwind signal a breach site throws and
    /// <c>Execute</c> alone catches. It is the single owner of the breach-evidence vocabulary: the
    /// closed limit-name set and the attempted-occurrence identity format. Breach sites hand it
    /// typed facts and it composes the evidence, so no emission site invents a variant. The
    /// attempted occurrence is never created, so its identity is composed rather than read off a
    /// minted record, and the signal's fields construct the <see cref="SafetyAbortPayload"/> the
    /// abort event carries.
    /// </summary>
    public sealed class SafetyAbortSignalTests
    {
        [Test]
        public void ComposeOccurrenceIdentity_WithDetail_JoinsFamilySubtypeAndDetail()
        {
            string identity = SafetyAbortSignal.ComposeOccurrenceIdentity(
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceExecutionStarted,
                "P4");

            Assert.AreEqual("SOURCE/SOURCE_EXECUTION_STARTED@P4", identity);
        }

        [Test]
        public void ComposeOccurrenceIdentity_WithoutDetail_OmitsTheSeparator()
        {
            string identity = SafetyAbortSignal.ComposeOccurrenceIdentity(
                EventFamilies.AddedExecution,
                ExecutionEventSubtypes.AddedExecutionStarted,
                null);

            Assert.AreEqual("ADDED_EXECUTION/ADDED_EXECUTION_STARTED", identity);
        }

        [Test]
        public void ComposeOccurrenceIdentity_EmptyDetail_OmitsTheSeparator()
        {
            string identity = SafetyAbortSignal.ComposeOccurrenceIdentity(
                EventFamilies.Reaction,
                ExecutionEventSubtypes.ImmediateReactionResolved,
                string.Empty);

            Assert.AreEqual("REACTION/IMMEDIATE_REACTION_RESOLVED", identity);
        }

        [Test]
        public void ComposeOccurrenceIdentity_MissingFamily_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => SafetyAbortSignal.ComposeOccurrenceIdentity(
                    null,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P1"));
        }

        [Test]
        public void ComposeOccurrenceIdentity_MissingSubtype_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => SafetyAbortSignal.ComposeOccurrenceIdentity(EventFamilies.Source, string.Empty, "P1"));
        }

        [Test]
        public void LimitName_EachRegistryRow_HasItsClosedToken()
        {
            Assert.AreEqual(
                "ADDED_EXECUTION_LINEAGE_DEPTH",
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionLineageDepth));
            Assert.AreEqual(
                "ADDED_EXECUTIONS_PER_ACTIVATION",
                SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionsPerActivation));
            Assert.AreEqual(
                "SOURCE_EXECUTION_UNITS_PER_EXECUTION",
                SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits));
            Assert.AreEqual(
                "EFFECT_REACTIONS_PER_EXECUTION",
                SafetyAbortSignal.LimitName(SafetyLimitFlags.EffectReactions));
            Assert.AreEqual(
                "TRANSFORMATIONS_PER_PENDING_OPERATION",
                SafetyAbortSignal.LimitName(SafetyLimitFlags.TransformationsOnPendingOperation));
        }

        [Test]
        public void LimitName_None_Throws()
        {
            Assert.Throws<ArgumentException>(() => SafetyAbortSignal.LimitName(SafetyLimitFlags.None));
        }

        [Test]
        public void LimitName_CombinedFlags_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => SafetyAbortSignal.LimitName(
                    SafetyLimitFlags.EffectReactions | SafetyLimitFlags.SourceExecutionUnits));
        }

        [Test]
        public void Construction_ComposesTheIdentityFromTypedFacts()
        {
            SafetyAbortSignal signal = new SafetyAbortSignal(
                EventFamilies.Operation,
                ExecutionEventSubtypes.PrimaryOperationModified,
                "U7",
                new RuntimeUnitID(7),
                OneLimit());

            Assert.AreEqual("OPERATION/PRIMARY_OPERATION_MODIFIED@U7", signal.OverLimitOccurrenceIdentity);
            Assert.AreEqual(new RuntimeUnitID(7), signal.AffectedUnit);
            Assert.AreEqual(1, signal.BreachedLimits.Count);
        }

        [Test]
        public void Construction_CarriesEverySimultaneouslyBreachedLimitInOrder()
        {
            List<BreachedLimit> limits = new List<BreachedLimit>
            {
                new BreachedLimit(
                    SafetyAbortSignal.LimitName(SafetyLimitFlags.AddedExecutionLineageDepth),
                    SafetyCeilings.AddedExecutionLineageDepth,
                    SafetyCeilings.AddedExecutionLineageDepth + 1),
                new BreachedLimit(
                    SafetyAbortSignal.LimitName(SafetyLimitFlags.SourceExecutionUnits),
                    SafetyCeilings.SourceExecutionUnitsPerExecution,
                    SafetyCeilings.SourceExecutionUnitsPerExecution)
            };

            SafetyAbortSignal signal = new SafetyAbortSignal(
                EventFamilies.AddedExecution,
                ExecutionEventSubtypes.AddedExecutionStarted,
                null,
                new RuntimeUnitID(3),
                limits);

            Assert.AreEqual(2, signal.BreachedLimits.Count);
            Assert.AreEqual("ADDED_EXECUTION_LINEAGE_DEPTH", signal.BreachedLimits[0].LimitName);
            Assert.AreEqual("SOURCE_EXECUTION_UNITS_PER_EXECUTION", signal.BreachedLimits[1].LimitName);
        }

        [Test]
        public void Construction_NoBreachedLimits_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new SafetyAbortSignal(
                    EventFamilies.Source,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P1",
                    new RuntimeUnitID(1),
                    new List<BreachedLimit>()));
        }

        [Test]
        public void Construction_NullBreachedLimits_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new SafetyAbortSignal(
                    EventFamilies.Source,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P1",
                    new RuntimeUnitID(1),
                    null));
        }

        [Test]
        public void Construction_MissingFamily_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new SafetyAbortSignal(
                    string.Empty,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P1",
                    new RuntimeUnitID(1),
                    OneLimit()));
        }

        [Test]
        public void Signal_IsASealedExceptionCarryingAMessage()
        {
            SafetyAbortSignal signal = new SafetyAbortSignal(
                EventFamilies.Source,
                ExecutionEventSubtypes.SourceExecutionStarted,
                "P1",
                new RuntimeUnitID(1),
                OneLimit());

            Assert.IsTrue(typeof(SafetyAbortSignal).IsSealed);
            Assert.IsInstanceOf<Exception>(signal);
            Assert.IsNotEmpty(signal.Message);
        }

        [Test]
        public void Signal_IsCaughtByItsOwnTypeAndNotByInvalidOperationException()
        {
            bool caughtAsSignal = false;

            try
            {
                throw new SafetyAbortSignal(
                    EventFamilies.Source,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P1",
                    new RuntimeUnitID(1),
                    OneLimit());
            }
            catch (SafetyAbortSignal)
            {
                caughtAsSignal = true;
            }

            Assert.IsTrue(caughtAsSignal);
            Assert.IsNotInstanceOf<InvalidOperationException>(
                new SafetyAbortSignal(
                    EventFamilies.Source,
                    ExecutionEventSubtypes.SourceExecutionStarted,
                    "P1",
                    new RuntimeUnitID(1),
                    OneLimit()));
        }

        [Test]
        public void Signal_FieldsConstructTheAbortPayload()
        {
            SafetyAbortSignal signal = new SafetyAbortSignal(
                EventFamilies.Reaction,
                ExecutionEventSubtypes.ImmediateReactionResolved,
                "U2",
                new RuntimeUnitID(2),
                OneLimit());

            SafetyAbortPayload payload = new SafetyAbortPayload(
                signal.OverLimitOccurrenceIdentity,
                signal.AffectedUnit,
                signal.BreachedLimits);

            Assert.AreEqual("REACTION/IMMEDIATE_REACTION_RESOLVED@U2", payload.OverLimitOccurrenceIdentity);
            Assert.AreEqual(new RuntimeUnitID(2), payload.AffectedUnit);
            Assert.AreEqual(1, payload.BreachedLimits.Count);
        }

        private static List<BreachedLimit> OneLimit()
        {
            return new List<BreachedLimit>
            {
                new BreachedLimit(
                    SafetyAbortSignal.LimitName(SafetyLimitFlags.EffectReactions),
                    SafetyCeilings.EffectReactionsPerExecution,
                    SafetyCeilings.EffectReactionsPerExecution)
            };
        }
    }
}
