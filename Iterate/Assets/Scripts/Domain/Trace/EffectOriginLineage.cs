using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The branch-local origin chain of an effect: the root-first list of instance identities that
    /// created it, each new value copying its parent's entries and appending one creator. The parent
    /// value is never mutated by an append, so a chain shared across branches stays independent. The
    /// containment test answers the origin lock — whether a given instance already appears upstream.
    /// Equality is structural over the entries so two independently built equal chains compare equal.
    /// </summary>
    public sealed record EffectOriginLineage
    {
        /// <summary>
        /// The empty lineage carried by ordinary effects with no recorded creator chain.
        /// </summary>
        public static EffectOriginLineage Empty { get; } = new EffectOriginLineage(Array.Empty<InstanceID>());

        /// <summary>
        /// The creator identities in root-first order; never null, possibly empty.
        /// </summary>
        public IReadOnlyList<InstanceID> Entries { get; }

        /// <summary>
        /// The chain length, equal to the entry count.
        /// </summary>
        public int Depth => Entries.Count;

        private EffectOriginLineage(IReadOnlyList<InstanceID> entries)
        {
            Entries = entries;
        }

        /// <summary>
        /// Produces a new lineage whose entries are this lineage's entries followed by the creator,
        /// leaving this value unchanged.
        /// </summary>
        /// <param name="creator">The creating instance identity to append at the tail.</param>
        /// <returns>A new root-first lineage one entry longer than this one.</returns>
        public EffectOriginLineage Append(InstanceID creator)
        {
            InstanceID[] appended = new InstanceID[Entries.Count + 1];
            for (int index = 0; index < Entries.Count; index++)
                appended[index] = Entries[index];
            appended[Entries.Count] = creator;

            return new EffectOriginLineage(appended);
        }

        /// <summary>
        /// Tests whether the given instance already appears anywhere in the chain.
        /// </summary>
        /// <param name="instance">The instance identity to search for.</param>
        /// <returns>True when the instance appears in the entries.</returns>
        public bool Contains(InstanceID instance)
        {
            for (int index = 0; index < Entries.Count; index++)
            {
                if (Entries[index] == instance)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Structural value equality over the ordered entries.
        /// </summary>
        /// <param name="other">The lineage to compare against.</param>
        /// <returns>True when both chains have identical entries in the same order.</returns>
        public bool Equals(EffectOriginLineage other)
        {
            return other != null && TraceEquality.ListEquals(Entries, other.Entries);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the chain depth.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => Depth;
    }
}