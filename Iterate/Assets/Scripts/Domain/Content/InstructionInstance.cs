using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A runtime Instruction instance: a frozen <see cref="InstructionDefinition"/> paired with a stable
    /// <see cref="InstanceID"/> and the Patches socketed to it, ordered by socket. Identity, not the
    /// definition, distinguishes two instances; attaching a Patch produces a derived record under the
    /// same identity. How many sockets exist is a transaction-level rule (WB-PAR-036), not a record
    /// invariant, so the record enforces only that sockets are one-based, distinct and ascending.
    /// </summary>
    /// <param name="InstanceID">The stable instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realises.</param>
    /// <param name="AttachedPatches">The socketed Patch attachments, ascending by socket; empty when unpatched.</param>
    public sealed record InstructionInstance(
        InstanceID InstanceID,
        InstructionDefinition Definition,
        IReadOnlyList<PatchAttachment> AttachedPatches
    )
    {
        /// <summary>
        /// The frozen definition this instance realises. Validated non-null at construction.
        /// </summary>
        public InstructionDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// The socketed Patch attachments in ascending socket order. Validated at construction, so an
        /// instance carrying a duplicate or out-of-order socket cannot exist.
        /// </summary>
        public IReadOnlyList<PatchAttachment> AttachedPatches { get; private init; } = RequireAttachments(AttachedPatches);

        /// <summary>
        /// Finds the attachment occupying a socket.
        /// </summary>
        /// <param name="socket">The socket number to read.</param>
        /// <param name="attachment">The attachment found, or null.</param>
        /// <returns>True when the socket is occupied; false otherwise.</returns>
        public bool TryGetAttachment(int socket, out PatchAttachment attachment)
        {
            for (int i = 0; i < AttachedPatches.Count; i++)
            {
                if (AttachedPatches[i].Socket == socket)
                {
                    attachment = AttachedPatches[i];
                    return true;
                }
            }

            attachment = null;
            return false;
        }

        /// <summary>
        /// Derives an instance carrying the given attachment, replacing whatever occupied that socket.
        /// </summary>
        /// <param name="attachment">The attachment to socket.</param>
        /// <returns>The derived instance under the same identity.</returns>
        /// <exception cref="ArgumentException">Thrown when the attachment is null.</exception>
        public InstructionInstance WithAttachment(PatchAttachment attachment)
        {
            if (attachment == null)
                throw new ArgumentException("An attachment is required.", nameof(attachment));

            List<PatchAttachment> derived = new List<PatchAttachment>(AttachedPatches.Count + 1);
            bool placed = false;
            for (int i = 0; i < AttachedPatches.Count; i++)
            {
                PatchAttachment existing = AttachedPatches[i];
                if (existing.Socket == attachment.Socket)
                {
                    derived.Add(attachment);
                    placed = true;
                    continue;
                }

                if (!placed && existing.Socket > attachment.Socket)
                {
                    derived.Add(attachment);
                    placed = true;
                }

                derived.Add(existing);
            }

            if (!placed)
                derived.Add(attachment);

            return this with { AttachedPatches = derived.ToArray() };
        }

        /// <summary>
        /// Derives an instance with the given socket emptied. An already-empty socket derives an equal
        /// record, so a caller need not check occupancy first.
        /// </summary>
        /// <param name="socket">The socket to empty.</param>
        /// <returns>The derived instance under the same identity.</returns>
        public InstructionInstance WithoutAttachment(int socket)
        {
            if (!TryGetAttachment(socket, out PatchAttachment _))
                return this;

            List<PatchAttachment> derived = new List<PatchAttachment>(AttachedPatches.Count - 1);
            for (int i = 0; i < AttachedPatches.Count; i++)
            {
                if (AttachedPatches[i].Socket != socket)
                    derived.Add(AttachedPatches[i]);
            }

            return this with { AttachedPatches = derived.ToArray() };
        }

        /// <summary>
        /// Compares two instances by identity, definition and attachments. The generated record
        /// equality compares the attachment list by reference, which would make two instances holding
        /// equal attachments — including two unpatched instances — unequal; attachments are part of what
        /// the instance *is*, so they are compared element-wise.
        /// </summary>
        /// <param name="other">The instance to compare against.</param>
        /// <returns>True when identity, definition and every attachment match; false otherwise.</returns>
        public bool Equals(InstructionInstance other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (InstanceID != other.InstanceID || !Definition.Equals(other.Definition))
                return false;

            if (AttachedPatches.Count != other.AttachedPatches.Count)
                return false;

            for (int i = 0; i < AttachedPatches.Count; i++)
            {
                if (!AttachedPatches[i].Equals(other.AttachedPatches[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Hashes identity, definition and every attachment, so equal instances hash alike.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(InstanceID);
            hash.Add(Definition);
            for (int i = 0; i < AttachedPatches.Count; i++)
            {
                hash.Add(AttachedPatches[i]);
            }

            return hash.ToHashCode();
        }
        
        /// <summary>
        /// Validates that the paired definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static InstructionDefinition RequireDefinition(InstructionDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("An InstructionInstance requires a definition.", nameof(definition));

            return definition;
        }

        /// <summary>
        /// Validates the attachment list: present, no null entry, and sockets strictly ascending, which
        /// rejects a socket below one, a duplicate socket and an out-of-order list in one pass.
        /// </summary>
        /// <param name="attachments">The candidate attachment list.</param>
        /// <returns>A frozen copy of the list.</returns>
        /// <exception cref="ArgumentException">Thrown when the list is null or its sockets are not strictly ascending.</exception>
        private static IReadOnlyList<PatchAttachment> RequireAttachments(IReadOnlyList<PatchAttachment> attachments)
        {
            if (attachments == null)
                throw new ArgumentException("An InstructionInstance requires an attachment list.", nameof(attachments));

            PatchAttachment[] copy = new PatchAttachment[attachments.Count];
            int previousSocket = 0;
            for (int i = 0; i < attachments.Count; i++)
            {
                PatchAttachment attachment = attachments[i];
                if (attachment == null)
                    throw new ArgumentException("An attachment list carries no null entries.", nameof(attachments));

                if (attachment.Socket <= previousSocket)
                {
                    throw new ArgumentException(
                        "An attachment list carries distinct sockets in ascending order; socket " +
                        attachment.Socket + " follows " + previousSocket + ".",
                        nameof(attachments));
                }

                previousSocket = attachment.Socket;
                copy[i] = attachment;
            }

            return copy;
        }
    }
}