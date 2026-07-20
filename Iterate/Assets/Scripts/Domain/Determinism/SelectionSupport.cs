using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Shared helpers for the selection strategies and the service pipeline.
    /// </summary>
    internal static class SelectionSupport
    {
        /// <summary>
        /// Extracts the candidate identities in their given canonical order into a new list.
        /// </summary>
        /// <param name="candidates">The candidates in canonical order.</param>
        /// <returns>A new list of the identities in canonical order.</returns>
        public static List<string> Identities(IReadOnlyList<CandidateEntry> candidates)
        {
            List<string> identities = new List<string>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
            {
                identities.Add(candidates[index].Identity);
            }

            return identities;
        }
        
        /// <summary>
        /// Returns a new list holding the Fisher–Yates permutation of the canonical identities, drawing
        /// swap indices from the per-decision stream (for i = n-1 down to 1: swap i with a draw in [0, i]).
        /// </summary>
        /// <param name="canonical">The identities in canonical order.</param>
        /// <param name="stream">The per-decision PCG stream.</param>
        /// <returns>A new permuted list; the input is not modified.</returns>
        public static List<string> FisherYates(IReadOnlyList<string> canonical, Pcg32 stream)
        {
            List<string> permuted = new List<string>(canonical);
            for (int index = permuted.Count - 1; index >= 1; index--)
            {
                uint swap = stream.NextUInt32((uint)(index + 1));
                (permuted[index], permuted[(int)swap]) = (permuted[(int)swap], permuted[index]);
            }

            return permuted;
        }
    }
}