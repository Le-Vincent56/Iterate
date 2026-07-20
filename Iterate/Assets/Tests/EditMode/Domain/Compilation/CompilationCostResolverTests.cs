using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the pure CAB-EVT-548 cost resolver and the COMPILATION-domain evaluator: base cost per
    /// classification and progression index, the CLEAN BUILD set stage and COMPILE AHEAD additive stage
    /// (including the CAB-EVT-551 combined case), qualifier and frequency eligibility, ledger purity,
    /// fail-fast on unrecognized vocabulary, and per-instance ledger keys. Fixtures mirror the shipped
    /// WB-DEP-002 and WB-DIR-004 shapes exactly.
    /// </summary>
    public sealed class CompilationCostResolverTests
    {
        [Test]
        public void Resolve_Initial_BaseZero()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.Initial, 0, NoEffects(), new CompilationEffectLedger());

            Assert.IsTrue(breakdown.BaseCostDefined);
            Assert.AreEqual(0, breakdown.BaseCost);
            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.IsFalse(breakdown.AdvancesProgression);
            Assert.AreEqual(0, breakdown.ProgressionIndex);
            CollectionAssert.IsEmpty(breakdown.Modifiers);
        }

        [Test]
        public void Resolve_Unchanged_BaseZero()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.Unchanged, 0, NoEffects(), new CompilationEffectLedger());

            Assert.AreEqual(0, breakdown.BaseCost);
            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.IsFalse(breakdown.AdvancesProgression);
        }

        [Test]
        public void Resolve_FreeOnlyChanged_BaseZero()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.FreeOnlyChanged, 0, NoEffects(), new CompilationEffectLedger());

            Assert.IsTrue(breakdown.BaseCostDefined);
            Assert.AreEqual(0, breakdown.BaseCost);
            Assert.IsFalse(breakdown.AdvancesProgression);
        }

        [Test]
        public void Resolve_OrdinaryIndexOne_BaseOne()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.OrdinaryEdited, 1, NoEffects(), new CompilationEffectLedger());

            Assert.IsTrue(breakdown.BaseCostDefined);
            Assert.AreEqual(1, breakdown.BaseCost);
            Assert.AreEqual(1, breakdown.FinalCost);
            Assert.IsTrue(breakdown.AdvancesProgression);
            Assert.AreEqual(1, breakdown.ProgressionIndex);
        }

        [Test]
        public void Resolve_OrdinaryIndexTwo_BaseTwo()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.OrdinaryEdited, 2, NoEffects(), new CompilationEffectLedger());

            Assert.AreEqual(2, breakdown.BaseCost);
            Assert.AreEqual(2, breakdown.FinalCost);
        }

        [Test]
        public void Resolve_OrdinaryIndexThree_BaseThree()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.OrdinaryEdited, 3, NoEffects(), new CompilationEffectLedger());

            Assert.AreEqual(3, breakdown.BaseCost);
            Assert.AreEqual(3, breakdown.FinalCost);
        }

        [Test]
        public void Resolve_OrdinaryIndexFour_BaseUndefined()
        {
            CompilationCostBreakdown breakdown = Resolve(CompilationClassification.OrdinaryEdited, 4, NoEffects(), new CompilationEffectLedger());

            Assert.IsFalse(breakdown.BaseCostDefined);
            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.IsTrue(breakdown.AdvancesProgression);
            CollectionAssert.IsEmpty(breakdown.Modifiers);
        }

        [Test]
        public void Resolve_CleanBuildOnOrdinary_SetsToZero()
        {
            ActiveCompilationEffect cleanBuild = CleanBuild(1);

            CompilationCostBreakdown breakdown = Resolve(
                CompilationClassification.OrdinaryEdited,
                1,
                new[] { cleanBuild },
                new CompilationEffectLedger());

            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.AreEqual(1, breakdown.Modifiers.Count);
            CostModifierEntry entry = breakdown.Modifiers[0];
            Assert.AreEqual(CostModifierStage.SetOrReplacement, entry.Stage);
            Assert.IsTrue(entry.SetsAbsolute);
            Assert.AreEqual(1, entry.CostBefore);
            Assert.AreEqual(0, entry.CostAfter);
            Assert.IsFalse(entry.NumericallyRedundant);
        }

        [Test]
        public void Resolve_CleanBuildOnFreeOnly_SkippedByQualifier()
        {
            ActiveCompilationEffect cleanBuild = CleanBuild(1);

            CompilationCostBreakdown breakdown = Resolve(
                CompilationClassification.FreeOnlyChanged,
                0,
                new[] { cleanBuild },
                new CompilationEffectLedger());

            Assert.AreEqual(0, breakdown.FinalCost);
            CollectionAssert.IsEmpty(breakdown.Modifiers);
        }

        [Test]
        public void Resolve_CleanBuildLedgerConsumed_SkippedByFrequency()
        {
            ActiveCompilationEffect cleanBuild = CleanBuild(1);
            CompilationEffectLedger ledger = new CompilationEffectLedger();
            ledger.MarkConsumed(cleanBuild.SourceKey);

            CompilationCostBreakdown breakdown = Resolve(
                CompilationClassification.OrdinaryEdited,
                1,
                new[] { cleanBuild },
                ledger);

            Assert.AreEqual(1, breakdown.FinalCost);
            CollectionAssert.IsEmpty(breakdown.Modifiers);
        }

        [Test]
        public void Resolve_CompileAheadOnOrdinary_ReducesByOne()
        {
            ActiveCompilationEffect compileAhead = CompileAhead(2);

            CompilationCostBreakdown breakdown = Resolve(
                CompilationClassification.OrdinaryEdited,
                2,
                new[] { compileAhead },
                new CompilationEffectLedger());

            Assert.AreEqual(1, breakdown.FinalCost);
            Assert.AreEqual(1, breakdown.Modifiers.Count);
            CostModifierEntry entry = breakdown.Modifiers[0];
            Assert.AreEqual(CostModifierStage.Additive, entry.Stage);
            Assert.IsFalse(entry.SetsAbsolute);
            Assert.AreEqual(2, entry.CostBefore);
            Assert.AreEqual(1, entry.CostAfter);
            Assert.IsFalse(entry.NumericallyRedundant);
        }

        [Test]
        public void Resolve_CleanBuildThenCompileAhead_CombinedCabEvt551()
        {
            ActiveCompilationEffect cleanBuild = CleanBuild(1);
            ActiveCompilationEffect compileAhead = CompileAhead(2);

            CompilationCostBreakdown breakdown = Resolve(
                CompilationClassification.OrdinaryEdited,
                1,
                new[] { cleanBuild, compileAhead },
                new CompilationEffectLedger());

            Assert.AreEqual(0, breakdown.FinalCost);
            Assert.AreEqual(2, breakdown.Modifiers.Count);

            CostModifierEntry setEntry = breakdown.Modifiers[0];
            Assert.AreEqual(CostModifierStage.SetOrReplacement, setEntry.Stage);
            Assert.AreEqual(1, setEntry.CostBefore);
            Assert.AreEqual(0, setEntry.CostAfter);
            Assert.IsFalse(setEntry.NumericallyRedundant);

            CostModifierEntry additiveEntry = breakdown.Modifiers[1];
            Assert.AreEqual(CostModifierStage.Additive, additiveEntry.Stage);
            Assert.AreEqual(0, additiveEntry.CostBefore);
            Assert.AreEqual(0, additiveEntry.CostAfter);
            Assert.IsTrue(additiveEntry.NumericallyRedundant);

            Assert.IsTrue(breakdown.AdvancesProgression);
        }

        [Test]
        public void Resolve_DoesNotMutateLedger()
        {
            ActiveCompilationEffect cleanBuild = CleanBuild(1);
            CompilationEffectLedger ledger = new CompilationEffectLedger();

            Resolve(CompilationClassification.OrdinaryEdited, 1, new[] { cleanBuild }, ledger);

            Assert.IsFalse(ledger.IsConsumed(cleanBuild.SourceKey));
        }

        [Test]
        public void Resolve_HypotheticalPreview_ReturnsRedundancyAndLeavesLedgerUntouched()
        {
            ActiveCompilationEffect cleanBuild = CleanBuild(1);
            ActiveCompilationEffect hypotheticalCompileAhead = CompileAhead(2);
            CompilationEffectLedger ledger = new CompilationEffectLedger();

            CompilationCostBreakdown breakdown = Resolve(
                CompilationClassification.OrdinaryEdited,
                1,
                new[] { cleanBuild, hypotheticalCompileAhead },
                ledger);

            Assert.IsTrue(breakdown.Modifiers[1].NumericallyRedundant);
            Assert.IsFalse(ledger.IsConsumed(cleanBuild.SourceKey));
        }

        [Test]
        public void Resolve_UnknownQualifierKind_Throws()
        {
            EffectDefinition effect = CostEffect(
                true,
                0,
                new[] { new TriggerQualifier("REGISTER", "VALUE") },
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS"),
                "COMPILATION");
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(1), "CLEAN BUILD", effect);

            Assert.Throws<ArgumentException>(() => Resolve(CompilationClassification.Initial, 0, new[] { active }, new CompilationEffectLedger()));
        }

        [Test]
        public void Resolve_UnknownFrequencyPair_Throws()
        {
            EffectDefinition effect = CostEffect(
                false,
                -1,
                Array.Empty<TriggerQualifier>(),
                StackingMode.IndependentResolution,
                new EffectFrequency("EVERY_QUALIFYING_EVENT", "EXECUTION"),
                "COMPILATION");
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-DIR-004", 0, new InstanceID(1), "COMPILE AHEAD", effect);

            Assert.Throws<ArgumentException>(() => Resolve(CompilationClassification.Initial, 0, new[] { active }, new CompilationEffectLedger()));
        }

        [Test]
        public void Resolve_NonCompilationCostKind_Throws()
        {
            EffectDefinition effect = CostEffect(
                false,
                -1,
                Array.Empty<TriggerQualifier>(),
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "COMPILATION"),
                "HEAT");
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-DIR-004", 0, new InstanceID(1), "COMPILE AHEAD", effect);

            Assert.Throws<ArgumentException>(() => Resolve(CompilationClassification.Initial, 0, new[] { active }, new CompilationEffectLedger()));
        }

        [Test]
        public void Resolve_UnknownStackingMode_Throws()
        {
            EffectDefinition effect = CostEffect(
                true,
                0,
                Array.Empty<TriggerQualifier>(),
                StackingMode.SetReplacement,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS"),
                "COMPILATION");
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(1), "CLEAN BUILD", effect);

            Assert.Throws<ArgumentException>(() => Resolve(CompilationClassification.Initial, 0, new[] { active }, new CompilationEffectLedger()));
        }

        [Test]
        public void Resolve_NonCostOperationInCompilationDomain_Throws()
        {
            EffectDefinition effect = new EffectDefinition(
                PhaseDomain.Compilation,
                new TriggerDescriptor(EventFamily.Lifecycle, "COMPILATION_COMMITTED", Array.Empty<TriggerQualifier>(), null),
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, null),
                null,
                null,
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "COMPILATION"));
            ActiveCompilationEffect active = ActiveCompilationEffect.For("WB-INS-001", 0, new InstanceID(1), "NUDGE", effect);

            Assert.Throws<ArgumentException>(() => Resolve(CompilationClassification.Initial, 0, new[] { active }, new CompilationEffectLedger()));
        }

        [Test]
        public void For_TwoInstancesOfOneDefinition_DistinctSourceKeys()
        {
            EffectDefinition effect = CleanBuildEffect();
            ActiveCompilationEffect first = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(1), "CLEAN BUILD", effect);
            ActiveCompilationEffect second = ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(2), "CLEAN BUILD", effect);

            Assert.AreNotEqual(first.SourceKey, second.SourceKey);
        }

        private static CompilationCostBreakdown Resolve(
            CompilationClassification classification,
            int progressionIndex,
            IReadOnlyList<ActiveCompilationEffect> activeEffects,
            CompilationEffectLedger ledger)
        {
            return CompilationCostResolver.Resolve(classification, progressionIndex, BuildParameters(), activeEffects, ledger);
        }

        private static IReadOnlyList<ActiveCompilationEffect> NoEffects()
        {
            return Array.Empty<ActiveCompilationEffect>();
        }

        private static ActiveCompilationEffect CleanBuild(int instanceIDValue)
        {
            return ActiveCompilationEffect.For("WB-DEP-002", 0, new InstanceID(instanceIDValue), "CLEAN BUILD", CleanBuildEffect());
        }

        private static ActiveCompilationEffect CompileAhead(int instanceIDValue)
        {
            return ActiveCompilationEffect.For("WB-DIR-004", 0, new InstanceID(instanceIDValue), "COMPILE AHEAD", CompileAheadEffect());
        }

        private static EffectDefinition CleanBuildEffect()
        {
            return CostEffect(
                true,
                0,
                new[] { new TriggerQualifier("OPERATION_CLASS", "EDITED_COMPILATION") },
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "PROCESS"),
                "COMPILATION");
        }

        private static EffectDefinition CompileAheadEffect()
        {
            return CostEffect(
                false,
                -1,
                Array.Empty<TriggerQualifier>(),
                StackingMode.IndependentResolution,
                new EffectFrequency("ONCE", "COMPILATION"),
                "COMPILATION");
        }

        private static EffectDefinition CostEffect(
            bool setsAbsolute,
            int amount,
            IReadOnlyList<TriggerQualifier> qualifiers,
            StackingMode stacking,
            EffectFrequency frequency,
            string costKind)
        {
            return new EffectDefinition(
                PhaseDomain.Compilation,
                new TriggerDescriptor(EventFamily.Lifecycle, "COMPILATION_COMMITTED", qualifiers, null),
                new CostModificationOperation(costKind, setsAbsolute, amount, 0, true),
                null,
                null,
                stacking,
                frequency);
        }

        private static ParameterSet BuildParameters()
        {
            string[] ids =
            {
                "WB-PAR-001", "WB-PAR-002", "WB-PAR-003", "WB-PAR-004", "WB-PAR-005",
                "WB-PAR-006", "WB-PAR-007", "WB-PAR-008", "WB-PAR-009", "WB-PAR-010",
                "WB-PAR-011", "WB-PAR-012", "WB-PAR-013", "WB-PAR-014", "WB-PAR-015",
                "WB-PAR-016", "WB-PAR-017", "WB-PAR-018", "WB-PAR-019", "WB-PAR-020",
                "WB-PAR-021", "WB-PAR-022", "WB-PAR-023", "WB-PAR-024", "WB-PAR-026",
                "WB-PAR-028", "WB-PAR-029", "WB-PAR-030", "WB-PAR-035", "WB-PAR-036"
            };

            Dictionary<string, double> values = new Dictionary<string, double>();
            for (int i = 0; i < ids.Length; i++)
                values[ids[i]] = 1.0;

            values["WB-PAR-017"] = 0.0;
            values["WB-PAR-018"] = 0.0;
            values["WB-PAR-019"] = 1.0;
            values["WB-PAR-020"] = 2.0;
            values["WB-PAR-021"] = 3.0;
            return new ParameterSet(values);
        }
    }
}
