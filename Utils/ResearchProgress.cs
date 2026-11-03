using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;

namespace HyperResearch.Utils;

/// <summary>
///     Client-side view of the player's research progress. The total and missing counts and
///     the missing item list all use the same predicate, so they cannot disagree.
/// </summary>
public static class ResearchProgress
{
    private static readonly List<int> EmptyItems = [];
    private static List<int>? _candidates;
    private static int _candidateItemCount = -1;
    private static List<int> _missingItems = [];
    private static HashSet<int> _missingItemSet = [];
    private static CreativeUnlocksTracker? _tracker;
    private static int _lastEditId = -1;
    private static int _total;
    private static bool _hasCache;

    /// <summary>The number of researchable items that are not fully researched.</summary>
    public static int MissingCount => EnsureCache().Missing.Count;

    /// <summary>The number of researchable items, excluding shared-research aliases.</summary>
    public static int Total => EnsureCache().Total;

    /// <summary>The number of researchable items that are fully researched.</summary>
    public static int ResearchedCount => Math.Max(0, Total - MissingCount);

    /// <summary>The items that are researchable but not fully researched.</summary>
    public static IReadOnlyList<int> MissingItems => EnsureCache().Missing;

    /// <summary>Increments whenever the cached progress is rebuilt or invalidated.</summary>
    public static int Version { get; private set; }

    public static bool IsMissing(int itemId)
    {
        if (itemId <= 0) return false;

        EnsureCache();
        return _hasCache && _missingItemSet.Contains(itemId);
    }

    /// <summary>
    ///     Drops the missing list after a research edit. The candidate list
    ///     (which items can be researched at all) is kept.
    /// </summary>
    public static void InvalidateProgress()
    {
        _hasCache = false;
        _tracker = null;
        _lastEditId = -1;
        _missingItems = [];
        _missingItemSet = [];
        _total = 0;
        Version++;
    }

    /// <summary>
    ///     Drops every cache. Call this after the world or the research catalog changes.
    /// </summary>
    public static void Invalidate()
    {
        InvalidateProgress();
        _candidates = null;
        _candidateItemCount = -1;
    }

    private static (List<int> Missing, int Total) EnsureCache()
    {
        if (Main.gameMenu || Main.LocalPlayerCreativeTracker is null)
            return (EmptyItems, 0);

        CreativeUnlocksTracker tracker = Main.LocalPlayerCreativeTracker;
        int lastEditId = tracker.ItemSacrifices.LastEditId;
        if (_hasCache && ReferenceEquals(_tracker, tracker) && _lastEditId == lastEditId)
            return (_missingItems, _total);

        List<int> candidates = GetCandidates();
        List<int> missing = new(candidates.Count);
        foreach (int itemId in candidates)
        {
            if (!Researcher.IsResearched(itemId))
                missing.Add(itemId);
        }

        _tracker = tracker;
        _lastEditId = lastEditId;
        _missingItems = missing;
        _missingItemSet = [.. missing];
        _total = candidates.Count;
        _hasCache = true;
        Version++;
        return (_missingItems, _total);
    }

    private static List<int> GetCandidates()
    {
        if (_candidates is not null && _candidateItemCount == ItemLoader.ItemCount)
            return _candidates;

        List<int> candidates = [];
        for (var itemId = 1; itemId < ItemLoader.ItemCount; itemId++)
        {
            if (IsResearchableForProgress(itemId))
                candidates.Add(itemId);
        }

        _candidates = candidates;
        _candidateItemCount = ItemLoader.ItemCount;
        return candidates;
    }

    private static bool IsResearchableForProgress(int itemId)
    {
        if (itemId <= 0 || itemId >= ItemLoader.ItemCount) return false;
        if (Researcher.GetSharedValue(itemId) != -1) return false;
        if (!ContentSamples.ItemsByType.TryGetValue(itemId, out Item? item) || item.IsAir) return false;
        if (!Researcher.IsResearchable(itemId)) return false;

        try
        {
            return ItemLoader.CanResearch(item);
        }
        catch
        {
            // A broken mod hook should not break the whole progress list.
            return false;
        }
    }
}
