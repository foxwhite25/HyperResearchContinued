using System;
using System.Collections.Generic;
using System.Reflection;
using HyperResearch.Utils;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace HyperResearch.Common.Systems;

/// <summary>
///     Switches the creative duplication grid into a read-only missing-items view and keeps
///     missing items from being duplicated. All state is client-side.
/// </summary>
[Autoload(Side = ModSide.Client)]
internal class MissingItemsSystem : ModSystem
{
    private static readonly FieldInfo? AvailableItemsField = typeof(UICreativeInfiniteItemsDisplay)
        .GetField("_itemIdsAvailableToShow", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? FiltererField = typeof(UICreativeInfiniteItemsDisplay)
        .GetField("_filterer", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? SorterField = typeof(UICreativeInfiniteItemsDisplay)
        .GetField("_sorter", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? GridField = typeof(UICreativeInfiniteItemsDisplay)
        .GetField("_itemGrid", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo? UpdateContentsMethod = typeof(UICreativeInfiniteItemsDisplay)
        .GetMethod("UpdateContents", BindingFlags.Instance | BindingFlags.NonPublic);

    private static UICreativeInfiniteItemsDisplay? _display;
    private static bool _reflectionFailureLogged;

    /// <summary>Whether the duplication grid is currently showing only missing items.</summary>
    public static bool Active { get; private set; }

    public override void Load()
    {
        On_UICreativeInfiniteItemsDisplay.UpdateContents += OnUpdateContents;
        On_ItemSlot.LeftClick_ItemArray_int_int += OnLeftClick;
        On_ItemSlot.RightClick_ItemArray_int_int += OnRightClick;
        On_ItemSlot.PickupItemIntoMouse += OnPickupItemIntoMouse;
        On_ItemSlot.OverrideHover_ItemArray_int_int += OnOverrideHover;
    }

    public override void Unload()
    {
        On_UICreativeInfiniteItemsDisplay.UpdateContents -= OnUpdateContents;
        On_ItemSlot.LeftClick_ItemArray_int_int -= OnLeftClick;
        On_ItemSlot.RightClick_ItemArray_int_int -= OnRightClick;
        On_ItemSlot.PickupItemIntoMouse -= OnPickupItemIntoMouse;
        On_ItemSlot.OverrideHover_ItemArray_int_int -= OnOverrideHover;
        Reset();
    }

    public override void OnWorldLoad()
    {
        Reset();
        ResearchProgress.Invalidate();
    }

    public override void OnWorldUnload()
    {
        Reset();
        ResearchProgress.Invalidate();
    }

    public static void Toggle()
    {
        if (!Active && ResearchProgress.MissingCount == 0) return;
        SetActive(!Active);
    }

    public static void SetActive(bool active)
    {
        if (Active == active) return;

        Active = active;
        Refresh();
    }

    public static void Deactivate() => SetActive(false);

    public static void Reset()
    {
        Active = false;
        _display = null;
    }

    public static void Refresh()
    {
        if (_display is null) return;
        if (UpdateContentsMethod is null)
        {
            LogReflectionFailure();
            return;
        }

        try
        {
            UpdateContentsMethod.Invoke(_display, null);
        }
        catch (Exception e)
        {
            LogFailure(e);
            Active = false;
        }
    }

    private static void OnUpdateContents(On_UICreativeInfiniteItemsDisplay.orig_UpdateContents orig,
        UICreativeInfiniteItemsDisplay self)
    {
        _display = self;
        orig(self);
        if (!Active) return;

        if (ResearchProgress.MissingCount == 0)
        {
            Active = false;
            return;
        }

        try
        {
            var list = (List<int>?)AvailableItemsField?.GetValue(self);
            var filterer = (EntryFilterer<Item, IItemEntryFilter>?)FiltererField?.GetValue(self);
            var sorter = (EntrySorter<int, ICreativeItemSortStep>?)SorterField?.GetValue(self);
            var grid = (UIDynamicItemCollection?)GridField?.GetValue(self);

            if (list is null || filterer is null || grid is null)
            {
                LogReflectionFailure();
                Active = false;
                return;
            }

            list.Clear();
            foreach (int itemId in ResearchProgress.MissingItems)
            {
                if (!ContentSamples.ItemsByType.TryGetValue(itemId, out Item? item)) continue;
                if (filterer.FitsFilter(item))
                    list.Add(itemId);
            }

            SortItems(list, sorter);
            grid.SetContentsToShow(list);
        }
        catch (Exception e)
        {
            LogFailure(e);
            Active = false;
        }
    }

    private static void SortItems(List<int> items, EntrySorter<int, ICreativeItemSortStep>? sorter)
    {
        if (sorter is null)
        {
            items.Sort(CompareByName);
            return;
        }

        try
        {
            items.Sort(sorter);
        }
        catch
        {
            // A mod item without a creative sorting id must not make the whole mode fail.
            items.Sort(CompareByName);
        }
    }

    private static int CompareByName(int x, int y) =>
        string.Compare(
            ContentSamples.ItemsByType[x].Name,
            ContentSamples.ItemsByType[y].Name,
            StringComparison.OrdinalIgnoreCase
        );

    private static bool ShouldBlock(Item[] inv, int context, int slot)
    {
        if (!Active || context != ItemSlot.Context.CreativeInfinite) return false;
        if (inv is null || slot < 0 || slot >= inv.Length) return false;

        Item item = inv[slot];
        return item is { IsAir: false } && ResearchProgress.IsMissing(item.type);
    }

    private static void OnLeftClick(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig, Item[] inv, int context,
        int slot)
    {
        if (ShouldBlock(inv, context, slot)) return;
        orig(inv, context, slot);
    }

    private static void OnRightClick(On_ItemSlot.orig_RightClick_ItemArray_int_int orig, Item[] inv, int context,
        int slot)
    {
        if (ShouldBlock(inv, context, slot)) return;
        orig(inv, context, slot);
    }

    private static void OnPickupItemIntoMouse(On_ItemSlot.orig_PickupItemIntoMouse orig, Item[] inv, int context,
        int slot, Player player)
    {
        if (ShouldBlock(inv, context, slot)) return;
        orig(inv, context, slot, player);
    }

    private static void OnOverrideHover(On_ItemSlot.orig_OverrideHover_ItemArray_int_int orig, Item[] inv,
        int context, int slot)
    {
        if (ShouldBlock(inv, context, slot)) return;
        orig(inv, context, slot);
    }

    private static void LogReflectionFailure()
    {
        if (_reflectionFailureLogged) return;

        _reflectionFailureLogged = true;
        ModContent.GetInstance<global::HyperResearch.HyperResearch>().Logger.Warn(
            "HyperResearch: could not access the creative duplication grid; " +
            "the missing-items view has been disabled for this session."
        );
    }

    private static void LogFailure(Exception e)
    {
        ModContent.GetInstance<global::HyperResearch.HyperResearch>().Logger.Warn(
            $"HyperResearch: missing-items view failed: {e}"
        );
    }
}
