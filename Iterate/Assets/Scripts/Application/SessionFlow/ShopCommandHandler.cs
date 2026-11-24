using System;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Applies the shop-stage operations through the open shop, including the two item Services, and
    /// leaves it. Patch and Dependency
    /// purchases install as they are bought, so the shop never holds an unresolved installation and
    /// leaving is never blocked.
    /// </summary>
    internal sealed class ShopCommandHandler
    {
        private const ChangedAreas PurchaseAreas = ChangedAreas.Shop | ChangedAreas.Economy | ChangedAreas.Repository;
        private readonly StageTransitions _transitions;

        /// <summary>
        /// Creates the handler over the stage transitions.
        /// </summary>
        /// <param name="transitions">The transitions.</param>
        internal ShopCommandHandler(StageTransitions transitions)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        /// <summary>
        /// Buys the routine offer in a slot.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="PurchaseOffer"/> command.</param>
        /// <returns>The purchase result.</returns>
        internal HandlerOutcome Purchase(SessionContext context, ISessionCommand command)
        {
            PurchaseResult bought = context.Shop.Purchase(((PurchaseOffer)command).Slot);
            return new HandlerOutcome(bought.Succeeded, bought, bought.Succeeded ? PurchaseAreas : ChangedAreas.None);
        }

        /// <summary>
        /// Rerolls the shop.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>The reroll result.</returns>
        internal HandlerOutcome Reroll(SessionContext context, ISessionCommand command)
        {
            RerollResult rerolled = context.Shop.Reroll();
            return new HandlerOutcome(rerolled.Succeeded, rerolled, rerolled.Succeeded ? ChangedAreas.Shop | ChangedAreas.Economy : ChangedAreas.None);
        }

        /// <summary>
        /// Pins or unpins a slot.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="PinOffer"/> command.</param>
        /// <returns>The pin result.</returns>
        internal HandlerOutcome Pin(SessionContext context, ISessionCommand command)
        {
            PinOffer pin = (PinOffer)command;
            ShopActionResult pinned = pin.Pinned ? context.Shop.Pin(pin.Slot) : context.Shop.Unpin(pin.Slot);
            return new HandlerOutcome(pinned.Succeeded, pinned, pinned.Succeeded ? ChangedAreas.Shop : ChangedAreas.None);
        }

        /// <summary>
        /// Buys the Patch in a slot and attaches it to a host socket.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="InstallPatch"/> command.</param>
        /// <returns>The attachment result.</returns>
        /// <exception cref="ArgumentException">Thrown when the command names no slot or names a grant.</exception>
        internal HandlerOutcome AttachPatch(SessionContext context, ISessionCommand command)
        {
            InstallPatch install = (InstallPatch)command;
            if (!install.Slot.HasValue || install.Grant.HasValue)
                throw new ArgumentException("A shop Patch purchase names a slot and no grant.", nameof(command));

            PatchAttachmentResult attached = context.Shop.PurchasePatch(install.Slot.Value, install.Host, install.Socket);
            return new HandlerOutcome(attached.Succeeded, attached, attached.Succeeded ? PurchaseAreas : ChangedAreas.None);
        }

        /// <summary>
        /// Buys and installs the Dependency in a slot.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="InstallDependency"/> command.</param>
        /// <returns>The installation result.</returns>
        internal HandlerOutcome BuyDependency(SessionContext context, ISessionCommand command)
        {
            InstallDependency install = (InstallDependency)command;
            DependencyInstallationResult installed = context.Shop.PurchaseDependency(install.Slot, install.DestroyFirst);
            return new HandlerOutcome(installed.Succeeded, installed, installed.Succeeded ? ChangedAreas.Shop | ChangedAreas.Economy : ChangedAreas.None);
        }

        /// <summary>
        /// Destroys an installed Dependency.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="DestroyDependency"/> command.</param>
        /// <returns>The destruction result.</returns>
        internal HandlerOutcome Destroy(SessionContext context, ISessionCommand command)
        {
            DependencyDestructionResult destroyed = context.Shop.DestroyDependency(((DestroyDependency)command).Dependency);
            return new HandlerOutcome(destroyed.Succeeded, destroyed, destroyed.Succeeded ? ChangedAreas.Economy : ChangedAreas.None);
        }

        /// <summary>
        /// Pays for the delete Service on an owned item.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="DeleteItem"/> command.</param>
        /// <returns>The Service result.</returns>
        internal HandlerOutcome Delete(SessionContext context, ISessionCommand command)
        {
            return Serviced(context.Shop.DeleteItem(((DeleteItem)command).Item));
        }

        /// <summary>
        /// Pays for the duplicate Service on an owned item.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="DuplicateItem"/> command.</param>
        /// <returns>The Service result.</returns>
        internal HandlerOutcome Duplicate(SessionContext context, ISessionCommand command)
        {
            return Serviced(context.Shop.DuplicateItem(((DuplicateItem)command).Item));
        }

        /// <summary>
        /// Leaves the shop: pins clear, the shop closes, and the Session enters the next step.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>An accepted outcome.</returns>
        internal HandlerOutcome Leave(SessionContext context, ISessionCommand command)
        {
            context.Shop.Close();
            context.Shop = null;
            return new HandlerOutcome(true, null, ChangedAreas.Shop | _transitions.AdvanceToNextStep(context));
        }

        /// <summary>
        /// Wraps a Service result.
        /// </summary>
        /// <param name="serviced">The Service result.</param>
        /// <returns>The outcome.</returns>
        private static HandlerOutcome Serviced(ServiceResult serviced)
        {
            return new HandlerOutcome(serviced.Succeeded, serviced, serviced.Succeeded ? ChangedAreas.Economy | ChangedAreas.Repository : ChangedAreas.None);
        }
    }
}