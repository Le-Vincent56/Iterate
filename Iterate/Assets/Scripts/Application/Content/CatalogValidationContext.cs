using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The shared validation toolkit and per-run state: the current file, the accumulating error list,
    /// the catalog-wide ID and display-name registries, the primitive field readers, and the shared
    /// effect/operation validators every rule set reuses. Enum-tier JSON tokens are exposed as
    /// controlled sets for membership checks; deep string registries are read from
    /// <see cref="CatalogVocabulary"/>.
    /// </summary>
    public sealed class CatalogValidationContext
    {
        /// <summary>
        /// The controlled rarity tokens.
        /// </summary>
        public static readonly ControlledVocabulary RarityTokens = new(
            "STARTER", "COMMON", "UNCOMMON", "RARE"
        );

        /// <summary>
        /// The controlled phase-domain tokens.
        /// </summary>
        public static readonly ControlledVocabulary PhaseDomainTokens = new(
            "EXECUTION", "COMPILATION", "BUILD_INTERACTION", "PROCESS_SETUP", "DISCLOSURE"
        );

        /// <summary>
        /// The controlled operation-kind tokens (the ten primitives).
        /// </summary>
        public static readonly ControlledVocabulary OperationKindTokens = new(
            "QUANTITY_CHANGE", "DISPOSITION_CHANGE", "ADDED_EXECUTION_REQUEST", "COUNTER_REQUEST",
            "COST_MODIFICATION", "RESCUE", "PREDICTION_VISIBILITY", "CONFIGURATION_MODIFICATION",
            "OPERATION_MODIFICATION", "TARGET_LOCK_UPDATE"
        );

        /// <summary>
        /// The controlled Core-register tokens.
        /// </summary>
        public static readonly ControlledVocabulary RegisterTokens = new("VALUE", "SIGNAL", "SCORE");

        /// <summary>
        /// The controlled quantity-operator tokens.
        /// </summary>
        public static readonly ControlledVocabulary QuantityOperatorTokens = new("ADD", "MULTIPLY");

        /// <summary>
        /// The controlled operand-source tokens.
        /// </summary>
        public static readonly ControlledVocabulary OperandSourceTokens = new("CONSTANT", "REGISTER", "LINE_NUMBER");

        /// <summary>
        /// The controlled structure-kind tokens.
        /// </summary>
        public static readonly ControlledVocabulary StructureKindTokens = new("REPEAT", "CONDITION");

        /// <summary>
        /// The controlled predicate-comparison tokens.
        /// </summary>
        public static readonly ControlledVocabulary PredicateComparisonTokens = new("IS_EVEN", "AT_LEAST");

        /// <summary>
        /// The controlled event-family tokens (the 15 families).
        /// </summary>
        public static readonly ControlledVocabulary EventFamilyTokens = new(
            "LIFECYCLE", "SOURCE", "OPERATION", "QUALIFICATION", "QUANTITY", "STRUCTURE", "DISPOSITION",
            "REACTION", "ADDED_EXECUTION", "INTERVENTION", "THRESHOLD", "TRANSACTION", "CONTENT_LIFECYCLE",
            "RANDOM_SELECTION", "SAFETY"
        );

        /// <summary>
        /// The controlled stacking-mode tokens (the nine named modes).
        /// </summary>
        public static readonly ControlledVocabulary StackingTokens = new(
            "INDEPENDENT_RESOLUTION", "ADDITIVE_PARAMETER", "MULTIPLICATIVE_PARAMETER", "SET_REPLACEMENT",
            "FIRST_QUALIFYING_SELECTION", "CAPPED_INDEPENDENT", "EXCLUSIVE_GROUP", "SHARED_FREQUENCY",
            "STRONGEST_ONLY"
        );

        /// <summary>
        /// The controlled timing-kind tokens.
        /// </summary>
        public static readonly ControlledVocabulary TimingKindTokens = new("BAND", "NAMED_BOUNDARY");

        /// <summary>
        /// The allowed top-level keys of an effect object.
        /// </summary>
        public static readonly ControlledVocabulary EffectKeys = new(
            "phaseDomain", "trigger", "operation", "targeting", "timing", "stacking", "frequency"
        );

        private static readonly ControlledVocabulary _resetScopes = new(
            "SOURCE_ACTIVATION", "SOURCE_EXECUTION", "LINE_PER_EXECUTION", "STRUCTURE_ENTRY",
            "REPEAT_ITERATION", "CONDITION_EVALUATION", "EXECUTION", "COMPILATION", "PROCESS",
            "SYSTEM", "SESSION"
        );

        private readonly List<CatalogError> _errors = new();

        private readonly HashSet<string> _definedIDs = new(StringComparer.Ordinal);

        private readonly HashSet<string> _displayNames = new(StringComparer.Ordinal);

        private readonly List<PendingReference> _references = new();

        /// <summary>
        /// The file name stamped on errors added while it is current.
        /// </summary>
        public string CurrentFile { get; set; } = string.Empty;

        /// <summary>
        /// The complete error list accumulated so far.
        /// </summary>
        public IReadOnlyList<CatalogError> Errors => _errors;

        /// <summary>
        /// Records a failure against the current file.
        /// </summary>
        /// <param name="jsonPath">The JSON path to the offending node.</param>
        /// <param name="ruleName">The stable rule name.</param>
        /// <param name="message">The human-readable message.</param>
        public void AddError(string jsonPath, string ruleName, string message)
        {
            _errors.Add(new CatalogError(CurrentFile, jsonPath, ruleName, message));
        }

        /// <summary>
        /// Registers a definition's identity for catalog-wide duplicate detection and cross-reference
        /// resolution, reporting a duplicate ID at the given path.
        /// </summary>
        /// <param name="id">The definition's ID.</param>
        /// <param name="displayName">The definition's display name.</param>
        /// <param name="jsonPath">The JSON path of the definition.</param>
        public void RegisterDefinition(string id, string displayName, string jsonPath)
        {
            if (!_definedIDs.Add(id))
                AddError(jsonPath, "definition.duplicate-id", "the id '" + id + "' is defined more than once.");

            _displayNames.Add(displayName);
        }

        /// <summary>
        /// Records a reference to another definition's display name, resolved after all files are read.
        /// </summary>
        /// <param name="jsonPath">The JSON path of the reference.</param>
        /// <param name="referencedName">The referenced display name.</param>
        /// <param name="ruleName">The rule name to report if it does not resolve.</param>
        public void RegisterReference(string jsonPath, string referencedName, string ruleName)
        {
            _references.Add(new PendingReference(CurrentFile, jsonPath, referencedName, ruleName));
        }

        /// <summary>
        /// Resolves every recorded reference against the collected display names, reporting the
        /// unresolved ones.
        /// </summary>
        public void ResolveReferences()
        {
            for (int index = 0; index < _references.Count; index++)
            {
                PendingReference reference = _references[index];
                if (!_displayNames.Contains(reference.ReferencedName))
                {
                    _errors.Add(new CatalogError(
                        reference.File,
                        reference.JsonPath,
                        reference.RuleName,
                        "'" + reference.ReferencedName + "' does not resolve to a defined item."
                    ));
                }
            }
        }

        /// <summary>
        /// Reads a required string field, reporting a missing or wrong-typed field.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="missingRule">The rule for an absent field.</param>
        /// <param name="typeRule">The rule for a wrong-typed field.</param>
        /// <param name="value">The read string, or null on failure.</param>
        /// <returns>True when a string was read.</returns>
        public bool TryString(
            JsonObject owner,
            string key,
            string jsonPath,
            string missingRule,
            string typeRule,
            out string value
        )
        {
            value = null;
            if (!owner.TryGet(key, out JsonValue node))
            {
                AddError(jsonPath, missingRule, "missing required field '" + key + "'.");
                return false;
            }

            if (node is not JsonString text)
            {
                AddError(jsonPath + "." + key, typeRule, "field '" + key + "' must be a string.");
                return false;
            }

            value = text.Value;
            return true;
        }

        /// <summary>
        /// Reads a required integer field (a JSON number written without a fraction or exponent).
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="missingRule">The rule for an absent field.</param>
        /// <param name="typeRule">The rule for a wrong-typed field.</param>
        /// <param name="value">The read value, or zero on failure.</param>
        /// <returns>True when an integer was read.</returns>
        public bool TryInteger(
            JsonObject owner,
            string key,
            string jsonPath,
            string missingRule,
            string typeRule,
            out long value
        )
        {
            value = 0L;
            if (!owner.TryGet(key, out JsonValue node))
            {
                AddError(jsonPath, missingRule, "missing required field '" + key + "'.");
                return false;
            }

            if (node is not JsonNumber { IsInteger: true } number)
            {
                AddError(jsonPath + "." + key, typeRule, "field '" + key + "' must be an integer.");
                return false;
            }

            value = number.IntegerValue;
            return true;
        }

        /// <summary>
        /// Reads a required boolean field.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="missingRule">The rule for an absent field.</param>
        /// <param name="typeRule">The rule for a wrong-typed field.</param>
        /// <param name="value">The read value, or false on failure.</param>
        /// <returns>True when a boolean was read.</returns>
        public bool TryBoolean(
            JsonObject owner,
            string key,
            string jsonPath,
            string missingRule,
            string typeRule,
            out bool value
        )
        {
            value = false;
            if (!owner.TryGet(key, out JsonValue node))
            {
                AddError(jsonPath, missingRule, "missing required field '" + key + "'.");
                return false;
            }

            if (node is not JsonBool flag)
            {
                AddError(jsonPath + "." + key, typeRule, "field '" + key + "' must be a boolean.");
                return false;
            }

            value = flag.Value;
            return true;
        }

        /// <summary>
        /// Reads a required array field.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="missingRule">The rule for an absent field.</param>
        /// <param name="typeRule">The rule for a wrong-typed field.</param>
        /// <param name="value">The read array, or null on failure.</param>
        /// <returns>True when an array was read.</returns>
        public bool TryArray(
            JsonObject owner,
            string key,
            string jsonPath,
            string missingRule,
            string typeRule,
            out JsonArray value
        )
        {
            value = null;
            if (!owner.TryGet(key, out JsonValue node))
            {
                AddError(jsonPath, missingRule, "missing required field '" + key + "'.");
                return false;
            }

            if (node is not JsonArray array)
            {
                AddError(jsonPath + "." + key, typeRule, "field '" + key + "' must be an array.");
                return false;
            }

            value = array;
            return true;
        }

        /// <summary>
        /// Reads a required object field.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="missingRule">The rule for an absent field.</param>
        /// <param name="typeRule">The rule for a wrong-typed field.</param>
        /// <param name="value">The read object, or null on failure.</param>
        /// <returns>True when an object was read.</returns>
        public bool TryObject(
            JsonObject owner,
            string key,
            string jsonPath,
            string missingRule,
            string typeRule,
            out JsonObject value
        )
        {
            value = null;
            if (!owner.TryGet(key, out JsonValue node))
            {
                AddError(jsonPath, missingRule, "missing required field '" + key + "'.");
                return false;
            }

            if (node is not JsonObject nested)
            {
                AddError(jsonPath + "." + key, typeRule, "field '" + key + "' must be an object.");
                return false;
            }

            value = nested;
            return true;
        }

        /// <summary>
        /// Reads a required numeric field of any lexical form.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="missingRule">The rule for an absent field.</param>
        /// <param name="typeRule">The rule for a wrong-typed field.</param>
        /// <param name="value">The read number, or null on failure.</param>
        /// <returns>True when a number was read.</returns>
        public bool TryNumber(
            JsonObject owner,
            string key,
            string jsonPath,
            string missingRule,
            string typeRule,
            out JsonNumber value
        )
        {
            value = null;
            if (!owner.TryGet(key, out JsonValue node))
            {
                AddError(jsonPath, missingRule, "missing required field '" + key + "'.");
                return false;
            }

            if (node is not JsonNumber number)
            {
                AddError(jsonPath + "." + key, typeRule, "field '" + key + "' must be a number.");
                return false;
            }

            value = number;
            return true;
        }

        /// <summary>
        /// Reports any key of the owner not in the allowed set.
        /// </summary>
        /// <param name="owner">The object to scan.</param>
        /// <param name="allowed">The allowed keys.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="ruleName">The rule for an unexpected key.</param>
        public void RejectUnknownKeys(
            JsonObject owner,
            ControlledVocabulary allowed,
            string jsonPath,
            string ruleName
        )
        {
            IReadOnlyList<string> keys = owner.Keys;
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                if (!allowed.Contains(key))
                    AddError(jsonPath + "." + key, ruleName, "unexpected field '" + key + "'.");
            }
        }

        /// <summary>
        /// Reports a value that is not a member of the controlled set.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="set">The controlled set.</param>
        /// <param name="jsonPath">The value's JSON path.</param>
        /// <param name="ruleName">The rule for a non-member.</param>
        public void RequireMembership(
            string value,
            ControlledVocabulary set,
            string jsonPath,
            string ruleName
        )
        {
            if (!set.Contains(value))
                AddError(jsonPath, ruleName, "'" + value + "' is not a controlled value.");
        }

        /// <summary>
        /// Validates the common definition fields (id format, rules text, display name, category match,
        /// rarity, tags) and registers the definition for the cross-cutting rules.
        /// </summary>
        /// <param name="definition">The definition object.</param>
        /// <param name="jsonPath">The definition's JSON path.</param>
        /// <param name="categoryToken">The file's declared category token.</param>
        /// <param name="idPrefix">The expected WB ID category prefix.</param>
        /// <param name="allowedKeys">The definition's allowed key set.</param>
        public void ValidateCommonFields(
            JsonObject definition,
            string jsonPath,
            string categoryToken,
            string idPrefix,
            ControlledVocabulary allowedKeys
        )
        {
            RejectUnknownKeys(definition, allowedKeys, jsonPath, "definition.unknown-field");

            string id = null;
            if (TryString(definition, "id", jsonPath, "definition.missing-field", "definition.field-type", out string idValue))
            {
                id = idValue;
                if (!IsValidID(idValue, idPrefix))
                    AddError(jsonPath, "definition.id-format", "the id '" + idValue + "' must match WB-" + idPrefix + "-### for its category.");
            }

            TryString(definition, "rulesText", jsonPath, "definition.missing-field", "definition.field-type", out _);

            string displayName = null;
            if (TryString(definition, "displayName", jsonPath, "definition.missing-field", "definition.field-type", out string displayValue))
                displayName = displayValue;

            if (TryString(definition, "category", jsonPath, "definition.missing-field", "definition.field-type", out string categoryValue) && !string.Equals(categoryValue, categoryToken, StringComparison.Ordinal))
                AddError(jsonPath + ".category", "definition.category-mismatch", "the category '" + categoryValue + "' does not match the file category '" + categoryToken + "'.");

            if (TryString(definition, "rarity", jsonPath, "definition.missing-field", "definition.field-type", out string rarityValue))
                RequireMembership(rarityValue, RarityTokens, jsonPath + ".rarity", "definition.unknown-rarity");

            ValidateTags(definition, jsonPath);

            if (id != null) RegisterDefinition(id, displayName ?? id, jsonPath);
        }

        /// <summary>
        /// Validates a definition's <c>effects</c> array (required) and each effect within it.
        /// </summary>
        /// <param name="definition">The definition object.</param>
        /// <param name="jsonPath">The definition's JSON path.</param>
        public void ValidateEffects(JsonObject definition, string jsonPath)
        {
            if (!TryArray(definition, "effects", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray effects))
                return;
            
            for (int index = 0; index < effects.Items.Count; index++)
            {
                JsonValue item = effects.Items[index];
                string path = jsonPath + ".effects[" + index + "]";
                if (item is JsonObject effect) ValidateEffect(effect, path);
                else AddError(path, "effect.not-object", "each effect must be an object.");
            }
        }

        /// <summary>
        /// Validates one declarative effect: field set, phase domain, operation, stacking, the
        /// per-domain trigger requirement, and the optional trigger/targeting/timing/frequency blocks.
        /// </summary>
        /// <param name="effect">The effect object.</param>
        /// <param name="jsonPath">The effect's JSON path.</param>
        public void ValidateEffect(JsonObject effect, string jsonPath)
        {
            RejectUnknownKeys(effect, EffectKeys, jsonPath, "effect.unknown-field");

            string phaseDomain = null;
            if (effect.TryGet("phaseDomain", out JsonValue phaseNode))
            {
                if (phaseNode is JsonString phaseText && PhaseDomainTokens.Contains(phaseText.Value))
                {
                    phaseDomain = phaseText.Value;
                }
                else
                {
                    string found = phaseNode is JsonString text ? text.Value : "<non-string>";
                    AddError(jsonPath + ".phaseDomain", "effect.unknown-phase-domain", "'" + found + "' is not a phase domain.");
                }
            }
            else AddError(jsonPath, "effect.missing-field", "an effect requires a phaseDomain.");

            if (TryObject(effect, "operation", jsonPath, "effect.missing-field", "effect.missing-field", out JsonObject operation))
                ValidateOperation(operation, jsonPath + ".operation");
            
            if (TryString(effect, "stacking", jsonPath, "effect.missing-field", "effect.unknown-stacking", out string stacking))
                RequireMembership(stacking, StackingTokens, jsonPath + ".stacking", "effect.unknown-stacking");

            bool hasTrigger = effect.TryGet("trigger", out JsonValue triggerNode);
            switch (hasTrigger)
            {
                case true when triggerNode is JsonObject triggerObject:
                    ValidateTrigger(triggerObject, jsonPath + ".trigger");
                    break;
                
                case false when string.Equals(phaseDomain, "EXECUTION", StringComparison.Ordinal):
                    AddError(jsonPath, "effect.trigger-required", "EXECUTION effects require a trigger.");
                    break;
            }

            if (effect.TryGet("targeting", out JsonValue targetingNode) && targetingNode is JsonObject targetingObject)
                ValidateTargeting(targetingObject, jsonPath + ".targeting");

            if (effect.TryGet("timing", out JsonValue timingNode) && timingNode is JsonObject timingObject)
                ValidateTiming(timingObject, jsonPath + ".timing");

            if (effect.TryGet("frequency", out JsonValue frequencyNode) && frequencyNode is JsonObject frequencyObject)
                ValidateFrequency(frequencyObject, jsonPath + ".frequency");
        }

        /// <summary>
        /// Validates an operation object: kind membership and the kind-specific payload.
        /// </summary>
        /// <param name="operation">The operation object.</param>
        /// <param name="jsonPath">The operation's JSON path.</param>
        public void ValidateOperation(JsonObject operation, string jsonPath)
        {
            if (!TryString(operation, "kind", jsonPath, "effect.operation-payload", "effect.operation-payload", out string kind))
                return;

            if (!OperationKindTokens.Contains(kind))
            {
                AddError(jsonPath + ".kind", "effect.unknown-operation-kind", "'" + kind + "' is not an operation kind.");
                return;
            }

            switch (kind)
            {
                case "QUANTITY_CHANGE":
                    ValidateQuantityChange(operation, jsonPath);
                    break;
                
                case "DISPOSITION_CHANGE":
                    RequireControlledField(operation, "newDisposition", CatalogVocabulary.DispositionNames, jsonPath);
                    break;
                
                case "ADDED_EXECUTION_REQUEST":
                    ValidateTargetingField(operation, "target", jsonPath);
                    RequireBooleanField(operation, "cancelOnInvalid", jsonPath);
                    break;
                
                case "COUNTER_REQUEST":
                    RequireControlledField(operation, "counter", CatalogVocabulary.CounterNames, jsonPath);
                    RequireIntegerField(operation, "delta", jsonPath);
                    RequireIntegerField(operation, "floor", jsonPath);
                    RequireIntegerField(operation, "ceiling", jsonPath);
                    RequireBooleanField(operation, "hasFloor", jsonPath);
                    RequireBooleanField(operation, "hasCeiling", jsonPath);
                    break;
                
                case "COST_MODIFICATION":
                    RequireControlledField(operation, "costKind", CatalogVocabulary.CostKinds, jsonPath);
                    RequireBooleanField(operation, "setsAbsolute", jsonPath);
                    RequireIntegerField(operation, "amount", jsonPath);
                    RequireIntegerField(operation, "floor", jsonPath);
                    RequireBooleanField(operation, "progressionAdvances", jsonPath);
                    break;
                
                case "RESCUE":
                    RequireControlledField(operation, "resultingDisposition", CatalogVocabulary.DispositionNames, jsonPath);
                    break;
                
                case "PREDICTION_VISIBILITY":
                    RequireControlledField(operation, "projection", CatalogVocabulary.PredictionProjections, jsonPath);
                    break;
                
                case "CONFIGURATION_MODIFICATION":
                    RequireControlledField(operation, "setting", CatalogVocabulary.ConfigurationSettings, jsonPath);
                    RequireIntegerField(operation, "amount", jsonPath);
                    RequireBooleanField(operation, "setsAbsolute", jsonPath);
                    break;
                
                case "OPERATION_MODIFICATION":
                    RequireIntegerField(operation, "operandDelta", jsonPath);
                    break;
                
                case "TARGET_LOCK_UPDATE":
                    ValidateTargetingField(operation, "selection", jsonPath);
                    break;
            }
        }

        /// <summary>
        /// Validates a single trigger qualifier: controlled kind and a present value.
        /// </summary>
        /// <param name="qualifier">The qualifier object.</param>
        /// <param name="jsonPath">The qualifier's JSON path.</param>
        public void ValidateQualifier(JsonObject qualifier, string jsonPath)
        {
            if (TryString(qualifier, "kind", jsonPath, "effect.missing-field", "effect.unknown-qualifier-kind", out string kind))
                RequireMembership(kind, CatalogVocabulary.QualifierKinds, jsonPath + ".kind", "effect.unknown-qualifier-kind");

            TryString(qualifier, "value", jsonPath, "effect.missing-field", "effect.missing-field", out _);
        }

        private static bool IsValidID(string id, string prefix)
        {
            string expected = "WB-" + prefix + "-";
            if (!id.StartsWith(expected, StringComparison.Ordinal))
                return false;

            string tail = id.Substring(expected.Length);
            if (tail.Length != 3)
                return false;

            for (int index = 0; index < tail.Length; index++)
            {
                char digit = tail[index];
                if (digit is < '0' or > '9')
                    return false;
            }

            return true;
        }

        private static bool IsCanonicalFrequencyPair(string allowance, string scope)
        {
            switch (allowance)
            {
                case "ONCE":
                case "FIRST_QUALIFYING_EVENT":
                case "FIRST_SUCCESSFUL_RESOLUTION":
                    return _resetScopes.Contains(scope);
                
                case "UP_TO_N":
                    return _resetScopes.Contains(scope) || string.Equals(scope, "DECLARED_SCOPE", StringComparison.Ordinal);
                
                case "EVERY_QUALIFYING_EVENT":
                    return string.Equals(scope, "DECLARED_SCOPE", StringComparison.Ordinal);
                
                default:
                    return false;
            }
        }

        private void ValidateTags(JsonObject definition, string jsonPath)
        {
            if (!TryArray(definition, "tags", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray tags))
                return;

            for (int index = 0; index < tags.Items.Count; index++)
            {
                JsonValue tag = tags.Items[index];
                if (tag is not JsonString text || text.Value.Length == 0)
                {
                    AddError(jsonPath + ".tags[" + index + "]", "definition.empty-tag", "tags must be non-empty strings.");
                }
            }
        }

        private void ValidateQuantityChange(JsonObject operation, string jsonPath)
        {
            if (TryString(operation, "register", jsonPath, "effect.operation-payload", "effect.operation-payload", out string register))
                RequireMembership(register, RegisterTokens, jsonPath + ".register", "effect.operation-payload");

            if (TryString(operation, "operator", jsonPath, "effect.operation-payload", "effect.operation-payload", out string quantityOperator))
                RequireMembership(quantityOperator, QuantityOperatorTokens, jsonPath + ".operator", "effect.operation-payload");

            if (TryObject(operation, "operand", jsonPath, "effect.operation-payload", "effect.operation-payload", out JsonObject operand))
                ValidateOperand(operand, jsonPath + ".operand");
        }

        private void ValidateOperand(JsonObject operand, string jsonPath)
        {
            if (!TryString(operand, "source", jsonPath, "effect.operation-payload", "effect.operation-payload", out string source))
                return;

            if (!OperandSourceTokens.Contains(source))
            {
                AddError(jsonPath + ".source", "effect.operation-payload", "'" + source + "' is not an operand source.");
                return;
            }

            if (string.Equals(source, "CONSTANT", StringComparison.Ordinal))
                RequireIntegerField(operand, "constant", jsonPath);
            else if (string.Equals(source, "REGISTER", StringComparison.Ordinal) && TryString(operand, "register", jsonPath, "effect.operation-payload", "effect.operation-payload", out string register))
                RequireMembership(register, RegisterTokens, jsonPath + ".register", "effect.operation-payload");
        }

        private void ValidateTrigger(JsonObject trigger, string jsonPath)
        {
            if (TryString(trigger, "eventFamily", jsonPath, "effect.missing-field", "effect.unknown-event-family", out string family))
                RequireMembership(family, EventFamilyTokens, jsonPath + ".eventFamily", "effect.unknown-event-family");

            if (TryString(trigger, "eventSubtype", jsonPath, "effect.missing-field", "effect.unknown-subtype", out string subtype))
                RequireMembership(subtype, CatalogVocabulary.EventSubtypes, jsonPath + ".eventSubtype", "effect.unknown-subtype");

            if (trigger.TryGet("qualifiers", out JsonValue qualifiersNode) && qualifiersNode is JsonArray qualifiers)
            {
                for (int index = 0; index < qualifiers.Items.Count; index++)
                {
                    if (qualifiers.Items[index] is JsonObject qualifier)
                        ValidateQualifier(qualifier, jsonPath + ".qualifiers[" + index + "]");
                }
            }

            if (trigger.TryGet("timing", out JsonValue timingNode) && timingNode is JsonObject timing)
                ValidateTiming(timing, jsonPath + ".timing");
        }

        private void ValidateTargeting(JsonObject targeting, string jsonPath)
        {
            if (TryString(targeting, "kind", jsonPath, "effect.missing-field", "effect.unknown-targeting-kind", out string kind))
                RequireMembership(kind, CatalogVocabulary.TargetingKinds, jsonPath + ".kind", "effect.unknown-targeting-kind");
        }

        private void ValidateTiming(JsonObject timing, string jsonPath)
        {
            if (!TryString(timing, "kind", jsonPath, "effect.missing-field", "effect.unknown-timing", out string kind))
                return;

            if (!TimingKindTokens.Contains(kind))
            {
                AddError(jsonPath + ".kind", "effect.unknown-timing", "'" + kind + "' is not a timing kind.");
                return;
            }

            if (!TryString(timing, "name", jsonPath, "effect.missing-field", "effect.unknown-timing", out string name))
                return;

            if (string.Equals(kind, "BAND", StringComparison.Ordinal))
            {
                if (CatalogVocabulary.TimingBands.Contains(name))
                    return;

                string rule = CatalogVocabulary.TimingBoundaries.Contains(name) ? "effect.timing-mismatch" : "effect.unknown-timing";
                AddError(jsonPath + ".name", rule, "'" + name + "' is not a timing band.");
            }
            else
            {
                if (CatalogVocabulary.TimingBoundaries.Contains(name))
                {
                    return;
                }

                string rule = CatalogVocabulary.TimingBands.Contains(name) ? "effect.timing-mismatch" : "effect.unknown-timing";
                AddError(jsonPath + ".name", rule, "'" + name + "' is not a named boundary.");
            }
        }

        private void ValidateFrequency(JsonObject frequency, string jsonPath)
        {
            bool allowanceOk = TryString(frequency, "allowance", jsonPath, "effect.missing-field", "effect.unknown-frequency-allowance", out string allowance);
            if (allowanceOk && !CatalogVocabulary.FrequencyAllowances.Contains(allowance))
            {
                AddError(jsonPath + ".allowance", "effect.unknown-frequency-allowance", "'" + allowance + "' is not a frequency allowance.");
                allowanceOk = false;
            }

            bool scopeOk = TryString(frequency, "scope", jsonPath, "effect.missing-field", "effect.unknown-frequency-scope", out string scope);
            if (scopeOk && !CatalogVocabulary.FrequencyScopes.Contains(scope))
            {
                AddError(jsonPath + ".scope", "effect.unknown-frequency-scope", "'" + scope + "' is not a frequency scope.");
                scopeOk = false;
            }

            if (allowanceOk && scopeOk && !IsCanonicalFrequencyPair(allowance, scope))
            {
                AddError(jsonPath, "effect.frequency-pair", "the frequency '" + allowance + "' with reset scope '" + scope + "' is not a canonical form.");
            }
        }

        private void RequireControlledField(
            JsonObject owner,
            string key,
            ControlledVocabulary set,
            string jsonPath
        )
        {
            if (TryString(owner, key, jsonPath, "effect.operation-payload", "effect.operation-payload", out string value))
                RequireMembership(value, set, jsonPath + "." + key, "effect.operation-payload");
        }

        private void RequireIntegerField(JsonObject owner, string key, string jsonPath)
        {
            TryInteger(owner, key, jsonPath, "effect.operation-payload", "effect.operation-payload", out _);
        }

        private void RequireBooleanField(JsonObject owner, string key, string jsonPath)
        {
            TryBoolean(owner, key, jsonPath, "effect.operation-payload", "effect.operation-payload", out _);
        }

        private void ValidateTargetingField(JsonObject owner, string key, string jsonPath)
        {
            if (TryObject(owner, key, jsonPath, "effect.operation-payload", "effect.operation-payload", out JsonObject targeting))
                ValidateTargeting(targeting, jsonPath + "." + key);
        }

        /// <summary>
        /// A reference to another definition's display name, pending resolution after all files load.
        /// </summary>
        /// <param name="File">The file the reference occurs in.</param>
        /// <param name="JsonPath">The JSON path of the reference.</param>
        /// <param name="ReferencedName">The referenced display name.</param>
        /// <param name="RuleName">The rule to report if it does not resolve.</param>
        private readonly record struct PendingReference(
            string File,
            string JsonPath,
            string ReferencedName,
            string RuleName
        );
    }
}