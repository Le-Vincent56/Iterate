using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Session's Active Branch in two layers: a confirmed eligible set that survives between
    /// Processes, and a mutable draft that exists only between beginning a configuration and confirming
    /// or discarding it. Over-capacity is a legal, representable draft the player edits their way out
    /// of — nothing is ever removed automatically — and a deletion from the Repository never rewrites
    /// what was confirmed: the snapshot handed to <see cref="BeginConfiguration"/> is the authority for
    /// what still exists, and anything the confirmed set has lost is reported on the next confirmation.
    /// </summary>
    public sealed class ActiveBranch
    {
        private readonly List<BranchConfirmationRecord> _records = new();

        private readonly HashSet<InstanceID> _draft = new();

        private readonly List<InstanceID> _dropped = new();

        private BranchConstraints _constraints;

        private RepositorySnapshot _snapshot;

        private Dictionary<InstanceID, RepositoryEntry> _available;

        /// <summary>
        /// The confirmed eligible set, or null before the first confirmation.
        /// </summary>
        public ActiveBranchConfiguration Confirmed { get; private set; }

        /// <summary>
        /// Whether a draft configuration is open.
        /// </summary>
        public bool HasConfigurationInProgress { get; private set; }

        /// <summary>
        /// How many instances the open draft selects; zero when no configuration is open.
        /// </summary>
        public int DraftCount => _draft.Count;

        /// <summary>
        /// The confirmation records in the order they occurred.
        /// </summary>
        public IReadOnlyList<BranchConfirmationRecord> Records => _records;

        /// <summary>
        /// Opens a draft configuration. The first one seeds every eligible entry the Repository holds,
        /// minus Quarantined content, which the system will not add for the player; a later one restores
        /// the previous Branch exactly — Quarantined content included, for the player to resolve —
        /// intersected with what the Repository still holds, leaving newly acquired entries unselected.
        /// Required content is resolved through the snapshot and included either way.
        /// </summary>
        /// <param name="constraints">The constraints this configuration is judged against.</param>
        /// <param name="repository">The Repository snapshot; the authority for what exists.</param>
        /// <exception cref="InvalidOperationException">Thrown when a configuration is already open.</exception>
        public void BeginConfiguration(BranchConstraints constraints, RepositorySnapshot repository)
        {
            if (HasConfigurationInProgress)
                throw new InvalidOperationException("A Branch configuration is already in progress.");

            _constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
            _snapshot = repository ?? throw new ArgumentNullException(nameof(repository));

            _available = new Dictionary<InstanceID, RepositoryEntry>();
            for (int index = 0; index < repository.Entries.Count; index++)
            {
                RepositoryEntry entry = repository.Entries[index];
                _available.Add(entry.Item.InstanceID, entry);
            }

            _draft.Clear();
            _dropped.Clear();

            if (Confirmed == null)
                SeedFromRepository();
            else
                SeedFromConfirmed();

            IncludeRequired();
            HasConfigurationInProgress = true;
        }

        /// <summary>
        /// Selects an instance into the draft.
        /// </summary>
        /// <param name="id">The instance to include.</param>
        /// <returns>The edit result.</returns>
        public BranchEditResult Include(InstanceID id)
        {
            if (!HasConfigurationInProgress)
                return BranchEditResult.Rejected(BranchEditRejection.NoConfigurationInProgress);

            if (!_available.TryGetValue(id, out RepositoryEntry entry))
                return BranchEditResult.Rejected(BranchEditRejection.NotARepositoryItem);

            if (!IsEligibleKind(entry.Item.Kind))
                return BranchEditResult.Rejected(BranchEditRejection.NotEligibleKind);

            if (Names(_constraints.Quarantined, entry.Item.DefinitionID))
                return BranchEditResult.Rejected(BranchEditRejection.QuarantinedCannotBeIncluded);

            _draft.Add(id);
            return BranchEditResult.Success;
        }

        /// <summary>
        /// Removes an instance from the draft.
        /// </summary>
        /// <param name="id">The instance to exclude.</param>
        /// <returns>The edit result.</returns>
        public BranchEditResult Exclude(InstanceID id)
        {
            if (!HasConfigurationInProgress)
                return BranchEditResult.Rejected(BranchEditRejection.NoConfigurationInProgress);

            if (!_available.TryGetValue(id, out RepositoryEntry entry))
                return BranchEditResult.Rejected(BranchEditRejection.NotARepositoryItem);

            if (IsRequiredInstance(entry))
                return BranchEditResult.Rejected(BranchEditRejection.RequiredCannotBeExcluded);

            _draft.Remove(id);
            return BranchEditResult.Success;
        }

        /// <summary>
        /// Reports everything wrong with the open draft at once.
        /// </summary>
        /// <returns>The validation; a valid one when the draft may be confirmed.</returns>
        public BranchValidation Validate()
        {
            List<string> missing = new();
            for (int index = 0; index < _constraints.Required.Count; index++)
            {
                string definitionID = _constraints.Required[index];
                if (!DraftContainsContent(definitionID))
                    missing.Add(definitionID);
            }

            List<InstanceID> quarantined = new();
            foreach (InstanceID id in _draft)
            {
                if (_available.TryGetValue(id, out RepositoryEntry entry)
                    && Names(_constraints.Quarantined, entry.Item.DefinitionID))
                {
                    quarantined.Add(id);
                }
            }

            return new BranchValidation(
                _draft.Count,
                _constraints.Capacity,
                _draft.Count > _constraints.Capacity,
                missing,
                quarantined
            );
        }

        /// <summary>
        /// Locks the draft as the confirmed eligible set when it validates.
        /// </summary>
        /// <returns>The confirmation result.</returns>
        public BranchConfirmationResult Confirm()
        {
            if (!HasConfigurationInProgress)
            {
                return BranchConfirmationResult.Rejected(new BranchValidation(
                    0,
                    0,
                    false,
                    Array.Empty<string>(),
                    Array.Empty<InstanceID>()
                ));
            }

            BranchValidation validation = Validate();
            if (!validation.IsValid)
                return BranchConfirmationResult.Rejected(validation);

            List<InstanceID> eligible = new(_draft.Count);
            for (int index = 0; index < _snapshot.Entries.Count; index++)
            {
                InstanceID id = _snapshot.Entries[index].Item.InstanceID;
                if (_draft.Contains(id))
                    eligible.Add(id);
            }

            BranchConfirmationRecord record = new(eligible, new List<InstanceID>(_dropped));
            Confirmed = new ActiveBranchConfiguration(eligible);
            _records.Add(record);
            CloseConfiguration();
            return BranchConfirmationResult.Success(validation, record);
        }

        /// <summary>
        /// Drops the open draft. The confirmed set is untouched, so leaving configuration never
        /// confirms anything.
        /// </summary>
        public void Discard()
        {
            CloseConfiguration();
        }

        /// <summary>
        /// Derives an instance's state within the open configuration.
        /// </summary>
        /// <param name="id">The instance to describe.</param>
        /// <returns>The derived flags; None when the Repository snapshot does not hold the instance.</returns>
        public BranchInstanceState StateOf(InstanceID id)
        {
            if (!HasConfigurationInProgress || !_available.TryGetValue(id, out RepositoryEntry entry))
                return BranchInstanceState.None;

            BranchInstanceState state = BranchInstanceState.None;
            if (IsEligibleKind(entry.Item.Kind))
                state |= BranchInstanceState.Eligible;

            if (_draft.Contains(id))
                state |= BranchInstanceState.Selected;

            if (IsRequiredInstance(entry))
                state |= BranchInstanceState.Required;

            if (Names(_constraints.Quarantined, entry.Item.DefinitionID))
                state |= BranchInstanceState.Quarantined;

            if (HasAnyTag(entry.Item.Tags, _constraints.RecommendedTags))
                state |= BranchInstanceState.Recommended;

            if (HasAnyTag(entry.Item.Tags, _constraints.CautionTags))
                state |= BranchInstanceState.Caution;

            return state;
        }

        /// <summary>
        /// Takes an immutable view of the confirmed set and the open draft.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public ActiveBranchSnapshot Snapshot()
        {
            List<InstanceID> draft = new(_draft.Count);
            foreach (InstanceID id in _draft)
            {
                draft.Add(id);
            }

            return new ActiveBranchSnapshot(Confirmed, draft, HasConfigurationInProgress);
        }

        /// <summary>
        /// Seeds the first configuration from every eligible entry the Repository holds, skipping
        /// Quarantined content, which the system does not add on the player's behalf.
        /// </summary>
        private void SeedFromRepository()
        {
            for (int index = 0; index < _snapshot.Entries.Count; index++)
            {
                RepositoryEntry entry = _snapshot.Entries[index];
                if (!IsEligibleKind(entry.Item.Kind))
                    continue;

                if (Names(_constraints.Quarantined, entry.Item.DefinitionID))
                    continue;

                _draft.Add(entry.Item.InstanceID);
            }
        }

        /// <summary>
        /// Seeds a later configuration from the confirmed set intersected with what the Repository
        /// still holds, recording anything it has lost. The prior Branch is restored exactly —
        /// Quarantined content included, which the validation then reports for the player to resolve.
        /// </summary>
        private void SeedFromConfirmed()
        {
            for (int index = 0; index < Confirmed.Eligible.Count; index++)
            {
                InstanceID id = Confirmed.Eligible[index];
                if (_available.ContainsKey(id))
                    _draft.Add(id);
                else
                    _dropped.Add(id);
            }
        }

        /// <summary>
        /// Includes the lowest-suffix held instance of each Required content ID, matching the
        /// Repository's own resolution rule.
        /// </summary>
        private void IncludeRequired()
        {
            for (int index = 0; index < _constraints.Required.Count; index++)
            {
                string definitionID = _constraints.Required[index];
                if (DraftContainsContent(definitionID))
                    continue;

                for (int entryIndex = 0; entryIndex < _snapshot.Entries.Count; entryIndex++)
                {
                    RepositoryEntry entry = _snapshot.Entries[entryIndex];
                    if (!string.Equals(entry.Item.DefinitionID, definitionID, StringComparison.Ordinal))
                        continue;

                    _draft.Add(entry.Item.InstanceID);
                    break;
                }
            }
        }

        /// <summary>
        /// Closes the open configuration and releases its draft state.
        /// </summary>
        private void CloseConfiguration()
        {
            HasConfigurationInProgress = false;
            _draft.Clear();
            _dropped.Clear();
            _available = new Dictionary<InstanceID, RepositoryEntry>();
        }

        /// <summary>
        /// Whether the draft already selects an instance of the given content.
        /// </summary>
        /// <param name="definitionID">The content's stable identity.</param>
        /// <returns>True when the draft satisfies that content.</returns>
        private bool DraftContainsContent(string definitionID)
        {
            foreach (InstanceID id in _draft)
            {
                if (_available.TryGetValue(id, out RepositoryEntry entry)
                    && string.Equals(entry.Item.DefinitionID, definitionID, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether an entry is the instance currently satisfying a Required content rule. Only the
        /// satisfying instance is protected, so a second copy of the same content stays excludable.
        /// </summary>
        /// <param name="entry">The entry to test.</param>
        /// <returns>True when excluding the entry would break a Required rule.</returns>
        private bool IsRequiredInstance(RepositoryEntry entry)
        {
            if (!Names(_constraints.Required, entry.Item.DefinitionID))
                return false;

            if (!_draft.Contains(entry.Item.InstanceID))
                return false;

            int selected = 0;
            foreach (InstanceID id in _draft)
            {
                if (_available.TryGetValue(id, out RepositoryEntry candidate)
                    && string.Equals(candidate.Item.DefinitionID, entry.Item.DefinitionID, StringComparison.Ordinal))
                {
                    selected++;
                }
            }

            return selected <= 1;
        }

        /// <summary>
        /// Whether a content ID appears in a constraint list.
        /// </summary>
        /// <param name="ids">The constraint list.</param>
        /// <param name="definitionID">The content's stable identity.</param>
        /// <returns>True when the list names the content.</returns>
        private static bool Names(IReadOnlyList<string> ids, string definitionID)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (string.Equals(ids[index], definitionID, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether an item carries any of the given tags.
        /// </summary>
        /// <param name="tags">The item's tags.</param>
        /// <param name="wanted">The tags to look for.</param>
        /// <returns>True when at least one tag matches.</returns>
        private static bool HasAnyTag(IReadOnlyList<string> tags, IReadOnlyList<string> wanted)
        {
            for (int index = 0; index < wanted.Count; index++)
            {
                for (int tagIndex = 0; tagIndex < tags.Count; tagIndex++)
                {
                    if (string.Equals(tags[tagIndex], wanted[index], StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether an item kind may be selected for a Branch. Every Repository item kind is eligible in
        /// this slice; the gate exists so a later ineligible kind has one place to be refused.
        /// </summary>
        /// <param name="kind">The item kind.</param>
        /// <returns>True when the kind is Branch-eligible.</returns>
        private static bool IsEligibleKind(RepositoryItemKind kind)
        {
            return kind is RepositoryItemKind.Instruction 
                or RepositoryItemKind.Structure 
                or RepositoryItemKind.Directive;
        }
    }
}