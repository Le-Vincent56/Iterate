using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Session's collection of unique item instances. Two copies of one definition are two
    /// entries with their own identities and their own acquisition-order suffixes; a suffix is
    /// allocated per definition, from one, and is never renumbered or reissued after a deletion, so
    /// the number the player learned for an item stays that item's number for the whole Session.
    /// Deletion is permanent for the Session. Legal-play failures are typed rejections that change
    /// nothing; exceptions are reserved for contract violations.
    /// </summary>
    public sealed class Repository
    {
        private readonly InstanceIDSource _instanceIDs;

        private readonly List<RepositoryEntry> _entries = new();

        private readonly Dictionary<InstanceID, RepositoryEntry> _byInstance = new();

        private readonly Dictionary<string, int> _lastSuffix = new(StringComparer.Ordinal);

        private readonly List<RepositoryRecord> _records = new();

        /// <summary>
        /// The entries in acquisition order.
        /// </summary>
        public IReadOnlyList<RepositoryEntry> Entries => _entries;

        /// <summary>
        /// The transaction records in the order they occurred.
        /// </summary>
        public IReadOnlyList<RepositoryRecord> Records => _records;

        public Repository(InstanceIDSource instanceIDs)
        {
            _instanceIDs = instanceIDs ?? throw new ArgumentNullException(nameof(instanceIDs));
        }

        /// <summary>
        /// Acquires one instance of a content definition, allocating an identity and the next unused
        /// suffix for that definition. A definition that is not one of the three item kinds is
        /// rejected before anything is allocated.
        /// </summary>
        /// <param name="definition">The content definition to acquire.</param>
        /// <param name="origin">How the item is being acquired.</param>
        /// <returns>The acquisition result.</returns>
        public AcquisitionResult Acquire(ContentDefinition definition, AcquisitionOrigin origin)
        {
            if (definition == null || !IsItem(definition.Category))
                return AcquisitionResult.Rejected(RepositoryRejection.NotAnItem);

            InstanceID id = _instanceIDs.Next();
            RepositoryItem item = RepositoryItem.From(definition, id);
            int suffix = AllocateSuffix(item.DefinitionID);
            RepositoryEntry entry = new(item, origin, origin == AcquisitionOrigin.Starter, suffix);

            _entries.Add(entry);
            _byInstance.Add(id, entry);

            AcquisitionRecord record = new(id, item.DefinitionID, origin, suffix);
            _records.Add(record);
            return AcquisitionResult.Success(entry, record);
        }

        /// <summary>
        /// Deletes one entry permanently for the Session. Starter entries and unknown identities are
        /// rejected; the freed suffix is not reused.
        /// </summary>
        /// <param name="id">The instance to delete.</param>
        /// <returns>The deletion result.</returns>
        public DeletionResult Delete(InstanceID id)
        {
            if (!_byInstance.TryGetValue(id, out RepositoryEntry entry))
                return DeletionResult.Rejected(RepositoryRejection.UnknownInstance);

            if (entry.IsStarterProtected)
                return DeletionResult.Rejected(RepositoryRejection.StarterProtected);

            _entries.Remove(entry);
            _byInstance.Remove(id);

            DeletionRecord record = new(id, entry.Item.DefinitionID, entry.Suffix);
            _records.Add(record);
            return DeletionResult.Success(record);
        }

        /// <summary>
        /// Swaps an entry's item for a derived record under the same identity and definition, keeping
        /// the entry's origin, protection and suffix. The Repository knows nothing about what makes
        /// the record derived — sockets and Patches belong elsewhere.
        /// </summary>
        /// <param name="id">The instance whose item is being replaced.</param>
        /// <param name="derived">The derived item, which must carry the same identity and definition.</param>
        /// <returns>The replacement result.</returns>
        public ReplacementResult Replace(InstanceID id, RepositoryItem derived)
        {
            if (!_byInstance.TryGetValue(id, out RepositoryEntry entry))
                return ReplacementResult.Rejected(RepositoryRejection.UnknownInstance);

            if (derived == null
                || derived.InstanceID != id
                || !string.Equals(derived.DefinitionID, entry.Item.DefinitionID, StringComparison.Ordinal))
            {
                return ReplacementResult.Rejected(RepositoryRejection.DefinitionMismatch);
            }

            RepositoryEntry replaced = entry with { Item = derived };
            _entries[_entries.IndexOf(entry)] = replaced;
            _byInstance[id] = replaced;

            ReplacementRecord record = new(id, replaced.Item.DefinitionID);
            _records.Add(record);
            return ReplacementResult.Success(record);
        }

        /// <summary>
        /// Looks up an entry by instance identity.
        /// </summary>
        /// <param name="id">The instance identity.</param>
        /// <param name="entry">The found entry, or null when absent.</param>
        /// <returns>True when the identity resolves to an entry.</returns>
        public bool TryGet(InstanceID id, out RepositoryEntry entry) => _byInstance.TryGetValue(id, out entry);

        /// <summary>
        /// Whether the Repository holds the given instance.
        /// </summary>
        /// <param name="id">The instance identity.</param>
        /// <returns>True when the instance is held.</returns>
        public bool Contains(InstanceID id) => _byInstance.ContainsKey(id);

        /// <summary>
        /// The entries of one definition, in acquisition order and therefore in suffix order.
        /// </summary>
        /// <param name="definitionID">The definition's stable identity.</param>
        /// <returns>That definition's entries; empty when none are held.</returns>
        public IReadOnlyList<RepositoryEntry> EntriesOf(string definitionID)
        {
            List<RepositoryEntry> matches = new();
            for (int index = 0; index < _entries.Count; index++)
            {
                RepositoryEntry entry = _entries[index];
                if (string.Equals(entry.Item.DefinitionID, definitionID, StringComparison.Ordinal))
                    matches.Add(entry);
            }

            return matches;
        }

        /// <summary>
        /// Resolves one held instance of a definition: the lowest-suffix entry not already spoken for.
        /// This is the single implementation of that rule — archetype seeding, Required Branch
        /// content, a Process's pre-installed source, scripted arrivals and guaranteed exposure all
        /// call it rather than repeating the scan.
        /// </summary>
        /// <param name="definitionID">The definition to resolve.</param>
        /// <param name="excluded">Instances already spoken for, which this call must skip.</param>
        /// <param name="entry">The resolved entry, or null when every copy is excluded or none is held.</param>
        /// <returns>True when an entry was resolved.</returns>
        public bool TryResolveContent(
            string definitionID,
            IReadOnlyCollection<InstanceID> excluded,
            out RepositoryEntry entry
        )
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                RepositoryEntry candidate = _entries[index];
                if (!string.Equals(candidate.Item.DefinitionID, definitionID, StringComparison.Ordinal))
                    continue;

                if (IsExcluded(excluded, candidate.Item.InstanceID))
                    continue;

                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Takes an immutable view of the entries in acquisition order.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public RepositorySnapshot Snapshot()
        {
            return new RepositorySnapshot(new List<RepositoryEntry>(_entries));
        }

        /// <summary>
        /// Whether a content category is one of the three Repository item kinds.
        /// </summary>
        /// <param name="category">The content category.</param>
        /// <returns>True when content of that category is a Repository item.</returns>
        private static bool IsItem(ContentCategory category)
        {
            return category is ContentCategory.Instruction 
                or ContentCategory.Structure 
                or ContentCategory.Directive;
        }

        /// <summary>
        /// Whether an instance appears in the exclusion set.
        /// </summary>
        /// <param name="excluded">The exclusion set; may be null.</param>
        /// <param name="id">The instance identity to test.</param>
        /// <returns>True when the instance is excluded.</returns>
        private static bool IsExcluded(IReadOnlyCollection<InstanceID> excluded, InstanceID id)
        {
            if (excluded == null)
                return false;

            foreach (InstanceID candidate in excluded)
            {
                if (candidate == id)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Allocates the next unused suffix for a definition. The counter only ever rises, so a
        /// deletion never frees a number for reuse.
        /// </summary>
        /// <param name="definitionID">The definition's stable identity.</param>
        /// <returns>The allocated suffix.</returns>
        private int AllocateSuffix(string definitionID)
        {
            _lastSuffix.TryGetValue(definitionID, out int lastIssued);
            int suffix = lastIssued + 1;
            _lastSuffix[definitionID] = suffix;
            return suffix;
        }
    }
}