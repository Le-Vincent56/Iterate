using System;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// One Patch socketed to one host: the socket number it occupies and the Patch instance in it.
    /// The socket is the discriminant that keeps two attachments of one Patch definition on one host
    /// distinguishable, so socket identity, not list position, is what the host orders by.
    /// </summary>
    /// <param name="Socket">The one-based socket number this attachment occupies.</param>
    /// <param name="Patch">The Patch instance in that socket.</param>
    public sealed record PatchAttachment(int Socket, PatchInstance Patch)
    {
        /// <summary>
        /// The one-based socket number. Validated at construction; socket numbering starts at one so a
        /// default-constructed zero can never read as a legal socket.
        /// </summary>
        public int Socket { get; } = RequireSocket(Socket);

        /// <summary>
        /// The Patch instance in this socket. Validated non-null at construction.
        /// </summary>
        public PatchInstance Patch { get; } = RequirePatch(Patch);

        /// <summary>
        /// Validates that the socket number is one-based.
        /// </summary>
        /// <param name="socket">The candidate socket number.</param>
        /// <returns>The socket number unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the socket is below one.</exception>
        private static int RequireSocket(int socket)
        {
            if (socket < 1)
                throw new ArgumentException("A Patch attachment requires a socket of at least one.", nameof(socket));

            return socket;
        }

        /// <summary>
        /// Validates that the attached Patch instance is present.
        /// </summary>
        /// <param name="patch">The candidate Patch instance.</param>
        /// <returns>The Patch instance unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the Patch instance is null.</exception>
        private static PatchInstance RequirePatch(PatchInstance patch)
        {
            if (patch == null)
                throw new ArgumentException("A Patch attachment requires a Patch instance.", nameof(patch));

            return patch;
        }
    }
}