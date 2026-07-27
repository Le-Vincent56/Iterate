namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The canon-pinned resolution order for the named current Score reactions, bound over the
    /// catalog's WB surrogate keys (CAB-EVT-532): host-local FEEDBACK PATCH resolves before OUTPUT
    /// CACHE, which resolves before OUTPUT PIPELINE. This order applies only to these named current
    /// effects while their interaction stays commutative (CAB-EVT-533) — it is not a universal
    /// category priority, and any noncommutative future reaction requires canon revision before
    /// adoption. All other effects share the trailing rank and order by stable instance identity.
    /// </summary>
    public static class ReactionPrecedence
    {
        /// <summary>
        /// Returns the declared precedence rank for a definition identity: lower resolves first.
        /// </summary>
        /// <param name="definitionID">The definition's surrogate-key identity.</param>
        /// <returns>0 for FEEDBACK PATCH, 1 for OUTPUT CACHE, 2 for OUTPUT PIPELINE, 3 for every other effect.</returns>
        public static int Rank(string definitionID)
        {
            switch (definitionID)
            {
                case "WB-PAT-005":
                    return 0;

                case "WB-DEP-005":
                    return 1;

                case "WB-DEP-011":
                    return 2;

                default:
                    return 3;
            }
        }
    }
}