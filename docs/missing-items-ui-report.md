# Missing Items UI: Investigation Report

## Context

The request is to show which items the player has not researched yet. This matches the recent Steam comment thread on the mod: the progress tracker only shows a total, so a player cannot tell whether the remaining gap is caused by items they missed or by modded debug items that can never be obtained.

Assumption: "missing" means a researchable item whose research is not complete. This is not about missing crafting materials.

The author's earlier objection was that many mods add unobtainable debug items with a research count, so 100% is often impossible. That makes a missing list more useful, not less: it is the only way to see which items are keeping the tracker below 100%. The list should include modded items, but it needs a way to separate vanilla completion from modded completion.

## TL;DR

- The vanilla duplication menu only receives fully researched items. There is no built-in missing list, filter, or placeholder icon.
- Reusing the duplication grid is technically feasible. Showing missing item icons is the easy part. The hard part is making them non-duplicable across every input path and keeping search, filters, sorting, and counts consistent.
- Recommended first version: a read-only "missing items" mode inside the duplication menu, toggled from the existing progress tracker. It reuses the vanilla search, category filters, sorting, scrolling, and grid. Default state is off, so vanilla behavior is unchanged.
- If the feature should grow into a proper checklist with vanilla/mod filters, "in progress" items, ignored items, and mod names, build a dedicated panel instead. The duplication menu's filter bar is already full, and mixing a read-only list into a spawn menu is awkward.
- Do not ship the mixed "red icons among usable items" view as the default. If it is implemented, make it opt-in and use a red X overlay plus a dimmed icon, not a red fill. In a missing-only view, a per-item red mark is unnecessary visual noise; the mode label is enough.
- Use one shared predicate for the count and the list. The current count logic does not check `ItemLoader.CanResearch`, and it has a dead condition in `ResearchSystem.CalculateResearchable`. Both should be fixed before the list is exposed.

## 1. How the vanilla research UI works

Relevant decompiled classes (from `tModLoader.dll`):

- `Terraria.GameContent.UI.Elements.UICreativeInfiniteItemsDisplay`
- `Terraria.GameContent.UI.Elements.UIDynamicItemCollection`
- `Terraria.GameContent.UI.States.UICreativePowersMenu`
- `Terraria.GameContent.Creative.CreativeUI`
- `Terraria.GameContent.Creative.ItemsSacrificedUnlocksTracker`
- `Terraria.UI.ItemSlot`

Key facts:

1. `UICreativeInfiniteItemsDisplay.UpdateContents()` calls `Main.LocalPlayerCreativeTracker.ItemSacrifices.FillListOfItemsThatCanBeObtainedInfinitely(...)`. That method only adds items whose sacrifice count is already at or above the required count.
2. The list is then passed through `_filterer.FitsFilter(...)` and sorted by `_sorter`, then handed to `_itemGrid.SetContentsToShow(...)`.
3. `_itemIdsAvailableTotal`, `_itemIdsAvailableToShow`, `_filterer`, `_sorter`, and `_itemGrid` are all private fields.
4. `UIDynamicItemCollection.DrawSelf` hardcodes `ItemSlot.Context.CreativeInfinite` (29). On hover it calls `ItemSlot.OverrideHover`, `LeftClick`, `RightClick`, and `MouseHover` directly.
5. For context 29, `ItemSlot.LeftClick` creates a full stack of the item on the cursor. `RightClick` eventually does the same through `PickupItemIntoMouse`. `OverrideHover` sets the pickup cursor. Any "unclickable" implementation must intercept these paths; a normal UI element click handler will not do it.
6. The vanilla menu never draws a locked/unavailable state for missing items. There is no existing "red icon" or placeholder behavior to reuse.

The mod already hooks private vanilla UI successfully:

- `On_UICreativePowersMenu.MainCategoryButtonClick` in `UI/DuplicationMenu.cs` proves that On hooks are generated for private methods.
- `IL_ItemSlot.Draw_SpriteBatch_ItemArray_int_int_Vector2_Color` in `Common/Systems/HooksSystem.cs` already replaces the item texture draw for every item slot, including context 29. This is the natural place to tint or overlay missing icons.
- `On_ItemFilters.BySearch.FitsFilter` already extends creative search with pinyin. Any missing list that reuses `ItemFilters.BySearch` gets pinyin search for free.

## 2. UI options

### Option A: Mixed list in the vanilla duplication grid

