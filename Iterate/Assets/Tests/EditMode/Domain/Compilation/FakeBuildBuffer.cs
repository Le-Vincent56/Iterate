using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// A list-backed <see cref="IBuildBuffer"/> test double with configurable removal capacity. It seeds
    /// installable items, answers peek-then-take, and records every <see cref="Take"/> and
    /// <see cref="AcceptRemoved(InstructionInstance)"/> call so tests can assert the buffer was touched
    /// only on a successful edit.
    /// </summary>
    public sealed class FakeBuildBuffer : IBuildBuffer
    {
        private readonly List<InstructionInstance> _instructions = new List<InstructionInstance>();
        private readonly List<StructureInstance> _structures = new List<StructureInstance>();
        private readonly List<DirectiveInstance> _directives = new List<DirectiveInstance>();
        private readonly List<InstanceID> _taken = new List<InstanceID>();
        private readonly List<InstructionInstance> _acceptedInstructions = new List<InstructionInstance>();
        private readonly List<StructureInstance> _acceptedStructures = new List<StructureInstance>();
        private readonly int _capacity;

        /// <summary>
        /// The identities passed to <see cref="Take"/>, in call order.
        /// </summary>
        public IReadOnlyList<InstanceID> TakenIDs => _taken;

        /// <summary>
        /// The Instruction instances passed to <see cref="AcceptRemoved(InstructionInstance)"/>.
        /// </summary>
        public IReadOnlyList<InstructionInstance> AcceptedInstructions => _acceptedInstructions;

        /// <summary>
        /// The Structure instances passed to <see cref="AcceptRemoved(StructureInstance)"/>.
        /// </summary>
        public IReadOnlyList<StructureInstance> AcceptedStructures => _acceptedStructures;

        /// <summary>
        /// The current number of items held.
        /// </summary>
        public int ItemCount => _instructions.Count + _structures.Count + _directives.Count;

        /// <inheritdoc />
        public bool HasRemovalCapacity => ItemCount < _capacity;

        /// <summary>
        /// Creates a buffer with the given total item capacity.
        /// </summary>
        /// <param name="capacity">The maximum number of items the buffer may hold.</param>
        public FakeBuildBuffer(int capacity)
        {
            _capacity = capacity;
        }

        /// <summary>
        /// Seeds an installable Instruction.
        /// </summary>
        /// <param name="instruction">The Instruction instance to make available.</param>
        public void AddInstruction(InstructionInstance instruction) => _instructions.Add(instruction);

        /// <summary>
        /// Seeds an installable Structure.
        /// </summary>
        /// <param name="structure">The Structure instance to make available.</param>
        public void AddStructure(StructureInstance structure) => _structures.Add(structure);

        /// <summary>
        /// Seeds an activatable Directive.
        /// </summary>
        /// <param name="directive">The Directive instance to make available.</param>
        public void AddDirective(DirectiveInstance directive) => _directives.Add(directive);

        /// <inheritdoc />
        public bool TryPeekInstruction(InstanceID instanceID, out InstructionInstance instance)
        {
            for (int i = 0; i < _instructions.Count; i++)
            {
                if (_instructions[i].InstanceID == instanceID)
                {
                    instance = _instructions[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryPeekStructure(InstanceID instanceID, out StructureInstance instance)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i].InstanceID == instanceID)
                {
                    instance = _structures[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryPeekDirective(InstanceID instanceID, out DirectiveInstance instance)
        {
            for (int i = 0; i < _directives.Count; i++)
            {
                if (_directives[i].InstanceID == instanceID)
                {
                    instance = _directives[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <inheritdoc />
        public void Take(InstanceID instanceID)
        {
            _taken.Add(instanceID);
            if (RemoveInstruction(instanceID))
                return;

            if (RemoveStructure(instanceID))
                return;

            RemoveDirective(instanceID);
        }

        /// <inheritdoc />
        public void AcceptRemoved(InstructionInstance removed)
        {
            _acceptedInstructions.Add(removed);
            _instructions.Add(removed);
        }

        /// <inheritdoc />
        public void AcceptRemoved(StructureInstance removed)
        {
            _acceptedStructures.Add(removed);
            _structures.Add(removed);
        }

        private bool RemoveInstruction(InstanceID instanceID)
        {
            for (int i = 0; i < _instructions.Count; i++)
            {
                if (_instructions[i].InstanceID == instanceID)
                {
                    _instructions.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private bool RemoveStructure(InstanceID instanceID)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i].InstanceID == instanceID)
                {
                    _structures.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private bool RemoveDirective(InstanceID instanceID)
        {
            for (int i = 0; i < _directives.Count; i++)
            {
                if (_directives[i].InstanceID == instanceID)
                {
                    _directives.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
