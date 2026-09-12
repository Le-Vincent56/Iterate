namespace Iterate.Domain.Content
{
    /// <summary>
    /// How a Process loads its Instruction Buffer: a scripted onboarding Process names the exact
    /// content and the execution each arrival follows; every other Process draws from the confirmed
    /// Active Branch. Serialized in JSON as SCRIPTED, DRAWN.
    /// </summary>
    public enum BufferLoadPolicy
    {
        Scripted,
        Drawn
    }
}