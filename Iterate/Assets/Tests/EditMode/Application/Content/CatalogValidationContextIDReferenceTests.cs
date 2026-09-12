using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the ID-based cross-reference resolution the catalog-extension kinds use: a reference
    /// records the ID it names and the WB prefix it expects, and resolution after every file is read
    /// reports an unresolved ID as <c>reference.unknown-id</c> and a resolved ID of the wrong kind
    /// under the referencing site's own rule name. The Done display-name reference path is unchanged.
    /// </summary>
    public sealed class CatalogValidationContextIDReferenceTests
    {
        [Test]
        public void IDReference_ToDefinedIDOfExpectedKind_ReportsNothing()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "instructions.json";
            context.RegisterDefinition("WB-INS-002", "Value += 2", "$[0]");

            context.CurrentFile = "pools.json";
            context.RegisterIDReference("$[0].members[0].content", "WB-INS-002", "WB-INS-", "reference.wrong-kind");
            context.ResolveReferences();

            Assert.AreEqual(0, context.Errors.Count, CatalogExtensionFixtures.Describe(context.Errors));
        }

        [Test]
        public void IDReference_ToUndefinedID_ReportsUnknownID()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "pools.json";
            context.RegisterIDReference("$[0].members[0].content", "WB-INS-999", "WB-INS-", "reference.wrong-kind");
            context.ResolveReferences();

            Assert.IsTrue(HasRule(context.Errors, "reference.unknown-id"), CatalogExtensionFixtures.Describe(context.Errors));
        }

        [Test]
        public void IDReference_ToUndefinedID_StampsTheReferencingFileAndPath()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "pools.json";
            context.RegisterIDReference("$[0].members[0].content", "WB-INS-999", "WB-INS-", "reference.wrong-kind");
            context.CurrentFile = "manifest.json";
            context.ResolveReferences();

            CatalogError error = context.Errors[0];
            Assert.AreEqual("pools.json", error.File);
            Assert.AreEqual("$[0].members[0].content", error.JsonPath);
        }

        [Test]
        public void IDReference_ToDefinedIDOfAnotherKind_ReportsTheCallersRuleName()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "instructions.json";
            context.RegisterDefinition("WB-INS-002", "Value += 2", "$[0]");

            context.CurrentFile = "archetypes.json";
            context.RegisterIDReference(
                "$[0].starterDependency",
                "WB-INS-002",
                "WB-DEP-",
                "archetype.starter-dependency-not-dependency"
            );
            context.ResolveReferences();

            Assert.IsTrue(
                HasRule(context.Errors, "archetype.starter-dependency-not-dependency"),
                CatalogExtensionFixtures.Describe(context.Errors)
            );
        }

        [Test]
        public void IDReference_OfWrongKind_DoesNotAlsoReportUnknownID()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "instructions.json";
            context.RegisterDefinition("WB-INS-002", "Value += 2", "$[0]");

            context.CurrentFile = "archetypes.json";
            context.RegisterIDReference("$[0].starterDependency", "WB-INS-002", "WB-DEP-", "reference.wrong-kind");
            context.ResolveReferences();

            Assert.AreEqual(1, context.Errors.Count, CatalogExtensionFixtures.Describe(context.Errors));
            Assert.IsFalse(HasRule(context.Errors, "reference.unknown-id"));
        }

        [Test]
        public void IDReference_ResolvesAgainstADefinitionRegisteredAfterIt()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "pools.json";
            context.RegisterIDReference("$[0].members[0].content", "WB-INS-002", "WB-INS-", "reference.wrong-kind");

            context.CurrentFile = "instructions.json";
            context.RegisterDefinition("WB-INS-002", "Value += 2", "$[0]");
            context.ResolveReferences();

            Assert.AreEqual(0, context.Errors.Count, CatalogExtensionFixtures.Describe(context.Errors));
        }

        [Test]
        public void DisplayNameReference_StillResolvesAgainstDisplayNames()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "instructions.json";
            context.RegisterDefinition("WB-INS-002", "Value += 2", "$[0]");

            context.CurrentFile = "patches.json";
            context.RegisterReference("$[0].hostEligibility", "Value += 2", "reference.unknown-host");
            context.ResolveReferences();

            Assert.AreEqual(0, context.Errors.Count, CatalogExtensionFixtures.Describe(context.Errors));
        }

        [Test]
        public void DisplayNameReference_ToAnUndefinedName_StillReportsItsOwnRule()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "patches.json";
            context.RegisterReference("$[0].hostEligibility", "No Such Item", "reference.unknown-host");
            context.ResolveReferences();

            Assert.IsTrue(HasRule(context.Errors, "reference.unknown-host"), CatalogExtensionFixtures.Describe(context.Errors));
        }

        [Test]
        public void IDReference_IsNotSatisfiedByAMatchingDisplayName()
        {
            CatalogValidationContext context = new();
            context.CurrentFile = "instructions.json";
            context.RegisterDefinition("WB-INS-002", "WB-DEP-001", "$[0]");

            context.CurrentFile = "archetypes.json";
            context.RegisterIDReference("$[0].starterDependency", "WB-DEP-001", "WB-DEP-", "reference.wrong-kind");
            context.ResolveReferences();

            Assert.IsTrue(HasRule(context.Errors, "reference.unknown-id"), CatalogExtensionFixtures.Describe(context.Errors));
        }

        /// <summary>
        /// Whether the error list contains an error with the given rule name.
        /// </summary>
        /// <param name="errors">The error list.</param>
        /// <param name="ruleName">The rule name to look for.</param>
        /// <returns>True when a matching error is present.</returns>
        private static bool HasRule(IReadOnlyList<CatalogError> errors, string ruleName)
        {
            foreach (CatalogError error in errors)
            {
                if (error.RuleName == ruleName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
