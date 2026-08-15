using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Skillbooks.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Skillbooks
{
    /// <summary>
    /// Builds one ItemSkillBook per discovered crafting trait via ICoreServerAPI.RegisterItem
    /// (IAssetManager.Add needs an undocumented internal Asset type, so it's intentionally avoided).
    ///
    /// Also registers a fallback "illegible" item for every trait in knownTraitCodes whose
    /// providing mod is no longer loaded -- otherwise an existing itemstack for it would
    /// collapse into the engine's generic "unknown item" placeholder. Same ItemSkillBook
    /// class either way; the "skillbooks:illegible" attribute set here picks the mode.
    /// </summary>
    public static class SkillBookRegistry
    {
        private static readonly AssetLocation SharedShape = new AssetLocation("game", "block/clutter/bookshelves/small-normal");

        private static readonly string[] TintPool =
        {
            "aged-darkgreen", "aged-orangebrown", "aged-brickred", "aged-darkgray", "aged-olive",
            "aged-cherryred", "aged-purpleorange", "aged-gray",
        };

        // Deliberately not in TintPool -- illegible books get their own distinct, worn look
        // rather than reusing a normal tint for aesthetics and for clarity at a glance.
        private const string IllegibleTexturePath = "item/lore/book-rotten1";

        public static void Generate(ICoreServerAPI api, Dictionary<string, DiscoveredTrait> craftingTraits, IEnumerable<string> knownTraitCodes, SkillBooksConfig config)
        {
            int i = 0;
            int registered = 0;
            foreach (KeyValuePair<string, DiscoveredTrait> entry in craftingTraits)
            {
                try
                {
                    RegisterBook(api, entry.Key, entry.Value, TintPool[i % TintPool.Length]);
                    registered++;
                }
                catch (System.Exception ex)
                {
                    api.Logger.Error($"[Skillbooks] Failed to register skill book for trait '{entry.Key}': {ex.Message}");
                }
                i++;
            }
            api.Logger.Event($"[Skillbooks] Registered {registered} of {craftingTraits.Count} skill book item(s)");

            int orphanedTotal = 0;
            int orphanedRegistered = 0;
            foreach (string traitCode in knownTraitCodes)
            {
                if (craftingTraits.ContainsKey(traitCode)) { continue; }
                // A blacklisted/not-allowlisted trait is a deliberate exclusion, not an orphan.
                if (!config.IsTraitEnabled(traitCode)) { continue; }
                orphanedTotal++;
                try
                {
                    RegisterIllegibleBook(api, traitCode);
                    orphanedRegistered++;
                }
                catch (System.Exception ex)
                {
                    api.Logger.Error($"[Skillbooks] Failed to register illegible skill book for orphaned trait '{traitCode}': {ex.Message}");
                }
            }
            if (orphanedTotal > 0)
            {
                api.Logger.Event($"[Skillbooks] Registered {orphanedRegistered} of {orphanedTotal} illegible skill book(s) for orphaned trait(s) (providing mod no longer loaded)");
            }
        }

        private static void RegisterBook(ICoreServerAPI api, string traitCode, DiscoveredTrait discovered, string tint)
        {
            SkillBookFlavour.FlavourText flavour = SkillBookFlavour.Resolve(api, traitCode, discovered);

            Item item = BuildBaseItem(api, traitCode, "item/lore/" + tint);
            item.Attributes = new JsonObject(new JObject
            {
                ["skillbooks:traitCode"] = traitCode,
                ["skillbooks:title"] = flavour.Title,
                ["skillbooks:blurb"] = flavour.Blurb,
                // Crafting traits don't normally carry stat Attributes, but a modded trait
                // can carry both (see ItemSkillBook.RefreshTraitStats) -- stashed here too
                // so the tooltip can show them on the rare trait where that's the case.
                ["skillbooks:attributes"] = discovered.Trait.Attributes is { Count: > 0 }
                    ? JObject.FromObject(discovered.Trait.Attributes)
                    : null,
                ["handbook"] = new JObject { ["exclude"] = true },
            });

            api.RegisterItem(item);
        }

        private static void RegisterIllegibleBook(ICoreServerAPI api, string traitCode)
        {
            Item item = BuildBaseItem(api, traitCode, IllegibleTexturePath);
            item.Attributes = new JsonObject(new JObject
            {
                ["skillbooks:traitCode"] = traitCode,
                ["skillbooks:illegible"] = true,
                ["handbook"] = new JObject { ["exclude"] = true },
            });

            api.RegisterItem(item);
        }

        private static Item BuildBaseItem(ICoreServerAPI api, string traitCode, string texturePath)
        {
            Item item = api.ClassRegistry.CreateItem("ItemSkillBook");
            item.Code = new AssetLocation("skillbooks", "skillbook-" + traitCode);
            item.MaxStackSize = 16;
            item.Shape = new CompositeShape { Base = SharedShape };
            item.Textures["cover"] = new CompositeTexture(new AssetLocation("game", texturePath));
            item.CreativeInventoryTabs = new[] { "skillbooks" };

            item.GuiTransform = new ModelTransform
            {
                Translation = new FastVec3f(0f, 0f, 0f),
                Rotation = new FastVec3f(-180f, 123f, 33f),
                Origin = new FastVec3f(0.48f, 0.21f, 0.5f),
                ScaleXYZ = new FastVec3f(-3.23f, 3.23f, 3.23f),
            };
            item.TpHandTransform = new ModelTransform
            {
                Translation = new FastVec3f(-0.79f, -0.36f, -0.73f),
                Rotation = new FastVec3f(0f, -84f, 7f),
                Origin = new FastVec3f(0.5f, 0.1f, 0.5f),
                Scale = 0.67f,
            };
            item.GroundTransform = new ModelTransform
            {
                Translation = new FastVec3f(0f, 0f, 0f),
                Rotation = new FastVec3f(0f, 0f, 90f),
                Origin = new FastVec3f(0.41f, 0f, 0.5f),
                Scale = 3.4f,
            };

            return item;
        }
    }
}
