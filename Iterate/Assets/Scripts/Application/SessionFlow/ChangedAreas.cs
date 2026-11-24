using System;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Which parts of Session state a command changed, so a subscriber refreshes only what moved.
    /// A command commonly changes several areas at once.
    /// </summary>
    [Flags]
    public enum ChangedAreas
    {
        /// <summary>
        /// Nothing changed.
        /// </summary>
        None = 0,

        /// <summary>
        /// The source inside the Core.
        /// </summary>
        Build = 1,

        /// <summary>
        /// The Instruction Buffer.
        /// </summary>
        Buffer = 2,

        /// <summary>
        /// The Process's Bytes.
        /// </summary>
        Bytes = 4,

        /// <summary>
        /// The Process's execution counters.
        /// </summary>
        Counters = 8,

        /// <summary>
        /// The Active Branch.
        /// </summary>
        Branch = 16,

        /// <summary>
        /// The Repository.
        /// </summary>
        Repository = 32,

        /// <summary>
        /// Tokens, RAM or installed Dependencies.
        /// </summary>
        Economy = 64,

        /// <summary>
        /// The open shop.
        /// </summary>
        Shop = 128,

        /// <summary>
        /// The current execution result.
        /// </summary>
        Execution = 256,

        /// <summary>
        /// The pending rewards.
        /// </summary>
        Rewards = 512,
    }
}