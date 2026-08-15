using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Skillbooks
{
    /// <summary>
    /// Three-tier flavour resolver, first match wins: 
    /// 1: Mod-supplied (assets/<domain>/config/skillbooks/<code>.json) 
    /// 2: Curated (assets/skillbooks/config/flavour-curated.json)
    /// 3: A procedural fallback built from the trait's own lang keys. Title/blurb fall back independently per field. 
    /// The mod-supplied path lives under config/, not a bare top-level folder -- confirmed via
    /// decompiling the game's DLLs that AssetManager only scans the fixed set of AssetCategory folder names
    /// (blocktypes, config, lang, etc.) at all; anything outside that set is never indexed,
    /// so TryGet silently never finds it regardless of correct placement.
    /// </summary>
    public static class SkillBookFlavour
    {
        public class FlavourText
        {
            public string Title;
            public string Blurb;
        }

        private static Dictionary<string, FlavourText> curatedCache;

        public static FlavourText Resolve(ICoreServerAPI api, string traitCode, DiscoveredTrait discovered)
        {
            FlavourText fallback = ProceduralFallback(traitCode);

            FlavourText tier1 = TryLoadModSupplied(api, discovered.SourceDomain, traitCode);
            if (tier1 != null) { return FillGaps(tier1, fallback); }

            FlavourText tier2 = TryLoadCurated(api, traitCode);
            if (tier2 != null) { return FillGaps(tier2, fallback); }

            return fallback;
        }

        private static FlavourText TryLoadModSupplied(ICoreServerAPI api, string traitModDomain, string traitCode)
        {
            if (string.IsNullOrEmpty(traitModDomain)) { return null; }

            AssetLocation loc = new AssetLocation(traitModDomain, "config/skillbooks/" + traitCode + ".json");
            IAsset asset = api.Assets.TryGet(loc);
            return asset?.ToObject<FlavourText>();
        }

        private static FlavourText TryLoadCurated(ICoreServerAPI api, string traitCode)
        {
            if (curatedCache == null)
            {
                AssetLocation loc = new AssetLocation("skillbooks", "config/flavour-curated.json");
                IAsset asset = api.Assets.TryGet(loc);
                curatedCache = asset?.ToObject<Dictionary<string, FlavourText>>() ?? new Dictionary<string, FlavourText>();
            }

            return curatedCache.TryGetValue(traitCode, out FlavourText text) ? text : null;
        }

        private static FlavourText FillGaps(FlavourText text, FlavourText fallback)
        {
            return new FlavourText
            {
                Title = string.IsNullOrEmpty(text.Title) ? fallback.Title : text.Title,
                Blurb = string.IsNullOrEmpty(text.Blurb) ? fallback.Blurb : text.Blurb,
            };
        }

        private static FlavourText ProceduralFallback(string traitCode)
        {
            string traitName = Lang.Get("trait-" + traitCode);
            string traitDesc = Lang.GetIfExists("traitdesc-" + traitCode);

            return new FlavourText
            {
                Title = Lang.Get("skillbooks:fallback-title", traitName),
                Blurb = traitDesc != null
                    ? Lang.Get("skillbooks:fallback-blurb-withdesc", traitDesc)
                    : Lang.Get("skillbooks:fallback-blurb-nodesc"),
            };
        }
    }
}
