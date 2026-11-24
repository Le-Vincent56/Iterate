using System.Collections.Generic;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the Branch being configured into a <see cref="BranchView"/>.
    /// </summary>
    internal static class BranchViewMapper
    {
        /// <summary>
        /// Maps the context's Branch configuration.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null when no configuration is in progress.</returns>
        internal static BranchView Map(SessionContext context)
        {
            if (context.Stage != SessionStage.BranchConfiguration)
                return null;

            SessionState session = context.Session;
            ActiveBranch branch = session.ActiveBranch;
            BranchValidation validation = branch.Validate();
            IReadOnlyList<RepositoryEntry> entries = session.Repository.Entries;
            List<BranchItemView> items = new(entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                RepositoryItem item = entries[index].Item;
                items.Add(new BranchItemView(item.InstanceID, item.DefinitionID, branch.StateOf(item.InstanceID)));
            }

            return new BranchView(
                validation.SelectedCount,
                validation.Capacity,
                validation.IsOverCapacity,
                validation.IsValid,
                new List<string>(validation.MissingRequired),
                items
            );
        }
    }
}