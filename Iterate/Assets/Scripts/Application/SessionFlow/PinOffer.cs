namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Pins or unpins the offer in a shop slot, protecting it from rerolls.
    /// </summary>
    /// <param name="Slot">The shop slot.</param>
    /// <param name="Pinned">True to pin, false to unpin.</param>
    public sealed record PinOffer(int Slot, bool Pinned) : ISessionCommand;
}