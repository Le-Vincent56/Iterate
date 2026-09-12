using System;
using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Folds a Process's process-setup effects over its authored baseline. Absolute settings land
    /// first, in list order, so a later absolute replaces an earlier one; additive adjustments then
    /// apply to whatever that left. Floors keep the resolved Process playable: Bytes may reach zero,
    /// but a Buffer and a Branch always keep at least one slot.
    /// </summary>
    public static class ProcessSetupResolver
    {
        private const string StartingBytesSetting = "STARTING_BYTES";

        private const string BufferCapacitySetting = "INSTRUCTION_BUFFER_CAPACITY";

        private const string BranchCapacitySetting = "ACTIVE_BRANCH_CAPACITY";

        /// <summary>
        /// Resolves a Process's setup.
        /// </summary>
        /// <param name="configuration">The authored Process configuration.</param>
        /// <param name="parameters">The locked parameter register, for the standard Branch capacity.</param>
        /// <param name="processSetupEffects">The process-setup effects active for this Process.</param>
        /// <returns>The resolved setup.</returns>
        public static ProcessSetup Resolve(
            ProcessConfigurationDefinition configuration,
            ParameterSet parameters,
            IReadOnlyList<ActiveSetupEffect> processSetupEffects
        )
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            int bufferCapacity = configuration.BufferCapacity;
            int startingBytes = configuration.StartingBytes;
            int branchCapacity = configuration.ActiveBranch?.Capacity ?? parameters.StandardActiveBranchCapacity;

            IReadOnlyList<ActiveSetupEffect> effects = processSetupEffects ?? Array.Empty<ActiveSetupEffect>();

            for (int index = 0; index < effects.Count; index++)
            {
                ConfigurationModificationOperation modification = effects[index].Modification;
                if (!modification.SetsAbsolute)
                    continue;

                if (modification.Setting == StartingBytesSetting) startingBytes = modification.Amount;
                else if (modification.Setting == BufferCapacitySetting) bufferCapacity = modification.Amount;
                else if (modification.Setting == BranchCapacitySetting) branchCapacity = modification.Amount;
            }

            for (int index = 0; index < effects.Count; index++)
            {
                ConfigurationModificationOperation modification = effects[index].Modification;
                if (modification.SetsAbsolute)
                    continue;

                if (modification.Setting == StartingBytesSetting) startingBytes += modification.Amount;
                else if (modification.Setting == BufferCapacitySetting) bufferCapacity += modification.Amount;
                else if (modification.Setting == BranchCapacitySetting) branchCapacity += modification.Amount;
            }

            return new ProcessSetup(
                Floor(bufferCapacity, 1),
                Floor(startingBytes, 0),
                Floor(branchCapacity, 1),
                configuration.Executions,
                configuration.MandatoryExecutions
            );
        }

        /// <summary>
        /// Clamps a resolved value to its floor.
        /// </summary>
        /// <param name="value">The resolved value.</param>
        /// <param name="floor">The lowest legal value.</param>
        /// <returns>The clamped value.</returns>
        private static int Floor(int value, int floor) => value < floor ? floor : value;
    }
}