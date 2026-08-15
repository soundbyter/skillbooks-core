using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Skillbooks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Skillbooks
{
    /// <summary>
    /// A discovered crafting trait plus the mod domain its config/traits.json entry came
    /// from, so SkillBookFlavour can probe that domain for a mod-supplied flavour override.
    /// </summary>
    public class DiscoveredTrait
    {
        public Trait Trait;
        public string SourceDomain;
    }

    /// <summary>
    /// Discovers traits that gate at least one loaded recipe via RequiresTrait, by
    /// cross-referencing every loaded recipe against the merged config/traits.json set.
    /// Must run at AssetsFinalize, the first lifecycle stage guaranteed to run after every
    /// mod's own recipe registration has completed.
    /// </summary>
    public static class TraitDiscovery
    {
        private static readonly string[] NonGridRegistryCodes =
        {
            "smithingrecipes",
            "knappingrecipes",
            "clayformingrecipes",
            "barrelrecipes",
        };

        public static Dictionary<string, DiscoveredTrait> Run(ICoreServerAPI api, SkillBooksConfig config)
        {
            HashSet<string> requiredTraitCodes = CollectRequiredTraitCodes(api);
            Dictionary<string, DiscoveredTrait> allTraits = LoadAllTraits(api);

            Dictionary<string, DiscoveredTrait> craftingTraits = new Dictionary<string, DiscoveredTrait>();
            int filteredOut = 0;
            foreach (string code in requiredTraitCodes)
            {
                if (!allTraits.TryGetValue(code, out DiscoveredTrait trait))
                {
                    api.Logger.Warning($"[Skillbooks] Recipe(s) require trait '{code}', but no such trait is defined in any loaded config/traits.json. Skipping.");
                    continue;
                }
                if (!config.IsTraitEnabled(code))
                {
                    filteredOut++;
                    continue;
                }
                craftingTraits[code] = trait;
            }

            api.Logger.Event($"[Skillbooks] Discovered {craftingTraits.Count} crafting trait(s) ({filteredOut} excluded by config): {string.Join(", ", craftingTraits.Keys)}");
            return craftingTraits;
        }

        private static HashSet<string> CollectRequiredTraitCodes(ICoreServerAPI api)
        {
            HashSet<string> codes = new HashSet<string>();

            foreach (GridRecipe recipe in api.World.GridRecipes)
            {
                if (!string.IsNullOrEmpty(recipe.RequiresTrait))
                {
                    codes.Add(recipe.RequiresTrait);
                }
            }

            foreach (string registryCode in NonGridRegistryCodes)
            {
                RecipeRegistryBase registry = api.World.GetRecipeRegistry(registryCode);
                if (registry == null) { continue; }

                foreach (IRecipeBase recipe in EnumerateRecipes(registry))
                {
                    if (!string.IsNullOrEmpty(recipe.RequiresTrait))
                    {
                        codes.Add(recipe.RequiresTrait);
                    }
                }
            }

            return codes;
        }

        private static IEnumerable<IRecipeBase> EnumerateRecipes(RecipeRegistryBase registry)
        {
            switch (registry)
            {
                case RecipeRegistryGeneric<SmithingRecipe> smithing:
                    foreach (SmithingRecipe r in smithing.Recipes) { yield return r; }
                    break;
                case RecipeRegistryGeneric<KnappingRecipe> knapping:
                    foreach (KnappingRecipe r in knapping.Recipes) { yield return r; }
                    break;
                case RecipeRegistryGeneric<ClayFormingRecipe> clayForming:
                    foreach (ClayFormingRecipe r in clayForming.Recipes) { yield return r; }
                    break;
                case RecipeRegistryGeneric<BarrelRecipe> barrel:
                    foreach (BarrelRecipe r in barrel.Recipes) { yield return r; }
                    break;
            }
        }

        /// <summary>
        /// Mirrors CharacterSystem.LoadTraits() rather than reading its own TraitsByCode,
        /// which only populates later, at ServerRunPhase.ModsAndConfigReady.
        /// </summary>
        private static Dictionary<string, DiscoveredTrait> LoadAllTraits(ICoreServerAPI api)
        {
            Dictionary<string, DiscoveredTrait> traits = new Dictionary<string, DiscoveredTrait>();
            Dictionary<AssetLocation, JToken> many = api.Assets.GetMany<JToken>(api.Logger, "config/traits", null);

            foreach (var (loc, token) in many)
            {
                if (token is JObject)
                {
                    AddTrait(traits, JsonUtil.ToObject<Trait>(token, loc.Domain, null), loc.Domain);
                }
                else if (token is JArray array)
                {
                    foreach (JToken entry in array)
                    {
                        AddTrait(traits, JsonUtil.ToObject<Trait>(entry, loc.Domain, null), loc.Domain);
                    }
                }
            }

            return traits;
        }

        private static void AddTrait(Dictionary<string, DiscoveredTrait> traits, Trait trait, string sourceDomain)
        {
            if (trait?.Code == null) { return; }
            traits[trait.Code] = new DiscoveredTrait { Trait = trait, SourceDomain = sourceDomain };
        }
    }
}