Add missing items to the existing grid. Researched items stay normal; missing items are dimmed and marked with a red X. Clicking, right-clicking, and the pickup cursor are blocked for missing items.

```
[normal item] [normal item] [red X item] [normal item] ...
```

Pros:

- Uses the existing search, category filters, sorting, scrollbar, and gamepad layout.
- Directly answers "is this specific item researched?" because both states appear in the same list.
- Smallest amount of new layout code.

Cons:

- Turns a spawn menu into a mixed read/write list. Dead slots invite clicks and feel broken.
- Adds thousands of unclickable entries early in a playthrough. The grid becomes noisy and slow to browse.
- Spoils all un-researched item names and icons by default.
- Requires blocking three input paths and keeping the cursor state consistent.
- The category filter bar is already full (11 filters, 440 px in a 480 px panel); adding a "Missing" filter button is not practical without layout changes.
- The visual result is the least accessible: color alone is not enough; a shape/overlay and tooltip are required.

Implementation sketch:

- Hook `On_UICreativeInfiniteItemsDisplay.UpdateContents`. After calling the original, reflect `_itemIdsAvailableToShow`, `_filterer`, `_sorter`, and `_itemGrid`.
- Append every missing item that passes `_filterer.FitsFilter`, sort with `_sorter`, and call `_itemGrid.SetContentsToShow(list)` again.
- Extend the existing `ItemSlot.Draw` IL hook to dim the item and draw a red X when `context == ItemSlot.Context.CreativeInfinite && !Researcher.IsResearched(item.type)`.
- Hook `ItemSlot.LeftClick`, `ItemSlot.RightClick`, and `ItemSlot.OverrideHover` for context 29 to block interactions for missing items. Keep `MouseHover` so the tooltip still works.
- Gate the feature behind a config option, default off.

Verdict: feasible, but not the best default. Use only if search-in-place is the main requirement and the user accepts a mixed list.

### Option B: Missing-only mode in the duplication menu (recommended first version)

Keep the vanilla duplication page, but add a toggle that switches the grid's source list from "researched items" to "missing items". The same search, category filters, sorting, scrolling, and pinyin search still apply. The grid is read-only in this mode.

```
Progress text:  76% researched (3712/4876 items)
                1164 missing - click to view

After click:    Showing 1164 missing items - click to return
                [search] [category filters]
                [missing item] [missing item] [missing item] ...
```

Pros:

- Reuses the entire vanilla UI and all mod-added filters/sorts.
- No second window and no new layout work.
- Missing-only view avoids the red-icon noise and the mixed read/write problem.
- Default behavior is unchanged until the player clicks the toggle.
- The progress tracker is the natural entry point and already exists in `UI/DuplicationMenu.cs`.

Cons:

- Still patches a private vanilla method and private fields.
- The mode is read-only; gamepad users can hover and scroll but cannot select anything meaningful.
- Discoverability depends on the progress text being visible; it is only shown when the duplication category is selected.
- If the feature grows to require vanilla/mod filters or an ignore list, the mode will not scale; the filter bar is full.

Implementation sketch:

- Add a static `ShowMissingItems` flag.
- In the `UpdateContents` postfix, if the flag is set, clear `_itemIdsAvailableToShow`, fill it with missing items that pass `_filterer.FitsFilter`, sort with `_sorter`, and set the grid contents. Otherwise leave the original list untouched.
- Add a second line under the progress text in `DuplicationMenu` with the missing count. Make it clickable and show a hover tooltip. On click, toggle the flag and invoke `UpdateContents` through reflection.
- Reset the flag when the creative menu closes, the world unloads, or the player leaves the duplication category.
- Block context 29 input for missing items while the mode is active.
- Optional: draw a small red X on the toggle, not on every item. The mode label already communicates the state.

Verdict: best fit for a small first version and for "reuse the research UI" without the downsides of a mixed list.

### Option C: Dedicated missing-items panel

Create a new read-only `UIState` with its own panel, title, search bar, category filters, grid, and scrollbar. Open it from the progress text, a keybind, or both.

```
+--------------------------------------------------+
| Missing items (1164)                         [X] |
| [search]                                         |
| [W][A][V][B][F][Ac][MA][C][T][M][?]              |
| +----------------------------------------------+ |
| | [item] [item] [item] [item] [item] [item]    | |
| | [item] [item] [item] [item] [item] [item]    | |
| +----------------------------------------------+ |
| Vanilla: 42 missing   Modded: 1122 missing       |
+--------------------------------------------------+
```

