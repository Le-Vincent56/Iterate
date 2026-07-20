using System;
using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The pure compilation-cost resolver and COMPILATION-domain evaluator. It stages base →
    /// set/replacement → additive → floor 0, reading base cost from the parameter register and modifiers
    /// from active COMPILATION-domain effects. It never mutates the ledger, so it is safe for previews. It
    /// fails fast on any COMPILATION-domain effect whose vocabulary it does not implement.
    /// </summary>
    public static class CompilationCostResolver
    {
        private const string CostKindCompilation = "COMPILATION";
        private const string QualifierKindOperationClass = "OPERATION_CLASS";
        private const string QualifierValueEditedCompilation = "EDITED_COMPILATION";
        private const string AllowanceFirstQualifyingEvent = "FIRST_QUALIFYING_EVENT";
        private const string AllowanceOnce = "ONCE";
        private const string ScopeProcess = "PROCESS";
        private const string ScopeCompilation = "COMPILATION";
        private const string ScopeExecution = "EXECUTION";

        /// <summary>
        /// Resolves the full cost breakdown for a compilation.
        /// </summary>
        /// <param name="classification">The compilation classification.</param>
        /// <param name="progressionIndex">The edited-compilation progression index.</param>
        /// <param name="parameters">The parameter register carrying base costs.</param>
        /// <param name="activeEffects">The active COMPILATION-domain effects.</param>
        /// <param name="ledger">The consumed-allowance ledger; read, never mutated.</param>
        /// <returns>The full cost breakdown.</returns>
        public static CompilationCostBreakdown Resolve(
            CompilationClassification classification,
            int progressionIndex,
            ParameterSet parameters,
            IReadOnlyList<ActiveCompilationEffect> activeEffects,
            CompilationEffectLedger ledger)
        {
            bool advances = classification == CompilationClassification.OrdinaryEdited;
            int recordedIndex = advances ? progressionIndex : 0;
            if (!TryBaseCost(classification, progressionIndex, parameters, out int baseCost))
                return new CompilationCostBreakdown(classification, recordedIndex, false, 0, Array.Empty<CostModifierEntry>(), 0, advances);

            List<ActiveCompilationEffect> qualifying = new List<ActiveCompilationEffect>();
            List<CostModificationOperation> qualifyingOperations = new List<CostModificationOperation>();
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (TryQualify(activeEffects[i], classification, ledger, out CostModificationOperation operation))
                {
                    qualifying.Add(activeEffects[i]);
                    qualifyingOperations.Add(operation);
                }
            }

            List<CostModifierEntry> modifiers = new List<CostModifierEntry>();
            int cost = baseCost;
            for (int i = 0; i < qualifying.Count; i++)
            {
                CostModificationOperation operation = qualifyingOperations[i];
                if (!operation.SetsAbsolute)
                    continue;

                int before = cost;
                cost = Clamp(operation.Amount, operation.Floor);
                modifiers.Add(new CostModifierEntry(
                    CostModifierStage.SetOrReplacement,
                    qualifying[i].SourceDisplayName,
                    true,
                    operation.Amount,
                    before,
                    cost,
                    before == cost)
                );
            }

            for (int i = 0; i < qualifying.Count; i++)
            {
                CostModificationOperation operation = qualifyingOperations[i];
                if (operation.SetsAbsolute)
                    continue;

                int before = cost;
                cost = Clamp(before + operation.Amount, operation.Floor);
                modifiers.Add(new CostModifierEntry(
                    CostModifierStage.Additive,
                    qualifying[i].SourceDisplayName,
                    false,
                    operation.Amount,
                    before,
                    cost,
                    before == cost)
                );
            }

            int finalCost = cost < 0 ? 0 : cost;
            return new CompilationCostBreakdown(classification, recordedIndex, true, baseCost, modifiers, finalCost, advances);
        }
        
        /// <summary>
        /// Collects the source keys of first-qualifying, Process-scoped effects that apply for this
        /// compilation and are not yet consumed — the keys a successful commit marks consumed. Pure; the
        /// ledger is unchanged.
        /// </summary>
        /// <param name="classification">The compilation classification.</param>
        /// <param name="activeEffects">The active COMPILATION-domain effects.</param>
        /// <param name="ledger">The consumed-allowance ledger.</param>
        /// <returns>The source keys to consume on commit.</returns>
        public static IReadOnlyList<string> CollectConsumableFirstQualifyingKeys(
            CompilationClassification classification,
            IReadOnlyList<ActiveCompilationEffect> activeEffects,
            CompilationEffectLedger ledger
        )
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < activeEffects.Count; i++)
            {
                ActiveCompilationEffect active = activeEffects[i];
                if (IsFirstQualifyingProcess(active.Effect.Frequency) && TryQualify(active, classification, ledger, out CostModificationOperation _))
                    keys.Add(active.SourceKey);
            }

            return keys;
        }

        /// <summary>
        /// Reads the base cost for a classification and progression index.
        /// </summary>
        /// <param name="classification">The compilation classification.</param>
        /// <param name="progressionIndex">The edited-compilation progression index.</param>
        /// <param name="parameters">The parameter register.</param>
        /// <param name="baseCost">The base cost when defined; zero otherwise.</param>
        /// <returns>True when a base cost is defined; false at ordinary index four or beyond.</returns>
        private static bool TryBaseCost(
            CompilationClassification classification,
            int progressionIndex,
            ParameterSet parameters,
            out int baseCost
        )
        {
            switch (classification)
            {
                case CompilationClassification.Initial:
                    baseCost = parameters.InitialCompilationCost;
                    return true;
                
                case CompilationClassification.Unchanged:
                    baseCost = parameters.UnchangedCompilationCost;
                    return true;
                
                case CompilationClassification.FreeOnlyChanged:
                    baseCost = 0;
                    return true;
                
                case CompilationClassification.OrdinaryEdited:
                    return TryOrdinaryBaseCost(progressionIndex, parameters, out baseCost);
                
                default:
                    baseCost = 0;
                    return false;
            }
        }

        /// <summary>
        /// Reads the ordinary edited-compilation base cost for a progression index.
        /// </summary>
        /// <param name="progressionIndex">The one-based progression index.</param>
        /// <param name="parameters">The parameter register.</param>
        /// <param name="baseCost">The base cost when defined; zero otherwise.</param>
        /// <returns>True for index one, two, or three; false at four or beyond.</returns>
        private static bool TryOrdinaryBaseCost(int progressionIndex, ParameterSet parameters, out int baseCost)
        {
            switch (progressionIndex)
            {
                case 1:
                    baseCost = parameters.FirstEditedCompilationCost;
                    return true;
                
                case 2:
                    baseCost = parameters.SecondEditedCompilationCost;
                    return true;
                
                case 3:
                    baseCost = parameters.ThirdEditedCompilationCost;
                    return true;
                
                default:
                    baseCost = 0;
                    return false;
            }
        }

        /// <summary>
        /// Validates a COMPILATION-domain effect's vocabulary and reports whether it applies. Throws on any
        /// unrecognized operation, cost kind, qualifier, frequency, or stacking mode.
        /// </summary>
        /// <param name="active">The active effect.</param>
        /// <param name="classification">The compilation classification.</param>
        /// <param name="ledger">The consumed-allowance ledger.</param>
        /// <param name="operation">The validated cost operation when eligible; null otherwise.</param>
        /// <returns>True when the effect contributes a modifier; false when skipped.</returns>
        private static bool TryQualify(
            ActiveCompilationEffect active,
            CompilationClassification classification,
            CompilationEffectLedger ledger,
            out CostModificationOperation operation)
        {
            operation = null;
            EffectDefinition effect = active.Effect;
            if (effect.PhaseDomain != PhaseDomain.Compilation)
                return false;

            if (!(effect.Operation is CostModificationOperation costOperation))
                throw new ArgumentException($"A COMPILATION-domain effect carries an unsupported operation kind '{effect.Operation.Kind}'.");

            if (costOperation.CostKind != CostKindCompilation)
                throw new ArgumentException($"A COMPILATION-domain effect carries an unsupported cost kind '{costOperation.CostKind}'.");

            bool restrictedToEdited = ValidateQualifiers(effect.Trigger);
            ValidateFrequency(effect.Frequency);
            ValidateStacking(effect.Stacking);

            operation = costOperation;
            if (restrictedToEdited && classification != CompilationClassification.OrdinaryEdited)
                return false;

            if (IsFirstQualifyingProcess(effect.Frequency) && ledger.IsConsumed(active.SourceKey))
                return false;

            return true;
        }

        /// <summary>
        /// Validates an effect's trigger qualifiers and reports whether it is restricted to edited
        /// compilations. Throws on any unrecognized qualifier.
        /// </summary>
        /// <param name="trigger">The trigger descriptor, or null.</param>
        /// <returns>True when the effect carries the edited-compilation qualifier.</returns>
        private static bool ValidateQualifiers(TriggerDescriptor trigger)
        {
            if (trigger == null || trigger.Qualifiers == null)
                return false;

            bool restrictedToEdited = false;
            IReadOnlyList<TriggerQualifier> qualifiers = trigger.Qualifiers;
            for (int i = 0; i < qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = qualifiers[i];
                if (qualifier.Kind == QualifierKindOperationClass && qualifier.Value == QualifierValueEditedCompilation)
                    restrictedToEdited = true;
                else
                    throw new ArgumentException($"A COMPILATION-domain effect carries an unsupported qualifier '{qualifier.Kind}/{qualifier.Value}'.");
            }

            return restrictedToEdited;
        }

        /// <summary>
        /// Validates an effect's frequency pair. Throws on any unrecognized pair.
        /// </summary>
        /// <param name="frequency">The frequency, or null.</param>
        private static void ValidateFrequency(EffectFrequency frequency)
        {
            if (frequency == null)
                return;

            switch (frequency.Allowance)
            {
                case AllowanceFirstQualifyingEvent when frequency.Scope == ScopeProcess:
                case AllowanceOnce when frequency.Scope is ScopeCompilation or ScopeExecution:
                    return;
                
                default:
                    throw new ArgumentException($"A COMPILATION-domain effect carries an unsupported frequency '{frequency.Allowance}/{frequency.Scope}'.");
            }
        }

        /// <summary>
        /// Validates an effect's stacking mode. Throws on anything but independent resolution.
        /// </summary>
        /// <param name="stacking">The stacking mode.</param>
        private static void ValidateStacking(StackingMode stacking)
        {
            if (stacking != StackingMode.IndependentResolution)
                throw new ArgumentException($"A COMPILATION-domain effect carries an unsupported stacking mode '{stacking}'.");
        }

        /// <summary>
        /// Whether a frequency is the first-qualifying-event, Process-scoped allowance the ledger tracks.
        /// </summary>
        /// <param name="frequency">The frequency, or null.</param>
        /// <returns>True when the frequency is first-qualifying-event over Process scope.</returns>
        private static bool IsFirstQualifyingProcess(EffectFrequency frequency)
        {
            return frequency is { Allowance: AllowanceFirstQualifyingEvent, Scope: ScopeProcess };
        }

        /// <summary>
        /// Clamps a value to a floor.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="floor">The minimum allowed value.</param>
        /// <returns>The value, or the floor when the value is below it.</returns>
        private static int Clamp(int value, int floor)
        {
            return value < floor ? floor : value;
        }
    }
}