using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed partial class Dialog_StorageGuide : Window
    {
        private static int selectedPage;
        private static int PracticalShelfPage => 7 + AdvancedPages.Length;
        private static readonly string[] PracticalPageKeys = { "Shelf", "Hotkeys", "Turret", "Grouping", "FindRecipes", "ModTrade" };
        private static bool IsPracticalPage => selectedPage >= PracticalShelfPage && selectedPage < PracticalShelfPage + PracticalPageKeys.Length;
        private static string PracticalTextKey(string suffix) => "MS_Guide_Practical" + PracticalPageKeys[selectedPage - PracticalShelfPage] + suffix;
        private static readonly Color Caption = new Color(.67f, .70f, .73f);
        private static readonly Color Accent = new Color(.95f, .80f, .35f);
        private static readonly Color LineColor = new Color(.32f, .42f, .48f);
        private Building_StorageCore core;
        private Map snapshotMap;
        private Thing sample;
        private Vector2 scroll, navScroll;
        private float bodyHeight, nextRefresh;
        private int completed;
        private readonly bool[] current = new bool[4];
        private readonly bool[] learned = new bool[6];
        private readonly List<Building_StorageReceiver> receivers = new List<Building_StorageReceiver>();
        private readonly List<Thing> samples = new List<Thing>();
        private readonly List<CheckLine> diagnosticLines = new List<CheckLine>();
        private readonly List<CheckLine> sampleLines = new List<CheckLine>();
        private sealed class CheckLine
        {
            internal string Text;
            internal bool? Passed;
            internal CheckLine(string text, bool? passed = null) { Text = text; Passed = passed; }
        }

        public override Vector2 InitialSize => new Vector2(Math.Min(960f, UI.screenWidth - 32f), Math.Min(720f, UI.screenHeight - 48f));
        private Map Map => ValidCore ? core.Map : (Current.ProgramState == ProgramState.Playing ? Find.CurrentMap : null);
        private bool ValidCore => core != null && core.Spawned && core.Faction == Faction.OfPlayer;

        public Dialog_StorageGuide(Building_StorageCore core = null)
        {
            this.core = core;
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            if (!ValidCore && Map != null)
            {
                var cores = CoresOnMap().Take(2).ToList();
                if (cores.Count == 1) core = cores[0];
            }
            Refresh();
        }

        private IEnumerable<Building_StorageCore> CoresOnMap()
        {
            return Map == null ? Enumerable.Empty<Building_StorageCore>() : Map.listerThings
                .ThingsOfDef(DefDatabase<ThingDef>.GetNamed("MS_StorageCore"))
                .OfType<Building_StorageCore>().Where(c => c.Spawned && c.Faction == Faction.OfPlayer);
        }

        private void Refresh()
        {
            if (!ValidCore) core = null;
            snapshotMap = Map;
            nextRefresh = Time.realtimeSinceStartup + .5f;
            snapshotMap?.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            receivers.Clear(); samples.Clear(); diagnosticLines.Clear(); sampleLines.Clear();
            var network = core?.Network;
            if (network != null)
                foreach (var node in network.Members)
                    if (node.parent is Building_StorageReceiver receiver && receiver.Spawned) receivers.Add(receiver);
            RefreshAdvanced();
            bool ready = network?.CanWork == true;
            current[0] = ready;
            current[1] = ready && network.UnitCount > 0;
            current[2] = ready && receivers.Count >= 3;
            current[3] = ready && core.Settings.filter.AllowedDefCount > 0;
            var progress = Current.Game?.GetComponent<GameComponent_StorageGuideNotice>();
            completed = 0;
            for (int i = 0; i < learned.Length; i++)
            {
                if (i < 4 && current[i]) progress?.Complete(i, core);
                learned[i] = progress?.Learned(i) == true;
                if (learned[i]) completed++;
            }
            if (core == null) { sample = null; return; }
            diagnosticLines.Add(new CheckLine("MS_NetworkStatus".Translate(StorageNetwork.StatusLabel(network)), ready));
            if (!ready) diagnosticLines.Add(new CheckLine("MS_Guide_NetworkFix".Translate()));
            if (network != null)
                diagnosticLines.Add(new CheckLine("MS_NetworkCapacity".Translate(network.UnitCount, network.UsedSlots, network.SlotCapacity), network.UnitCount > 0));
            diagnosticLines.Add(new CheckLine("MS_Guide_Receivers".Translate(receivers.Count), receivers.Count >= 3));
            diagnosticLines.Add(new CheckLine("MS_Guide_FilterReady".Translate(), core.Settings.filter.AllowedDefCount > 0));
            diagnosticLines.Add(new CheckLine("MS_Guide_Priority".Translate(core.Settings.Priority.Label())));
            samples.AddRange(receivers.SelectMany(r => r.GetSlotGroup().HeldThings).Where(t => t.Spawned && !t.Destroyed));
            if (!samples.Contains(sample)) sample = samples.FirstOrDefault();
            if (sample == null) return;
            bool blocked = !ready;
            if (!core.Settings.AllowedToAccept(sample)) { AddSampleProblem("MS_Guide_Rejected"); blocked = true; }
            else if (ready && network.GetCountCanAccept(sample) == 0) { AddSampleProblem("MS_Guide_NoCapacity"); blocked = true; }
            if (sample.IsForbidden(Faction.OfPlayer)) { AddSampleProblem("MS_Guide_Forbidden"); blocked = true; }
            if (core.Map.reservationManager.IsReservedByAnyoneOf(sample, Faction.OfPlayer)) { AddSampleProblem("MS_Guide_Reserved"); blocked = true; }
            if (!blocked) sampleLines.Add(new CheckLine("MS_Guide_WaitRefresh".Translate(), true));
        }

        private void AddSampleProblem(string key) { sampleLines.Add(new CheckLine(key.Translate(), false)); }

        public override void DoWindowContents(Rect rect)
        {
            if (Time.realtimeSinceStartup >= nextRefresh || Map != snapshotMap || (core != null && !ValidCore)) Refresh();
            if (selectedPage >= 7 && SelectedAdvanced == null && !IsPracticalPage) selectedPage = 0;
            var font = Text.Font;
            var anchor = Text.Anchor;
            bool wrap = Text.WordWrap;
            var color = GUI.color;
            try
            {
                GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, rect.width * .5f, 36), "MS_Guide_Title".Translate());
                Text.Font = GameFont.Small;
                Rect progressRect = new Rect(rect.width * .5f, 5, rect.width * .5f - 28, 28);
                var stage = Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.Stage ?? StorageGuideStage.Basics;
                Label(progressRect, "MS_Guide_Progress".Translate(completed + AdvancedPages.Count(AdvancedComplete),
                    6 + AdvancedPages.Count(p => p.Def != null), ("MS_Guide_Stage" + stage).Translate()), Caption, TextAnchor.MiddleRight);
                TooltipHandler.TipRegion(progressRect, "MS_Guide_ProgressHelp".Translate());
                string rewardText = ("MS_Guide_Reward" + stage).Translate();
                float rewardHeight = Text.CalcHeight(rewardText, rect.width);
                Label(new Rect(0, 40, rect.width, rewardHeight), rewardText, Accent);
                float contentTop = 40 + rewardHeight + 12;
                float navWidth = Math.Min(220, rect.width * .28f);
                DrawNavigation(new Rect(0, contentTop, navWidth, rect.height - contentTop));
                DrawPage(new Rect(navWidth + 14, contentTop, rect.width - navWidth - 14, rect.height - contentTop));
            }
            finally { GUI.color = color; Text.Font = font; Text.Anchor = anchor; Text.WordWrap = wrap; }
        }

        private void DrawNavigation(Rect rect)
        {
            Label(new Rect(rect.x + 10, rect.y, rect.width - 20, 24), "MS_Guide_Basics".Translate(), Caption);
            Rect viewport = new Rect(rect.x, rect.y + 32, rect.width, Math.Max(1, rect.height - 120));
            int advancedCount = AdvancedPages.Count(p => p.Def != null);
            float navHeight = 6 * 46 + 38 + advancedCount * 46 + 38 + PracticalPageKeys.Length * 46 + 60;
            Rect view = new Rect(0, 0, rect.width - 16, Math.Max(navHeight, viewport.height));
            Widgets.BeginScrollView(viewport, ref navScroll, view);
            float rowY = 0;
            for (int i = 0; i < 6; i++)
            {
                DrawNavRow(new Rect(0, rowY, view.width, 42), i, ("MS_Guide_Nav" + i).Translate());
                rowY += 46;
            }
            Label(new Rect(10, rowY + 8, view.width - 20, 24), "MS_Guide_Advanced".Translate(), Caption);
            rowY += 38;
            for (int i = 0; i < AdvancedPages.Length; i++)
            {
                var page = AdvancedPages[i];
                if (page.Def == null) continue;
                DrawNavRow(new Rect(0, rowY, view.width, 42), i + 7, page.Def.LabelCap, page.Def);
                rowY += 46;
            }
            Label(new Rect(10, rowY + 8, view.width - 20, 24), "MS_Guide_Practical".Translate(), Caption);
            rowY += 38;
            for (int i = 0; i < PracticalPageKeys.Length; i++)
            {
                string key = "MS_Guide_Practical" + PracticalPageKeys[i] + (i == 0 ? "Nav" : "Title");
                DrawNavRow(new Rect(0, rowY, view.width, 42), PracticalShelfPage + i, key.Translate());
                rowY += 46;
            }
            DrawNavRow(new Rect(0, rowY + 16, view.width, 42), 6, "MS_Guide_Nav6".Translate());
            Widgets.EndScrollView();
            float bottom = rect.yMax - 78;
            Widgets.DrawBoxSolid(new Rect(rect.x + 8, bottom, rect.width - 16, 1), LineColor);
            Label(new Rect(rect.x + 10, bottom + 8, rect.width - 20, 22), "MS_Guide_TargetNetwork".Translate(), Caption);
            Rect choose = new Rect(rect.x + 4, bottom + 34, rect.width - 8, 38);
            string text = core == null ? "MS_Guide_ChooseCore".Translate().ToString() : "MS_Guide_CoreAt".Translate(core.LabelCap, core.Position.ToString()).ToString();
            var oldFont = Text.Font; Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(choose, text, active: Map != null)) ChooseCore();
            Text.Font = oldFont;
            TooltipHandler.TipRegion(choose, text);
        }

        private void DrawPage(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(16);
            Text.Font = GameFont.Medium;
            string title = IsPracticalPage ? PracticalTextKey("Title").Translate().ToString() :
                SelectedAdvanced != null ? SelectedAdvanced.Def.LabelCap.ToString() : selectedPage == 6 ? "MS_Guide_Diagnosis".Translate().ToString() : ("MS_Guide_Step" + selectedPage).Translate().ToString();
            float titleHeight = Math.Max(36, Text.CalcHeight(title, inner.width));
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, titleHeight), title);
            Text.Font = GameFont.Small;
            float y = inner.y + titleHeight + 8;
            if (selectedPage != 6 && !IsPracticalPage)
            {
                float artHeight = Math.Min(184, Math.Max(0, (inner.height - titleHeight - 190) * .72f));
                if (artHeight >= 140) { DrawDiagram(new Rect(inner.x, y, inner.width, artHeight)); y += artHeight + 14; }
            }
            Rect footer = new Rect(inner.x, inner.yMax - 36, inner.width, 36);
            float contentBottom = IsPracticalPage ? inner.yMax : footer.y - 16;
            Rect viewport = new Rect(inner.x, y, inner.width, Math.Max(1, contentBottom - y));
            Rect view = new Rect(0, 0, viewport.width - 18, Math.Max(bodyHeight, viewport.height));
            Widgets.BeginScrollView(viewport, ref scroll, view);
            float cursor = 0;
            if (IsPracticalPage)
            {
                Paragraph(view.width, ref cursor, PracticalTextKey("Body").Translate());
                if (selectedPage == PracticalShelfPage)
                    Paragraph(view.width, ref cursor, "MS_Guide_PracticalShelfNote".Translate(), Caption);
            }
            else if (selectedPage == 6) DrawDiagnosis(view.width, ref cursor);
            else if (SelectedAdvanced != null) DrawAdvanced(view.width, ref cursor);
            else DrawChapter(view.width, ref cursor);
            bodyHeight = cursor + 6;
            Widgets.EndScrollView();
            if (IsPracticalPage) return;
            Widgets.DrawBoxSolid(new Rect(inner.x, footer.y - 9, inner.width, 1), LineColor);
            DrawActions(footer);
        }

        private void DrawChapter(float width, ref float y)
        {
            Paragraph(width, ref y, ("MS_Guide_Body" + selectedPage).Translate());
            if (core == null)
            {
                DrawCheck(width, ref y, new CheckLine((Map == null ? "MS_Guide_NoMap" : "MS_Guide_NoCore").Translate()));
                return;
            }
            y += 8;
            DrawCheck(width, ref y, new CheckLine((learned[selectedPage] ? "MS_Guide_Learned" : "MS_Guide_NotLearned").Translate(), learned[selectedPage]));
            if (selectedPage < 4)
                DrawCheck(width, ref y, new CheckLine("MS_Guide_Current".Translate((current[selectedPage] ? "MS_Guide_Met" : "MS_Guide_Unmet").Translate()), current[selectedPage]));
            if (selectedPage == 0) DrawCheck(width, ref y, new CheckLine("MS_NetworkStatus".Translate(StorageNetwork.StatusLabel(core.Network))));
            if (selectedPage == 1 && core.Network != null)
                DrawCheck(width, ref y, new CheckLine("MS_NetworkCapacity".Translate(core.Network.UnitCount, core.Network.UsedSlots, core.Network.SlotCapacity)));
            if (selectedPage == 2) DrawCheck(width, ref y, new CheckLine("MS_Guide_Receivers".Translate(receivers.Count), receivers.Count >= 3));
            if (selectedPage == 3) DrawCheck(width, ref y, new CheckLine("MS_Guide_Priority".Translate(core.Settings.Priority.Label())));
            if (selectedPage == 5) { y += 8; Paragraph(width, ref y, "MS_Guide_Reabsorb".Translate(), Caption); }
        }

        private void DrawDiagnosis(float width, ref float y)
        {
            Paragraph(width, ref y, "MS_Guide_DiagIntro".Translate(), Caption);
            if (core == null) DrawCheck(width, ref y, new CheckLine((Map == null ? "MS_Guide_NoMap" : "MS_Guide_NoCore").Translate()));
            else
            {
                foreach (var line in diagnosticLines) DrawCheck(width, ref y, line);
                y += 16;
                Paragraph(width, ref y, "MS_Guide_SampleSection".Translate(), Accent);
                if (sample == null) Paragraph(width, ref y, "MS_Guide_NoSample".Translate(), Caption);
                else
                {
                    string sampleLabel = "MS_Guide_Sample".Translate(sample.LabelCap, sample.Position.ToString());
                    float height = Math.Max(34, Text.CalcHeight(sampleLabel, width - 16) + 12);
                    if (Widgets.ButtonText(new Rect(0, y, width, height), sampleLabel)) ChooseSample();
                    y += height + 10;
                    foreach (var line in sampleLines) DrawCheck(width, ref y, line);
                }
            }
            y += 16;
            Paragraph(width, ref y, "MS_Guide_AdviceSection".Translate(), Accent);
            Paragraph(width, ref y, "MS_Guide_HaulingAdvice".Translate(), Caption);
            Paragraph(width, ref y, "MS_Guide_Reabsorb".Translate(), Caption);
        }

        private static void Paragraph(float width, ref float y, string text, Color? color = null)
        {
            float height = Text.CalcHeight(text, width);
            Label(new Rect(0, y, width, height), text, color ?? Color.white);
            y += height + 12;
        }

        private static void DrawCheck(float width, ref float y, CheckLine line)
        {
            float height = Math.Max(28, Text.CalcHeight(line.Text, width - 30) + 6);
            GUI.color = line.Passed == false ? Accent : Color.white;
            GUI.DrawTexture(new Rect(0, y + 3, 20, 20), line.Passed.HasValue
                ? (line.Passed.Value ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex) : TexButton.Info);
            GUI.color = Color.white;
            Label(new Rect(30, y + 2, width - 30, height), line.Text, line.Passed.HasValue ? Color.white : Caption);
            y += height + 4;
        }

        // Use this mod's Def icons to explain the topology; this is not a map preview.
        private void DrawDiagram(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(.065f, .085f, .10f));
            float cellWidth = rect.width / 3;
            float centerY = rect.y + rect.height * .40f;
            Widgets.DrawBoxSolid(new Rect(rect.x + cellWidth * .5f, centerY, cellWidth * 2, 2), LineColor);
            int focus = selectedPage == 1 ? 2 : (selectedPage == 2 || selectedPage == 4 ? 0 : 1);
            string[] defs = SelectedAdvanced == null
                ? new[] { "MS_StorageReceiver", "MS_StorageCore", "MS_StorageUnit" }
                : new[] { "MS_StorageUnit", "MS_StorageCore", SelectedAdvanced.DefName };
            if (SelectedAdvanced != null) focus = 2;
            for (int i = 0; i < 3; i++)
            {
                float x = rect.x + cellWidth * i;
                Rect tile = new Rect(x + 8, rect.y + 8, cellWidth - 16, rect.height - 32);
                if (focus == i) Widgets.DrawBoxSolid(tile, new Color(.25f, .31f, .33f, .45f));
                var def = DefDatabase<ThingDef>.GetNamed(defs[i]);
                float size = Math.Min(64, rect.height * .40f);
                if (i == 0 && SelectedAdvanced == null)
                {
                    float small = Math.Min(34, cellWidth / 3 - 5);
                    for (int j = 0; j < 3; j++)
                        Widgets.DefIcon(new Rect(x + cellWidth / 2 + (j - 1) * (small + 2) - small / 2,
                            centerY - small / 2, small, small), def);
                }
                else Widgets.DefIcon(new Rect(x + (cellWidth - size) / 2, centerY - size / 2, size, size), def);
                var font = Text.Font; Text.Font = GameFont.Tiny;
                Label(new Rect(x + 4, rect.yMax - 51, cellWidth - 8, 28), (SelectedAdvanced == null ? ("MS_Guide_Diagram" + i).Translate().ToString() : def.LabelCap.ToString()),
                    focus == i ? Accent : Caption, TextAnchor.MiddleCenter);
                Text.Font = font;
            }
            var previousFont = Text.Font; Text.Font = GameFont.Tiny;
            Label(new Rect(rect.x + 8, rect.yMax - 23, rect.width - 16, 20), "MS_Guide_Schematic".Translate(), Caption, TextAnchor.MiddleCenter);
            Text.Font = previousFont;
        }

        private static void Label(Rect rect, string text, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var oldColor = GUI.color; var oldAnchor = Text.Anchor;
            GUI.color = color; Text.Anchor = anchor;
            Widgets.Label(rect, text);
            GUI.color = oldColor; Text.Anchor = oldAnchor;
        }

        private void DrawActions(Rect rect)
        {
            if (SelectedAdvanced != null) { DrawAdvancedActions(rect); return; }
            float width = (rect.width - 10) / 2;
            Rect left = new Rect(rect.x, rect.y, width, rect.height);
            Rect right = new Rect(rect.x + width + 10, rect.y, width, rect.height);
            switch (selectedPage)
            {
                case 0:
                    BuildButton(left, "MS_StorageCore");
                    ActionButton(right, "MS_Guide_LocateCore", ValidCore, () => Locate(core)); break;
                case 1:
                    BuildButton(left, "MS_StorageUnit"); BuildButton(right, "MS_StorageConduit"); break;
                case 2:
                    BuildButton(left, "MS_StorageReceiver");
                    ActionButton(right, "MS_Guide_ShowReceiversShort", receivers.Count > 0, () =>
                    {
                        var targets = receivers.Where(r => r.Spawned).ToArray();
                        if (targets.Length == 0) return;
                        Locate(targets[0]);
                        foreach (var target in targets.Skip(1)) Find.Selector.Select(target);
                    }); break;
                case 3:
                    ActionButton(left, "MS_Guide_OpenFilterShort", ValidCore, OpenFilter);
                    ActionButton(right, "MS_Guide_LocateCore", ValidCore, () => Locate(core)); break;
                case 4: case 5:
                    ActionButton(left, "MS_Guide_OpenInventoryShort", ValidCore, () =>
                    { ExitToMap(); Find.WindowStack.Add(new Dialog_StorageInventory(core)); });
                    ActionButton(right, "MS_Guide_Nav6", true, () => { selectedPage = 6; scroll = Vector2.zero; bodyHeight = 0; }); break;
                default:
                    ActionButton(left, "MS_Guide_OpenFilterShort", ValidCore, OpenFilter);
                    ActionButton(right, "MS_Guide_LocateItem", sample?.Spawned == true, () => Locate(sample)); break;
            }
        }

        private static void ActionButton(Rect rect, string key, bool enabled, Action action)
        { if (Widgets.ButtonText(rect, key.Translate(), active: enabled) && enabled) action(); }

        private void OpenFilter()
        { if (ValidCore) { Locate(core); InspectPaneUtility.OpenTab(typeof(ITab_Storage)); } }

        private void ChooseCore()
        {
            var options = CoresOnMap().Select(c => new FloatMenuOption("MS_Guide_CoreAt".Translate(c.LabelCap, c.Position.ToString()),
                () => { core = c; sample = null; scroll = Vector2.zero; Refresh(); })).ToList();
            if (options.Count == 0) Messages.Message("MS_Guide_NoCore".Translate(), MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ChooseSample()
        {
            var options = samples.Where(t => t.Spawned && !t.Destroyed).Select(t => new FloatMenuOption(
                "MS_Guide_Sample".Translate(t.LabelCap, t.Position.ToString()), () => { sample = t; Refresh(); })).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private void BuildButton(Rect rect, string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamed(defName);
            bool enabled = Map != null;
            // Vanilla resolves visibility against the active map; resolve again after switching.
            if (enabled && Map == Find.CurrentMap) enabled = BuildCopyCommandUtility.FindAllowedDesignator(def) != null;
            if (!enabled) TooltipHandler.TipRegion(rect, (Map == null ? "MS_Guide_NoMap" : "MS_Guide_BuildLocked").Translate());
            if (!Widgets.ButtonText(rect, "MS_Guide_Build".Translate(def.LabelCap), active: enabled) || !enabled) return;
            Current.Game.CurrentMap = Map;
            var designator = BuildCopyCommandUtility.FindAllowedDesignator(def);
            if (designator == null) { Messages.Message("MS_Guide_BuildLocked".Translate(), MessageTypeDefOf.RejectInput, false); return; }
            ExitToMap();
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Architect);
            designator.ProcessInput(Event.current);
        }

        private void Locate(Thing target)
        {
            if (target?.Spawned != true) return;
            ExitToMap();
            Current.Game.CurrentMap = target.Map;
            CameraJumper.TryJump(target);
            Find.Selector.ClearSelection();
            Find.Selector.Select(target);
        }

        private void ExitToMap()
        {
            Close();
            Find.WindowStack.TryRemove(typeof(Dialog_ModSettings));
            Find.WindowStack.TryRemove(typeof(Dialog_Options));
        }
    }
}
