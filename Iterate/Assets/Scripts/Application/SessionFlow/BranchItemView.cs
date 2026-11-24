using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One Repository item as Branch configuration sees it.
    /// </summary>
    /// <param name="Instance">The item's instance identity.</param>
    /// <param name="DefinitionID">Its definition.</param>
    /// <param name="State">Its eligibility, selection and constraint flags.</param>
    public sealed record BranchItemView(InstanceID Instance, string DefinitionID, BranchInstanceState State);
}