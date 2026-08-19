using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Skillbooks
{
    /// <summary>
    /// Adds a genuine "Learned Traits" tab to the vanilla character dialog, alongside
    /// "Character" and "Traits". CharacterSystem (vsessentialsmod, confirmed via decompile)
    /// adds its own "Traits" tab the exact same way: charDlg.Tabs/.RenderTabHandlers are
    /// plain mutable lists exposed through get-only properties, not something that needs
    /// reflection or patching to extend -- ComposeExtraGuis (a separate hook used for side
    /// panels like Environment/Stats) doesn't create an actual clickable tab, which is why
    /// the first version of this feature didn't land where expected.
    ///
    /// Lists every code in the shared "skillbooksLearnedTraits" watched attribute (see
    /// ItemSkillBook.RecordLearnedTrait) rather than "extraTraits" itself -- extraTraits is
    /// a generic vanilla extension point other mods write to as well (race selection, for
    /// one), so it can't double as "traits granted by a skillbook" for display purposes.
    /// Unlike vanilla's own Traits tab (a fixed-size richtext box with no scrollbar --
    /// confirmed via decompile, and the actual source of the "too many traits" overflow bug
    /// this feature exists to work around), this one has a real scrollbar.
    ///
    /// Coexistence with other tab-adding mods (aldiclasses, rustboundmagic, etc.): safe
    /// against any mod that -- like this one -- computes its own DataInt from live list
    /// state immediately before inserting, regardless of load order between mods, since
    /// Tabs.Add always appends and each mod reads the count right before its own insert.
    /// Not defensible against a mod that hardcodes a DataInt not matching its own eventual
    /// position (the mistake vanilla's own CharacterSystem makes, worked around here by
    /// deferring to LevelFinalize so its tab is always added -- and correct -- before ours)
    /// -- DataInt is a literal index into RenderTabHandlers, not just an identifier, so
    /// there's no "pick an unused value" workaround for a neighbor mod that gets its own
    /// value wrong; that breaks the dispatch for everyone, not just that mod's own tab.
    /// </summary>
    public class SkillBookTraitsTab : ModSystem
    {
        private const string TextKey = "skillbookslearnedtraits-text";
        private const string ScrollbarKey = "skillbookslearnedtraits-scroll";
        private const float ViewportHeight = 200f;

        private ICoreClientAPI capi;
        private GuiDialogCharacterBase dlg;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            // Deferred to LevelFinalize rather than done here directly: GuiTab clicks
            // resolve via GuiTab.DataInt (confirmed via decompiling
            // GuiElementHorizontalTabs.SetValue), not list position, and CharacterSystem
            // hardcodes DataInt=1 for its own "Traits" tab assuming nothing else has been
            // added yet. Registering here (during StartClientSide) raced CharacterSystem's
            // own StartClientSide and sometimes won, landing us at DataInt=1 too and
            // silently hijacking clicks on the real Traits tab. LevelFinalize always runs
            // after every mod's StartClientSide has completed, so by the time we read
            // dlg.Tabs.Count here, CharacterSystem's tab already exists and our own
            // self-computed DataInt is guaranteed correct.
            api.Event.LevelFinalize += RegisterTab;
        }

        private void RegisterTab()
        {
            dlg = capi.Gui.LoadedGuis.Find(d => d is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            if (dlg == null) { return; }

            // DataInt must equal the index our handler will occupy in RenderTabHandlers --
            // GuiElementHorizontalTabs.SetValue calls handler(tabs[selectedIndex].DataInt),
            // and GuiDialogCharacter.OnTabClicked uses that value directly as
            // rendertabhandlers[curTab]. Since RenderTabHandlers.Add always appends, that
            // index is exactly the current count, read live immediately before our own Add
            // so it's correct regardless of how many other mods (well-behaved ones, anyway
            // -- see class remarks) have already added their own tabs by this point.
            dlg.Tabs.Add(new GuiTab
            {
                Name = Lang.Get("skillbooks:learnedtraits-title"),
                DataInt = dlg.Tabs.Count,
            });
            dlg.RenderTabHandlers.Add(ComposeTab);
        }

        private void ComposeTab(GuiComposer compo)
        {
            ElementBounds textBounds = ElementBounds.Fixed(0, 25, 365, ViewportHeight);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7).WithFixedWidth(20);

            RichTextComponentBase[] comps = VtmlUtil.Richtextify(capi, BuildText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15));

            compo
                .BeginClip(clippingBounds)
                    .AddInset(insetBounds, 3)
                    .AddRichtext(comps, textBounds, TextKey)
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, ScrollbarKey)
            ;

            // The caller (GuiDialogCharacter.ComposeGuis) invokes this handler *before* its
            // own Compose() call, so the richtext element's real laid-out height isn't known
            // yet here -- reading Bounds.fixedHeight now would just see the un-laid-out
            // placeholder. Deferred one tick so Compose() has already run by the time this
            // fires, matching how GuiDialogLogViewer/GuiDialogHandbook size their scrollbars
            // only after their own Compose() call.
            capi.Event.EnqueueMainThreadTask(FixScrollbarHeight, "skillbookslearnedtraits-fixscroll");
        }

        private void FixScrollbarHeight()
        {
            GuiComposer compo = dlg?.Composers["playercharacter"];
            GuiElementRichtext richtext = compo?.GetRichtext(TextKey);
            GuiElementScrollbar scrollbar = compo?.GetScrollbar(ScrollbarKey);
            if (richtext == null || scrollbar == null) { return; }

            scrollbar.SetHeights(ViewportHeight, (float)richtext.Bounds.fixedHeight);
        }

        private void OnNewScrollbarValue(float value)
        {
            GuiElementRichtext richtext = dlg.Composers["playercharacter"]?.GetRichtext(TextKey);
            if (richtext == null) { return; }
            richtext.Bounds.fixedY = 3 - value;
            richtext.Bounds.CalcWorldBounds();
        }

        /// <summary>
        /// Stat bonuses need each trait's raw Attributes, which skillbooksLearnedTraits
        /// doesn't carry on its own -- reloaded from config/traits.json the same way
        /// TraitDiscovery.LoadAllTraits does server-side, since IAssetManager.GetMany is
        /// equally available client-side.
        /// </summary>
        private string BuildText()
        {
            string[] learnedCodes = capi.World.Player.Entity.WatchedAttributes.GetStringArray("skillbooksLearnedTraits", Array.Empty<string>());
            if (learnedCodes.Length == 0) { return Lang.Get("skillbooks:learnedtraits-empty"); }

            Dictionary<string, Dictionary<string, double>> attributesByCode = LoadTraitAttributes(capi);

            StringBuilder text = new StringBuilder();
            foreach (string code in learnedCodes)
            {
                text.AppendLine(Lang.Get("trait-" + code));

                string traitDesc = Lang.GetIfExists("traitdesc-" + code);
                if (!string.IsNullOrEmpty(traitDesc))
                {
                    text.AppendLine(traitDesc);
                }

                if (attributesByCode.TryGetValue(code, out Dictionary<string, double> attributes))
                {
                    string attrText = TraitAttributeFormatter.Format(new JsonObject(JObject.FromObject(attributes)));
                    if (!string.IsNullOrEmpty(attrText))
                    {
                        text.AppendLine(attrText);
                    }
                }

                text.AppendLine();
            }

            return text.ToString().TrimEnd();
        }

        private static Dictionary<string, Dictionary<string, double>> LoadTraitAttributes(ICoreClientAPI capi)
        {
            Dictionary<string, Dictionary<string, double>> attributesByCode = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<AssetLocation, JToken> many = capi.Assets.GetMany<JToken>(capi.Logger, "config/traits", null);

            foreach (var (loc, token) in many)
            {
                if (token is JObject) { AddTrait(attributesByCode, JsonUtil.ToObject<Trait>(token, loc.Domain, null)); }
                else if (token is JArray array)
                {
                    foreach (JToken entry in array) { AddTrait(attributesByCode, JsonUtil.ToObject<Trait>(entry, loc.Domain, null)); }
                }
            }

            return attributesByCode;
        }

        private static void AddTrait(Dictionary<string, Dictionary<string, double>> attributesByCode, Trait trait)
        {
            if (trait?.Code == null || trait.Attributes is not { Count: > 0 }) { return; }
            attributesByCode[trait.Code] = trait.Attributes;
        }
    }
}
