using System;
using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The single implementation of mechanical equivalence: two arrangements are equivalent
    /// when they compare equal per position by kind, Core-line value, occupant instance identity,
    /// attached-Patch identity, and owning Structure identity. Child order falls out of the per-position
    /// comparison, since both arrangements number contiguously from one.
    /// </summary>
    public static class ArrangementEquivalence
    {
        /// <summary>
        /// Compares two arrangements for mechanical equivalence.
        /// </summary>
        /// <param name="left">The first arrangement.</param>
        /// <param name="right">The second arrangement.</param>
        /// <returns>True when the arrangements are mechanically equivalent; false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when either arrangement is null.</exception>
        public static bool AreEquivalent(SourceArrangement left, SourceArrangement right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                throw new ArgumentException("Equivalence requires two arrangements.");

            if (left.SlotCount != right.SlotCount)
                return false;

            for (int i = 0; i < left.SlotCount; i++)
            {
                if (!SlotsEquivalent(left.Slots[i], right.Slots[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Compares two same-position slots for mechanical equivalence.
        /// </summary>
        /// <param name="left">The first slot.</param>
        /// <param name="right">The second slot.</param>
        /// <returns>True when the slots are equivalent; false otherwise.</returns>
        private static bool SlotsEquivalent(SourceSlot left, SourceSlot right)
        {
            if (left.Kind != right.Kind)
                return false;

            return left.Kind switch
            {
                SourceSlotKind.Core => left.Core == right.Core,
                SourceSlotKind.Empty => true,
                SourceSlotKind.Instruction => InstructionsEquivalent(left.Instruction, right.Instruction),
                SourceSlotKind.StructureHeader or SourceSlotKind.ContainedEmpty => left.Structure.InstanceID == right.Structure.InstanceID,
                SourceSlotKind.ContainedInstruction => left.Structure.InstanceID == right.Structure.InstanceID && InstructionsEquivalent(left.Instruction, right.Instruction),
                _ => false
            };
        }

        /// <summary>
        /// Compares two Instruction occupants by instance identity and attached-Patch identity.
        /// </summary>
        /// <param name="left">The first Instruction occupant.</param>
        /// <param name="right">The second Instruction occupant.</param>
        /// <returns>True when both identities match; false otherwise.</returns>
        private static bool InstructionsEquivalent(InstructionInstance left, InstructionInstance right)
        {
            return left.InstanceID == right.InstanceID && AttachedPatchesEquivalent(left.AttachedPatch, right.AttachedPatch);
        }

        /// <summary>
        /// Compares two attached Patches by instance identity, treating both-absent as equivalent.
        /// </summary>
        /// <param name="left">The first attached Patch, or null.</param>
        /// <param name="right">The second attached Patch, or null.</param>
        /// <returns>True when both are absent or share an identity; false otherwise.</returns>
        private static bool AttachedPatchesEquivalent(PatchInstance left, PatchInstance right)
        {
            if (left == null && right == null)
                return true;

            if (left == null || right == null)
                return false;

            return left.InstanceID == right.InstanceID;
        }
    }
}