Pros:

- Does not alter the duplication menu at all, apart from the entry point.
- Can include controls that do not fit in the vanilla filter bar: All / Vanilla / Modded, Missing / In progress, ignored items, mod name in tooltips.
- Can be extended into a real research checklist: progress bars, "where to get it", sorting by progress, etc.
- Read-only by design; no risk of duplicating a missing item.
- Easier to keep stable than patching private vanilla UI internals.

Cons:

- More code: a new panel, search/filter wiring, grid, scrollbar, close handling, localization, and an entry point.
- Another window on screen; needs positioning and resolution handling.
- Reusing `UIDynamicItemCollection` still calls context 29 input methods, so the same input hooks are needed unless the grid is reimplemented.
- Gamepad navigation needs extra work or can be marked as unsupported initially.

Implementation sketch:

- Add `UI/MissingItemsMenu.cs` as a `UIState`, modeled on `UI/PrefixWindow.cs` and the vanilla duplication page.
- Use the public vanilla widgets:
  - `EntryFilterer<Item, IItemEntryFilter>` with the same `ItemFilters` list.
  - `ItemFilters.BySearch` for search; pinyin works through the existing hook.
  - `EntrySorter<int, ICreativeItemSortStep>` with `SortingSteps.ByCreativeSortingId` and `SortingSteps.Alphabetical`.
  - `UICreativeItemsInfiniteFilteringOptions` for the category buttons.
  - `UISearchBar`, `UIList`, `UIScrollbar`, `UIDynamicItemCollection` for the list.
- Rebuild the list when the search/filter changes or `Main.LocalPlayerCreativeTracker.ItemSacrifices.LastEditId` changes.
- Register it in `Common/Systems/UISystem.cs` like `PrefixWindow`; add update and draw layers.
- Open it from a clickable "1164 missing" line in `DuplicationMenu`, and optionally from a new keybind in `Common/Systems/KeybindSystem.cs`.

Verdict: best long-term architecture if the feature is expected to grow. More work than Option B.

### Option D: New category in the creative powers menu

Add a "Missing items" main category next to Duplication and Research.

Pros: most discoverable; fits the existing creative menu navigation.

Cons:

- `UICreativePowersMenu.RefreshElementsOrder` and `CreateMainPowerStrip` are private and hardcode the category options.
- The main power strip is a vertical strip; adding another button affects layout and gamepad snap points.
- The category enum and `IsShowingResearchMenu` logic are private.
- High maintenance risk for a feature that is fundamentally a list.

Verdict: not recommended.

### Option E: Chat command or file export

Add a command such as `/missing` or a debug keybind that prints or writes a list of missing item names.

Pros: tiny; useful for debugging and for completionists who want a file.

Cons: not a UI; thousands of chat lines are unusable for a normal player.

Verdict: useful as a complement, especially a debug export that includes mod names and `CanResearch` state. Not a substitute for the UI.

### Comparison

| Option | Reuses grid | New UI work | Risk | Best for |
| --- | --- | --- | --- | --- |
| A. Mixed red icons | Yes | Low-medium | High: input paths, noise, spoilers | Search-in-place checks |
| B. Missing-only mode | Yes | Low | Medium: private vanilla hook | Small first version |
| C. Dedicated panel | No (reuses widgets) | Medium-high | Medium: new UI, input hooks | Long-term checklist |
| D. New menu category | Partly | Medium | High: private menu layout | Maximum discoverability |
| E. Command/export | No | Very low | Low | Debugging and exports |

## 3. Data correctness

Every UI option should use the same predicate. A suggested helper:

```csharp
public static bool IsMissing(int itemId)
{
    if (itemId <= 0 || itemId >= ItemLoader.ItemCount) return false;
    if (Researcher.GetSharedValue(itemId) != -1) return false;
    if (!ContentSamples.ItemsByType.TryGetValue(itemId, out Item? item) || item.IsAir) return false;
    if (!Researcher.IsResearchable(itemId)) return false;
    if (!ItemLoader.CanResearch(item)) return false;
    return !Researcher.IsResearched(itemId);
}
```

Important edge cases:

