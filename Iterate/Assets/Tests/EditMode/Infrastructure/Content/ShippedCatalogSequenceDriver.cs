using System;
using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// The Infrastructure assembly's own copy of the Domain sequencing driver. Test assemblies do not
    /// reference each other, so this is a deliberate duplicate of
    /// <c>Iterate.Domain.Progression.Tests.SessionSequenceDriver</c> — the same precedent
    /// <c>ShippedCatalogCompilationTests</c> set when it carried its own build-buffer double. It is
    /// generated from that file by namespace and type rename only; any behavioural edit belongs in both.
    ///
    /// The test-only sequencing driver standing in for Session Flow, which this slice does not build.
    /// It plays the whole child in order — seed a Session, resolve setup, configure and confirm a
    /// Branch, create a Process, fire arrivals, drive a real <see cref="BuildState"/> over the real
    /// Buffer, compile with the Process rule's effects and debit the breakdown, then run the real
    /// <see cref="ExecutionScheduler"/> — so the seams are exercised by one caller rather than
    /// asserted piecemeal.
    ///
    /// One rule here is a decision rather than plumbing: the execution configuration carries the
    /// Process rule <em>only</em> when the rule declares an EXECUTION-domain effect, because the engine
    /// treats a rule's mere presence as evidence that a Process counter exists.
    /// </summary>
    public sealed class ShippedCatalogSequenceDriver
    {
        private const string ContentCatalogStamp = "Content Catalog";

        private const string RandomServiceStamp = "Random Service";

        private const string NoRuleConfiguration = "none";

        private readonly ContentCatalog _catalog;

        private readonly List<ExecutionRecord> _executions = new();

        private BuildState _build;

        /// <summary>
        /// The Session this driver sequences.
        /// </summary>
        /// <summary>
        /// How many archives went through the Process rather than straight to the Buffer. Counted here
        /// because ProcessState exposes no archive count, and this card is tests-only.
        /// </summary>
        private int _archivesThroughProcess;

        public SessionState Session { get; }

        /// <summary>
        /// The Process in flight, or null before one is created.
        /// </summary>
        public ProcessState Process { get; private set; }

        /// <summary>
        /// The Build state over the Process's arrangement and Buffer, or null before a Process opens.
        /// </summary>
        public BuildState Build => _build;

        /// <summary>
        /// The shop most recently opened, or null before the first one.
        /// </summary>
        public ShopState Shop { get; private set; }

        /// <summary>
        /// The execution records produced so far, in order.
        /// </summary>
        public IReadOnlyList<ExecutionRecord> Executions => _executions;

        public ShippedCatalogSequenceDriver(
            ContentCatalog catalog,
            StarterArchetypeDefinition archetype,
            string systemIdentity,
            string sessionIdentity,
            string sessionSeedIdentity
        )
        {
            _catalog = catalog;
            Session = SessionState.Create(catalog, archetype, systemIdentity, sessionIdentity, sessionSeedIdentity);
        }

        /// <summary>
        /// Opens a Branch configuration against a Process's authored constraints and reports how the
        /// seeded draft stands. A Process with no Branch spec is a tutorial Process and configures
        /// nothing.
        /// </summary>
        /// <param name="configuration">The Process whose constraints the draft is judged against.</param>
        /// <returns>The validation of the seeded draft.</returns>
        public BranchValidation ConfigureBranch(ProcessConfigurationDefinition configuration)
        {
            ActiveBranchSpec spec = configuration.ActiveBranch;
            BranchConstraints constraints = new(
                spec.Capacity,
                spec.Required,
                spec.Quarantined,
                spec.RecommendedTags,
                spec.CautionTags
            );

            Session.ActiveBranch.BeginConfiguration(constraints, Session.Repository.Snapshot());
            return Session.ActiveBranch.Validate();
        }

        /// <summary>
        /// Excludes selected instances, highest identity first, until the draft is inside capacity.
        /// This stands in for the player resolving an over-capacity draft; `Exclude` refuses to drop a
        /// Required instance, so the trim stops short rather than breaking a constraint.
        /// </summary>
        /// <returns>The validation after trimming.</returns>
        public BranchValidation TrimToCapacity()
        {
            ActiveBranch branch = Session.ActiveBranch;
            IReadOnlyList<RepositoryEntry> entries = Session.Repository.Entries;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (!branch.Validate().IsOverCapacity)
                    break;

                branch.Exclude(entries[index].Item.InstanceID);
            }

            return branch.Validate();
        }

        /// <summary>
        /// Confirms the open Branch configuration.
        /// </summary>
        /// <returns>The confirmation result.</returns>
        public BranchConfirmationResult ConfirmBranch()
        {
            return Session.ActiveBranch.Confirm();
        }

        /// <summary>
        /// Resolves setup and creates a Process, opening a Build over its arrangement and Buffer.
        /// </summary>
        /// <param name="configuration">The authored Process configuration.</param>
        /// <returns>The creation result.</returns>
        public ProcessCreationResult CreateProcess(ProcessConfigurationDefinition configuration)
        {
            ProcessSetup setup = ProcessSetupResolver.Resolve(
                configuration,
                _catalog.Parameters,
                Session.Economy.SetupEffectsFor(configuration.ID)
            );

            ProcessCreationResult result = ProcessState.Create(
                configuration,
                setup,
                _catalog,
                Session,
                Session.ActiveBranch.Confirmed
            );

            if (result.Succeeded)
            {
                Process = result.State;
                _build = new BuildState(Process.InitialArrangement, Process.Buffer, _catalog.Parameters);
                _archivesThroughProcess = 0;
            }

            return result;
        }

        /// <summary>
        /// Fires the arrival moment following a given execution.
        /// </summary>
        /// <param name="afterExecution">The execution the moment follows.</param>
        /// <returns>The arrival result.</returns>
        public ArrivalResult Arrive(int afterExecution)
        {
            return Process.Arrive(new ArrivalMoment(afterExecution));
        }

        /// <summary>
        /// Installs a Buffer item at a source position through Compilation's own edit.
        /// </summary>
        /// <param name="item">The Buffer item's instance identity.</param>
        /// <param name="position">The one-based source position.</param>
        /// <returns>The operation result.</returns>
        public BuildOperationResult Install(InstanceID item, int position)
        {
            return _build.Apply(new InstallEdit(item, new SourcePosition(position)));
        }

        /// <summary>
        /// Removes whatever occupies a source position, returning it to the Buffer.
        /// </summary>
        /// <param name="position">The one-based source position.</param>
        /// <returns>The operation result.</returns>
        public BuildOperationResult Remove(int position)
        {
            return _build.Apply(new RemoveEdit(new SourcePosition(position)));
        }

        /// <summary>
        /// Compiles the current Build with the Process rule's COMPILATION-domain effects and debits the
        /// breakdown's final cost from the Process ledger. The debit is the caller's job by Compilation's
        /// standing contract, which is exactly what this driver exists to prove.
        /// </summary>
        /// <returns>The compilation attempt.</returns>
        public CompilationAttempt Compile()
        {
            CompilationAttempt attempt = _build.Compile(Process.Bytes.Balance, Process.CompilationEffects);
            if (attempt.Committed)
                Process.Bytes.Debit(new ByteAmount(attempt.Breakdown.FinalCost), "compilation");

            return attempt;
        }

        /// <summary>
        /// Runs one execution over a committed compiled source and records it against the Process
        /// counters.
        /// </summary>
        /// <param name="source">The committed compiled source.</param>
        /// <returns>The frozen execution record.</returns>
        public ExecutionRecord Execute(CompiledSource source)
        {
            ExecutionRequest request = BuildRequest(source);
            ExecutionRecord record = new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
            _executions.Add(record);
            Process.Counters.RecordExecution();
            return record;
        }

        /// <summary>
        /// Assembles the engine request from the Process and Session snapshots.
        /// </summary>
        /// <param name="source">The committed compiled source.</param>
        /// <returns>The execution request.</returns>
        public ExecutionRequest BuildRequest(CompiledSource source)
        {
            ProcessConfigurationDefinition configuration = Process.Configuration;
            ProcessRuleInstance rule = DeclaresExecutionEffect(Process.ProcessRule) ? Process.ProcessRule : null;
            string ordinal = (_executions.Count + 1).ToString(CultureInfo.InvariantCulture);
            ProcessThresholdSpec thresholds = configuration.Thresholds;

            ProcessExecutionConfiguration executionConfiguration = new(
                configuration.ID.Value + ":execution:" + ordinal,
                configuration.ID.Value + ":compilation:" + ordinal,
                _catalog.Revision,
                configuration.ID.Value,
                configuration.Core.Value,
                rule == null ? NoRuleConfiguration : rule.Definition.ID.Value,
                Session.SessionSeedIdentity,
                new ProcessThresholds(
                    new ScoreValue(thresholds.Pass),
                    new ScoreValue(thresholds.Optimize),
                    new ScoreValue(thresholds.Benchmark)
                ),
                rule,
                null
            );

            List<RevisionStamp> stamps = new()
            {
                new RevisionStamp(ContentCatalogStamp, _catalog.Revision),
                new RevisionStamp(RandomServiceStamp, DeterminismService.RevisionIdentity)
            };

            List<DependencyInstance> installed = new(Session.Economy.Dependencies.AllInstalled);

            return new ExecutionRequest(
                source,
                executionConfiguration,
                stamps,
                new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0)),
                installed
            );
        }

        /// <summary>
        /// Opens the shop preceding a Process. The shop is held so a test can pin, reroll and buy
        /// through the driver rather than reaching past it.
        /// </summary>
        /// <param name="shop">The shop's surrogate-key identity.</param>
        /// <param name="target">The Process the shop precedes.</param>
        /// <returns>The open result.</returns>
        public ShopOpenResult OpenShop(ShopID shop, ProcessID target)
        {
            if (!_catalog.TryGetShop(shop, out ShopDefinition definition))
                throw new ArgumentException("The catalog does not define " + shop.Value + ".", nameof(shop));

            ShopOpenResult result = ShopState.Open(definition, _catalog, Session, target);
            if (result.Succeeded)
                Shop = result.Shop;

            AssertArchiveAgreement();
            return result;
        }

        /// <summary>
        /// Evaluates the current Process's thresholds against an execution, through the record overload
        /// so result validity is the record's own rather than a forwarded guess.
        /// </summary>
        /// <param name="record">The execution to evaluate.</param>
        /// <returns>The evaluation.</returns>
        public ThresholdEvaluation EvaluateThresholds(ExecutionRecord record)
        {
            AssertArchiveAgreement();
            return ThresholdEvaluator.Evaluate(record, Process.Configuration.Thresholds);
        }

        /// <summary>
        /// Resolves the current Process's reward package for an execution, in both phases: the plan is
        /// built and then applied, so a package that cannot resolve awards nothing.
        /// </summary>
        /// <param name="record">The execution whose tier is rewarded.</param>
        /// <returns>The applied resolution.</returns>
        public RewardResolution ResolveRewards(ExecutionRecord record)
        {
            RewardPackageID packageID = Process.Configuration.RewardPackage;
            if (!_catalog.TryGetRewardPackage(packageID, out RewardPackageDefinition package))
                throw new ArgumentException("The catalog does not define " + packageID.Value + ".", nameof(record));

            ThresholdEvaluation evaluation = EvaluateThresholds(record);
            RewardPlanResult planned = RewardResolver.Plan(package, evaluation.Reached, Session, _catalog);
            if (!planned.Succeeded)
                throw new InvalidOperationException("The reward package " + package.ID.Value + " did not resolve.");

            RewardResolution resolution = RewardResolver.Apply(planned.Plan, Session, _catalog);
            AssertArchiveAgreement();
            return resolution;
        }

        /// <summary>
        /// Archives a buffered item through the Process, so the Dependencies that observe an archive
        /// fire. Archiving through the Buffer directly would commit the archive and skip them.
        /// </summary>
        /// <param name="item">The buffered instance to archive.</param>
        /// <returns>The archive result.</returns>
        public ArchiveResult Archive(InstanceID item)
        {
            ArchiveResult result = Process.Archive(item);
            if (result.Succeeded)
                _archivesThroughProcess += 1;

            AssertArchiveAgreement();
            return result;
        }

        /// <summary>
        /// Archives the item held outside a full Buffer through the Process.
        /// </summary>
        /// <returns>The archive result.</returns>
        public ArchiveResult ArchiveIncoming()
        {
            ArchiveResult result = Process.ArchiveIncoming();
            if (result.Succeeded)
                _archivesThroughProcess += 1;

            AssertArchiveAgreement();
            return result;
        }

        /// <summary>
        /// Ends the current Process, expiring the Utilities committed to it.
        /// </summary>
        /// <returns>How many commitments ended.</returns>
        public int CompleteProcess()
        {
            AssertArchiveAgreement();
            return Session.Economy.ExpireUtilitiesFor(Process.Configuration.ID);
        }

        /// <summary>
        /// Checks that every archive the Buffer committed was one the Process observed. The Buffer's
        /// own archive methods stay public for Compilation's seam, so this is the standing proof that
        /// no driver step reached past the Process and silently skipped the archive observers.
        /// </summary>
        private void AssertArchiveAgreement()
        {
            if (Process == null)
                return;

            int archived = 0;
            IReadOnlyList<BufferRecord> records = Process.Buffer.Records;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] is ArchiveRecord)
                    archived++;
            }

            if (archived != _archivesThroughProcess)
            {
                throw new InvalidOperationException(
                    "The Buffer committed " + archived + " archives but " + _archivesThroughProcess
                        + " went through the Process: something archived through the Buffer directly.");
            }
        }

        /// <summary>
        /// Whether a Process rule declares at least one EXECUTION-domain effect. A rule that declares
        /// none must not reach the engine: the scheduler gates Process-counter evidence on the rule's
        /// mere presence, so a COMPILATION-only rule would stamp counter evidence for a counter that
        /// does not exist.
        /// </summary>
        /// <param name="rule">The Process-rule instance, or null.</param>
        /// <returns>True when the rule declares an EXECUTION-domain effect.</returns>
        public static bool DeclaresExecutionEffect(ProcessRuleInstance rule)
        {
            if (rule == null)
                return false;

            IReadOnlyList<EffectDefinition> effects = rule.Definition.Effects;
            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index].PhaseDomain == PhaseDomain.Execution)
                    return true;
            }

            return false;
        }
    }
}
