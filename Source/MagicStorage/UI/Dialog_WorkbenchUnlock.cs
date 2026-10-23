using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace MagicStorage
{
    internal sealed class Dialog_WorkbenchUnlock : Window
    {
        private readonly Building_StorageCore core;
        private readonly ThingDef table;
        private readonly Action unlocked;
        private readonly List<ThingDef> materials;
        private ThingDef stuff;
        private Vector2 scroll;
        private bool failed;

        public override Vector2 InitialSize => new Vector2(Math.Min(560f, UI.screenWidth), Math.Min(540f, UI.screenHeight));

        internal Dialog_WorkbenchUnlock(Building_StorageCore core, ThingDef table, Action unlocked)
        {
            this.core = core; this.table = table; this.unlocked = unlocked;
            materials = GenStuff.AllowedStuffsFor(table).OrderBy(def => def.label, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (materials.Count > 0)
            {
                stuff = GenStuff.DefaultStuffFor(table);
                if (!materials.Contains(stuff)) stuff = materials[0];
            }
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned || !MagicStorageMod.Settings.WorkbenchUnlockMode) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30f, 36f), "MS_Craft_UnlockWorkbench".Translate());
            Text.Font = GameFont.Small;
            string question = "MS_Craft_UnlockWorkbenchQuestion".Translate(table.LabelCap);
            float questionHeight = Text.CalcHeight(question, rect.width);
            Widgets.Label(new Rect(0, 46f, rect.width, questionHeight), question);
            float y = 46f + questionHeight + 12f;
            if (table.MadeFromStuff)
            {
                string materialLabel = stuff == null ? "MS_Craft_NoLegalStuff".Translate().ToString() : stuff.LabelCap.ToString();
                if (Widgets.ButtonText(new Rect(0, y, rect.width, 32f), "MS_Craft_UnlockStuff".Translate(materialLabel)) && materials.Count > 0)
                {
                    var options = materials.Select(material => new FloatMenuOption(material.LabelCap, () =>
                    { stuff = material; failed = false; scroll = Vector2.zero; })).ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                y += 42f;
            }

            bool valid = CraftingWorkbenchUnlocks.TryGetCosts(table, stuff, out var costs);
            var counts = new Dictionary<ThingDef, long>();
            bool enough = valid && CraftingWorkbenchUnlocks.TryPlan(core, costs, out counts, out _);

            Widgets.Label(new Rect(0, y, rect.width, 28f), "MS_Craft_UnlockMaterials".Translate());
            y += 30f;
            var outer = new Rect(0, y, rect.width, Math.Max(0f, rect.height - y - 108f));
            Widgets.DrawMenuSection(outer);
            float width = outer.width - 24f;
            var heights = valid ? costs.Select(cost => Math.Max(36f, Text.CalcHeight(cost.thingDef.LabelCap, Math.Max(1f, width - 178f)) + 8f)).ToList()
                : new List<float>();
            var view = new Rect(0, 0, width, heights.Sum());
            Widgets.BeginScrollView(outer.ContractedBy(4f), ref scroll, view);
            float rowY = 0;
            if (valid)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    var cost = costs[i];
                    counts.TryGetValue(cost.thingDef, out long available);
                    Widgets.DefIcon(new Rect(4f, rowY + 4f, 28f, 28f), cost.thingDef);
                    Widgets.InfoCardButton(36f, rowY + (heights[i] - 24f) / 2f, cost.thingDef);
                    Widgets.Label(new Rect(68f, rowY + 4f, Math.Max(1f, width - 178f), heights[i] - 8f), cost.thingDef.LabelCap);
                    Color previous = GUI.color;
                    if (available < cost.count) GUI.color = Color.red;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(width - 104f, rowY, 100f, heights[i]), available + " / " + cost.count);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = previous;
                    rowY += heights[i];
                }
            }
            Widgets.EndScrollView();

            string status = failed ? "MS_Craft_UnlockFailed" : !valid ? "MS_Craft_NoLegalStuff" :
                enough ? "MS_Craft_UnlockSharedHint" : "MS_Craft_UnlockMissingMaterials";
            Widgets.Label(new Rect(0, rect.height - 98f, rect.width, 50f), status.Translate());
            float buttonWidth = (rect.width - 12f) / 2f;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enough;
            bool confirm = Widgets.ButtonText(new Rect(0, rect.height - 36f, buttonWidth, 36f), "Confirm".Translate());
            GUI.enabled = previousEnabled;
            if (confirm)
            {
                if (CraftingWorkbenchUnlocks.TryUnlock(core, table, stuff))
                { Close(); unlocked?.Invoke(); return; }
                failed = true;
            }
            if (Widgets.ButtonText(new Rect(buttonWidth + 12f, rect.height - 36f, buttonWidth, 36f), "Cancel".Translate())) Close();
        }
    }
}
