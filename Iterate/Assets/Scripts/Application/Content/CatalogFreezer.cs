using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Builds the frozen, immutable <see cref="ContentCatalog"/> from a validated file set. Assumes the
    /// validator has passed, so it reads fields directly; any exception thrown while freezing one file
    /// is wrapped into a <see cref="CatalogLoadException"/> carrying a single <c>freeze.unexpected</c>
    /// error naming that file.
    /// </summary>
    public sealed class CatalogFreezer
    {
        /// <summary>
        /// Freezes the validated catalog into immutable definitions.
        /// </summary>
        /// <param name="manifest">The typed manifest projection.</param>
        /// <param name="files">The parsed, validated file set.</param>
        /// <returns>The frozen catalog.</returns>
        public ContentCatalog Freeze(CatalogManifest manifest, CatalogFileSet files)
        {
            ParameterSet parameters = null;
            List<InstructionDefinition> instructions = new();
            List<StructureDefinition> structures = new();
            List<DirectiveDefinition> directives = new();
            List<DependencyDefinition> dependencies = new();
            List<PatchDefinition> patches = new();
            List<UtilityDefinition> utilities = new();

            for (int index = 0; index < manifest.Files.Count; index++)
            {
                CatalogManifestEntry entry = manifest.Files[index];
                try
                {
                    JsonArray rows = (JsonArray)files.ContentFiles[entry.File];
                    switch (entry.Kind)
                    {
                        case CatalogFileKind.Parameters:
                            parameters = FreezeParameters(rows);
                            break;
                        
                        case CatalogFileKind.Instruction:
                            FreezeInstructions(rows, instructions);
                            break;
                        
                        case CatalogFileKind.Structure:
                            FreezeStructures(rows, structures);
                            break;
                        
                        case CatalogFileKind.Directive:
                            FreezeDirectives(rows, directives);
                            break;
                        
                        case CatalogFileKind.Dependency:
                            FreezeDependencies(rows, dependencies);
                            break;
                        
                        case CatalogFileKind.Patch:
                            FreezePatches(rows, patches);
                            break;
                        
                        case CatalogFileKind.Utility:
                            FreezeUtilities(rows, utilities);
                            break;
                    }
                }
                catch (CatalogLoadException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new CatalogLoadException(new[]
                    {
                        new CatalogError(entry.File, "$", "freeze.unexpected", "unexpected error freezing the file: " + exception.Message)
                    });
                }
            }

            return new ContentCatalog(
                manifest.Revision,
                parameters,
                instructions,
                structures,
                directives,
                dependencies,
                patches,
                utilities
            );
        }

        /// <summary>
        /// Builds the parameter register from its rows.
        /// </summary>
        /// <param name="rows">The parameter rows.</param>
        /// <returns>The constructed parameter set.</returns>
        private static ParameterSet FreezeParameters(JsonArray rows)
        {
            Dictionary<string, double> values = new(StringComparer.Ordinal);
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                values[ReadString(row, "id")] = ReadNumber(row, "value");
            }

            return new ParameterSet(values);
        }

        /// <summary>
        /// Freezes each Instruction row into the target list.
        /// </summary>
        /// <param name="rows">The Instruction rows.</param>
        /// <param name="target">The list to append the frozen Instructions to.</param>
        private static void FreezeInstructions(JsonArray rows, List<InstructionDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                TriggerQualifier positionQualifier = Has(row, "positionQualifier")
                    ? FreezeQualifier(ReadObject(row, "positionQualifier"))
                    : null;
                IReadOnlyList<string> ineligibility = Has(row, "ineligibilityTags")
                    ? ReadStringList(ReadArray(row, "ineligibilityTags"))
                    : Array.Empty<string>();

                target.Add(new InstructionDefinition(
                    new InstructionID(ReadString(row, "id")),
                    ReadString(row, "rulesText"),
                    ReadString(row, "displayName"),
                    ParseEnum<ContentCategory>(ReadString(row, "category")),
                    ParseEnum<Rarity>(ReadString(row, "rarity")),
                    ReadStringList(ReadArray(row, "tags")),
                    ReadInteger(row, "sourceFootprint"),
                    (QuantityChangeOperation)FreezeOperation(ReadObject(row, "primaryOperation")),
                    positionQualifier,
                    ineligibility
                ));
            }
        }

        /// <summary>
        /// Freezes each Structure row into the target list.
        /// </summary>
        /// <param name="rows">The Structure rows.</param>
        /// <param name="target">The list to append the frozen Structures to.</param>
        private static void FreezeStructures(JsonArray rows, List<StructureDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                int repeatCount = Has(row, "repeatCount") ? ReadInteger(row, "repeatCount") : 0;
                StructurePredicate predicate = Has(row, "predicate")
                    ? FreezePredicate(ReadObject(row, "predicate"))
                    : null;

                target.Add(new StructureDefinition(
                    new StructureID(ReadString(row, "id")),
                    ReadString(row, "rulesText"),
                    ReadString(row, "displayName"),
                    ParseEnum<ContentCategory>(ReadString(row, "category")),
                    ParseEnum<Rarity>(ReadString(row, "rarity")),
                    ReadStringList(ReadArray(row, "tags")),
                    ReadInteger(row, "sourceFootprint"),
                    ParseEnum<StructureKind>(ReadString(row, "structureKind")),
                    repeatCount,
                    predicate
                ));
            }
        }

        /// <summary>
        /// Freezes each Directive row into the target list.
        /// </summary>
        /// <param name="rows">The Directive rows.</param>
        /// <param name="target">The list to append the frozen Directives to.</param>
        private static void FreezeDirectives(JsonArray rows, List<DirectiveDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new DirectiveDefinition(
                    new DirectiveID(ReadString(row, "id")),
                    ReadString(row, "rulesText"),
                    ReadString(row, "displayName"),
                    ParseEnum<ContentCategory>(ReadString(row, "category")),
                    ParseEnum<Rarity>(ReadString(row, "rarity")),
                    ReadStringList(ReadArray(row, "tags")),
                    FreezeEffects(row)
                ));
            }
        }

        /// <summary>
        /// Freezes each Dependency row into the target list.
        /// </summary>
        /// <param name="rows">The Dependency rows.</param>
        /// <param name="target">The list to append the frozen Dependencies to.</param>
        private static void FreezeDependencies(JsonArray rows, List<DependencyDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new DependencyDefinition(
                    new DependencyID(ReadString(row, "id")),
                    ReadString(row, "rulesText"),
                    ReadString(row, "displayName"),
                    ParseEnum<ContentCategory>(ReadString(row, "category")),
                    ParseEnum<Rarity>(ReadString(row, "rarity")),
                    ReadStringList(ReadArray(row, "tags")),
                    ReadInteger(row, "ram"),
                    FreezeEffects(row)
                ));
            }
        }

        /// <summary>
        /// Freezes each Patch row into the target list.
        /// </summary>
        /// <param name="rows">The Patch rows.</param>
        /// <param name="target">The list to append the frozen Patches to.</param>
        private static void FreezePatches(JsonArray rows, List<PatchDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new PatchDefinition(
                    new PatchID(ReadString(row, "id")),
                    ReadString(row, "rulesText"),
                    ReadString(row, "displayName"),
                    ParseEnum<ContentCategory>(ReadString(row, "category")),
                    ParseEnum<Rarity>(ReadString(row, "rarity")),
                    ReadStringList(ReadArray(row, "tags")),
                    new PatchHostEligibility(ReadString(row, "hostEligibility")),
                    FreezeEffects(row)
                ));
            }
        }

        /// <summary>
        /// Freezes each Utility row into the target list.
        /// </summary>
        /// <param name="rows">The Utility rows.</param>
        /// <param name="target">The list to append the frozen Utilities to.</param>
        private static void FreezeUtilities(JsonArray rows, List<UtilityDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new UtilityDefinition(
                    new UtilityID(ReadString(row, "id")),
                    ReadString(row, "rulesText"),
                    ReadString(row, "displayName"),
                    ParseEnum<ContentCategory>(ReadString(row, "category")),
                    ParseEnum<Rarity>(ReadString(row, "rarity")),
                    ReadStringList(ReadArray(row, "tags")),
                    FreezeEffects(row)
                ));
            }
        }

        /// <summary>
        /// Freezes a definition's effects array.
        /// </summary>
        /// <param name="definition">The definition object carrying the effects.</param>
        /// <returns>The frozen effect list.</returns>
        private static IReadOnlyList<EffectDefinition> FreezeEffects(JsonObject definition)
        {
            JsonArray raw = ReadArray(definition, "effects");
            List<EffectDefinition> effects = new(raw.Items.Count);
            for (int index = 0; index < raw.Items.Count; index++)
            {
                effects.Add(FreezeEffect((JsonObject)raw.Items[index]));
            }

            return effects;
        }

        /// <summary>
        /// Freezes one effect: phase domain, optional trigger/targeting/timing/frequency, operation,
        /// and stacking.
        /// </summary>
        /// <param name="effect">The effect object.</param>
        /// <returns>The frozen effect definition.</returns>
        private static EffectDefinition FreezeEffect(JsonObject effect)
        {
            TriggerDescriptor trigger = Has(effect, "trigger") ? FreezeTrigger(ReadObject(effect, "trigger")) : null;
            TargetingRule targeting = Has(effect, "targeting") ? FreezeTargeting(ReadObject(effect, "targeting")) : null;
            EffectTiming timing = Has(effect, "timing") ? FreezeTiming(ReadObject(effect, "timing")) : null;
            EffectFrequency frequency = null;
            if (Has(effect, "frequency"))
            {
                JsonObject raw = ReadObject(effect, "frequency");
                frequency = new EffectFrequency(ReadString(raw, "allowance"), ReadString(raw, "scope"));
            }

            return new EffectDefinition(
                ParseEnum<PhaseDomain>(ReadString(effect, "phaseDomain")),
                trigger,
                FreezeOperation(ReadObject(effect, "operation")),
                targeting,
                timing,
                ParseEnum<StackingMode>(ReadString(effect, "stacking")),
                frequency
            );
        }

        /// <summary>
        /// Freezes a trigger descriptor: event family and subtype, qualifiers, and observation timing.
        /// </summary>
        /// <param name="trigger">The trigger object.</param>
        /// <returns>The frozen trigger descriptor.</returns>
        private static TriggerDescriptor FreezeTrigger(JsonObject trigger)
        {
            List<TriggerQualifier> qualifiers = new();
            if (Has(trigger, "qualifiers"))
            {
                JsonArray raw = ReadArray(trigger, "qualifiers");
                for (int index = 0; index < raw.Items.Count; index++)
                {
                    qualifiers.Add(FreezeQualifier((JsonObject)raw.Items[index]));
                }
            }

            EffectTiming timing = Has(trigger, "timing") ? FreezeTiming(ReadObject(trigger, "timing")) : null;
            return new TriggerDescriptor(
                ParseEnum<EventFamily>(ReadString(trigger, "eventFamily")),
                ReadString(trigger, "eventSubtype"),
                qualifiers,
                timing
            );
        }

        /// <summary>
        /// Freezes an operation by dispatching on its kind to the matching typed operation record.
        /// </summary>
        /// <param name="operation">The operation object.</param>
        /// <returns>The frozen operation.</returns>
        private static EffectOperation FreezeOperation(JsonObject operation)
        {
            string kind = ReadString(operation, "kind");
            switch (kind)
            {
                case "QUANTITY_CHANGE":
                    return FreezeQuantityChange(operation);
                case "DISPOSITION_CHANGE":
                    return new DispositionChangeOperation(ReadString(operation, "newDisposition"));
                case "ADDED_EXECUTION_REQUEST":
                    return new AddedExecutionRequestOperation(
                        FreezeTargeting(ReadObject(operation, "target")),
                        ReadBoolean(operation, "cancelOnInvalid")
                    );
                case "COUNTER_REQUEST":
                    return new CounterRequestOperation(
                        ReadString(operation, "counter"),
                        ReadInteger(operation, "delta"),
                        ReadInteger(operation, "floor"),
                        ReadInteger(operation, "ceiling"),
                        ReadBoolean(operation, "hasFloor"),
                        ReadBoolean(operation, "hasCeiling")
                    );
                case "COST_MODIFICATION":
                    return new CostModificationOperation(
                        ReadString(operation, "costKind"),
                        ReadBoolean(operation, "setsAbsolute"),
                        ReadInteger(operation, "amount"),
                        ReadInteger(operation, "floor"),
                        ReadBoolean(operation, "progressionAdvances")
                    );
                case "RESCUE":
                    return new RescueOperation(ReadString(operation, "resultingDisposition"));
                case "PREDICTION_VISIBILITY":
                    return new PredictionVisibilityOperation(ReadString(operation, "projection"));
                case "CONFIGURATION_MODIFICATION":
                    return new ConfigurationModificationOperation(
                        ReadString(operation, "setting"),
                        ReadInteger(operation, "amount"),
                        ReadBoolean(operation, "setsAbsolute")
                    );
                case "OPERATION_MODIFICATION":
                    return new OperationModificationOperation(ReadInteger(operation, "operandDelta"));
                case "TARGET_LOCK_UPDATE":
                    return new TargetLockUpdateOperation(FreezeTargeting(ReadObject(operation, "selection")));
                default:
                    throw new InvalidOperationException("unknown operation kind '" + kind + "'.");
            }
        }

        /// <summary>
        /// Freezes a quantity-change operation: register, operator, and operand.
        /// </summary>
        /// <param name="operation">The operation object.</param>
        /// <returns>The frozen quantity-change operation.</returns>
        private static QuantityChangeOperation FreezeQuantityChange(JsonObject operation)
        {
            return new QuantityChangeOperation(
                ParseEnum<CoreRegister>(ReadString(operation, "register")),
                ParseEnum<QuantityOperator>(ReadString(operation, "operator")),
                FreezeOperand(ReadObject(operation, "operand"))
            );
        }

        /// <summary>
        /// Freezes an operand into its constant, register, or line-number spec.
        /// </summary>
        /// <param name="operand">The operand object.</param>
        /// <returns>The frozen operand spec.</returns>
        private static OperandSpec FreezeOperand(JsonObject operand)
        {
            OperandSource source = ParseEnum<OperandSource>(ReadString(operand, "source"));
            switch (source)
            {
                case OperandSource.Constant:
                    return OperandSpec.FromConstant(ReadInteger(operand, "constant"));
                case OperandSource.Register:
                    return OperandSpec.FromRegister(ParseEnum<CoreRegister>(ReadString(operand, "register")));
                default:
                    return OperandSpec.FromLineNumber();
            }
        }

        /// <summary>
        /// Freezes a Condition predicate: register, comparison, and operand.
        /// </summary>
        /// <param name="predicate">The predicate object.</param>
        /// <returns>The frozen predicate.</returns>
        private static StructurePredicate FreezePredicate(JsonObject predicate)
        {
            return new StructurePredicate(
                ParseEnum<CoreRegister>(ReadString(predicate, "register")),
                ParseEnum<PredicateComparison>(ReadString(predicate, "comparison")),
                ReadInteger(predicate, "operand")
            );
        }

        /// <summary>
        /// Freezes a targeting rule: kind and optional argument.
        /// </summary>
        /// <param name="targeting">The targeting object.</param>
        /// <returns>The frozen targeting rule.</returns>
        private static TargetingRule FreezeTargeting(JsonObject targeting)
        {
            string argument = Has(targeting, "argument") ? ReadString(targeting, "argument") : string.Empty;
            return new TargetingRule(ReadString(targeting, "kind"), argument);
        }

        /// <summary>
        /// Freezes a trigger qualifier: kind and value.
        /// </summary>
        /// <param name="qualifier">The qualifier object.</param>
        /// <returns>The frozen qualifier.</returns>
        private static TriggerQualifier FreezeQualifier(JsonObject qualifier)
        {
            return new TriggerQualifier(ReadString(qualifier, "kind"), ReadString(qualifier, "value"));
        }

        /// <summary>
        /// Freezes an effect timing: band-or-boundary kind and its name.
        /// </summary>
        /// <param name="timing">The timing object.</param>
        /// <returns>The frozen timing.</returns>
        private static EffectTiming FreezeTiming(JsonObject timing)
        {
            return new EffectTiming(ParseEnum<TimingKind>(ReadString(timing, "kind")), ReadString(timing, "name"));
        }

        /// <summary>
        /// Reads a JSON array of strings into a string list.
        /// </summary>
        /// <param name="array">The array of string values.</param>
        /// <returns>The read string list.</returns>
        private static IReadOnlyList<string> ReadStringList(JsonArray array)
        {
            List<string> values = new(array.Items.Count);
            for (int index = 0; index < array.Items.Count; index++)
            {
                values.Add(((JsonString)array.Items[index]).Value);
            }

            return values;
        }

        /// <summary>
        /// Parses a SCREAMING_SNAKE JSON token into its enum value by stripping underscores and matching
        /// case-insensitively.
        /// </summary>
        /// <typeparam name="TEnum">The target enum type.</typeparam>
        /// <param name="token">The JSON token.</param>
        /// <returns>The parsed enum value.</returns>
        private static TEnum ParseEnum<TEnum>(string token) where TEnum : struct
        {
            string candidate = token.Replace("_", string.Empty);
            if (!Enum.TryParse(candidate, true, out TEnum value))
                throw new InvalidOperationException("unknown token '" + token + "' for " + typeof(TEnum).Name + ".");

            return value;
        }

        /// <summary>
        /// Returns a member value by key, or null when absent.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The member value, or null.</returns>
        private static JsonValue Get(JsonObject owner, string key)
        {
            owner.TryGet(key, out JsonValue value);
            return value;
        }

        /// <summary>
        /// Reads a required string member.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The string value.</returns>
        private static string ReadString(JsonObject owner, string key) => ((JsonString)Get(owner, key)).Value;

        /// <summary>
        /// Reads a required integer member.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The integer value.</returns>
        private static int ReadInteger(JsonObject owner, string key) => (int)((JsonNumber)Get(owner, key)).IntegerValue;

        /// <summary>
        /// Reads a required numeric member as a double.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The numeric value.</returns>
        private static double ReadNumber(JsonObject owner, string key) => ((JsonNumber)Get(owner, key)).DoubleValue;

        /// <summary>
        /// Reads a required boolean member.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The boolean value.</returns>
        private static bool ReadBoolean(JsonObject owner, string key) => ((JsonBool)Get(owner, key)).Value;

        /// <summary>
        /// Reads a required object member.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The object value.</returns>
        private static JsonObject ReadObject(JsonObject owner, string key) => (JsonObject)Get(owner, key);

        /// <summary>
        /// Reads a required array member.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>The array value.</returns>
        private static JsonArray ReadArray(JsonObject owner, string key) => (JsonArray)Get(owner, key);

        /// <summary>
        /// Whether the owner carries a member with the given key.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The member key.</param>
        /// <returns>True when the member is present.</returns>
        private static bool Has(JsonObject owner, string key) => owner.TryGet(key, out _);
    }
}