using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// One component's identity for reproduction: its display name and its revision identity. Both are
    /// required — a display name without a revision identity is insufficient to reproduce a run.
    /// </summary>
    public readonly record struct RevisionStamp
    {
        /// <summary>
        /// The component's display name; never empty.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The component's revision identity; never empty.
        /// </summary>
        public string Revision { get; }

        public RevisionStamp(string name, string revision)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A revision stamp requires a name.", nameof(name));
            
            if (string.IsNullOrEmpty(revision))
                throw new ArgumentException("A revision stamp requires a revision identity.", nameof(revision));

            Name = name;
            Revision = revision;
        }
    }
}