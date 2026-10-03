using System;
using System.Collections.Generic;
using System.Reflection;
using HyperResearch.Common.Configs;
using HyperResearch.Common.ModPlayers;
using HyperResearch.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ModLoader;
using Terraria.UI;

namespace HyperResearch.Common.Systems;

public class HooksSystem : ModSystem
{
    internal static event Action? WorldLoaded;
    internal static event Action? WorldUnloaded;

    private static readonly FieldInfo? CreativeSearchField = typeof(ItemFilters.BySearch).GetField(
        "_search", BindingFlags.Instance | BindingFlags.NonPublic);

    public override void Load()
    {
        IL_ItemSlot.Draw_SpriteBatch_ItemArray_int_int_Vector2_Color += EditItemSlotDraw;
        On_ItemFilters.BySearch.FitsFilter += EditItemSearchFilter;
    }

    public override void Unload()
    {
        IL_ItemSlot.Draw_SpriteBatch_ItemArray_int_int_Vector2_Color -= EditItemSlotDraw;
        On_ItemFilters.BySearch.FitsFilter -= EditItemSearchFilter;
        PinyinMatcher.ClearCache();
    }

    public override void OnWorldLoad() => WorldLoaded?.Invoke();

    public override void OnWorldUnload() => WorldUnloaded?.Invoke();

    private static bool EditItemSearchFilter(On_ItemFilters.BySearch.orig_FitsFilter orig,
        ItemFilters.BySearch self, Item entry)
    {
        if (!HyperConfig.Instance.PinyinSearch || CreativeSearchField is null)
            return orig(self, entry);

        string? search;
        try
        {
            search = CreativeSearchField.GetValue(self) as string;
        }
        catch
        {
            return orig(self, entry);
        }

        // A null search shows everything and is handled by vanilla.
        if (string.IsNullOrEmpty(search)) return orig(self, entry);

        try
        {
            foreach (TooltipLine line in BuildTooltipLines(entry))
            {
                if (string.IsNullOrEmpty(line.Text)) continue;

                if (line.Text.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    PinyinMatcher.Matches(line.Text, search))
                {
                    return true;
                }
            }

            return entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   PinyinMatcher.Matches(entry.Name, search);
        }
        catch
        {
            // Never break the search box: fall back to vanilla matching.
            return orig(self, entry);
        }
    }

    private static string[] _tooltipLines = [];
    private static string[] _tooltipNames = [];
    private static bool[] _tooltipPrefixLines = [];
    private static bool[] _tooltipBadPrefixLines = [];

    /// <summary>
    /// Builds the same tooltip lines that vanilla's creative search filter inspects.
    /// The scratch arrays are reused because the filter runs once per item on the UI thread.
    /// </summary>
    private static List<TooltipLine> BuildTooltipLines(Item item)
    {
        int size = Math.Max(1, (30 + item.ToolTip?.Lines).GetValueOrDefault());
        if (_tooltipLines.Length < size)
        {
            _tooltipLines = new string[size];
            _tooltipNames = new string[size];
            _tooltipPrefixLines = new bool[size];
            _tooltipBadPrefixLines = new bool[size];
        }

        int numLines = 1;
        int yoyoLogo = 0;
        int researchLine = 0;
        Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyoLogo, ref researchLine, item.knockBack,
            ref numLines, _tooltipLines, _tooltipPrefixLines, _tooltipBadPrefixLines, _tooltipNames, out _);

        return ItemLoader.ModifyTooltips(item, ref numLines, _tooltipNames, ref _tooltipLines,
            ref _tooltipPrefixLines, ref _tooltipBadPrefixLines, ref yoyoLogo, out Color?[] _, -1);
    }

    private void EditItemSlotDraw(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            c.FindNext(out ILCursor[] m1, i => i.MatchLdcI4(-1)); // int num9 = -1;
            c.GotoNext(MoveType.After, i => i.MatchBrtrue(m1[0].Next!)); // if (!flag2)
            
            c.RemoveRange(15);
            c.EmitLdarg0(); // spriteBatch
            c.EmitLdloc(7); // texture
            c.EmitLdarg(4); // position
            c.EmitLdloc(8); // color2
            c.EmitLdarg2(); // context
            c.EmitLdloc1(); // item
            c.EmitDelegate((SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Color color, int context, Item item) =>
            {
                if (HyperConfig.Instance.VisualizeBuffStatus && context == ItemSlot.Context.CreativeInfinite)
                {
                    if (ConfigOptions.UseResearchedBannersBuff &&
                        BannerSystem.TryItemToBanner(item.type, out int bannerId) &&
                        Main.LocalPlayer.GetModPlayer<BannerPlayer>().ResearchedBanners.TryGetValue(bannerId, out bool bannerEnabled))
                    {
                        color = bannerEnabled ? new Color(53, 111, 85) : new Color(107, 57, 81);
                    }
                    else if (ConfigOptions.UseResearchedPotionsBuff &&
                    BuffUtils.IsAcceptableBuffItem(item) &&
                    Main.LocalPlayer.GetModPlayer<BuffPlayer>().Buffs.TryGetValue(item.buffType, out bool buffEnabled))
                    {
                        color = buffEnabled ? new Color(53, 111, 85) : new Color(107, 57, 81);
                    }
                }
                spriteBatch.Draw(texture, position, null, color, 0f, default, Main.inventoryScale, SpriteEffects.None, 0f);
            });
        }
        catch
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<HyperResearch>(), il);
        }
    }
}
