using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_CosmicPrinter : Window
    {
        private readonly Building_CosmicPrinter printer;
        private readonly CosmicPrinterSettings draft;
        private readonly string[] buffers = new string[4];
        private static readonly Color[] QualityColors = { new Color(.8f,.25f,.25f), new Color(.7f,.55f,.3f),
            Color.gray, new Color(.4f,.7f,.8f), new Color(.4f,.8f,.5f), new Color(.7f,.5f,.85f), new Color(1f,.85f,.2f) };
        public override Vector2 InitialSize => new Vector2(790, 540);

        public Dialog_CosmicPrinter(Building_CosmicPrinter printer)
        {
            this.printer = printer;
            draft = printer.Settings.Copy();
            doCloseX = true; doCloseButton = false; absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!printer.Spawned) { Close(); return; }
            CosmicPrinterSettings settings = draft;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Printer_Title".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 48), "MS_Printer_Hint".Translate());
            float left = 320, rightX = left + 20, rightWidth = rect.width - rightX;
            Widgets.DrawMenuSection(new Rect(0, 98, left, 280));
            Percentage(new Rect(12, 112, left - 24, 30), "MS_Printer_Speed", ref settings.WorkSpeed, 100000, 0);
            Percentage(new Rect(12, 154, left - 24, 30), "MS_Printer_Efficiency", ref settings.Efficiency, 1000, 1);
            Integer(new Rect(12, 196, left - 24, 30), "MS_Printer_Skill", ref settings.SkillLevel, 0, 20, 2);
            Integer(new Rect(12, 238, left - 24, 30), "MS_Printer_Offset", ref settings.QualityOffset, -6, 6, 3);
            if (Widgets.ButtonText(new Rect(12, 292, left - 24, 32), "MS_Printer_Reset".Translate()))
            { settings.Reset(); Array.Clear(buffers, 0, buffers.Length); }
            Widgets.Label(new Rect(12, 334, left - 24, 36), "MS_Printer_ZeroPauses".Translate());

            Widgets.DrawMenuSection(new Rect(rightX, 98, rightWidth, 120));
            Rate(new Rect(rightX + 12, 108, rightWidth - 24, 44), "MS_Printer_Inspiration", ref settings.InspirationChance);
            Rate(new Rect(rightX + 12, 160, rightWidth - 24, 44), "MS_Printer_Failure", ref settings.FailureChance);
            settings.Normalize();

            Widgets.DrawMenuSection(new Rect(rightX, 230, rightWidth, 192));
            Widgets.Label(new Rect(rightX + 12, 238, rightWidth - 24, 26), "MS_Printer_Preview".Translate());
            double[] chances = settings.QualityProbabilities();
            for (int i = 0; i < chances.Length; i++)
            {
                float y = 270 + i * 20;
                GUI.color = QualityColors[i];
                Widgets.Label(new Rect(rightX + 12, y, 80, 22), ((QualityCategory)i).GetLabel().CapitalizeFirst());
                GUI.color = Color.white;
                Rect bar = new Rect(rightX + 96, y + 5, rightWidth - 166, 9);
                Widgets.DrawBoxSolid(bar, new Color(.17f,.19f,.21f));
                Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * (float)chances[i], bar.height), QualityColors[i]);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rightX + rightWidth - 66, y, 54, 20), chances[i].ToString("P1"));
                Text.Anchor = TextAnchor.UpperLeft;
            }

            bool allowChanges = MagicStorageMod.Settings.AllowPrinterParameterChanges;
            if (!allowChanges)
            {
                Color previousColor = GUI.color;
                TextAnchor previousAnchor = Text.Anchor;
                GUI.color = Color.red;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0, rect.height - 80, rect.width, 40), "MS_Printer_EditingLocked".Translate());
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
            bool previousEnabled = GUI.enabled;
            bool confirm;
            GUI.enabled = previousEnabled && allowChanges;
            try
            {
                confirm = Widgets.ButtonText(new Rect(rect.width / 2 - 170, rect.height - 34, 160, 32),
                    "MS_Printer_Confirm".Translate(), active: allowChanges);
            }
            finally { GUI.enabled = previousEnabled; }
            if (confirm && printer.Settings.TryApply(draft, MagicStorageMod.Settings.AllowPrinterParameterChanges)) Close();
            if (Widgets.ButtonText(new Rect(rect.width / 2 + 10, rect.height - 34, 160, 32), "MS_Printer_Close".Translate())) Close();
        }

        private void Percentage(Rect row, string key, ref float factor, float maxFactor, int index)
        {
            float percent = factor * 100f;
            float before = percent;
            Widgets.Label(new Rect(row.x, row.y + 3, row.width - 110, row.height), key.Translate());
            Widgets.TextFieldNumeric(new Rect(row.xMax - 100, row.y, 100, row.height), ref percent, ref buffers[index], 0, maxFactor * 100f);
            if (percent != before) factor = percent / 100f;
            TooltipHandler.TipRegion(row, (key + "Tip").Translate());
        }
        private void Integer(Rect row, string key, ref int value, int min, int max, int index)
        {
            Widgets.Label(new Rect(row.x, row.y + 3, row.width - 110, row.height), key.Translate());
            Widgets.TextFieldNumeric(new Rect(row.xMax - 100, row.y, 100, row.height), ref value, ref buffers[index], min, max);
            TooltipHandler.TipRegion(row, (key + "Tip").Translate());
        }
        private static void Rate(Rect row, string key, ref float value)
        {
            Widgets.Label(new Rect(row.x, row.y, row.width, 24), key.Translate() + ": " + value.ToString("P0"));
            value = Widgets.HorizontalSlider(new Rect(row.x, row.y + 26, row.width, 16), value, 0, 1, roundTo: .01f);
            TooltipHandler.TipRegion(row, (key + "Tip").Translate());
        }
    }
}
