using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_StorageShelfPresets : Window
    {
        private readonly Building_StorageSupplyShelf shelf;
        private List<StorageShelfPresets.Entry> entries = new List<StorageShelfPresets.Entry>();
        private string name = "";
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(760f, 620f);

        internal Dialog_StorageShelfPresets(Building_StorageSupplyShelf shelf)
        {
            this.shelf = shelf;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            doCloseButton = true;
            Try(Refresh);
        }

        private void Refresh() { entries = StorageShelfPresets.List(); }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 32f, 32f), "MS_Shelf_LocalPresets".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40f, rect.width, 44f), "MS_Shelf_LocalPresetsHint".Translate());
            name = Widgets.TextField(new Rect(0, 94f, rect.width - 124f, 32f), name, 64);
            if (Widgets.ButtonText(new Rect(rect.width - 114f, 94f, 114f, 32f), "MS_Shelf_SavePreset".Translate(),
                active: !string.IsNullOrWhiteSpace(name)))
                Save();
            Rect viewport = new Rect(0, 142f, rect.width, rect.height - 246f);
            Rect content = new Rect(0, 0, viewport.width - 20f, Math.Max(42f, entries.Count * 42f));
            Widgets.BeginScrollView(viewport, ref scroll, content);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    float y = i * 42f;
                    Widgets.Label(new Rect(0, y + 4f, content.width - 200f, 32f), entry.Name.Truncate(content.width - 200f));
                    if (Widgets.ButtonText(new Rect(content.width - 190f, y, 90f, 32f), "MS_Shelf_LoadPreset".Translate()))
                    { Try(() => Load(entry)); break; }
                    if (Widgets.ButtonText(new Rect(content.width - 94f, y, 90f, 32f), "MS_Shelf_DeletePreset".Translate()))
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("MS_Shelf_DeletePresetConfirm".Translate(entry.Name),
                            () => Try(() => { File.Delete(entry.Path); Refresh(); }), destructive: true));
                        break;
                    }
                }
                if (entries.Count == 0)
                    Widgets.Label(new Rect(0, 4f, content.width, 32f), "MS_Shelf_NoLocalPresets".Translate());
            }
            finally { Widgets.EndScrollView(); }
            Widgets.Label(new Rect(0, rect.height - 92f, rect.width, 44f), "MS_Shelf_PresetFolder".Translate(StorageShelfPresets.FolderPath));
        }

        private void Save()
        {
            string trimmed = name.Trim();
            var overwrite = entries.FirstOrDefault(entry => string.Equals(entry.Name, trimmed, StringComparison.OrdinalIgnoreCase));
            // Capture now, before opening an overwrite confirmation.
            var snapshot = new Building_StorageSupplyShelf.ShelfPreset(shelf);
            Action save = () => Try(() => { StorageShelfPresets.Save(trimmed, snapshot, overwrite); name = ""; Refresh(); });
            if (overwrite == null) save();
            else Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("MS_Shelf_OverwritePresetConfirm".Translate(trimmed), save));
        }

        private void Load(StorageShelfPresets.Entry entry)
        {
            List<string> missing;
            var preset = StorageShelfPresets.Load(entry, out missing);
            if (!shelf.ApplyPreset(preset)) return;
            Close();
            if (missing.Count == 0) return;
            Find.WindowStack.Add(new Dialog_MessageBox("MS_Shelf_MissingPresetItems".Translate(),
                buttonAText: "MS_Shelf_PresetConfirm".Translate(),
                buttonBText: "MS_Shelf_ViewMissingPresetItems".Translate(),
                buttonBAction: () => Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", missing),
                    buttonAText: "MS_Shelf_PresetConfirm".Translate(), title: "MS_Shelf_MissingPresetItemsTitle".Translate()))));
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                Find.WindowStack.Add(new Dialog_MessageBox("MS_Shelf_PresetFileError".Translate(exception.Message)));
            }
        }
    }
}
