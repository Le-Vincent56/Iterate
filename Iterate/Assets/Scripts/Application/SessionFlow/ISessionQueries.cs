using System.Collections.Generic;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Read-only access to the Session. Each view is a copy taken when it is asked for, and is null
    /// when the Session has no such state in its current stage. Each preview delegates to the
    /// operation's own preview, changes nothing, and is null outside the stage the operation belongs to.
    /// </summary>
    public interface ISessionQueries
    {
        /// <summary>
        /// The stage the Session stands in.
        /// </summary>
        SessionStage Stage { get; }

        /// <summary>
        /// Why the Session ended; None while it runs.
        /// </summary>
        SessionEndReason EndReason { get; }

        /// <summary>
        /// The Session as a whole.
        /// </summary>
        /// <returns>The view, or null when no Session is active.</returns>
        SessionView Session();

        /// <summary>
        /// The Process in play.
        /// </summary>
        /// <returns>The view, or null outside a Process.</returns>
        ProcessView Process();

        /// <summary>
        /// The source and its compilation status.
        /// </summary>
        /// <returns>The view, or null outside the Build stage.</returns>
        BuildView Build();

        /// <summary>
        /// The Branch being configured.
        /// </summary>
        /// <returns>The view, or null outside Branch configuration.</returns>
        BranchView Branch();

        /// <summary>
        /// The open shop.
        /// </summary>
        /// <returns>The view, or null outside a shop.</returns>
        ShopView Shop();

        /// <summary>
        /// The result under review.
        /// </summary>
        /// <returns>The view, or null outside Result Review.</returns>
        ResultView Result();

        /// <summary>
        /// The rewards still to resolve.
        /// </summary>
        /// <returns>The view, or null outside the rewards stage.</returns>
        RewardView Rewards();

        /// <summary>
        /// The route choice.
        /// </summary>
        /// <returns>The view, or null outside the route choice.</returns>
        RouteView Routes();

        /// <summary>
        /// Previews buying the routine offer in a slot.
        /// </summary>
        /// <param name="slot">The slot.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        PurchasePreview PreviewPurchase(int slot);

        /// <summary>
        /// Previews a reroll.
        /// </summary>
        /// <returns>The preview, or null outside a shop.</returns>
        RerollPreview PreviewReroll();

        /// <summary>
        /// Previews attaching a Patch: a shop slot's Patch in a shop, or a granted Patch at rewards.
        /// </summary>
        /// <param name="slot">The shop slot; null at rewards.</param>
        /// <param name="grant">The grant; null in a shop.</param>
        /// <param name="host">The host Instruction.</param>
        /// <param name="socket">The host socket.</param>
        /// <returns>The preview, or null when the stage offers no such attachment.</returns>
        PatchAttachmentPreview PreviewPatch(
            int? slot,
            InstanceID? grant,
            InstanceID host,
            int socket
        );

        /// <summary>
        /// Previews buying and installing the Dependency in a slot.
        /// </summary>
        /// <param name="slot">The slot.</param>
        /// <param name="destroyFirst">The Dependencies to destroy first.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        DependencyInstallationPreview PreviewDependency(int slot, IReadOnlyList<InstanceID> destroyFirst);

        /// <summary>
        /// Previews destroying an installed Dependency.
        /// </summary>
        /// <param name="dependency">The Dependency.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        DependencyDestructionPreview PreviewDestroy(InstanceID dependency);

        /// <summary>
        /// Previews the delete Service on an owned item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        ServicePreview PreviewDelete(InstanceID item);

        /// <summary>
        /// Previews the duplicate Service on an owned item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The preview, or null outside a shop.</returns>
        ServicePreview PreviewDuplicate(InstanceID item);
    }
}