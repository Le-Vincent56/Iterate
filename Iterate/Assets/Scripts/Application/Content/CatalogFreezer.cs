using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Compilation;
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
            List<ProcessRuleDefinition> processRules = new();
            List<CoreDefinition> cores = new();
            List<ProcessConfigurationDefinition> processConfigurations = new();
            List<ShopDefinition> shops = new();
            List<PoolDefinition> pools = new();
            List<RewardPackageDefinition> rewardPackages = new();
            List<RouteDefinition> routes = new();
            List<StarterArchetypeDefinition> starterArchetypes = new();
            List<SystemDefinition> systems = new();

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
                        
                                                
                        case CatalogFileKind.ProcessRule:
                            FreezeProcessRules(rows, processRules);
                            break;
                        
                        case CatalogFileKind.Core:
                            FreezeCores(rows, cores);
                            break;

                        case CatalogFileKind.ProcessConfiguration:
                            FreezeProcessConfigurations(rows, processConfigurations);
                            break;

                        case CatalogFileKind.Shop:
                            FreezeShops(rows, shops);
                            break;

                        case CatalogFileKind.Pool:
                            FreezePools(rows, pools);
                            break;

                        case CatalogFileKind.RewardPackage:
                            FreezeRewardPackages(rows, rewardPackages);
                            break;

                        case CatalogFileKind.Route:
                            FreezeRoutes(rows, routes);
                            break;

                        case CatalogFileKind.StarterArchetype:
                            FreezeStarterArchetypes(rows, starterArchetypes);
                            break;

                        case CatalogFileKind.System:
                            FreezeSystems(rows, systems);
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
                utilities,
                processRules,
                cores,
                processConfigurations,
                shops,
                pools,
                rewardPackages,
                routes,
                starterArchetypes,
                systems
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
        /// Freezes each Process-rule row into the target list.
        /// </summary>
        /// <param name="rows">The Process-rule rows.</param>
        /// <param name="target">The list to append the frozen Process rules to.</param>
        private static void FreezeProcessRules(JsonArray rows, List<ProcessRuleDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new ProcessRuleDefinition(
                    new ProcessRuleID(ReadString(row, "id")),
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
        /// Freezes each Core row into the target list.
        /// </summary>
        /// <param name="rows">The Core rows.</param>
        /// <param name="target">The list to append the frozen Cores to.</param>
        private static void FreezeCores(JsonArray rows, List<CoreDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new CoreDefinition(
                    new CoreID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    FreezeCoreLines(ReadArray(row, "lines")),
                    new SourcePosition(ReadInteger(row, "finalOutputPosition"))
                ));
            }
        }

        /// <summary>
        /// Freezes a Core's ordered line array.
        /// </summary>
        /// <param name="rows">The Core line rows.</param>
        /// <returns>The frozen line specs in authored order.</returns>
        private static IReadOnlyList<CoreLineSpec> FreezeCoreLines(JsonArray rows)
        {
            List<CoreLineSpec> lines = new(rows.Items.Count);
            for (int index = 0; index < rows.Items.Count; index++)
            {
                lines.Add(FreezeCoreLine((JsonObject)rows.Items[index]));
            }

            return lines;
        }

        /// <summary>
        /// Freezes one Core line, recursing into a fixed Structure's contained instruction.
        /// </summary>
        /// <param name="line">The Core line row.</param>
        /// <returns>The frozen line spec.</returns>
        private static CoreLineSpec FreezeCoreLine(JsonObject line)
        {
            CoreLineOperation operation = Has(line, "operation")
                ? FreezeCoreLineOperation(ReadObject(line, "operation"))
                : null;
            StructurePredicate predicate = Has(line, "predicate")
                ? FreezePredicate(ReadObject(line, "predicate"))
                : null;
            CoreLineSpec contained = Has(line, "contained")
                ? FreezeCoreLine(ReadObject(line, "contained"))
                : null;

            return new CoreLineSpec(
                ReadInteger(line, "position"),
                ParseEnum<CoreLineKind>(ReadString(line, "kind")),
                operation,
                predicate,
                contained
            );
        }

        /// <summary>
        /// Freezes a Core line's typed operation.
        /// </summary>
        /// <param name="operation">The operation object.</param>
        /// <returns>The frozen Core line operation.</returns>
        private static CoreLineOperation FreezeCoreLineOperation(JsonObject operation)
        {
            return new CoreLineOperation(
                ParseEnum<CoreLineOperator>(ReadString(operation, "operator")),
                ParseEnum<CoreRegister>(ReadString(operation, "register")),
                FreezeOperand(ReadObject(operation, "operand"))
            );
        }

        /// <summary>
        /// Freezes each Process-configuration row into the target list.
        /// </summary>
        /// <param name="rows">The Process-configuration rows.</param>
        /// <param name="target">The list to append the frozen configurations to.</param>
        private static void FreezeProcessConfigurations(JsonArray rows, List<ProcessConfigurationDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                ProcessRuleID? processRule = Has(row, "processRule")
                    ? new ProcessRuleID(ReadString(row, "processRule"))
                    : null;
                ShopID? precedingShop = Has(row, "precedingShop")
                    ? new ShopID(ReadString(row, "precedingShop"))
                    : null;
                ExposureSpec exposure = Has(row, "exposure")
                    ? new ExposureSpec(ReadStringList(ReadArray(ReadObject(row, "exposure"), "guaranteed")))
                    : null;
                ActiveBranchSpec activeBranch = Has(row, "activeBranch")
                    ? FreezeActiveBranch(ReadObject(row, "activeBranch"))
                    : null;

                JsonObject thresholds = ReadObject(row, "thresholds");
                target.Add(new ProcessConfigurationDefinition(
                    new ProcessID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    ParseEnum<ProcessRole>(ReadString(row, "role")),
                    new CoreID(ReadString(row, "core")),
                    processRule,
                    new ProcessThresholdSpec(
                        ReadInteger(thresholds, "pass"),
                        ReadInteger(thresholds, "optimize"),
                        ReadInteger(thresholds, "benchmark")
                    ),
                    ReadInteger(row, "executions"),
                    ReadBoolean(row, "mandatoryExecutions"),
                    ReadInteger(row, "startingBytes"),
                    ReadInteger(row, "bufferCapacity"),
                    ReadInteger(row, "sourceCapacity"),
                    FreezeInitialSource(ReadArray(row, "initialSource")),
                    FreezeBufferLoad(ReadObject(row, "bufferLoad")),
                    exposure,
                    activeBranch,
                    new RewardPackageID(ReadString(row, "rewardPackage")),
                    precedingShop
                ));
            }
        }

        /// <summary>
        /// Freezes a Process's Active Branch constraints.
        /// </summary>
        /// <param name="branch">The Active Branch object.</param>
        /// <returns>The frozen Branch constraints.</returns>
        private static ActiveBranchSpec FreezeActiveBranch(JsonObject branch)
        {
            return new ActiveBranchSpec(
                ReadInteger(branch, "capacity"),
                ReadStringList(ReadArray(branch, "required")),
                ReadStringList(ReadArray(branch, "quarantined")),
                Has(branch, "recommendedTags") ? ReadStringList(ReadArray(branch, "recommendedTags")) : Array.Empty<string>(),
                Has(branch, "cautionTags") ? ReadStringList(ReadArray(branch, "cautionTags")) : Array.Empty<string>()
            );
        }

        /// <summary>
        /// Freezes a Process's pre-installed source entries.
        /// </summary>
        /// <param name="rows">The initial-source rows.</param>
        /// <returns>The frozen entries in authored order.</returns>
        private static IReadOnlyList<InitialSourceSpec> FreezeInitialSource(JsonArray rows)
        {
            List<InitialSourceSpec> entries = new(rows.Items.Count);
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                entries.Add(new InitialSourceSpec(ReadInteger(row, "position"), ReadString(row, "content")));
            }

            return entries;
        }

        /// <summary>
        /// Freezes a Process's Buffer load plan, filling the pair its policy does not use with empty
        /// lists rather than nulls.
        /// </summary>
        /// <param name="load">The Buffer load object.</param>
        /// <returns>The frozen load plan.</returns>
        private static BufferLoadSpec FreezeBufferLoad(JsonObject load)
        {
            List<ArrivalLoadSpec> arrivals = new();
            if (Has(load, "arrivals"))
            {
                JsonArray rows = ReadArray(load, "arrivals");
                for (int index = 0; index < rows.Items.Count; index++)
                {
                    JsonObject row = (JsonObject)rows.Items[index];
                    arrivals.Add(new ArrivalLoadSpec(
                        ReadInteger(row, "afterExecution"),
                        ReadStringList(ReadArray(row, "items"))
                    ));
                }
            }

            return new BufferLoadSpec(
                ParseEnum<BufferLoadPolicy>(ReadString(load, "policy")),
                Has(load, "initial") ? ReadStringList(ReadArray(load, "initial")) : Array.Empty<string>(),
                arrivals,
                Has(load, "initialCount") ? ReadInteger(load, "initialCount") : 0,
                Has(load, "arrivalsAfterExecutions") ? ReadIntegerList(ReadArray(load, "arrivalsAfterExecutions")) : Array.Empty<int>()
            );
        }

        /// <summary>
        /// Freezes each shop row into the target list.
        /// </summary>
        /// <param name="rows">The shop rows.</param>
        /// <param name="target">The list to append the frozen shops to.</param>
        private static void FreezeShops(JsonArray rows, List<ShopDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                PoolID? rerollPool = Has(row, "rerollPool") ? new PoolID(ReadString(row, "rerollPool")) : null;

                List<ShopOffer> offers = new();
                JsonArray offerRows = ReadArray(row, "fixedOffers");
                for (int offerIndex = 0; offerIndex < offerRows.Items.Count; offerIndex++)
                {
                    JsonObject offer = (JsonObject)offerRows.Items[offerIndex];
                    offers.Add(new ShopOffer(
                        ReadString(offer, "offerID"),
                        ReadString(offer, "content"),
                        ReadInteger(offer, "price")
                    ));
                }

                target.Add(new ShopDefinition(
                    new ShopID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    ReadInteger(row, "slots"),
                    ReadBoolean(row, "rerollsEnabled"),
                    ReadBoolean(row, "pinningEnabled"),
                    ReadBoolean(row, "dependenciesEnabled"),
                    ReadBoolean(row, "servicesEnabled"),
                    offers,
                    rerollPool
                ));
            }
        }

        /// <summary>
        /// Freezes each acquisition-pool row into the target list.
        /// </summary>
        /// <param name="rows">The pool rows.</param>
        /// <param name="target">The list to append the frozen pools to.</param>
        private static void FreezePools(JsonArray rows, List<PoolDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                int? selectionCount = Has(row, "selectionCount") ? ReadInteger(row, "selectionCount") : null;

                List<PoolMember> members = new();
                JsonArray memberRows = ReadArray(row, "members");
                for (int memberIndex = 0; memberIndex < memberRows.Items.Count; memberIndex++)
                {
                    JsonObject member = (JsonObject)memberRows.Items[memberIndex];
                    int? price = Has(member, "price") ? ReadInteger(member, "price") : null;
                    members.Add(new PoolMember(ReadString(member, "content"), price));
                }

                target.Add(new PoolDefinition(
                    new PoolID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    ReadString(row, "selectionMethod"),
                    selectionCount,
                    members
                ));
            }
        }

        /// <summary>
        /// Freezes each reward-package row into the target list, preserving component order.
        /// </summary>
        /// <param name="rows">The reward-package rows.</param>
        /// <param name="target">The list to append the frozen packages to.</param>
        private static void FreezeRewardPackages(JsonArray rows, List<RewardPackageDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];

                List<RewardComponent> components = new();
                JsonArray componentRows = ReadArray(row, "components");
                for (int componentIndex = 0; componentIndex < componentRows.Items.Count; componentIndex++)
                {
                    JsonObject component = (JsonObject)componentRows.Items[componentIndex];
                    int? amount = Has(component, "amount") ? ReadInteger(component, "amount") : null;
                    string reference = Has(component, "reference") ? ReadString(component, "reference") : null;
                    components.Add(new RewardComponent(
                        ParseEnum<RewardTier>(ReadString(component, "tier")),
                        ParseEnum<RewardComponentKind>(ReadString(component, "kind")),
                        amount,
                        reference
                    ));
                }

                target.Add(new RewardPackageDefinition(
                    new RewardPackageID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    components
                ));
            }
        }

        /// <summary>
        /// Freezes each route row into the target list.
        /// </summary>
        /// <param name="rows">The route rows.</param>
        /// <param name="target">The list to append the frozen routes to.</param>
        private static void FreezeRoutes(JsonArray rows, List<RouteDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new RouteDefinition(
                    new RouteID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    new ProcessID(ReadString(row, "process")),
                    new ShopID(ReadString(row, "shop"))
                ));
            }
        }

        /// <summary>
        /// Freezes each Starter Archetype row into the target list.
        /// </summary>
        /// <param name="rows">The archetype rows.</param>
        /// <param name="target">The list to append the frozen archetypes to.</param>
        private static void FreezeStarterArchetypes(JsonArray rows, List<StarterArchetypeDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];
                target.Add(new StarterArchetypeDefinition(
                    new StarterArchetypeID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    ReadStringList(ReadArray(row, "startingRepository")),
                    new DependencyID(ReadString(row, "starterDependency"))
                ));
            }
        }

        /// <summary>
        /// Freezes each System row into the target list, preserving stage order.
        /// </summary>
        /// <param name="rows">The System rows.</param>
        /// <param name="target">The list to append the frozen Systems to.</param>
        private static void FreezeSystems(JsonArray rows, List<SystemDefinition> target)
        {
            for (int index = 0; index < rows.Items.Count; index++)
            {
                JsonObject row = (JsonObject)rows.Items[index];

                List<SystemStage> stages = new();
                JsonArray stageRows = ReadArray(row, "stages");
                for (int stageIndex = 0; stageIndex < stageRows.Items.Count; stageIndex++)
                {
                    stages.Add(FreezeSystemStage((JsonObject)stageRows.Items[stageIndex]));
                }

                target.Add(new SystemDefinition(
                    new SystemID(ReadString(row, "id")),
                    ReadString(row, "displayName"),
                    stages
                ));
            }
        }

        /// <summary>
        /// Freezes one System stage, carrying only the reference its kind admits.
        /// </summary>
        /// <param name="stage">The stage object.</param>
        /// <returns>The frozen stage.</returns>
        private static SystemStage FreezeSystemStage(JsonObject stage)
        {
            ProcessID? process = Has(stage, "process") ? new ProcessID(ReadString(stage, "process")) : null;
            ShopID? shop = Has(stage, "shop") ? new ShopID(ReadString(stage, "shop")) : null;

            List<RouteID> routes = new();
            if (Has(stage, "routes"))
            {
                JsonArray rows = ReadArray(stage, "routes");
                for (int index = 0; index < rows.Items.Count; index++)
                {
                    routes.Add(new RouteID(((JsonString)rows.Items[index]).Value));
                }
            }

            return new SystemStage(
                ParseEnum<SystemStageKind>(ReadString(stage, "kind")),
                process,
                shop,
                routes
            );
        }

        /// <summary>
        /// Reads a JSON array of integers into an integer list.
        /// </summary>
        /// <param name="array">The array of integer values.</param>
        /// <returns>The read integer list.</returns>
        private static IReadOnlyList<int> ReadIntegerList(JsonArray array)
        {
            List<int> values = new(array.Items.Count);
            for (int index = 0; index < array.Items.Count; index++)
            {
                values.Add((int)((JsonNumber)array.Items[index]).IntegerValue);
            }

            return values;
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
                
                case "RESOURCE_GAIN":
                    return new ResourceGainOperation(
                        ReadString(operation, "resource"),
                        ReadInteger(operation, "amount")
                    );
                
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