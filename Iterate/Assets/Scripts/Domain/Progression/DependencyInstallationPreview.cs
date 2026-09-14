using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What an installation would do to RAM and Tokens, computed by the same validation the commit
    /// runs. RAM is shown before, freed, and after, because a composite that destroys to make room is
    /// only legible as all three.
    /// </summary>
    /// <param name="Rejection">Why it would be refused; None when it would succeed.</param>
    /// <param name="RAMBefore">The usage before.</param>
    /// <param name="RAMFreed">The usage the destructions would release.</param>
    /// <param name="RAMAfter">The usage after destroying and installing.</param>
    /// <param name="Capacity">The rack capacity the result is measured against.</param>
    /// <param name="Refunds">The Tokens the destructions would refund.</param>
    /// <param name="Price">The Tokens the installation costs.</param>
    /// <param name="TokensBefore">The balance before.</param>
    /// <param name="TokensAfter">The balance after, when it would succeed.</param>
    public sealed record DependencyInstallationPreview(
        DependencyRejection Rejection,
        int RAMBefore,
        int RAMFreed,
        int RAMAfter,
        int Capacity,
        int Refunds,
        int Price,
        TokenAmount TokensBefore,
        TokenAmount TokensAfter
    );
}