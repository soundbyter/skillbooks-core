using System.Collections.Generic;
using Skillbooks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Skillbooks
{
    /// <summary>
    /// Hooks skill books into loot and trader offers. Neither a generic "loot table" asset
    /// nor a public way to append to a trader's offer pool exists, so both hooks work
    /// around the engine rather than through a documented extension point.
    /// </summary>
    public static class SkillBookLootPatcher
    {
        /// <summary>
        /// Loot-bearing blocks cache their resolved drop list in a private field at OnLoaded,
        /// before AssetsFinalize runs, so there's nothing to patch afterward. Instead: hook
        /// DidBreakBlock and roll an independent bonus chance per matching block code.
        /// </summary>
        public static void RegisterLootHook(ICoreServerAPI api, Dictionary<string, DiscoveredTrait> craftingTraits, SkillBooksConfig config)
        {
            if (craftingTraits.Count == 0) { return; }

            List<string> traitCodes = new List<string>(craftingTraits.Keys);
            AssetLocation[] targetPatterns = new AssetLocation[config.LootTargetBlockCodes.Length];
            for (int i = 0; i < targetPatterns.Length; i++)
            {
                targetPatterns[i] = AssetLocation.Create(config.LootTargetBlockCodes[i]);
            }

            api.Event.DidBreakBlock += (byPlayer, oldBlockId, blockSel) =>
            {
                Block oldBlock = api.World.GetBlock(oldBlockId);
                if (oldBlock?.Code == null) { return; }

                bool matches = false;
                foreach (AssetLocation pattern in targetPatterns)
                {
                    if (WildcardUtil.Match(pattern, oldBlock.Code)) { matches = true; break; }
                }
                if (!matches) { return; }

                // At most one book per break -- rolling every trait independently made
                // near-every vessel drop a stack.
                string traitCode = traitCodes[api.World.Rand.Next(traitCodes.Count)];
                if (api.World.Rand.NextDouble() >= config.GetLootSpawnChance(traitCode)) { return; }

                Item book = api.World.GetItem(new AssetLocation("skillbooks", "skillbook-" + traitCode));
                if (book == null) { return; }

                api.World.SpawnItemEntity(new ItemStack(book), blockSel.Position);
            };

            api.Logger.Notification($"[Skillbooks] Loot hook armed for block pattern(s): {string.Join(", ", config.LootTargetBlockCodes)}");
        }

        /// <summary>
        /// No public event fires on trader restock, and RefreshBuyingSellingInventory is
        /// protected/non-virtual, so this polls every matching trader and watches
        /// WatchedAttributes["lastRefreshTotalDays"] (advanced by the engine's own restock
        /// logic every ~7 in-game days) for an advance. On one, rolls TraderSpawnChance and
        /// injects a random book into an empty ItemSlotTrade if one's free.
        /// </summary>
        public static void RegisterTraderHook(ICoreServerAPI api, Dictionary<string, DiscoveredTrait> craftingTraits, SkillBooksConfig config)
        {
            if (!config.TraderEnabled || craftingTraits.Count == 0 || config.TraderOffers.Length == 0) { return; }

            List<string> traitCodes = new List<string>(craftingTraits.Keys);
            Dictionary<long, double> lastKnownRefreshDay = new Dictionary<long, double>();

            api.Event.RegisterGameTickListener(_ =>
            {
                foreach (Entity entity in api.World.LoadedEntities.Values)
                {
                    if (entity is not EntityTradingHumanoid trader || trader.TradeProps == null) { continue; }
                    if (!MatchesConfiguredTrader(trader, config.TraderOffers)) { continue; }

                    double currentRefreshDay = trader.WatchedAttributes.GetDouble("lastRefreshTotalDays", double.MinValue);
                    bool seenBefore = lastKnownRefreshDay.TryGetValue(entity.EntityId, out double previousRefreshDay);
                    lastKnownRefreshDay[entity.EntityId] = currentRefreshDay;

                    // Only treat an advance as a real restock -- first-ever sighting of a
                    // trader just establishes a baseline, not a rotation to roll against.
                    if (!seenBefore || currentRefreshDay <= previousRefreshDay) { continue; }

                    if (api.World.Rand.NextDouble() >= config.TraderSpawnChance) { continue; }

                    TryInjectBook(api, trader, traitCodes, config);
                }
            }, 60000);

            api.Logger.Notification($"[Skillbooks] Trader hook armed for trader type(s): {string.Join(", ", config.TraderOffers)} ({config.TraderSpawnChance:P2} chance per rotation)");
        }

        private static bool MatchesConfiguredTrader(EntityTradingHumanoid trader, string[] traderOffers)
        {
            string path = trader.Code?.Path;
            if (string.IsNullOrEmpty(path)) { return false; }

            foreach (string traderCode in traderOffers)
            {
                if (path.Contains(traderCode)) { return true; }
            }
            return false;
        }

        private static void TryInjectBook(ICoreServerAPI api, EntityTradingHumanoid trader, List<string> traitCodes, SkillBooksConfig config)
        {
            ItemSlotTrade[] sellingSlots = trader.Inventory.SellingSlots;
            ItemSlotTrade targetSlot = null;
            foreach (ItemSlotTrade slot in sellingSlots)
            {
                if (slot != null && (slot.TradeItem == null || slot.TradeItem.Stock <= 0))
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null) { return; }

            string traitCode = traitCodes[api.World.Rand.Next(traitCodes.Count)];
            Item book = api.World.GetItem(new AssetLocation("skillbooks", "skillbook-" + traitCode));
            if (book == null) { return; }

            int price = System.Math.Max(1, (int)System.Math.Round((config.TraderBasePrice + api.World.Rand.Next(-2, 3)) * config.TraderPriceMultiplier));

            targetSlot.SetTradeItem(new ResolvedTradeItem
            {
                Stack = new ItemStack(book, 1),
                Price = price,
                Stock = 1,
            });
            targetSlot.MarkDirty();
        }
    }
}
