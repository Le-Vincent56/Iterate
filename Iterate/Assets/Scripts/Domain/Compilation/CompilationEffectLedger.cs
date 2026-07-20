using System.Collections.Generic;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The Process-scoped ledger of consumed first-qualifying compilation-effect allowances, keyed by
    /// per-source-instance key. Owned by the Build state; the resolver reads it but never mutates it —
    /// consumption happens only at a successful commit.
    /// </summary>
    public sealed class CompilationEffectLedger
    {
        private readonly HashSet<string> _consumed = new HashSet<string>();

        /// <summary>
        /// Whether a source key's allowance has been consumed this Process.
        /// </summary>
        /// <param name="sourceKey">The per-source-instance key.</param>
        /// <returns>True when the key has been marked consumed.</returns>
        public bool IsConsumed(string sourceKey)
        {
            return _consumed.Contains(sourceKey);
        }

        /// <summary>
        /// Marks a source key's allowance consumed for the rest of the Process.
        /// </summary>
        /// <param name="sourceKey">The per-source-instance key.</param>
        public void MarkConsumed(string sourceKey)
        {
            _consumed.Add(sourceKey);
        }
    }
}