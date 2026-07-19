using System;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// Thrown when the catalog JSON reader encounters a strictness or syntax violation. Carries the
    /// 1-indexed line and column of the offending position.
    /// </summary>
    public sealed class CatalogJsonParseException : Exception
    {
        /// <summary>
        /// The 1-indexed source line of the offending position.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// The 1-indexed source column of the offending position.
        /// </summary>
        public int Column { get; }

        public CatalogJsonParseException(int line, int column, string message)
            : base("JSON parse error at line " + line + ", column " + column + ": " + message)
        {
            Line = line;
            Column = column;
        }
    }
}