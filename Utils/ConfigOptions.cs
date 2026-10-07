using System.Collections.Generic;
using HyperResearch.Common.Configs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace HyperResearch.Utils;

public static class ConfigOptions
{
    private static bool UseServerSettings =>
        Main.netMode == NetmodeID.MultiplayerClient && ServerConfig.Instance.UseServerSettings;

    public static bool IgnoreLocationConditions => UseServerSettings
        ? ServerConfig.Instance.IgnoreLocationConditions
        : HyperConfig.Instance.IgnoreLocationConditions;

    public static bool IgnoreTimeConditions => UseServerSettings
        ? ServerConfig.Instance.IgnoreTimeConditions
        : HyperConfig.Instance.IgnoreTimeConditions;

    public static bool IgnoreEventConditions => UseServerSettings
        ? ServerConfig.Instance.IgnoreEventConditions
        : HyperConfig.Instance.IgnoreEventConditions;

    public static bool IgnoreMoonPhaseConditions => UseServerSettings
        ? ServerConfig.Instance.IgnoreMoonPhaseConditions
        : HyperConfig.Instance.IgnoreMoonPhaseConditions;

    public static bool BalanceShimmerAutoresearch => UseServerSettings
        ? ServerConfig.Instance.BalanceShimmerAutoresearch
        : HyperConfig.Instance.BalanceShimmerAutoresearch;

    public static bool BalancePrefixPicker => UseServerSettings
        ? ServerConfig.Instance.BalancePrefixPicker
        : HyperConfig.Instance.BalancePrefixPicker;

    public static bool UseResearchedBannersBuff => UseServerSettings
        ? ServerConfig.Instance.UseResearchedBannersBuff
        : HyperConfig.Instance.UseResearchedBannersBuff;

    public static bool UseResearchedPotionsBuff => UseServerSettings
        ? ServerConfig.Instance.UseResearchedPotionsBuff
        : HyperConfig.Instance.UseResearchedPotionsBuff;

    public static bool OnlyOneItemNeeded => UseServerSettings
        ? ServerConfig.Instance.OnlyOneItemNeeded
        : HyperConfig.Instance.OnlyOneItemNeeded;

    public static Dictionary<ItemDefinition, uint> ItemResearchCountOverride => UseServerSettings
        ? ServerConfig.Instance.ItemResearchCountOverride
        : HyperConfig.Instance.ItemResearchCountOverride;
}