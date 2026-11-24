using System;
using System.Reflection;
using NUnit.Framework;
using Iterate.Application.SessionFlow;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="CommandResult"/>: the two factories are its only constructors, so an accepted
    /// result carrying a rejection, or a rejected result carrying none, cannot be built. Members are
    /// compared one by one, because the record's generated equality compares its Domain result by
    /// reference.
    /// </summary>
    public sealed class CommandResultTests
    {
        [Test]
        public void Accept_CarriesAcceptanceNoRejectionTheDomainResultAndTheStage()
        {
            object domainResult = new object();

            CommandResult result = CommandResult.Accept(domainResult, SessionStage.Build);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CommandRejection.None, result.Rejection);
            Assert.AreSame(domainResult, result.DomainResult);
            Assert.AreEqual(SessionStage.Build, result.Stage);
        }

        [Test]
        public void Accept_NullDomainResult_IsAllowed()
        {
            CommandResult result = CommandResult.Accept(null, SessionStage.Shop);

            Assert.IsTrue(result.Accepted);
            Assert.IsNull(result.DomainResult);
        }

        [TestCase(CommandRejection.WrongStage)]
        [TestCase(CommandRejection.Gated)]
        [TestCase(CommandRejection.DomainRejected)]
        [TestCase(CommandRejection.NoSession)]
        public void Reject_CarriesRefusalTheRejectionTheDomainResultAndTheStage(CommandRejection rejection)
        {
            object domainResult = new object();

            CommandResult result = CommandResult.Reject(rejection, domainResult, SessionStage.ResultReview);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(rejection, result.Rejection);
            Assert.AreSame(domainResult, result.DomainResult);
            Assert.AreEqual(SessionStage.ResultReview, result.Stage);
        }

        [Test]
        public void Reject_None_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CommandResult.Reject(CommandRejection.None, null, SessionStage.Build));
        }

        [Test]
        public void CommandResult_HasNoPublicConstructor()
        {
            ConstructorInfo[] constructors = typeof(CommandResult).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            Assert.AreEqual(0, constructors.Length, "Only Accept and Reject may build a CommandResult.");
        }

        [Test]
        public void CommandResult_MembersHaveNoSetter()
        {
            string[] members = { "Accepted", "Rejection", "DomainResult", "Stage" };
            foreach (string member in members)
            {
                PropertyInfo property = typeof(CommandResult).GetProperty(member);
                Assert.IsNotNull(property, member);
                Assert.IsFalse(property.CanWrite, $"{member} must not be settable, or a with-expression could forge a result.");
            }
        }
    }
}
