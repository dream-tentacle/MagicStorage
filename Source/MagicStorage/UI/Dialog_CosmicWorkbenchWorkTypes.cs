using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class Dialog_CosmicWorkbenchWorkTypes : Window
    {
        private readonly Building_CosmicWorkbench bench;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Math.Min(440f, UI.screenWidth), Math.Min(520f, UI.screenHeight));

        internal Dialog_CosmicWorkbenchWorkTypes(Building_CosmicWorkbench bench)
        {
            this.bench = bench;
            forcePause = true; absorbInputAroundWindow = true;
            doCloseX = true; doCloseButton = true; closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!bench.Spawned || bench.Faction != Faction.OfPlayer) { Close(); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width - 30f, 36f), "MS_Craft_AllowedWorkTypes".Translate());
            Text.Font = GameFont.Small;
            string hint = "MS_Craft_AllowedWorkTypesHint".Translate();
            float hintHeight = Text.CalcHeight(hint, rect.width);
            Widgets.Label(new Rect(0f, 44f, rect.width, hintHeight), hint);
            float top = 44f + hintHeight + 12f;
            var roomRoleRect = new Rect(0f, top, rect.width, 32f);
            if (Widgets.ButtonText(roomRoleRect, "MS_Craft_PreferredRoomRole".Translate(bench.PreferredRoomRole.LabelCap) + " ▾"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var role in CosmicWorkbenchRoomRoles.Choices)
                {
                    RoomRoleDef selected = role;
                    options.Add(new FloatMenuOption(selected.LabelCap, () => bench.SetPreferredRoomRole(selected)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(roomRoleRect, "MS_Craft_PreferredRoomRoleHint".Translate());
            top += 44f;
            var outer = new Rect(0f, top, rect.width, Math.Max(0f, rect.height - top - 60f));
            float width = outer.width - 20f;
            float height = 0f;
            foreach (var type in CraftingRecipeCatalog.WorkTypes)
                height += Math.Max(34f, Text.CalcHeight(type.labelShort.CapitalizeFirst(), width - 36f) + 8f);
            Widgets.BeginScrollView(outer, ref scroll, new Rect(0f, 0f, width, height));
            float y = 0f;
            foreach (var type in CraftingRecipeCatalog.WorkTypes)
            {
                string label = type.labelShort.CapitalizeFirst();
                float rowHeight = Math.Max(34f, Text.CalcHeight(label, width - 36f) + 8f);
                bool allowed = bench.AllowsWorkType(type);
                bool before = allowed;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, rowHeight), label, ref allowed, paintable: true);
                if (allowed != before) bench.SetWorkTypeAllowed(type, allowed);
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }
    }
}
