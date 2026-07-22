namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The canon-pinned resolution order for the named current Score reactions, bound over the
    /// catalog's WB surrogate keys: OUTPUT CACHE resolves before OUTPUT PIPELINE;
    /// FEEDBACK PATCH precedes both when Patches land). This order applies to the named current
    /// effects only and any noncommutative future reaction requires canon revision
    /// before adoption. All other effects share one rank and  order by stable instance identity.
    ///
    /// </summary>
    public static class ReactionPrecedence
    {
        /// <summary>
        /// Returns the declared precedence rank for a definition identity: lower resolves first.
        /// </summary>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <returns>0 for OUTPUT CACHE, 1 for OUTPUT PIPELINE, 2 for every other effect.</returns>
        public static int Rank(string definitionID)
        {
            switch (definitionID)
            {
                case "WB-DEP-005":
                    return 0;

                case "WB-DEP-011":
                    return 1;

                default:
                    return 2;
            }
        }
    }
}