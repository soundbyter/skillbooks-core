using Newtonsoft.Json.Linq;
using Skillbooks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Skillbooks.Recipes
{
    /// <summary>
    /// Skillbook + knife -> leather. Plain GridRecipe (no random output needed, unlike
    /// reroll), but still built in code rather than shipped as static JSON so the leather
    /// amount stays config-driven and the recipe can be skipped entirely when disabled.
    /// </summary>
    public static class SalvageRecipe
    {
        public static void Register(ICoreServerAPI api, SkillBooksConfig config)
        {
            if (!config.SalvageEnabled) { return; }

            GridRecipe recipe = Build(api, config);
            if (recipe == null) { return; }

            api.RegisterCraftingRecipe(recipe);

            // Wildcard ingredient matching doesn't consult item Attributes, so
            // SalvageIllegibleOnly can't be expressed declaratively. MatchesRecipe is the veto
            // hook for that; recipe identity is checked by reference, which stays stable
            // server-side, so this only ever targets this one recipe.
            if (config.SalvageIllegibleOnly)
            {
                api.Event.MatchesRecipe += (player, matchedRecipe, ingredients) =>
                {
                    if (!ReferenceEquals(matchedRecipe, recipe)) { return true; }
                    foreach (ItemSlot slot in ingredients)
                    {
                        if (slot?.Itemstack?.Collectible is Skillbooks.ItemSkillBook book)
                        {
                            return book.IsIllegible;
                        }
                    }
                    return true;
                };
            }

            api.Logger.Notification("[Skillbooks] Salvage recipe registered.");
        }

        private static GridRecipe Build(ICoreServerAPI api, SkillBooksConfig config)
        {
            JObject json = new JObject
            {
                ["ingredientPattern"] = "BK",
                ["width"] = 2,
                ["height"] = 1,
                ["shapeless"] = true,
                ["ingredients"] = new JObject
                {
                    ["B"] = new JObject { ["type"] = "item", ["code"] = "skillbooks:skillbook-*" },
                    ["K"] = new JObject
                    {
                        ["type"] = "item",
                        ["code"] = "game:knife-*",
                        ["isTool"] = true,
                        ["toolDurabilityCost"] = 2,
                    },
                },
                ["output"] = new JObject
                {
                    ["type"] = "item",
                    ["code"] = "game:leather-normal-plain",
                    ["quantity"] = config.SalvageLeatherAmount,
                },
            };

            GridRecipe recipe = JsonUtil.ToObject<GridRecipe>(json, "skillbooks", null);
            if (!recipe.Resolve(api.World, "skillbooks salvage recipe"))
            {
                api.Logger.Error("[Skillbooks] Failed to resolve salvage recipe.");
                return null;
            }

            return recipe;
        }
    }
}
