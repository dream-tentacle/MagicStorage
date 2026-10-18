using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_StorageInventoryRule : Window
    {
        private readonly StorageInventoryRule draft;
        private readonly StorageInventoryRule existingRule;
        private readonly ThingFilter parent = ThingFilter.CreateOnlyEverStorableThingFilter();
        private readonly ThingFilterUI.UIState state = new ThingFilterUI.UIState();
        private readonly Map map;
        private readonly Action<StorageInventoryRule> confirmed;
        private readonly string defaultName;
        public override Vector2 InitialSize => new Vector2(520f, Math.Min(760f, UI.screenHeight));

        internal Dialog_StorageInventoryRule(Map map, int number, Action<StorageInventoryRule> confirmed,
            StorageInventoryRule existingRule = null)
        {
            this.map = map;
            this.confirmed = confirmed;
            this.existingRule = existingRule;
            draft = existingRule?.Clone() ?? new StorageInventoryRule();
            defaultName = existingRule?.Name ?? "MS_UI_FilterDefaultName".Translate(number).ToString();
            if (existingRule == null)
            {
                draft.Name = defaultName;
                draft.Filter.SetDisallowAll();
            }
            draft.Filter.AllowedHitPointsPercents = new FloatRange(0f, 1f);
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width - 30f, 32f),
                (existingRule == null ? "MS_UI_FilterCreate" : "MS_UI_FilterEdit").Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, 70f, 28f), "MS_UI_FilterName".Translate());
            draft.Name = Widgets.TextField(new Rect(76f, 40f, rect.width - 76f, 28f), draft.Name);
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(0f, 80f, rect.width, rect.height - 190f),
                state, draft.Filter, parent, forceHideHitPointsConfig: true, map: map);
            ThingDef icon = draft.ResolveIcon();
            Rect iconRect = new Rect(0f, rect.height - 94f, 36f, 36f);
            if (icon != null) Widgets.DefIcon(iconRect, icon);
            else GUI.DrawTexture(iconRect.ContractedBy(4f), TexButton.CategorizedResourceReadout);
            if (Widgets.ButtonText(new Rect(44f, iconRect.y, 160f, 36f), "MS_UI_FilterChooseIcon".Translate()))
                Find.WindowStack.Add(new Dialog_StorageInventoryIcon(draft.IconDef, selected => draft.IconDef = selected));
            string iconLabel = draft.IconDef == null ? "MS_UI_FilterIconAutomatic".Translate().ToString() : draft.IconDef.LabelCap.ToString();
            Rect iconLabelRect = new Rect(216f, iconRect.y + 5f, rect.width - 216f, 28f);
            Widgets.Label(iconLabelRect, iconLabel.Truncate(iconLabelRect.width));
            TooltipHandler.TipRegion(iconLabelRect, iconLabel + (icon == null ? "" : "\n" + icon.LabelCap.ToString()));
            float buttonWidth = (rect.width - 16f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, rect.height - 40f, buttonWidth, 40f), "OK".Translate()))
            {
                draft.Name = draft.Name.NullOrEmpty() || draft.Name.Trim().Length == 0 ? defaultName : draft.Name.Trim();
                draft.Filter.AllowedHitPointsPercents = new FloatRange(0f, 1f);
                if (existingRule != null) existingRule.CopyFrom(draft);
                confirmed(existingRule ?? draft);
                Close();
            }
            if (Widgets.ButtonText(new Rect(buttonWidth + 16f, rect.height - 40f, buttonWidth, 40f), "CancelButton".Translate()))
                Close();
        }
    }
}
