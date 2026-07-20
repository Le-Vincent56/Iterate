using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The Process-scoped Build invoker and commit owner. It holds the current arrangement, the compiled
    /// arrangement and free-only baseline that drive classification, the buffer seam, the archive, the
    /// pending pragmas, the progression counter, the effect ledger, and the parameter register. It runs a
    /// Command-typed edit, activates Directives, and commits atomically into an immutable CompiledSource.
    /// </summary>
    public sealed class BuildState
    {
        private readonly IBuildBuffer _buffer;
        private readonly ParameterSet _parameters;
        private readonly CompilationEffectLedger _ledger;
        private readonly List<InstanceID> _archivedInstances;
        private readonly List<DirectiveInstance> _pendingPragmas;
        private SourceArrangement _currentArrangement;
        private SourceArrangement _compiledArrangement;
        private SourceArrangement _freeOnlyBaseline;
        private int _editedCompilationCount;

        /// <summary>
        /// The arrangement the player is currently editing.
        /// </summary>
        public SourceArrangement CurrentArrangement => _currentArrangement;

        /// <summary>
        /// The instances archived by free-only edits this Process, in application order.
        /// </summary>
        public IReadOnlyList<InstanceID> ArchivedInstances => _archivedInstances;

        /// <summary>
        /// The activated Directives awaiting the next commit, in activation order.
        /// </summary>
        public IReadOnlyList<DirectiveInstance> PendingPragmas => _pendingPragmas;

        /// <summary>
        /// The Process-scoped edited-compilation progression counter.
        /// </summary>
        public int EditedCompilationCount => _editedCompilationCount;

        /// <summary>
        /// Creates an edit-only Build invoker without a parameter register; commit and status are
        /// unavailable until one is supplied through the three-argument constructor.
        /// </summary>
        /// <param name="initialArrangement">The starting arrangement.</param>
        /// <param name="buffer">The buffer seam edits draw from and return to.</param>
        public BuildState(SourceArrangement initialArrangement, IBuildBuffer buffer) : this(initialArrangement, buffer, null)
        { }

        /// <summary>
        /// Creates a Build invoker over an initial arrangement, a buffer seam, and a parameter register. The
        /// free-only baseline starts at the initial arrangement; no compiled arrangement exists until the
        /// first commit.
        /// </summary>
        /// <param name="initialArrangement">The starting arrangement.</param>
        /// <param name="buffer">The buffer seam edits draw from and return to.</param>
        /// <param name="parameters">The parameter register carrying base costs, or null for edit-only use.</param>
        public BuildState(SourceArrangement initialArrangement, IBuildBuffer buffer, ParameterSet parameters)
        {
            _currentArrangement = initialArrangement ?? throw new ArgumentException("A BuildState requires an initial arrangement.", nameof(initialArrangement));
            _freeOnlyBaseline = initialArrangement;
            _compiledArrangement = null;
            _buffer = buffer ?? throw new ArgumentException("A BuildState requires a buffer.", nameof(buffer));
            _parameters = parameters;
            _ledger = new CompilationEffectLedger();
            _archivedInstances = new List<InstanceID>();
            _pendingPragmas = new List<DirectiveInstance>();
            _editedCompilationCount = 0;
        }

        /// <summary>
        /// Runs an edit and, on success, replaces the current arrangement — and, for a free-only edit, the
        /// free-only baseline — appending any archived instances.
        /// </summary>
        /// <param name="edit">The edit to apply.</param>
        /// <returns>Success, or the edit's typed rejection.</returns>
        public BuildOperationResult Apply(ISourceEdit edit)
        {
            if (edit == null)
                throw new ArgumentException("An edit is required.", nameof(edit));

            BuildOperationResult result = edit.TryApply(_currentArrangement, _buffer, out SourceEditOutcome outcome);
            if (!result.Succeeded)
                return result;

            _currentArrangement = outcome.Arrangement;
            if (edit.IsFreeOnly)
                _freeOnlyBaseline = outcome.Arrangement;

            for (int i = 0; i < outcome.ArchivedInstances.Count; i++)
            {
                _archivedInstances.Add(outcome.ArchivedInstances[i]);
            }

            return result;
        }

        /// <summary>
        /// Classifies the current arrangement against the compiled arrangement and free-only baseline. Before
        /// the first commit no compiled arrangement exists, so the result is Initial.
        /// </summary>
        /// <returns>The compilation classification.</returns>
        public CompilationClassification Classify()
        {
            if (_compiledArrangement == null)
                return CompilationClassification.Initial;

            if (ArrangementEquivalence.AreEquivalent(_currentArrangement, _compiledArrangement))
                return CompilationClassification.Unchanged;

            if (ArrangementEquivalence.AreEquivalent(_currentArrangement, _freeOnlyBaseline))
                return CompilationClassification.FreeOnlyChanged;

            return CompilationClassification.OrdinaryEdited;
        }

        /// <summary>
        /// Installs a buffered item into a destination position.
        /// </summary>
        /// <param name="bufferItem">The buffered item identity.</param>
        /// <param name="destination">The destination position.</param>
        /// <returns>Success, or a typed rejection.</returns>
        public BuildOperationResult TryInstall(InstanceID bufferItem, SourcePosition destination)
        {
            return Apply(new InstallEdit(bufferItem, destination));
        }

        /// <summary>
        /// Moves an occupied origin to an empty destination.
        /// </summary>
        /// <param name="origin">The origin position.</param>
        /// <param name="destination">The destination position.</param>
        /// <returns>Success, or a typed rejection.</returns>
        public BuildOperationResult TryMove(SourcePosition origin, SourcePosition destination)
        {
            return Apply(new MoveEdit(origin, destination));
        }

        /// <summary>
        /// Swaps two compatible occupied targets.
        /// </summary>
        /// <param name="first">The first target position.</param>
        /// <param name="second">The second target position.</param>
        /// <returns>Success, or a typed rejection.</returns>
        public BuildOperationResult TrySwap(SourcePosition first, SourcePosition second)
        {
            return Apply(new SwapEdit(first, second));
        }

        /// <summary>
        /// Removes an occupied origin back to the buffer.
        /// </summary>
        /// <param name="origin">The origin position.</param>
        /// <returns>Success, or a typed rejection.</returns>
        public BuildOperationResult TryRemove(SourcePosition origin)
        {
            return Apply(new RemoveEdit(origin));
        }

        /// <summary>
        /// Overwrites an occupied player target with a buffered Overwrite Instruction.
        /// </summary>
        /// <param name="overwriteItem">The buffered Overwrite Instruction identity.</param>
        /// <param name="target">The occupied player target position.</param>
        /// <returns>Success, or a typed rejection.</returns>
        public BuildOperationResult TryOverwrite(InstanceID overwriteItem, SourcePosition target)
        {
            return Apply(new OverwriteEdit(overwriteItem, target));
        }

        /// <summary>
        /// Activates a buffered Directive as a pending pragma, irreversibly. Not a source edit.
        /// </summary>
        /// <param name="bufferDirective">The buffered Directive identity.</param>
        /// <returns>Success, or a typed rejection.</returns>
        public BuildOperationResult TryActivateDirective(InstanceID bufferDirective)
        {
            if (_buffer.TryPeekDirective(bufferDirective, out DirectiveInstance directive))
            {
                _buffer.Take(bufferDirective);
                _pendingPragmas.Add(directive);
                return BuildOperationResult.Success;
            }

            if (_buffer.TryPeekInstruction(bufferDirective, out InstructionInstance _) || _buffer.TryPeekStructure(bufferDirective, out StructureInstance _))
                return BuildOperationResult.Rejected(SourceEditRejection.NotADirective);

            return BuildOperationResult.Rejected(SourceEditRejection.ItemNotInBuffer);
        }

        /// <summary>
        /// Commits the current Build into an immutable CompiledSource, or blocks without mutating anything.
        /// On commit it snapshots the arrangement and pragmas, re-seeds both baselines, advances progression
        /// when the breakdown says so, consumes first-qualifying allowances, and clears the pragmas.
        /// </summary>
        /// <param name="availableBytes">The Bytes available to pay the final cost.</param>
        /// <param name="installedEffects">The active COMPILATION-domain effects from installed sources.</param>
        /// <returns>A committed or blocked attempt, both carrying the breakdown.</returns>
        public CompilationAttempt Compile(ByteAmount availableBytes, IReadOnlyList<ActiveCompilationEffect> installedEffects)
        {
            if (_parameters == null)
                throw new ArgumentException("Compilation requires a BuildState constructed with a parameter register.");

            CompilationClassification classification = Classify();
            int progressionIndex = classification == CompilationClassification.OrdinaryEdited ? _editedCompilationCount + 1 : 0;
            List<ActiveCompilationEffect> actives = BuildActiveEffects(installedEffects);
            CompilationCostBreakdown breakdown = CompilationCostResolver.Resolve(classification, progressionIndex, _parameters, actives, _ledger);

            if (!breakdown.BaseCostDefined)
                return CompilationAttempt.Blocked(CompilationBlockReason.BaseCostUndefined, breakdown, availableBytes);

            if (breakdown.FinalCost > availableBytes.Value)
                return CompilationAttempt.Blocked(CompilationBlockReason.InsufficientBytes, breakdown, availableBytes);

            List<DirectiveInstance> pragmaSnapshot = new List<DirectiveInstance>(_pendingPragmas);
            CompiledSource source = new CompiledSource(_currentArrangement, pragmaSnapshot, breakdown);
            _compiledArrangement = _currentArrangement;
            _freeOnlyBaseline = _currentArrangement;
            if (breakdown.AdvancesProgression)
                _editedCompilationCount++;

            IReadOnlyList<string> consumedKeys = CompilationCostResolver.CollectConsumableFirstQualifyingKeys(classification, actives, _ledger);
            for (int i = 0; i < consumedKeys.Count; i++)
            {
                _ledger.MarkConsumed(consumedKeys[i]);
            }

            _pendingPragmas.Clear();
            return CompilationAttempt.Success(source, breakdown, availableBytes);
        }

        /// <summary>
        /// Derives the read-only Build status from a pure preview: the six UX-RUN-001 states and the
        /// UX-RUN-017 next-edited base-cost transparency.
        /// </summary>
        /// <param name="availableBytes">The Bytes available.</param>
        /// <param name="installedEffects">The active COMPILATION-domain effects from installed sources.</param>
        /// <returns>The Build status projection.</returns>
        public BuildStatus GetStatus(ByteAmount availableBytes, IReadOnlyList<ActiveCompilationEffect> installedEffects)
        {
            if (_parameters == null)
                throw new ArgumentException("Status requires a BuildState constructed with a parameter register.");

            CompilationClassification classification = Classify();
            int progressionIndex = classification == CompilationClassification.OrdinaryEdited ? _editedCompilationCount + 1 : 0;
            List<ActiveCompilationEffect> actives = BuildActiveEffects(installedEffects);
            CompilationCostBreakdown preview = CompilationCostResolver.Resolve(classification, progressionIndex, _parameters, actives, _ledger);

            bool sourceCompiled = _compiledArrangement != null && classification == CompilationClassification.Unchanged;
            bool sourceChanged = classification == CompilationClassification.FreeOnlyChanged || classification == CompilationClassification.OrdinaryEdited;
            bool ordinaryEditsPresent = classification == CompilationClassification.OrdinaryEdited;
            bool freeOnlyChangesPresent = classification == CompilationClassification.FreeOnlyChanged;
            bool pendingPragma = _pendingPragmas.Count > 0;
            bool compilationBlocked = !preview.BaseCostDefined || preview.FinalCost > availableBytes.Value;

            bool nextDefined = TryNextEditedBaseCost(_editedCompilationCount + 1, out int nextBaseCost);
            return new BuildStatus(
                sourceCompiled,
                sourceChanged,
                ordinaryEditsPresent,
                freeOnlyChangesPresent,
                pendingPragma,
                compilationBlocked,
                classification,
                preview,
                availableBytes,
                nextDefined,
                nextBaseCost
            );
        }

        /// <summary>
        /// Builds the active-effect list from installed effects plus the pending pragmas' compilation
        /// effects, each keyed by its Directive instance.
        /// </summary>
        /// <param name="installedEffects">The active effects from installed sources.</param>
        /// <returns>The combined active-effect list.</returns>
        private List<ActiveCompilationEffect> BuildActiveEffects(IReadOnlyList<ActiveCompilationEffect> installedEffects)
        {
            List<ActiveCompilationEffect> actives = new List<ActiveCompilationEffect>();
            for (int i = 0; i < installedEffects.Count; i++)
            {
                actives.Add(installedEffects[i]);
            }

            for (int i = 0; i < _pendingPragmas.Count; i++)
            {
                DirectiveInstance pragma = _pendingPragmas[i];
                IReadOnlyList<EffectDefinition> effects = pragma.Definition.Effects;
                for (int e = 0; e < effects.Count; e++)
                {
                    actives.Add(ActiveCompilationEffect.For(pragma.Definition.ID.Value, e, pragma.InstanceID, pragma.Definition.DisplayName, effects[e]));
                }
            }

            return actives;
        }

        /// <summary>
        /// Reads the base cost the next edited compilation would take.
        /// </summary>
        /// <param name="nextIndex">The next edited-compilation progression index.</param>
        /// <param name="baseCost">The base cost when defined; zero otherwise.</param>
        /// <returns>True for the first, second, or third; false at four or beyond.</returns>
        private bool TryNextEditedBaseCost(int nextIndex, out int baseCost)
        {
            switch (nextIndex)
            {
                case 1:
                    baseCost = _parameters.FirstEditedCompilationCost;
                    return true;
                
                case 2:
                    baseCost = _parameters.SecondEditedCompilationCost;
                    return true;
                
                case 3:
                    baseCost = _parameters.ThirdEditedCompilationCost;
                    return true;
                
                default:
                    baseCost = 0;
                    return false;
            }
        }
    }
}