- Shared research aliases: `ContentSamples.CreativeResearchItemPersistentIdOverride` maps an item to the item it shares research with. `ResearchSystem.CalculateResearchable` already skips aliases with `Researcher.GetSharedValue(itemId) != -1`. The missing list must skip them too, otherwise the list and the denominator disagree.
- `ItemLoader.CanResearch`: `CreativeUI.SacrificeItem` refuses items where any `ModItem`/`GlobalItem` returns `CanResearch == false`, but `Researcher.IsResearchable` does not check it. Such items can be counted as missing forever even though they can never be researched. This is directly relevant to debug items.
- `ResearchUnlockCount`: mod items default to 1. Mods can set it to 0 to remove the item from the research catalog. `Researcher.IsResearchable` already covers that case because it checks the catalog.
- Count overrides: `ResearchSystem.OverrideDefaultResearchCount` removes catalog entries for count 0 and adds/updates entries for positive counts. The list should be built after that runs, as `OnWorldLoad` already does.
- `OnlyOneItemNeeded`: `Researcher.IsResearched` uses the vanilla required count, while `Researcher.GetTotalNeeded` returns 1 when the option is enabled. `HyperPlayer.OnEnterWorld` auto-completes items with at least one sacrificed item, but toggling the option mid-world can leave a mismatch. Use `IsResearched` as the completion truth for the list; do not mix it with `GetTotalNeeded` for the completed/incomplete decision.
- Partial research: an item with `GetResearchedCount > 0` but not fully researched is still missing. It can be shown with an "in progress" filter or a progress bar. This is more actionable than a flat missing list.
- Modded unobtainable items: there is no reliable vanilla or tModLoader flag for "unobtainable". `ItemLoader.CanResearch` catches explicit opt-outs, but mods that add debug items with the default research count cannot be detected automatically. Do not try to infer obtainability from recipes/loot: many valid items have no recipe, no loot table, and no shop entry.
- Team-shared research: the mod's team share actually calls `CreativeUI.ResearchItem` locally, so `IsResearched` reflects shared items. No server-side list is needed. Different team members can still have different missing lists if server sync settings differ.
- Progress count drift: `HyperPlayer.ItemsResearchedCount` increments on every `OnResearched`, including alias items, while `ResearchSystem.ResearchableItemsCount` excludes aliases. The displayed percentage can exceed 100% or disagree with a list-based count. The missing list is a good opportunity to make the count authoritative:
  - Compute `total` and `missing` from the same predicate.
  - Derive `researched = total - missing`.
  - Or fix `HyperPlayer.OnResearch` to ignore aliases and keep a separate accurate count. Deriving from the list is simpler.
- `ResearchSystem.CalculateResearchable` has a dead condition:

  ```csharp
  if (Researcher.IsResearchable(itemId)
      || (!CountOverride.TryGetValue(itemId, out int count) && count > 0))
  ```

  `count` is 0 when `TryGetValue` fails, so the second branch can never be true. Either remove it or replace it with the intended `CountOverride.TryGetValue(itemId, out count) && count > 0`. Do not copy this predicate into the missing list.

A simple consistency check at world load is worth adding behind a debug log: `missing.Count + researchedCount == ResearchableItemsCount`. If it fails, log the item IDs so mod conflicts and aliases can be diagnosed.

## 4. Technical notes

### Hooks and reflection points

| Purpose | Vanilla member | Approach |
| --- | --- | --- |
| Replace/append grid contents | `UICreativeInfiniteItemsDisplay.UpdateContents()` (private) | `On_` hook; the mod already hooks private UI methods |
| Read/write filtered list | `_itemIdsAvailableToShow` | Reflection, cached `FieldInfo` |
| Apply the current filters | `_filterer` (`EntryFilterer<Item, IItemEntryFilter>`) | Reflection; `FitsFilter` is public |
| Preserve sorting and mod sort steps | `_sorter` (`EntrySorter<int, ICreativeItemSortStep>`) | Reflection; `IComparer<int>` |
| Refresh the grid | `_itemGrid` (`UIDynamicItemCollection`) | Reflection; `SetContentsToShow` is public |
| Toggle refresh | `UpdateContents()` | Reflection invocation after changing the mode flag |
| Capture the display instance | `UICreativePowersMenu._infiniteItemsWindow` | Hook `InitializePage` or reflect through `Main.CreativeMenu` |
| Block duplication | `ItemSlot.LeftClick(Item[], int, int)` | `On_` hook; skip the original for missing items in the active mode |
| Block right-click duplication | `ItemSlot.RightClick(Item[], int, int)` | Same |
| Remove the pickup cursor | `ItemSlot.OverrideHover(Item[], int, int)` | Same; keep `MouseHover` for tooltips |
| Draw the missing marker | `ItemSlot.Draw` | Extend the existing IL hook delegate; context 29 is already isolated |

