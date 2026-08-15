using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace Skillbooks
{
    /// <summary>
    /// Persists every crafting-trait code ever discovered in this savegame, so
    /// SkillBookRegistry can keep registering an illegible item for a trait whose providing
    /// mod has since been removed, instead of the code just disappearing. Stored via
    /// ISaveGame.GetData/StoreData, the correct per-world persistence point.
    /// </summary>
    public static class TraitHistory
    {
        private const string DataKey = "skillbooks:knownTraitCodes";

        public static HashSet<string> LoadAndUpdate(ICoreServerAPI api, IEnumerable<string> currentTraitCodes)
        {
            string[] stored = api.WorldManager.SaveGame.GetData(DataKey, System.Array.Empty<string>());
            HashSet<string> known = new HashSet<string>(stored);
            foreach (string code in currentTraitCodes) { known.Add(code); }

            api.WorldManager.SaveGame.StoreData(DataKey, known.ToArray());
            return known;
        }
    }
}
