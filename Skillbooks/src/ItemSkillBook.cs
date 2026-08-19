using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Skillbooks
{
    /// <summary>
    /// Hold right-click ~2s to permanently grant the trait named in this item's own
    /// "skillbooks:traitCode" attribute. Holding a temporal gear in the offhand while
    /// reading switches the same interaction into a reroll instead (fixed 1-gear cost,
    /// since the offhand only ever holds one item).
    /// </summary>
    public class ItemSkillBook : Item
    {
        private const float SecondsToRead = 2f;
        private const float SecondsToReroll = 3.5f;

        private static readonly AssetLocation TemporalGearCode = new AssetLocation("game", "gear-temporal");

        private IProgressBar progressBar;

        public string TraitCode => Attributes?["skillbooks:traitCode"].AsString();

        /// <summary>
        /// Set when this book's trait code is no longer discovered (its providing mod was
        /// removed). Shows fixed flavour text, grants nothing, and isn't consumed on read --
        /// but can still be rerolled into a fresh valid book.
        /// </summary>
        public bool IsIllegible => Attributes?["skillbooks:illegible"].AsBool(false) ?? false;

        /// <summary>
        /// Title/blurb are resolved once by SkillBookFlavour at registration time and stashed
        /// in Attributes, avoiding runtime lang entries for codes only known after discovery.
        /// </summary>
        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (IsIllegible) { return Lang.Get("skillbooks:item-illegible-skillbook"); }
            return Attributes?["skillbooks:title"].AsString() ?? base.GetHeldItemName(itemStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (IsIllegible)
            {
                dsc.AppendLine(Lang.Get("skillbooks:illegible-blurb"));
                return;
            }
            string blurb = Attributes?["skillbooks:blurb"].AsString();
            if (!string.IsNullOrEmpty(blurb))
            {
                dsc.AppendLine(blurb);
            }
            AppendTraitSummary(dsc);
        }

        /// <summary>
        /// The flavour text alone never says which trait this actually is or what it does --
        /// this appends that mechanical info below it. traitdesc covers what a crafting
        /// trait unlocks; TraitAttributeFormatter covers the rarer case of a trait that also
        /// carries stat Attributes.
        /// </summary>
        private void AppendTraitSummary(StringBuilder dsc)
        {
            string traitCode = TraitCode;
            if (string.IsNullOrEmpty(traitCode)) { return; }

            dsc.AppendLine();
            dsc.AppendLine(Lang.Get("skillbooks:tooltip-grants", Lang.Get("trait-" + traitCode)));

            string traitDesc = Lang.GetIfExists("traitdesc-" + traitCode);
            if (!string.IsNullOrEmpty(traitDesc))
            {
                dsc.AppendLine(traitDesc);
            }

            string attrText = TraitAttributeFormatter.Format(Attributes?["skillbooks:attributes"]);
            if (!string.IsNullOrEmpty(attrText))
            {
                dsc.AppendLine(attrText);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (byEntity.Controls.ShiftKey) { return; }
            handHandling = EnumHandHandling.PreventDefault;

            if (api is ICoreClientAPI capi)
            {
                ModSystemProgressBar progressBarSystem = capi.ModLoader.GetModSystem<ModSystemProgressBar>();
                progressBarSystem.RemoveProgressbar(progressBar);
                progressBar = progressBarSystem.AddProgressbar();
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            bool rerolling = HasOffhandGear(byEntity);
            float duration = rerolling ? SecondsToReroll : SecondsToRead;

            if (progressBar != null)
            {
                progressBar.Progress = secondsUsed / duration;
            }

            if (rerolling && byEntity.World is IClientWorldAccessor clientWorld)
            {
                clientWorld.AddCameraShake(0.035f);
            }

            return secondsUsed < duration;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            RemoveProgressBar();
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            RemoveProgressBar();

            float completionDuration = HasOffhandGear(byEntity) ? SecondsToReroll : SecondsToRead;
            if (secondsUsed < completionDuration - 0.1f) { return; }
            if (byEntity.World.Side != EnumAppSide.Server) { return; }

            string traitCode = TraitCode;
            if (string.IsNullOrEmpty(traitCode)) { return; }

            IPlayer player = (byEntity as EntityPlayer)?.Player;
            if (player == null) { return; }

            ICoreAPI resolvedApi = api;
            SkillBooksModSystem modSystem = resolvedApi.ModLoader.GetModSystem<SkillBooksModSystem>();
            ItemSlot offhandSlot = byEntity.LeftHandItemSlot;
            bool illegible = IsIllegible;

            bool rerollAllowed = modSystem.Config.RerollEnabled && HasOffhandGear(byEntity)
                && (illegible || !modSystem.Config.RerollIllegibleOnly);
            if (rerollAllowed)
            {
                Reroll(resolvedApi, modSystem, traitCode, slot, offhandSlot, player);
                return;
            }

            if (illegible)
            {
                (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooks:msg-illegible"), EnumChatType.Notification);
                return;
            }

            string[] extraTraits = byEntity.WatchedAttributes.GetStringArray("extraTraits", System.Array.Empty<string>());
            if (extraTraits.Contains(traitCode))
            {
                (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooks:msg-alreadyknown"), EnumChatType.Notification);
                return;
            }

            byEntity.WatchedAttributes.SetStringArray("extraTraits", extraTraits.Append(traitCode).ToArray());
            byEntity.WatchedAttributes.MarkPathDirty("extraTraits");
            RecordLearnedTrait(byEntity, traitCode);
            RefreshTraitStats(resolvedApi, byEntity);

            slot.TakeOut(1);
            slot.MarkDirty();

            (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooks:msg-traitlearned", Lang.Get("trait-" + traitCode)), EnumChatType.Notification);
        }

        /// <summary>
        /// extraTraits is a generic vanilla extension point (CharacterSystem only ever reads
        /// it) that any mod can add codes to -- race selection and other mods use it too, so
        /// it can't double as "traits granted specifically by a skillbook" for display
        /// purposes. Tracked separately here, under an unprefixed key shared with Skillbooks:
        /// Stats the same way extraTraits itself is shared, so a "Learned Traits" tab can
        /// show only what was actually read from a book.
        /// </summary>
        private static void RecordLearnedTrait(EntityAgent byEntity, string traitCode)
        {
            string[] learned = byEntity.WatchedAttributes.GetStringArray("skillbooksLearnedTraits", System.Array.Empty<string>());
            if (learned.Contains(traitCode)) { return; }

            byEntity.WatchedAttributes.SetStringArray("skillbooksLearnedTraits", learned.Append(traitCode).ToArray());
            byEntity.WatchedAttributes.MarkPathDirty("skillbooksLearnedTraits");
        }

        /// <summary>
        /// A modded trait can carry both a crafting gate and stat Attributes. Nothing but
        /// CharacterSystem.setCharacterClass re-triggers applyTraitAttributes, so this forces
        /// that refresh after granting -- otherwise a stat-carrying trait would do nothing
        /// until the player's next relog. initializeGear:false skips the client-only gear
        /// re-equip. Try/catch: a stale characterClass code shouldn't undo the grant above.
        /// </summary>
        private static void RefreshTraitStats(ICoreAPI api, EntityAgent byEntity)
        {
            if (byEntity is not EntityPlayer entityPlayer) { return; }
            string currentClassCode = byEntity.WatchedAttributes.GetString("characterClass");
            if (string.IsNullOrEmpty(currentClassCode)) { return; }

            try
            {
                CharacterSystem characterSystem = api.ModLoader.GetModSystem<CharacterSystem>();
                characterSystem?.setCharacterClass(entityPlayer, currentClassCode, initializeGear: false);
            }
            catch (System.Exception ex)
            {
                api.Logger.Warning($"[Skillbooks] Failed to refresh trait stats after granting a trait: {ex.Message}");
            }
        }

        private static void Reroll(ICoreAPI api, SkillBooksModSystem modSystem, string currentTraitCode, ItemSlot bookSlot, ItemSlot offhandSlot, IPlayer player)
        {
            List<Item> candidates = new List<Item>();
            foreach (string candidateTraitCode in modSystem.CraftingTraits.Keys)
            {
                if (candidateTraitCode == currentTraitCode) { continue; }
                Item bookItem = api.World.GetItem(new AssetLocation("skillbooks", "skillbook-" + candidateTraitCode));
                if (bookItem != null) { candidates.Add(bookItem); }
            }
            if (candidates.Count == 0)
            {
                // No other trait to reroll into -- fall back to allowing the same one.
                Item ownBook = api.World.GetItem(new AssetLocation("skillbooks", "skillbook-" + currentTraitCode));
                if (ownBook != null) { candidates.Add(ownBook); }
            }
            if (candidates.Count == 0) { return; }

            Item chosen = candidates[api.World.Rand.Next(candidates.Count)];

            offhandSlot.TakeOut(1);
            offhandSlot.MarkDirty();
            bookSlot.TakeOut(1);
            bookSlot.MarkDirty();

            ItemStack resultStack = new ItemStack(chosen);
            if (!player.InventoryManager.TryGiveItemstack(resultStack))
            {
                api.World.SpawnItemEntity(resultStack, player.Entity.Pos.XYZ);
            }

            (player as IServerPlayer)?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("skillbooks:msg-rerolled"), EnumChatType.Notification);
        }

        private static bool HasOffhandGear(EntityAgent byEntity)
        {
            return byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.Equals(TemporalGearCode) == true;
        }

        private void RemoveProgressBar()
        {
            if (api is ICoreClientAPI capi && progressBar != null)
            {
                capi.ModLoader.GetModSystem<ModSystemProgressBar>().RemoveProgressbar(progressBar);
                progressBar = null;
            }
        }
    }
}
