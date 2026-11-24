using System;
using System.Collections.Generic;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Answers queries over the same context the Session surface writes, and never writes it. Views
    /// are copied by one mapper each. Previews delegate to the Domain's own previews and change
    /// nothing. A Service previewed in a shop that does not offer Services is refused exactly as the
    /// Service itself would be.
    /// </summary>
    public sealed class SessionQueryService : ISessionQueries
    {
        private readonly SessionContext _context;

        /// <summary>
        /// The stage the Session stands in.
        /// </summary>
        public SessionStage Stage => _context.Stage;

        /// <summary>
        /// Why the Session ended; None while it runs.
        /// </summary>
        public SessionEndReason EndReason => _context.EndReason;

        /// <summary>
        /// Creates the queries over a Session surface's context.
        /// </summary>
        /// <param name="flow">The Session surface whose state is read.</param>
        /// <exception cref="ArgumentNullException">Thrown when the surface is null.</exception>
        public SessionQueryService(SessionFlow flow)
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));

            _context = flow.Context;
        }

        /// <summary>
        /// The Session as a whole.
        /// </summary>
        /// <returns>The view, or null when no Session is active.</returns>
        public SessionView Session()
        {
            return SessionViewMapper.Map(_context);
        }

        /// <summary>
        /// The Process in play.
        /// </summary>
        /// <returns>The view, or null outside a Process.</returns>
        public ProcessView Process()
        {
            return ProcessViewMapper.Map(_context);
        }

        /// <summary>
        /// The source and its compilation status.
        /// </summary>
        /// <returns>The view, or null outside the Build stage.</returns>
        public BuildView Build()
        {
            return BuildViewMapper.Map(_context);
        }

        /// <summary>
        /// The Branch being configured.
        /// </summary>
        /// <returns>The view, or null outside Branch configuration.</returns>
        public BranchView Branch()
        {
            return BranchViewMapper.Map(_context);
        }

        /// <summary>
        /// The open shop.
        /// </summary>
        /// <returns>The view, or null outside a shop.</returns>
        public ShopView Shop()
        {
            return ShopViewMapper.Map(_context);
        }

        /// <summary>
        /// The result under review.
        /// </summary>
        /// <returns>The view, or null outside Result Review.</returns>
        public ResultView Result()
        {
            return ResultViewMapper.Map(_context);
        }

        /// <summary>
        /// The rewards still to resolve.
        /// </summary>
        /// <returns>The view, or null outside the rewards stage.</returns>
        public RewardView Rewards()
        {
            return RewardViewMapper.Map(_context);
        }

        /// <summary>
        /// The route choice.
        /// </summary>
        /// <returns>The view, or null outside the route choice.</returns>
        public RouteView Routes()
        {
            return RouteViewMapper.Map(_context);
        }

        /// <summary>
        /// Previews buying the routine offer in a slot.
        /// </summary>
        /// <param name="slot">The slot.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        public PurchasePreview PreviewPurchase(int slot)
        {
            return _context.Shop?.PreviewPurchase(slot);
        }

        /// <summary>
        /// Previews a reroll.
        /// </summary>
        /// <returns>The preview, or null outside a shop.</returns>
        public RerollPreview PreviewReroll()
        {
            return _context.Shop?.PreviewReroll();
        }

        /// <summary>
        /// Previews attaching a Patch: a shop slot's Patch in a shop, or a granted Patch at rewards.
        /// </summary>
        /// <param name="slot">The shop slot; null at rewards.</param>
        /// <param name="grant">The grant; null in a shop.</param>
        /// <param name="host">The host Instruction.</param>
        /// <param name="socket">The host socket.</param>
        /// <returns>The preview, or null when the stage offers no such attachment.</returns>
        public PatchAttachmentPreview PreviewPatch(
            int? slot,
            InstanceID? grant,
            InstanceID host,
            int socket
        )
        {
            if (_context.Stage == SessionStage.Shop && slot.HasValue)
                return _context.Shop.PreviewPatchPurchase(slot.Value, host, socket);

            if (_context.Stage != SessionStage.Rewards || !grant.HasValue)
                return null;

            SessionState session = _context.Session;
            if (!session.Economy.TryGetGrant(grant.Value, out PatchGrant entry))
                return null;

            PatchAttachmentRequest request = new(
                entry.Definition,
                host,
                socket,
                0,
                entry.GrantIdentity,
                entry.Basis
            );

            return new PatchInstaller(session).Preview(request);
        }

        /// <summary>
        /// Previews buying and installing the Dependency in a slot.
        /// </summary>
        /// <param name="slot">The slot.</param>
        /// <param name="destroyFirst">The Dependencies to destroy first.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        public DependencyInstallationPreview PreviewDependency(int slot, IReadOnlyList<InstanceID> destroyFirst)
        {
            return _context.Shop?.PreviewDependencyPurchase(slot, destroyFirst);
        }

        /// <summary>
        /// Previews destroying an installed Dependency.
        /// </summary>
        /// <param name="dependency">The Dependency.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        public DependencyDestructionPreview PreviewDestroy(InstanceID dependency)
        {
            return _context.Shop == null ? null : new DependencyInstaller(_context.Session).PreviewDestroy(dependency);
        }

        /// <summary>
        /// Previews the delete Service on an owned item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        public ServicePreview PreviewDelete(InstanceID item)
        {
            if (_context.Shop == null)
                return null;

            return ServicesOffered() ? new RepositoryServices(_context.Session).PreviewDelete(item) : ServicesRefused();
        }

        /// <summary>
        /// Previews the duplicate Service on an owned item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        public ServicePreview PreviewDuplicate(InstanceID item)
        {
            if (_context.Shop == null)
                return null;

            return ServicesOffered() ? new RepositoryServices(_context.Session).PreviewDuplicate(item) : ServicesRefused();
        }

        /// <summary>
        /// Whether the open shop offers the item Services.
        /// </summary>
        /// <returns>True when it does.</returns>
        private bool ServicesOffered()
        {
            return _context.Shop.IsOpen && _context.Shop.Definition.ServicesEnabled;
        }

        /// <summary>
        /// The preview of a Service the shop does not offer.
        /// </summary>
        /// <returns>The refused preview, leaving the Tokens as they are.</returns>
        private ServicePreview ServicesRefused()
        {
            return new ServicePreview(ServiceRejection.ServicesNotOffered, 0, 0, _context.Session.Economy.Tokens.Balance);
        }
    }
}