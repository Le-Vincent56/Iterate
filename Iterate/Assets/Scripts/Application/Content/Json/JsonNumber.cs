namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A JSON number. <see cref="IsInteger"/> reflects the lexical form: a number with no fraction or
    /// exponent carries its value in <see cref="IntegerValue"/>; otherwise the value is in
    /// <see cref="DoubleValue"/>.
    /// </summary>
    /// <param name="Line">The 1-indexed source line of the number's first character.</param>
    /// <param name="Column">The 1-indexed source column of the number's first character.</param>
    /// <param name="IsInteger">Whether the number was written without a fraction or exponent.</param>
    /// <param name="IntegerValue">The integer value when <paramref name="IsInteger"/>; otherwise zero.</param>
    /// <param name="DoubleValue">The value as a double; equals the integer value for integer forms.</param>
    public sealed record JsonNumber(
        int Line,
        int Column,
        bool IsInteger,
        long IntegerValue,
        double DoubleValue
    ) : JsonValue(Line, Column);
}