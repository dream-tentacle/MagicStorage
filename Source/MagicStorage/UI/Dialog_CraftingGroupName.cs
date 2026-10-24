using System;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_CraftingGroupName : Window
    {
        private readonly Action<string> confirm;
        private string name;
        private bool focused;
        public override Vector2 InitialSize => new Vector2(380f, 190f);

        internal Dialog_CraftingGroupName(string currentName, Action<string> confirm)
        {
            name = currentName ?? ""; this.confirm = confirm;
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true;
            closeOnAccept = false; closeOnCancel = true; closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 28f, 32f), "MS_Craft_RenameGroup".Translate());
            Text.Font = GameFont.Small;
            GUI.SetNextControlName("CraftingGroupName");
            name = Widgets.TextField(new Rect(0, 44f, rect.width, 32f), name, 64);
            if (!focused) { UI.FocusControl("CraftingGroupName", this); focused = true; }
            float width = (rect.width - 12f) / 2f;
            if (Widgets.ButtonText(new Rect(0, rect.height - 35f, width, 35f), "MS_Craft_GroupConfirm".Translate(),
                active: !string.IsNullOrWhiteSpace(name)))
            { confirm(name.Trim()); Close(); }
            if (Widgets.ButtonText(new Rect(width + 12f, rect.height - 35f, width, 35f), "MS_Craft_GroupCancel".Translate())) Close();
        }
    }
}