The mod's `HooksSystem` currently rewrites the item texture draw with `IL_ItemSlot.Draw...`. Adding the red X inside that delegate avoids a second IL hook. Load the marker texture in `UISystem` next to the other UI assets and unload it in `UISystem.Unload`.

### Input behavior

The vanilla grid calls `ItemSlot` methods directly from `UIDynamicItemCollection.DrawSelf`, not through the UI event system. This is the most important implementation detail:

- A normal `OnLeftClick` handler on a custom element will not prevent duplication in the vanilla grid.
- Blocking `ItemSlot.LeftClick` is required for the left-click full-stack clone.
- Blocking `ItemSlot.RightClick` is required for the right-click clone path.
- Blocking `ItemSlot.OverrideHover` is required to avoid the pickup cursor.
- `ItemSlot.MouseHover` should still run so the existing custom research tooltip (`BaseGlobalItem.ModifyTooltips`) shows `Research x more...`.

If a dedicated panel is built with a custom read-only grid that never calls those methods, the input hooks can be avoided, but the grid drawing code then has to be written or copied.

### Rendering

- Prefer a small red X in the corner plus a slight dim over a full red fill. Players still need to recognize the item icon.
- Do not rely on color alone. The X shape and the tooltip make the state readable for color-blind players.
- Distinguish "not started" (red X) from "in progress" (amber bar or amber X) if partial items are included.
- Add a new 16x16 or 32x32 asset under `Assets/Images/UI/`, load it through `UISystem`, and unload it.
- The existing `ItemSlot.Draw` delegate receives `position`, `texture`, and `color`; draw the marker with the same `Main.inventoryScale` to stay aligned.
- In missing-only mode, per-item markers are redundant. A red toggle/banner is enough; the items are all missing by definition.

### Search, filters, and sorting

- Reusing `_filterer` and `_sorter` preserves vanilla and mod-added behavior. This is a strong argument for Options A/B over a hand-written list.
- The pinyin search hook applies to any `ItemFilters.BySearch`, so it works automatically in both the duplication menu and a dedicated panel.
- `SortingSteps.ByCreativeSortingId` uses `ContentSamples.ItemCreativeSortingId`. Mod items normally have entries; if a mod item is missing one, the sort could throw. A defensive fallback to `Alphabetical` is worth adding in a custom panel.
- The vanilla category filter bar has 11 buttons at 40 px each: 440 px wide, inside a 480 px panel with a 20 px left offset. A 12th button would overflow the window. Do not plan on a "Missing" filter button in that bar unless the layout is changed.

### Performance

- Iterating `ItemLoader.ItemCount` (several thousand with mods) and doing dictionary lookups is cheap. Running it on every search keystroke is probably acceptable, but caching is better:
  - Cache the base missing list and invalidate on `Main.LocalPlayerCreativeTracker.ItemSacrifices.LastEditId` or a world load.
  - Apply search/category filters to the cached list on each keystroke.
- `ItemLoader.CanResearch` enumerates GlobalItem hooks for every item. Cache the result with the missing list; do not call it in a per-frame loop.
- `UIDynamicItemCollection` already virtualizes drawing and texture loading; adding a few thousand list entries does not mean drawing all of them.
- Sorting several thousand integers with the vanilla sorter is already what the vanilla menu does. Adding missing items at most doubles that work, once per list rebuild.

### Gamepad, resolution, and localization

- In missing-only mode, the grid is read-only; gamepad navigation can still hover items but pressing A does nothing. Either accept that, skip snap points, or use a dedicated panel with its own navigation.
- A dedicated panel should clamp its position and height to `Main.screenWidth`/`Main.screenHeight`, especially at 720p and high UI scales. `PrefixWindow` uses a fixed `Left = 564`, which is a useful template but not a universal layout.
- Add localization keys for the toggle, the mode label, the empty state, the panel title, the filters, and the config option. The mod ships en-US, ru-RU, and zh-Hans; adding keys only to en-US will fall back, but the other two should be updated rather than left with English text.

## 5. Recommendation

### Primary recommendation: Option B for v1

