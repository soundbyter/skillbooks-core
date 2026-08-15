using System.Collections.Generic;
using Skillbooks.Config;
using Skillbooks.Recipes;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Skillbooks
{
    public class SkillBooksModSystem : ModSystem
    {
        public Dictionary<string, DiscoveredTrait> CraftingTraits { get; private set; } = new Dictionary<string, DiscoveredTrait>();

        /// <summary>
        /// Every trait code ever discovered in this savegame, current ones included -- a
        /// superset of CraftingTraits.Keys. See TraitHistory for why this needs to persist
        /// across boots rather than just reflecting the current mod set.
        /// </summary>
        public HashSet<string> KnownTraitCodes { get; private set; } = new HashSet<string>();

        public SkillBooksConfig Config { get; private set; }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemSkillBook", typeof(ItemSkillBook));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            if (api is ICoreServerAPI sapi)
            {
                SkillBooksConfig config = SkillBooksConfig.Load(sapi);
                Config = config;
                CraftingTraits = TraitDiscovery.Run(sapi, config);
                KnownTraitCodes = TraitHistory.LoadAndUpdate(sapi, CraftingTraits.Keys);
                SkillBookRegistry.Generate(sapi, CraftingTraits, KnownTraitCodes, config);
                SkillBookLootPatcher.RegisterTraderHook(sapi, CraftingTraits, config);
                SkillBookLootPatcher.RegisterLootHook(sapi, CraftingTraits, config);

                SalvageRecipe.Register(sapi, config);
            }
        }
    }
}
