namespace Iterate.Application.Logging
{
    /// <summary>
    /// Discriminates which slot of a <see cref="LogField"/> holds the field's value.
    /// </summary>
    public enum LogFieldKind
    {
        None = 0,
        String = 1,
        Int64 = 2,
        Double = 3,
        Bool = 4,
        Boxed = 5
    }
}