Implement a missing-only mode in the duplication menu, toggled from the progress tracker:

1. Add a shared `ResearchProgress` helper with `IsMissing`, `GetMissingItems`, and `GetMissingCount`.
2. Add a clickable second line under the existing progress text: `{0} missing - click to view`.
3. Add a static mode flag and a `UpdateContents` hook that replaces the grid source list with filtered/sorted missing items while the flag is active.
4. Block `ItemSlot.LeftClick`, `RightClick`, and `OverrideHover` for context 29 when the mode is active and the item is missing.
5. Reset the mode when the creative menu closes or the world unloads.
6. Add a config option to show/hide the entry point and localization keys.
7. Test with an empty research profile, a nearly complete profile, modded items, aliases, count overrides, and team-shared research.

This gives the player the requested list with the least new UI and keeps the normal duplication menu unchanged by default.

### When to choose Option C instead

Build the dedicated panel if any of these are requirements:

- A "Vanilla only" / "Modded" toggle, to make completion meaningful with mods that add unobtainable debug items.
- An "In progress" filter or progress bars for partially researched items.
- An "Ignore this item" action for modded items that can never be obtained.
- A mod-name column or tooltip, or a mod filter.
- A persistent checklist that can stay open while the player plays, independent of the creative menu.
- Future "where to get it" information.

Those features do not fit cleanly into the duplication menu's full filter bar, and they turn the feature from a simple list into a real checklist. Start with C if that is the intended scope; otherwise B is the smaller, safer step.

### Do not choose Option A as the default

The mixed red-icon list is technically possible and matches the original suggestion, but it degrades the duplication menu's core purpose. If it is implemented at all, make it an opt-in config, use a red X overlay over a dimmed icon, and show the research progress in the tooltip. Do not use a red fill; players need to recognize the item.

## 6. Open questions

1. Should the missing list include modded items by default, or start with a "Vanilla only" toggle and show modded completion separately?
2. Should items that `ItemLoader.CanResearch` rejects be hidden, marked as "not researchable", or shown as a diagnostic? The author's objection suggests they should at least be visible somewhere.
3. Should the feature be a mode in the duplication menu or a separate panel? The answer determines whether "Vanilla only" and "Ignore" are feasible in v1.
4. Should the list be gated to avoid spoilers (for example, a config option or a post-Moon Lord unlock)? The original forum suggestion proposed a Moon Lord gate or a consumable.
5. Should partially researched items be included in the same list, or shown under an "In progress" filter?
6. Should clicking a missing item do anything? A read-only tooltip is the safe default. Opening the research page with a ghost item is risky because the creative menu may return the slot item to the player on close.
7. Should ignored items be excluded from the progress denominator? If yes, the meaning of "100%" becomes user-defined and needs to be documented.
8. Is gamepad support required for the first version? The mod's existing `PrefixWindow` is mouse-first, so there is precedent for deferring it.

## Implementation status

Option B is implemented:

- `Common/Systems/MissingItemsSystem.cs` switches the duplication grid to a read-only missing-items list, hooks `UICreativeInfiniteItemsDisplay.UpdateContents`, and blocks left-click, right-click, pickup, and the pickup cursor for missing items.
- `Utils/ResearchProgress.cs` owns the shared predicate (`GetSharedValue`, `IsResearchable`, `ItemLoader.CanResearch`, `IsResearched`), caches the missing list, and exposes the total/researched/missing counts.
- `UI/DuplicationMenu.cs` shows a clickable missing-items line under the progress text and resets the mode on world unload, menu close, and category changes.
- `Common/Configs/HyperConfig.cs` adds `ShowMissingItemsButton` (default on).
- The missing line and config are localized in en-US, ru-RU, and zh-Hans.
- Missing items use the disabled-buff tint, including items that share an already-enabled buff type such as Well Fed.
- The buff and banner toggle tooltips and keybind paths require the hovered item to be researched, so a missing item cannot toggle a buff shared with a researched item.
- The missing-items view survives closing and reopening the creative menu; it still resets when the main category changes or the world unloads.

The total percentage now uses the same predicate as the missing list, so it excludes items whose `CanResearch` is false and items that share research through `CreativeResearchItemPersistentIdOverride`. This can change the displayed percentage slightly compared with the old count.

Not covered by Option B: a vanilla/modded split, ignoring individual items, and an "in progress" filter. Those remain Option C work.
