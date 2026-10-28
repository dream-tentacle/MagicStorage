using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_CraftingPresets : Window
    {
        private readonly Building_StorageCore core;
        private readonly Action changed;
        private List<CraftingPresets.Entry> entries = new List<CraftingPresets.Entry>();
        private string name = "";
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Math.Min(820f, UI.screenWidth), Math.Min(640f, UI.screenHeight));

        internal Dialog_CraftingPresets(Building_StorageCore core, Action changed)
        {
            this.core = core; this.changed = changed;
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true; doCloseButton = true;
            Try(Refresh);
        }

        private void Refresh() { entries = CraftingPresets.List(); }

        public override void DoWindowContents(Rect rect)
        {
            if (!core.Spawned) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 32, 32), "MS_Craft_PresetsTitle".Translate());
            Text.Font = GameFont.Small;
            string hint = "MS_Craft_PresetsHint".Translate();
            float hintHeight = Text.CalcHeight(hint, rect.width);
            Widgets.Label(new Rect(0, 40, rect.width, hintHeight), hint);
            float saveY = 40 + hintHeight + 12;
            name = Widgets.TextField(new Rect(0, saveY, rect.width - 124, 32), name, 64);
            if (Widgets.ButtonText(new Rect(rect.width - 114, saveY, 114, 32), "MS_Craft_PresetSave".Translate(),
                active: !string.IsNullOrWhiteSpace(name) && core.Crafting.Orders.Count > 0)) Save();
            float listY = saveY + 46;
            Rect viewport = new Rect(0, listY, rect.width, Math.Max(1, rect.height - listY - 104));
            Rect content = new Rect(0, 0, viewport.width - 20, Math.Max(42, entries.Count * 42));
            Widgets.BeginScrollView(viewport, ref scroll, content);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    float y = i * 42;
                    var label = new Rect(0, y + 4, Math.Max(1, content.width - 252), 32);
                    Widgets.Label(label, entry.Name.Truncate(label.width));
                    TooltipHandler.TipRegion(label, entry.Name);
                    if (Widgets.ButtonText(new Rect(content.width - 246, y, 76, 32), "MS_Craft_PresetAppend".Translate()))
                    { Try(() => Load(entry, false)); break; }
                    if (Widgets.ButtonText(new Rect(content.width - 164, y, 76, 32), "MS_Craft_PresetReplace".Translate()))
                    { Try(() => Load(entry, true)); break; }
                    if (Widgets.ButtonText(new Rect(content.width - 82, y, 76, 32), "MS_Craft_PresetDelete".Translate()))
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("MS_Craft_PresetDeleteConfirm".Translate(entry.Name),
                            () => Try(() => { File.Delete(entry.Path); Refresh(); }), destructive: true));
                        break;
                    }
                }
                if (entries.Count == 0) Widgets.Label(new Rect(0, 4, content.width, 32), "MS_Craft_PresetsEmpty".Translate());
            }
            finally { Widgets.EndScrollView(); }
            Widgets.Label(new Rect(0, rect.height - 92, rect.width, 52), "MS_Craft_PresetFolder".Translate(CraftingPresets.FolderPath));
        }

        private void Save()
        {
            Try(() =>
            {
                string trimmed = name.Trim();
                var overwrite = entries.FirstOrDefault(entry => string.Equals(entry.Name, trimmed, StringComparison.OrdinalIgnoreCase));
                var snapshot = CraftingPreset.Capture(core.Crafting);
                Action save = () => Try(() => { CraftingPresets.Save(trimmed, snapshot, overwrite); name = ""; Refresh(); });
                if (overwrite == null) save();
                else Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("MS_Craft_PresetOverwriteConfirm".Translate(trimmed), save));
            });
        }

        private void Load(CraftingPresets.Entry entry, bool replace)
        {
            List<string> missing;
            var preset = CraftingPresets.Load(entry, out missing);
            if (preset.Orders.Count == 0)
            {
                Find.WindowStack.Add(new Dialog_MessageBox("MS_Craft_PresetNoUsableOrders".Translate() +
                    (missing.Count == 0 ? "" : "\n\n" + string.Join("\n", missing))));
                return;
            }
            Action apply = () => Try(() =>
            {
                if (!core.Spawned) { Close(); return; }
                if (!core.Crafting.ApplyPreset(preset, replace))
                {
                    Find.WindowStack.Add(new Dialog_MessageBox("MS_Craft_PresetReturnFailed".Translate()));
                    return;
                }
                changed(); Close();
            });
            string confirmation = replace ? "MS_Craft_PresetReplaceConfirm".Translate(entry.Name).ToString() : "";
            if (missing.Count > 0)
                confirmation += (confirmation.Length == 0 ? "" : "\n\n") + "MS_Craft_PresetMissing".Translate() + "\n" + string.Join("\n", missing);
            if (confirmation.Length == 0) apply();
            else Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(confirmation, apply, destructive: replace));
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception exception)
            { Find.WindowStack.Add(new Dialog_MessageBox("MS_Craft_PresetError".Translate(exception.Message))); }
        }
    }
}
