using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Dialog_CosmicPrinter : Window
    {
        private readonly Building_CosmicPrinter printer;
        private readonly CosmicPrinterSettings draft;
        private Pawn draftBoundPawn;
        private readonly List<SkillDef> previewSkills;
        private SkillDef previewSkill;
        private readonly string[] buffers = new string[4];
        private const float ProbabilityTransitionDuration = .25f;
        private double[] displayedChances;
        private double[] transitionStartChances;
        private double[] targetChances;
        private float probabilityTransitionStartTime;
        private static readonly Color[] QualityColors =
        {
            new Color(168f / 255f, 50f / 255f, 50f / 255f),   // Awful: #A83232
            new Color(242f / 255f, 154f / 255f, 56f / 255f),  // Poor: #F29A38
            new Color(170f / 255f, 170f / 255f, 170f / 255f), // Normal: #AAAAAA
            new Color(85f / 255f, 199f / 255f, 106f / 255f),  // Good: #55C76A
            new Color(77f / 255f, 159f / 255f, 1f),           // Excellent: #4D9FFF
            new Color(180f / 255f, 119f / 255f, 232f / 255f), // Masterwork: #B477E8
            new Color(242f / 255f, 204f / 255f, 77f / 255f)   // Legendary: #F2CC4D
        };
        public override Vector2 InitialSize => new Vector2(790, 580);

        public Dialog_CosmicPrinter(Building_CosmicPrinter printer)
        {
            this.printer = printer;
            draft = printer.Settings.Copy();
            draftBoundPawn = printer.BoundPawn;
            previewSkills = DefDatabase<RecipeDef>.AllDefsListForReading.Where(CraftingRecipeCatalog.Supports)
                .Select(recipe => recipe.workSkill).Where(skill => skill != null).Distinct()
                .OrderBy(skill => skill.LabelCap.ToString()).ToList();
            previewSkill = previewSkills.FirstOrDefault();
            doCloseX = true; doCloseButton = false; absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!printer.Spawned || printer.Faction != Faction.OfPlayer) { Close(); return; }
            CosmicPrinterSettings settings = draft;
            bool allowChanges = MagicStorageMod.Settings.AllowPrinterParameterChanges;
            bool bound = draftBoundPawn != null;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 30, 36), "MS_Printer_Title".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, rect.width, 48), "MS_Printer_Hint".Translate());
            float left = 320, rightX = left + 20, rightWidth = rect.width - rightX;
            Widgets.DrawMenuSection(new Rect(0, 98, left, 280));
            bool controlsEnabled = GUI.enabled;
            try
            {
                GUI.enabled = controlsEnabled && allowChanges && !bound;
                Percentage(new Rect(12, 112, left - 24, 30), "MS_Printer_Speed", ref settings.WorkSpeed, 100000, 0);
                Percentage(new Rect(12, 154, left - 24, 30), "MS_Printer_Efficiency", ref settings.Efficiency, 1000, 1);
                Integer(new Rect(12, 196, left - 24, 30), "MS_Printer_Skill", ref settings.SkillLevel, 0, 20, 2);
                GUI.enabled = controlsEnabled && allowChanges;
                Integer(new Rect(12, 238, left - 24, 30), "MS_Printer_Offset", ref settings.QualityOffset, -6, 6, 3);
                if (Widgets.ButtonText(new Rect(12, 292, left - 24, 32), "MS_Printer_Reset".Translate()))
                {
                    float speed = settings.WorkSpeed, efficiency = settings.Efficiency;
                    int skill = settings.SkillLevel;
                    settings.Reset();
                    if (bound) { settings.WorkSpeed = speed; settings.Efficiency = efficiency; settings.SkillLevel = skill; }
                    Array.Clear(buffers, 0, buffers.Length);
                }
            }
            finally { GUI.enabled = controlsEnabled; }
            if (bound)
            {
                Rect mask = new Rect(8, 104, left - 16, 124);
                Widgets.DrawBoxSolid(mask, new Color(.28f, .28f, .28f, .9f));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(mask, "MS_Printer_FollowBoundPawn".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Rect bindRect = new Rect(12, 334, left - 24, 32);
            if (Widgets.ButtonText(bindRect, bound ? "MS_Printer_BoundTo".Translate(draftBoundPawn.LabelShortCap)
                : "MS_Printer_BindTo".Translate())) OpenBindingMenu();
            TooltipHandler.TipRegion(bindRect, "MS_Printer_BindingTip".Translate());

            Widgets.DrawMenuSection(new Rect(rightX, 98, rightWidth, 120));
            try
            {
                GUI.enabled = controlsEnabled && allowChanges;
                Rate(new Rect(rightX + 12, 108, rightWidth - 24, 44), "MS_Printer_Inspiration", ref settings.InspirationChance);
                Rate(new Rect(rightX + 12, 160, rightWidth - 24, 44), "MS_Printer_Failure", ref settings.FailureChance);
            }
            finally { GUI.enabled = controlsEnabled; }
            settings.Normalize();

            Widgets.DrawMenuSection(new Rect(rightX, 230, rightWidth, bound ? 226 : 192));
            string previewHeading = "MS_Printer_Preview".Translate();
            float previewHeadingWidth = Text.CalcSize(previewHeading).x;
            Widgets.Label(new Rect(rightX + 12, 238, previewHeadingWidth, 26), previewHeading);
            Rect previewHintRect = new Rect(rightX + 12 + previewHeadingWidth + 8, 238,
                Math.Max(0f, rightWidth - 32 - previewHeadingWidth), 26);
            string previewHint = "MS_Printer_PreviewHint".Translate();
            Color previewHintColor = GUI.color;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(previewHintRect, previewHint.Truncate(previewHintRect.width));
            TooltipHandler.TipRegion(previewHintRect, previewHint);
            GUI.color = previewHintColor;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (bound && previewSkill != null && Widgets.ButtonText(new Rect(rightX + 12, 266, rightWidth - 24, 26),
                PreviewSkillLabel(previewSkill)))
            {
                var options = previewSkills.Select(skill => new FloatMenuOption(PreviewSkillLabel(skill),
                    () => previewSkill = skill)).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            double[] chances = settings.QualityProbabilities(bound
                ? (int?)PrinterPawnBinding.SkillLevel(draftBoundPawn, previewSkill) : null);
            UpdateDisplayedChances(chances);
            for (int i = 0; i < chances.Length; i++)
            {
                float y = (bound ? 304 : 270) + i * 20;
                GUI.color = QualityColors[i];
                Widgets.Label(new Rect(rightX + 12, y, 80, 22), ((QualityCategory)i).GetLabel().CapitalizeFirst());
                GUI.color = Color.white;
                Rect bar = new Rect(rightX + 96, y + 5, rightWidth - 166, 9);
                Widgets.DrawBoxSolid(bar, new Color(.17f,.19f,.21f));
                Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * (float)displayedChances[i], bar.height), QualityColors[i]);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rightX + rightWidth - 66, y, 54, 20), chances[i].ToString("P1"));
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (!allowChanges)
            {
                Color previousColor = GUI.color;
                TextAnchor previousAnchor = Text.Anchor;
                GUI.color = Color.red;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0, rect.height - 62, rect.width, 24), "MS_Printer_EditingLocked".Translate());
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
            bool previousEnabled = GUI.enabled;
            bool copy;
            bool confirm;
            Rect copyRect = new Rect(rect.width / 2 - 290, rect.height - 34, 240, 32);
            GUI.enabled = previousEnabled;
            try
            {
                copy = Widgets.ButtonText(copyRect, "MS_Printer_CopyToOthers".Translate());
                confirm = Widgets.ButtonText(new Rect(rect.width / 2 - 30, rect.height - 34, 140, 32),
                    "MS_Printer_Confirm".Translate());
            }
            finally { GUI.enabled = previousEnabled; }
            TooltipHandler.TipRegion(copyRect, "MS_Printer_CopyToOthersTip".Translate());
            if (copy) CopyToOtherPrinters();
            if (confirm && printer.TryApplyConfiguration(draft, draftBoundPawn, MagicStorageMod.Settings.AllowPrinterParameterChanges)) Close();
            if (Widgets.ButtonText(new Rect(rect.width / 2 + 130, rect.height - 34, 160, 32), "MS_Printer_Close".Translate())) Close();
        }

        private void CopyToOtherPrinters()
        {
            bool allowChanges = MagicStorageMod.Settings.AllowPrinterParameterChanges;
            if (!printer.Spawned || printer.Faction != Faction.OfPlayer ||
                !printer.TryApplyConfiguration(draft, draftBoundPawn, allowChanges)) return;

            int copied = 0;
            foreach (Building building in printer.Map.listerBuildings.AllBuildingsColonistOfDef(printer.def))
            {
                var other = building as Building_CosmicPrinter;
                if (other != null && other != printer && other.TryApplyConfiguration(draft, draftBoundPawn, allowChanges)) copied++;
            }
            Messages.Message("MS_Printer_CopiedToOthers".Translate(copied), MessageTypeDefOf.TaskCompletion, false);
        }

        private void OpenBindingMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("MS_Printer_Unbind".Translate(), () => SetDraftBinding(null))
            };
            foreach (Pawn pawn in printer.Map.mapPawns.FreeColonistsSpawned
                .Where(pawn => PrinterPawnBinding.IsAvailable(pawn, printer.Map)).OrderBy(pawn => pawn.LabelShort))
            {
                Pawn selected = pawn;
                options.Add(new FloatMenuOption(selected.LabelShortCap, () =>
                {
                    if (printer.Spawned && PrinterPawnBinding.IsAvailable(selected, printer.Map)) SetDraftBinding(selected);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SetDraftBinding(Pawn pawn)
        {
            draftBoundPawn = pawn;
            GUIUtility.keyboardControl = 0;
            Array.Clear(buffers, 0, buffers.Length);
        }

        private string PreviewSkillLabel(SkillDef skill)
            => skill.LabelCap + " (" + PrinterPawnBinding.SkillLevel(draftBoundPawn, skill) + ")";

        private void UpdateDisplayedChances(double[] chances)
        {
            float now = Time.realtimeSinceStartup;
            if (displayedChances == null)
            {
                displayedChances = (double[])chances.Clone();
                transitionStartChances = (double[])chances.Clone();
                targetChances = (double[])chances.Clone();
                probabilityTransitionStartTime = now;
                return;
            }

            // Absolute real time keeps the animation independent of game speed and GUI event count.
            float progress = Mathf.Clamp01((now - probabilityTransitionStartTime) / ProbabilityTransitionDuration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            bool targetChanged = false;
            for (int i = 0; i < chances.Length; i++)
            {
                displayedChances[i] = progress >= 1f ? targetChances[i]
                    : transitionStartChances[i] + (targetChances[i] - transitionStartChances[i]) * easedProgress;
                if (chances[i] != targetChances[i]) targetChanged = true;
            }

            if (targetChanged)
            {
                // Retarget from the current lengths, including when the previous transition is unfinished.
                Array.Copy(displayedChances, transitionStartChances, chances.Length);
                Array.Copy(chances, targetChances, chances.Length);
                probabilityTransitionStartTime = now;
            }
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
