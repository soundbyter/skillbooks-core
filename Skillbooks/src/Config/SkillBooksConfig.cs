using System.Collections.Generic;
using Vintagestory.API.Server;

namespace Skillbooks.Config
{
    /// <summary>
    /// TraitAllowlist and TraitBlacklist are mutually exclusive: a non-empty allowlist means
    /// only those traits get books; otherwise the blacklist excludes specific traits from an
    /// otherwise-open set.
    /// </summary>
    public class SkillBooksConfig
    {
        public string[] TraitBlacklist = System.Array.Empty<string>();
        public string[] TraitAllowlist = System.Array.Empty<string>();

        public double LootSpawnChance = 0.001;
        public string[] LootTargetBlockCodes = { "game:lootvessel-*" };

        public bool TraderEnabled = true;
        public string[] TraderOffers = { "treasurehunter" };
        public double TraderPriceMultiplier = 1.0;

        /// <summary>
        /// Independent probability, checked whenever a matching trader is detected to have
        /// actually restocked (see SkillBookLootPatcher.RegisterTraderHook).
        /// </summary>
        public double TraderSpawnChance = 0.005;

        /// <summary>
        /// Base price in rusty gears (the only trader currency), before TraderPriceMultiplier
        /// and a small +/-2 random variance.
        /// </summary>
        public int TraderBasePrice = 24;

        public bool SalvageEnabled = true;
        public int SalvageLeatherAmount = 2;

        /// <summary>If true, only illegible/orphaned books can be salvaged.</summary>
        public bool SalvageIllegibleOnly = false;

        public bool RerollEnabled = true;

        /// <summary>If true, only illegible/orphaned books can be rerolled.</summary>
        public bool RerollIllegibleOnly = false;

        public Dictionary<string, PerTraitOverride> PerTraitOverrides = new Dictionary<string, PerTraitOverride>();

        public class PerTraitOverride
        {
            public double? LootSpawnChance;
        }

        private const string FileName = "skillbooks.json";

        public static SkillBooksConfig Load(ICoreServerAPI api)
        {
            SkillBooksConfig config = api.LoadModConfig<SkillBooksConfig>(FileName) ?? new SkillBooksConfig();
            api.StoreModConfig(config, FileName);
            return config;
        }

        public bool IsTraitEnabled(string traitCode)
        {
            if (TraitAllowlist.Length > 0)
            {
                return System.Array.IndexOf(TraitAllowlist, traitCode) >= 0;
            }
            return System.Array.IndexOf(TraitBlacklist, traitCode) < 0;
        }

        public double GetLootSpawnChance(string traitCode)
        {
            if (PerTraitOverrides.TryGetValue(traitCode, out PerTraitOverride over) && over.LootSpawnChance.HasValue)
            {
                return over.LootSpawnChance.Value;
            }
            return LootSpawnChance;
        }
    }
}
