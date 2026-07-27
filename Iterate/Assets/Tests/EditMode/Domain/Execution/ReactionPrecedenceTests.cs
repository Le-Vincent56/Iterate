using NUnit.Framework;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="ReactionPrecedence"/>'s CAB-EVT-532 Score-reaction order: host-local
    /// FEEDBACK PATCH resolves before OUTPUT CACHE, which resolves before OUTPUT PIPELINE, and every
    /// other effect shares the trailing rank so instance identity orders it.
    /// </summary>
    public sealed class ReactionPrecedenceTests
    {
        [Test]
        public void FeedbackPatch_RanksFirst()
        {
            Assert.AreEqual(0, ReactionPrecedence.Rank("WB-PAT-005"));
        }

        [Test]
        public void OutputCache_RanksSecond()
        {
            Assert.AreEqual(1, ReactionPrecedence.Rank("WB-DEP-005"));
        }

        [Test]
        public void OutputPipeline_RanksThird()
        {
            Assert.AreEqual(2, ReactionPrecedence.Rank("WB-DEP-011"));
        }

        [Test]
        public void UnrankedEffect_SharesTheTrailingRank()
        {
            Assert.AreEqual(3, ReactionPrecedence.Rank("WB-DEP-001"));
        }

        [Test]
        public void FeedbackBeforeCacheBeforePipeline()
        {
            int feedback = ReactionPrecedence.Rank("WB-PAT-005");
            int cache = ReactionPrecedence.Rank("WB-DEP-005");
            int pipeline = ReactionPrecedence.Rank("WB-DEP-011");

            Assert.Less(feedback, cache);
            Assert.Less(cache, pipeline);
        }
    }
}
