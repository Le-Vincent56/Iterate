using NUnit.Framework;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Registry tests for <see cref="EventFamilies"/>: the fifteen CAB controlled event-family tokens,
    /// verbatim and case-sensitive, each exposed as a named constant and a member of the vocabulary.
    /// </summary>
    public sealed class EventFamiliesTests
    {
        private static readonly string[] _AllTokens =
        {
            "LIFECYCLE",
            "SOURCE",
            "OPERATION",
            "QUALIFICATION",
            "QUANTITY",
            "STRUCTURE",
            "DISPOSITION",
            "REACTION",
            "ADDED_EXECUTION",
            "INTERVENTION",
            "THRESHOLD",
            "TRANSACTION",
            "CONTENT_LIFECYCLE",
            "RANDOM_SELECTION",
            "SAFETY"
        };

        [Test]
        public void All_ContainsExactlyTheFifteenRegistryTokens()
        {
            // Assert — count and membership together pin the registry exactly.
            Assert.That(EventFamilies.All.Count, Is.EqualTo(15));

            foreach (string token in _AllTokens)
                Assert.That(EventFamilies.All.Contains(token), Is.True, token);
        }

        [Test]
        public void Constants_CarryTheRegistryTokensVerbatim()
        {
            Assert.That(EventFamilies.Lifecycle, Is.EqualTo("LIFECYCLE"));
            Assert.That(EventFamilies.Source, Is.EqualTo("SOURCE"));
            Assert.That(EventFamilies.Operation, Is.EqualTo("OPERATION"));
            Assert.That(EventFamilies.Qualification, Is.EqualTo("QUALIFICATION"));
            Assert.That(EventFamilies.Quantity, Is.EqualTo("QUANTITY"));
            Assert.That(EventFamilies.Structure, Is.EqualTo("STRUCTURE"));
            Assert.That(EventFamilies.Disposition, Is.EqualTo("DISPOSITION"));
            Assert.That(EventFamilies.Reaction, Is.EqualTo("REACTION"));
            Assert.That(EventFamilies.AddedExecution, Is.EqualTo("ADDED_EXECUTION"));
            Assert.That(EventFamilies.Intervention, Is.EqualTo("INTERVENTION"));
            Assert.That(EventFamilies.Threshold, Is.EqualTo("THRESHOLD"));
            Assert.That(EventFamilies.Transaction, Is.EqualTo("TRANSACTION"));
            Assert.That(EventFamilies.ContentLifecycle, Is.EqualTo("CONTENT_LIFECYCLE"));
            Assert.That(EventFamilies.RandomSelection, Is.EqualTo("RANDOM_SELECTION"));
            Assert.That(EventFamilies.Safety, Is.EqualTo("SAFETY"));
        }

        [Test]
        public void All_MembershipIsCaseSensitive()
        {
            Assert.That(EventFamilies.All.Contains("lifecycle"), Is.False);
            Assert.That(EventFamilies.All.Contains("Safety"), Is.False);
        }
    }
}
