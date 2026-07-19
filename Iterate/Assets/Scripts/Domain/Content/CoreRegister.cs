namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Core register an operation reads or writes. Serialized in JSON as VALUE, SIGNAL, SCORE.
    /// </summary>
    public enum CoreRegister
    {
        Value,
        Signal,
        Score
    }
}