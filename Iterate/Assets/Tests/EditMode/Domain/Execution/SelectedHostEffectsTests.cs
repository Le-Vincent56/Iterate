using NUnit.Framework;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the canon-verbatim selected-host constant: exactly STANDARD LIBRARY's catalog key uses
    /// selected-host semantics; first-successful-event Dependencies and unknown keys never do.
    /// </summary>
    public sealed class SelectedHostEffectsTests
    {
        [Test]
        public void StandardLibrary_IsSelectedHost()
        {
            Assert.IsTrue(SelectedHostEffects.IsSelectedHost("WB-DEP-001"));
        }

        [Test]
        public void ParallelChannel_IsNotSelectedHost()
        {
            Assert.IsFalse(SelectedHostEffects.IsSelectedHost("WB-DEP-004"));
        }

        [Test]
        public void SafeMode_IsNotSelectedHost()
        {
            Assert.IsFalse(SelectedHostEffects.IsSelectedHost("WB-DEP-007"));
        }

        [Test]
        public void UnknownKey_IsNotSelectedHost()
        {
            Assert.IsFalse(SelectedHostEffects.IsSelectedHost("WB-DEP-999"));
        }
    }
}
