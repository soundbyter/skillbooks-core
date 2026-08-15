using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Skillbooks
{
    /// <summary>
    /// Registers a single static handbook page describing the skill book mechanic as a
    /// concept, not one entry per trait -- keeps which traits have books a surprise while
    /// still being discoverable.
    /// Also describes the reroll mechanic (Holding temporal gear in offhand and holding right click)
    /// and salvaging (crafting with a knife). Actual recipe previews may be added later.
    /// </summary>
    public class SkillBookHandbook : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.ModLoader.GetModSystem<ModSystemSurvivalHandbook>().OnInitCustomPages += pages => AddPage(api, pages);
        }

        private void AddPage(ICoreClientAPI api, List<GuiHandbookPage> pages)
        {
            GuiHandbookTextPage page = new GuiHandbookTextPage
            {
                pageCode = "skillbooks-concept",
                Title = Lang.Get("skillbooks:handbook-title"),
                Text = Lang.Get("skillbooks:handbook-text"),
            };
            page.Init(api);
            pages.Add(page);
        }
    }
}
