using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One Process in flight: the arrangement it opened with, its Buffer, its Bytes, its counters, and
    /// the scripted moments still to fire. Created in one step, so a Process either opens in a legal
    /// state or reports why it cannot.
    ///
    /// The initial source is installed through Compilation's own <c>InstallEdit</c> on a transient
    /// build state rather than by constructing slots here: install semantics have exactly one owner,
    /// and a Process that opens with three items placed is indistinguishable from one the player
    /// placed by hand. The Process rule's COMPILATION-domain effects are exposed as
    /// <see cref="CompilationEffects"/> for the caller to pass to <c>Compile</c>; whether the rule also
    /// reaches the execution engine is the caller's decision, because the engine treats a rule's mere
    /// presence as evidence that a Process counter exists.
    /// </summary>
    public sealed class ProcessState
    {
        private readonly List<InstanceID> _exposed = new();
        private readonly HashSet<int> _firedMoments = new();
        private readonly ProcessConfigurationDefinition _configuration;
        private readonly Repository _repository;
        private ExposureDraw _draw;

        /// <summary>
        /// The authored configuration this Process runs.
        /// </summary>
        public ProcessConfigurationDefinition Configuration => _configuration;

        /// <summary>
        /// The resolved setup this Process opened at.
        /// </summary>
        public ProcessSetup Setup { get; }

        /// <summary>
        /// The arrangement the Process opened with, after the initial source was installed.
        /// </summary>
        public SourceArrangement InitialArrangement { get; private set; }

        /// <summary>
        /// The Process's Instruction Buffer.
        /// </summary>
        public InstructionBuffer Buffer { get; }

        /// <summary>
        /// The Process's Byte ledger.
        /// </summary>
        public ByteLedger Bytes { get; }

        /// <summary>
        /// The Process's execution counters.
        /// </summary>
        public ProcessCounters Counters { get; }

        /// <summary>
        /// The Process's identity, for stamping decisions and evidence.
        /// </summary>
        public string ProcessIdentity { get; }

        /// <summary>
        /// The Process rule instance, or null when the configuration names none.
        /// </summary>
        public ProcessRuleInstance ProcessRule { get; }

        /// <summary>
        /// The rule's COMPILATION-domain effects, wrapped for Compilation. Empty when the Process
        /// carries no rule or the rule declares nothing in that domain.
        /// </summary>
        public IReadOnlyList<ActiveCompilationEffect> CompilationEffects { get; }

        /// <summary>
        /// The instances this Process has already drawn on, which an arrival never re-uses.
        /// </summary>
        public IReadOnlyList<InstanceID> ExposedInstanceIDs => _exposed;
        
        /// <summary>
        /// The exposure decisions this Process has resolved, in draw order, cancellations included.
        /// Empty for a scripted Process, which never constructs a draw and consumes no position.
        /// </summary>
        public IReadOnlyList<RandomDecisionRecord> ExposureRecords => _draw == null ? Array.Empty<RandomDecisionRecord>() : _draw.Records;

        private ProcessState(
            ProcessConfigurationDefinition configuration,
            ProcessSetup setup,
            Repository repository,
            InstructionBuffer buffer,
            ByteLedger bytes,
            ProcessCounters counters,
            ProcessRuleInstance processRule,
            IReadOnlyList<ActiveCompilationEffect> compilationEffects,
            SourceArrangement initialArrangement
        )
        {
            _configuration = configuration;
            _repository = repository;
            Setup = setup;
            Buffer = buffer;
            Bytes = bytes;
            Counters = counters;
            ProcessRule = processRule;
            CompilationEffects = compilationEffects;
            InitialArrangement = initialArrangement;
            ProcessIdentity = configuration.ID.Value;
        }

        /// <summary>
        /// Opens a Process: materialise the Core, build the Buffer, ledger and counters at their
        /// resolved values, instantiate the Process rule, load the scripted starting Buffer, and
        /// install the pre-installed source through Compilation.
        /// </summary>
        /// <param name="configuration">The authored Process configuration.</param>
        /// <param name="setup">The resolved setup.</param>
        /// <param name="catalog">The frozen catalog.</param>
        /// <param name="session">The Session whose Repository supplies content.</param>
        /// <param name="confirmedBranch">The confirmed Active Branch; null for a tutorial Process.</param>
        /// <returns>The creation result.</returns>
        public static ProcessCreationResult Create(
            ProcessConfigurationDefinition configuration,
            ProcessSetup setup,
            ContentCatalog catalog,
            SessionState session,
            ActiveBranchConfiguration confirmedBranch
        )
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (session == null) throw new ArgumentNullException(nameof(session));

            if (configuration.BufferLoad.Policy == BufferLoadPolicy.Drawn && confirmedBranch == null)
                return ProcessCreationResult.Rejected(ProcessCreationRejection.BranchMissing);

            if (!catalog.TryGetCore(configuration.Core, out CoreDefinition core))
                return ProcessCreationResult.Rejected(ProcessCreationRejection.CoreMissing);

            CoreMaterialization materialized = CoreMaterializer.Materialize(core);
            if (!materialized.Succeeded)
                return ProcessCreationResult.Rejected(materialized.Rejection);

            InstructionBuffer buffer = new(setup.BufferCapacity);
            ByteLedger bytes = new(new ByteAmount(setup.StartingBytes));
            ProcessCounters counters = new(setup.Executions, setup.MandatoryExecutions);

            ProcessRuleInstance rule = null;
            IReadOnlyList<ActiveCompilationEffect> compilationEffects = Array.Empty<ActiveCompilationEffect>();
            if (configuration.ProcessRule.HasValue)
            {
                if (!catalog.TryGetProcessRule(configuration.ProcessRule.Value, out ProcessRuleDefinition definition))
                    return ProcessCreationResult.Rejected(ProcessCreationRejection.ScriptedContentMissing);

                rule = new ProcessRuleInstance(session.InstanceIDs.Next(), definition);
                compilationEffects = BuildCompilationEffects(rule);
            }

            ProcessState process = new(
                configuration,
                setup,
                session.Repository,
                buffer,
                bytes,
                counters,
                rule,
                compilationEffects,
                materialized.Arrangement
            );

            if (configuration.BufferLoad.Policy == BufferLoadPolicy.Drawn)
            {
                process._draw = new ExposureDraw(
                    confirmedBranch,
                    session.Repository,
                    process._exposed,
                    session,
                    process.ProcessIdentity
                );
            }

            ProcessCreationRejection loaded = process.LoadInitialBuffer();
            if (loaded != ProcessCreationRejection.None)
                return ProcessCreationResult.Rejected(loaded);

            ProcessCreationRejection installed = process.InstallInitialSource(
                catalog.Parameters,
                materialized.Arrangement
            );
            if (installed != ProcessCreationRejection.None)
                return ProcessCreationResult.Rejected(installed);

            return ProcessCreationResult.Success(process);
        }

        /// <summary>
        /// Fires one scripted arrival moment, admitting its content in authored order. A moment fires
        /// exactly once, never after the final execution, and never partially.
        /// </summary>
        /// <param name="moment">The moment to fire.</param>
        /// <returns>The arrival result.</returns>
        public ArrivalResult Arrive(ArrivalMoment moment)
        {
            if (moment.AfterExecution >= Counters.Allowance)
                return ArrivalResult.Rejected(ArrivalRejection.AfterFinalExecution);
            
            if (_draw != null)
                return ArriveDrawn(moment.AfterExecution);

            ArrivalLoadSpec load = FindMoment(moment.AfterExecution);
            if (load == null)
                return ArrivalResult.Rejected(ArrivalRejection.NoSuchMoment);

            if (_firedMoments.Contains(moment.AfterExecution))
                return ArrivalResult.Rejected(ArrivalRejection.MomentAlreadyFired);

            List<RepositoryEntry> resolved = new(load.Items.Count);
            for (int index = 0; index < load.Items.Count; index++)
            {
                if (!_repository.TryResolveContent(load.Items[index], _exposed, out RepositoryEntry entry))
                    return ArrivalResult.Rejected(ArrivalRejection.ContentMissing);

                resolved.Add(entry);
                _exposed.Add(entry.Item.InstanceID);
            }

            List<RepositoryItem> arrived = new(resolved.Count);
            for (int index = 0; index < resolved.Count; index++)
            {
                Buffer.Admit(resolved[index].Item);
                arrived.Add(resolved[index].Item);
            }

            _firedMoments.Add(moment.AfterExecution);
            return ArrivalResult.Success(arrived);
        }

        /// <summary>
        /// Fires one drawn arrival moment: a single exposure draw at the next position. A cancelled
        /// decision is a legal outcome — the moment fires, its record is kept, and nothing arrives.
        /// </summary>
        /// <param name="afterExecution">The execution the moment follows.</param>
        /// <returns>The arrival result.</returns>
        private ArrivalResult ArriveDrawn(int afterExecution)
        {
            if (!DeclaresDrawnMoment(afterExecution))
                return ArrivalResult.Rejected(ArrivalRejection.NoSuchMoment);

            if (_firedMoments.Contains(afterExecution))
                return ArrivalResult.Rejected(ArrivalRejection.MomentAlreadyFired);

            List<RepositoryItem> arrived = new(1);
            ExposureDrawResult drawn = _draw.DrawNext();
            if (drawn.Succeeded)
            {
                Buffer.Admit(drawn.Item);
                arrived.Add(drawn.Item);
            }

            _firedMoments.Add(afterExecution);
            return ArrivalResult.Success(arrived);
        }

        /// <summary>
        /// Whether the drawn load declares an arrival after the given execution.
        /// </summary>
        /// <param name="afterExecution">The execution the moment follows.</param>
        /// <returns>True when the moment is declared.</returns>
        private bool DeclaresDrawnMoment(int afterExecution)
        {
            IReadOnlyList<int> moments = _configuration.BufferLoad.ArrivalsAfterExecutions;
            for (int index = 0; index < moments.Count; index++)
            {
                if (moments[index] == afterExecution)
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Takes an immutable view of the Process.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public ProcessSnapshot Snapshot()
        {
            return new ProcessSnapshot(
                InitialArrangement,
                Buffer.Slots,
                Bytes.Balance,
                Counters.ExecutionsRun,
                Counters.ExecutionsRemaining,
                Buffer.IsOverflowing
            );
        }

        /// <summary>
        /// Loads the starting Buffer: a scripted plan admits its named content in authored order; a
        /// drawn plan admits its guaranteed content first, then draws the remainder. Guaranteed content
        /// is resolved before any draw so it never enters a random denominator (CAB-EVT-865).
        /// </summary>
        /// <returns>None on success, or the rejection.</returns>
        private ProcessCreationRejection LoadInitialBuffer()
        {
            if (_draw == null)
                return LoadScriptedBuffer();

            IReadOnlyList<string> guaranteed = _configuration.Exposure == null
                ? Array.Empty<string>()
                : _configuration.Exposure.Guaranteed;

            for (int index = 0; index < guaranteed.Count; index++)
            {
                if (!_draw.TryResolveGuaranteed(guaranteed[index], out RepositoryEntry entry))
                    return ProcessCreationRejection.GuaranteedContentMissing;

                _exposed.Add(entry.Item.InstanceID);
                Buffer.Admit(entry.Item);
            }

            int remaining = _configuration.BufferLoad.InitialCount - guaranteed.Count;
            for (int index = 0; index < remaining; index++)
            {
                ExposureDrawResult drawn = _draw.DrawNext();
                if (drawn.Succeeded)
                    Buffer.Admit(drawn.Item);
            }

            return ProcessCreationRejection.None;
        }

        /// <summary>
        /// Admits the scripted starting Buffer content, marking each instance exposed.
        /// </summary>
        /// <returns>None on success, or the rejection.</returns>
        private ProcessCreationRejection LoadScriptedBuffer()
        {
            IReadOnlyList<string> initial = _configuration.BufferLoad.Initial;
            for (int index = 0; index < initial.Count; index++)
            {
                if (!_repository.TryResolveContent(initial[index], _exposed, out RepositoryEntry entry))
                    return ProcessCreationRejection.ScriptedContentMissing;

                _exposed.Add(entry.Item.InstanceID);
                Buffer.Admit(entry.Item);
            }

            return ProcessCreationRejection.None;
        }

        /// <summary>
        /// Installs the pre-installed source through Compilation: each entry is admitted to the Buffer
        /// and installed by an <c>InstallEdit</c> on a transient build state, whose final arrangement
        /// becomes the Process's opening arrangement. The transient state is then discarded.
        /// </summary>
        /// <param name="parameters">The locked parameter register Compilation reads its cost bands from.</param>
        /// <param name="arrangement">The materialised Core-and-empty arrangement.</param>
        /// <returns>None on success, or the rejection.</returns>
        private ProcessCreationRejection InstallInitialSource(ParameterSet parameters, SourceArrangement arrangement)
        {
            IReadOnlyList<InitialSourceSpec> entries = _configuration.InitialSource;
            if (entries.Count == 0)
            {
                InitialArrangementOverride(arrangement);
                return ProcessCreationRejection.None;
            }

            BuildState build = new(arrangement, Buffer, parameters);

            for (int index = 0; index < entries.Count; index++)
            {
                InitialSourceSpec spec = entries[index];
                if (!_repository.TryResolveContent(spec.Content, _exposed, out RepositoryEntry entry))
                    return ProcessCreationRejection.ScriptedContentMissing;

                _exposed.Add(entry.Item.InstanceID);
                Buffer.Admit(entry.Item);

                BuildOperationResult applied = build.Apply(
                    new InstallEdit(entry.Item.InstanceID, new SourcePosition(spec.Position))
                );

                if (!applied.Succeeded)
                    return ProcessCreationRejection.InitialSourceIllegal;
            }

            InitialArrangementOverride(build.CurrentArrangement);
            return ProcessCreationRejection.None;
        }

        /// <summary>
        /// Fixes the Process's opening arrangement once installation has finished.
        /// </summary>
        /// <param name="arrangement">The arrangement the Process opens with.</param>
        private void InitialArrangementOverride(SourceArrangement arrangement)
        {
            InitialArrangement = arrangement;
        }

        /// <summary>
        /// Finds the authored load after a given execution.
        /// </summary>
        /// <param name="afterExecution">The execution the moment follows.</param>
        /// <returns>The load, or null when none is declared.</returns>
        private ArrivalLoadSpec FindMoment(int afterExecution)
        {
            IReadOnlyList<ArrivalLoadSpec> arrivals = _configuration.BufferLoad.Arrivals;
            for (int index = 0; index < arrivals.Count; index++)
            {
                if (arrivals[index].AfterExecution == afterExecution)
                    return arrivals[index];
            }

            return null;
        }

        /// <summary>
        /// Wraps a Process rule's COMPILATION-domain effects for the Compilation caller, attributed to
        /// the rule instance exactly as any other content's effects are.
        /// </summary>
        /// <param name="rule">The Process-rule instance.</param>
        /// <returns>The active compilation effects in declaration order.</returns>
        private static IReadOnlyList<ActiveCompilationEffect> BuildCompilationEffects(ProcessRuleInstance rule)
        {
            List<ActiveCompilationEffect> effects = new();
            IReadOnlyList<EffectDefinition> declared = rule.Definition.Effects;
            for (int index = 0; index < declared.Count; index++)
            {
                if (declared[index].PhaseDomain != PhaseDomain.Compilation)
                    continue;

                effects.Add(ActiveCompilationEffect.For(
                    rule.Definition.ID.Value,
                    index,
                    rule.InstanceID,
                    rule.Definition.DisplayName,
                    declared[index]
                ));
            }

            return effects;
        }
    }
}