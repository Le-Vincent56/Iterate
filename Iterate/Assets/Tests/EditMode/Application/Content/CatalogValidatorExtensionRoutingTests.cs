using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Domain.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests that the validator routes every manifest category token to a rule set — the eight
    /// extension tokens as well as the eight the Done catalog already carried — so a newly authored
    /// file is validated rather than silently accepted as an unknown category.
    /// </summary>
    public sealed class CatalogValidatorExtensionRoutingTests
    {
        private static readonly string[] _extensionTokens =
        {
            "CORE",
            "PROCESS_CONFIGURATION",
            "SHOP",
            "POOL",
            "REWARD_PACKAGE",
            "ROUTE",
            "STARTER_ARCHETYPE",
            "SYSTEM"
        };

        private static readonly string[] _doneTokens =
        {
            "PARAMETERS",
            "INSTRUCTION",
            "STRUCTURE",
            "DIRECTIVE",
            "DEPENDENCY",
            "PATCH",
            "UTILITY",
            "PROCESS_RULE"
        };

        [Test]
        public void EveryExtensionCategoryToken_IsRouted()
        {
            foreach (string token in _extensionTokens)
            {
                IReadOnlyList<CatalogError> errors = ValidateTokenAlone(token);

                Assert.IsFalse(
                    CatalogTestFixtures.HasRule(errors, "manifest.unknown-category"),
                    "the category token '" + token + "' is not routed to a rule set."
                );
            }
        }

        [Test]
        public void EveryDoneCategoryToken_IsStillRouted()
        {
            foreach (string token in _doneTokens)
            {
                IReadOnlyList<CatalogError> errors = ValidateTokenAlone(token);

                Assert.IsFalse(
                    CatalogTestFixtures.HasRule(errors, "manifest.unknown-category"),
                    "the category token '" + token + "' is no longer routed to a rule set."
                );
            }
        }

        [Test]
        public void TheRoutedTokenCount_MatchesTheCatalogFileKindCount()
        {
            Assert.AreEqual(
                _doneTokens.Length + _extensionTokens.Length,
                System.Enum.GetValues(typeof(CatalogFileKind)).Length
            );
        }

        [Test]
        public void AnUnknownCategoryToken_IsStillReported()
        {
            IReadOnlyList<CatalogError> errors = ValidateTokenAlone("INTERLUDE");

            Assert.IsTrue(CatalogTestFixtures.HasRule(errors, "manifest.unknown-category"));
        }

        /// <summary>
        /// Validates a one-file manifest carrying the given category token over an empty array, so the
        /// only thing under test is whether the token routes to a rule set.
        /// </summary>
        /// <param name="token">The manifest category token.</param>
        /// <returns>The complete error list.</returns>
        private static IReadOnlyList<CatalogError> ValidateTokenAlone(string token)
        {
            string manifest = @"{
                ""revision"": ""0.2.0"",
                ""schemaVersion"": ""1.0.0"",
                ""files"": [ { ""file"": ""under-test.json"", ""category"": """ + token + @""" } ]
            }";

            return CatalogTestFixtures.Validate(manifest, ("under-test.json", "[]"));
        }
    }
}
