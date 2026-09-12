namespace Iterate.Domain.Content
{
    /// <summary>
    /// The kind of catalog file a manifest entry declares. Maps one-to-one onto the manifest JSON
    /// category tokens (PARAMETERS, INSTRUCTION, STRUCTURE, DIRECTIVE, DEPENDENCY, PATCH, UTILITY,
    /// PROCESS_RULE, CORE, PROCESS_CONFIGURATION, SHOP, POOL, REWARD_PACKAGE, ROUTE, STARTER_ARCHETYPE,
    /// SYSTEM); the freezer maps the content kinds onto <see cref="ContentCategory"/>.
    /// </summary>
    public enum CatalogFileKind
    {
        Parameters,
        Instruction,
        Structure,
        Directive,
        Dependency,
        Patch,
        Utility,
        ProcessRule,
        Core,
        ProcessConfiguration,
        Shop,
        Pool,
        RewardPackage,
        Route,
        StarterArchetype,
        System
    }
}