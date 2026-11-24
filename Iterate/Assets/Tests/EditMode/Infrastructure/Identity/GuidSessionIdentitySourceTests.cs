using NUnit.Framework;
using Iterate.Application.Onboarding;
using Iterate.Infrastructure.Identity;

namespace Iterate.Infrastructure.Identity.Tests
{
    /// <summary>
    /// Tests <see cref="GuidSessionIdentitySource"/>: every issue is new, and a Session identity never
    /// carries its seed.
    /// </summary>
    public sealed class GuidSessionIdentitySourceTests
    {
        [Test]
        public void Next_TwoIssues_DifferInBothIdentities()
        {
            GuidSessionIdentitySource source = new();

            SessionIdentities first = source.Next();
            SessionIdentities second = source.Next();

            Assert.AreNotEqual(first.SessionIdentity, second.SessionIdentity);
            Assert.AreNotEqual(first.SeedIdentity, second.SeedIdentity);
        }

        [Test]
        public void Next_TheSessionIdentityDoesNotContainTheSeed()
        {
            SessionIdentities issued = new GuidSessionIdentitySource().Next();

            StringAssert.DoesNotContain(issued.SeedIdentity, issued.SessionIdentity);
            StringAssert.DoesNotContain(issued.SessionIdentity, issued.SeedIdentity);
            StringAssert.StartsWith("session-", issued.SessionIdentity);
            StringAssert.StartsWith("seed-", issued.SeedIdentity);
        }
    }
}
