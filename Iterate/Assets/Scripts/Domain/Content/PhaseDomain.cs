namespace Iterate.Domain.Content
{
    /// <summary>
    /// The phase domain that owns an effect's moment. Serialized in JSON as EXECUTION, COMPILATION,
    /// BUILD_INTERACTION, PROCESS_SETUP, DISCLOSURE.
    /// </summary>
    public enum PhaseDomain
    {
        Execution,
        Compilation,
        BuildInteraction,
        ProcessSetup,
        Disclosure
    }
}