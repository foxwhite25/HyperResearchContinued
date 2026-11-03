using System;
using System.Linq;
using System.Reflection;
using HyperResearch.Common.Configs;
using HyperResearch.Common.Systems;
using HyperResearch.Utils;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace HyperResearch.UI;

internal class DuplicationMenu : UIState
{
    private static readonly Color MissingColor = new(255, 102, 109);

    private int _creativePowerSelected;
    private int _lastTotal = -1;
    private int _lastMissingCount = -1;
    private bool _lastMissingMode;
    private LocalizedText _totalResearchedText = null!;
    private LocalizedText _missingItemsText = null!;
    private LocalizedText _showingMissingItemsText = null!;
    private LocalizedText _allResearchedText = null!;
    private LocalizedText _missingItemsHoverText = null!;
    private LocalizedText _showingMissingItemsHoverText = null!;
    private UIText _uiTotalResearchedText = null!;
    private UIText _uiMissingItemsText = null!;

    public override void OnInitialize()
    {
        HooksSystem.WorldLoaded += OnWorldLoad;
        HooksSystem.WorldUnloaded += OnWorldUnload;
        On_UICreativePowersMenu.MainCategoryButtonClick += OnCreativePowerMenuCategoryChanged;

        _totalResearchedText = Language.GetText("Mods.HyperResearch.UI.DuplicationMenu.TotalResearched");
        _missingItemsText = Language.GetText("Mods.HyperResearch.UI.DuplicationMenu.MissingItems");
        _showingMissingItemsText = Language.GetText("Mods.HyperResearch.UI.DuplicationMenu.ShowingMissingItems");
        _allResearchedText = Language.GetText("Mods.HyperResearch.UI.DuplicationMenu.AllResearched");
        _missingItemsHoverText = Language.GetText("Mods.HyperResearch.UI.DuplicationMenu.MissingItemsHover");
        _showingMissingItemsHoverText =
            Language.GetText("Mods.HyperResearch.UI.DuplicationMenu.ShowingMissingItemsHover");

        _uiTotalResearchedText = new UIText("")
        {
            Left = StyleDimension.FromPixels(88f),
            Top = StyleDimension.FromPixelsAndPercent(-60f, 1f),
            TextColor = Colors.JourneyMode
        };
        _uiMissingItemsText = new UIText("")
        {
            Left = StyleDimension.FromPixels(88f),
            Top = StyleDimension.FromPixelsAndPercent(-36f, 1f),
            TextColor = MissingColor
        };
        _uiMissingItemsText.OnLeftClick += (_, _) =>
        {
            if (ResearchProgress.MissingCount == 0) return;

            MissingItemsSystem.Toggle();
            _lastMissingCount = -1;
        };
        _uiMissingItemsText.OnUpdate += UpdateMissingItemsHover;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Researcher.IsPlayerInJourneyMode) return;

        bool showTotal = HyperConfig.Instance.ShowTotalResearchedItemsCount;
        bool showMissing = HyperConfig.Instance.ShowMissingItemsButton;
        bool show = Main.CreativeMenu.Enabled && _creativePowerSelected == 1;

        if (!showTotal && !showMissing)
        {
            if (Children.Any()) RemoveAllChildren();
            if (MissingItemsSystem.Active) MissingItemsSystem.Deactivate();
            return;
        }

        if (!show)
        {
            if (Children.Any()) RemoveAllChildren();

            // Keep the missing-items mode when the creative menu is closed so it can
            // reopen on the same view. Changing the main category still resets it.
            if (MissingItemsSystem.Active
                && (!showMissing || Main.CreativeMenu.Enabled))
                MissingItemsSystem.Deactivate();

            return;
        }

        if (!showMissing && MissingItemsSystem.Active)
            MissingItemsSystem.Deactivate();

        EnsureChildren(showTotal, showMissing);

        int total = ResearchProgress.Total;
        int missing = ResearchProgress.MissingCount;
        if (missing == 0 && MissingItemsSystem.Active)
            MissingItemsSystem.Deactivate();

        bool active = MissingItemsSystem.Active;
        if (_lastTotal == total && _lastMissingCount == missing && _lastMissingMode == active) return;

        _lastTotal = total;
        _lastMissingCount = missing;
        _lastMissingMode = active;
        UpdateTexts(total, missing, active);
    }

    private void EnsureChildren(bool showTotal, bool showMissing)
    {
        bool hasTotal = Children.Contains(_uiTotalResearchedText);
        bool hasMissing = Children.Contains(_uiMissingItemsText);
        if (hasTotal == showTotal && hasMissing == showMissing) return;

        RemoveAllChildren();

        if (showTotal)
            Append(_uiTotalResearchedText);

        if (showMissing)
        {
            _uiMissingItemsText.Top = StyleDimension.FromPixelsAndPercent(showTotal ? -36f : -60f, 1f);
            Append(_uiMissingItemsText);
        }
    }

    private void UpdateTexts(int total, int missing, bool active)
    {
        int researched = Math.Max(0, total - missing);
        float percent = total > 0 ? (float)researched / total * 100f : 0f;

        _uiTotalResearchedText.TextColor = total > 0 && missing == 0 ? Colors.CoinGold : Colors.JourneyMode;
        _uiTotalResearchedText.SetText(_totalResearchedText.Format($"{percent:0.00}", researched, total));

        if (missing == 0)
        {
            _uiMissingItemsText.SetText(_allResearchedText);
            _uiMissingItemsText.TextColor = Colors.CoinGold;
            return;
        }

        if (active)
        {
            _uiMissingItemsText.SetText(_showingMissingItemsText.Format(missing));
            _uiMissingItemsText.TextColor = Colors.JourneyMode;
        }
        else
        {
            _uiMissingItemsText.SetText(_missingItemsText.Format(missing));
            _uiMissingItemsText.TextColor = MissingColor;
        }
    }

    private void UpdateMissingItemsHover(UIElement affectedElement)
    {
        if (!affectedElement.IsMouseHovering || ResearchProgress.MissingCount == 0) return;

        Main.LocalPlayer.mouseInterface = true;
        Main.instance.MouseText(
            MissingItemsSystem.Active
                ? _showingMissingItemsHoverText.Value
                : _missingItemsHoverText.Value
        );
    }

    private void OnWorldLoad()
    {
        _creativePowerSelected = 0;
        _lastTotal = -1;
        _lastMissingCount = -1;
        _lastMissingMode = false;
        MissingItemsSystem.Reset();
        ResearchProgress.Invalidate();
    }

    private void OnWorldUnload()
    {
        MissingItemsSystem.Reset();
        ResearchProgress.Invalidate();
        if (Children.Any()) RemoveAllChildren();
    }

    private void OnCreativePowerMenuCategoryChanged(On_UICreativePowersMenu.orig_MainCategoryButtonClick orig,
        UICreativePowersMenu menu, UIMouseEvent evt, UIElement el)
    {
        orig.Invoke(menu, evt, el);
        FieldInfo? mainCategoryField =
            menu.GetType().GetField("_mainCategory", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mainCategoryField is null) return;

        object? menuTree = mainCategoryField.GetValue(menu);
        FieldInfo? currentOptionField = menuTree?.GetType().GetField("CurrentOption");
        if (currentOptionField is null) return;

        _creativePowerSelected = (int)(currentOptionField.GetValue(menuTree) ?? 0);
        if (_creativePowerSelected != 1 && MissingItemsSystem.Active)
            MissingItemsSystem.Deactivate();
    }
}
