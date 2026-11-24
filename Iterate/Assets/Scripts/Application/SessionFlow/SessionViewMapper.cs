using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the Session's economy and possessions into a <see cref="SessionView"/>.
    /// </summary>
    internal static class SessionViewMapper
    {
        /// <summary>
        /// Maps the context's Session.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null when no Session is active.</returns>
        internal static SessionView Map(SessionContext context)
        {
            SessionState session = context.Session;
            if (session == null)
                return null;

            IReadOnlyList<RepositoryEntry> entries = session.Repository.Entries;
            List<RepositoryItemView> repository = new(entries.Count);
            for (int index = 0; index < entries.Count; index++)
                repository.Add(new RepositoryItemView(entries[index].Item.InstanceID, entries[index].Item.DefinitionID, entries[index].Suffix));

            DependencyRack rack = session.Economy.Dependencies;
            IReadOnlyList<DependencyInstance> installed = rack.AllInstalled;
            List<DependencyView> dependencies = new(installed.Count);
            for (int index = 0; index < installed.Count; index++)
            {
                DependencyInstance dependency = installed[index];
                dependencies.Add(new DependencyView(dependency.InstanceID, dependency.Definition.ID.Value, dependency.Definition.RAM, dependency == rack.Starter));
            }

            return new SessionView(
                context.Stage,
                session.Economy.Tokens.Balance.Value,
                rack.Capacity,
                rack.Usage,
                repository,
                dependencies,
                context.Plan.CommittedRoute
            );
        }
    }
}