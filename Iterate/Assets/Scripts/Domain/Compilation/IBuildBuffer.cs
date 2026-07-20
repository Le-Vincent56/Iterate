using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The narrow seam onto the Instruction Buffer. Peek-then-take lets an edit validate fully before any
    /// mutation; the removal-capacity check and accept-removed sinks complete the surface. Child 5's real
    /// Instruction Buffer implements this later; a test double backs it for now.
    /// </summary>
    public interface IBuildBuffer
    {
        /// <summary>
        /// Whether the buffer has room to accept one removed item.
        /// </summary>
        bool HasRemovalCapacity { get; }

        /// <summary>
        /// Attempts to read a buffered Instruction without removing it.
        /// </summary>
        /// <param name="instanceID">The instance identity to resolve.</param>
        /// <param name="instance">The Instruction instance when found; null otherwise.</param>
        /// <returns>True when a matching Instruction is buffered; false otherwise.</returns>
        bool TryPeekInstruction(InstanceID instanceID, out InstructionInstance instance);

        /// <summary>
        /// Attempts to read a buffered Structure without removing it.
        /// </summary>
        /// <param name="instanceID">The instance identity to resolve.</param>
        /// <param name="instance">The Structure instance when found; null otherwise.</param>
        /// <returns>True when a matching Structure is buffered; false otherwise.</returns>
        bool TryPeekStructure(InstanceID instanceID, out StructureInstance instance);

        /// <summary>
        /// Attempts to read a buffered Directive without removing it.
        /// </summary>
        /// <param name="instanceID">The instance identity to resolve.</param>
        /// <param name="instance">The Directive instance when found; null otherwise.</param>
        /// <returns>True when a matching Directive is buffered; false otherwise.</returns>
        bool TryPeekDirective(InstanceID instanceID, out DirectiveInstance instance);

        /// <summary>
        /// Removes a previously peeked item from the buffer.
        /// </summary>
        /// <param name="instanceID">The identity of the item to remove.</param>
        void Take(InstanceID instanceID);

        /// <summary>
        /// Accepts a removed Instruction back into the buffer.
        /// </summary>
        /// <param name="removed">The Instruction instance being returned.</param>
        void AcceptRemoved(InstructionInstance removed);

        /// <summary>
        /// Accepts a removed Structure back into the buffer.
        /// </summary>
        /// <param name="removed">The Structure instance being returned.</param>
        void AcceptRemoved(StructureInstance removed);
    }
}