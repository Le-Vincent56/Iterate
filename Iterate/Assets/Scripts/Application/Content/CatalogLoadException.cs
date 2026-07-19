using System;
using System.Collections.Generic;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Thrown when the catalog cannot be loaded. Carries the complete list of failures; the message
    /// summarizes the count and names the first failure's file and rule.
    /// </summary>
    public sealed class CatalogLoadException : Exception
    {
        /// <summary>
        /// The complete list of load failures.
        /// </summary>
        public IReadOnlyList<CatalogError> Errors { get; }

        public CatalogLoadException(IReadOnlyList<CatalogError> errors) : base(BuildMessage(errors))
        {
            Errors = errors ?? Array.Empty<CatalogError>();
        }

        /// <summary>
        /// Builds the exception message summarizing the failure count and naming the first failure.
        /// </summary>
        /// <param name="errors">The failure list.</param>
        /// <returns>The summary message.</returns>
        private static string BuildMessage(IReadOnlyList<CatalogError> errors)
        {
            if (errors == null || errors.Count == 0)
                return "The catalog failed to load.";

            CatalogError first = errors[0];
            return "The catalog failed to load with " + errors.Count + " error(s); first: "
                   + first.File + " / " + first.RuleName + ": " + first.Message;
        }
    }
}