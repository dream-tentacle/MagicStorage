using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed partial class Dialog_StorageGuide
    {
        private sealed class AdvancedPage
        {
            internal readonly string Key, DefName;
            internal ThingDef Def => DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
            internal AdvancedPage(string key, string defName) { Key = key; DefName = defName; }
        }

        // Stable page identities: absent DLC definitions are hidden, not renumbered.
        private static readonly AdvancedPage[] AdvancedPages =
        {
            new AdvancedPage("Workbench", "MS_CosmicWorkbench"),
            new AdvancedPage("Gestator", "MS_CosmicMechGestator"),
            new AdvancedPage("Shelf", "MS_StorageSupplyShelf"),
            new AdvancedPage("Construction", "MS_ConstructionSupplyUnit"),
            new AdvancedPage("Apparel", "MS_StorageApparelAdapter"),
            new AdvancedPage("Fuel", "MS_FuelSupplier"),
            new AdvancedPage("Transport", "MS_TransportSupplier"),
            new AdvancedPage("Caravan", "MS_CaravanTeleporter"),
            new AdvancedPage("ProductionOutput", "MS_ProductionOutputPort")
        };
        private AdvancedPage SelectedAdvanced => selectedPage >= 7 && selectedPage < 7 + AdvancedPages.Length &&
            AdvancedPages[selectedPage - 7].Def != null ? AdvancedPages[selectedPage - 7] : null;
        private readonly Dictionary<string, List<Thing>> advancedBuildings = new Dictionary<string, List<Thing>>();

        private void RefreshAdvanced()
        {
            advancedBuildings.Clear();
            if (Map == null) return;
            foreach (var page in AdvancedPages)
            {
                var def = page.Def;
                if (def == null) continue;
                advancedBuildings[page.DefName] = Map.listerThings.ThingsOfDef(def)
                    .Where(t => t.Spawned && !t.Destroyed && t.Faction == Faction.OfPlayer).ToList();
                if (page.Key == "ProductionOutput" ? AdvancedBuildings(page).Count > 0 :
                    AdvancedBuildings(page).Any(InSelectedNetwork))
                    Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.CompleteAdvanced(
                        System.Array.IndexOf(GameComponent_StorageGuideNotice.AdvancedDefNames, page.DefName), core);
            }
        }

        private List<Thing> AdvancedBuildings(AdvancedPage page)
        {
            return advancedBuildings.TryGetValue(page.DefName, out var buildings) ? buildings : new List<Thing>();
        }

        private bool InSelectedNetwork(Thing thing) => ValidCore && core.Network != null &&
            thing.Spawned && !thing.Destroyed && thing.Faction == Faction.OfPlayer &&
            thing.TryGetComp<CompStorageNode>()?.Network == core.Network;

        private bool AdvancedComplete(AdvancedPage page) =>
            page.Def != null && Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.AdvancedLearned(
                System.Array.IndexOf(GameComponent_StorageGuideNotice.AdvancedDefNames, page.DefName)) == true;

        private void DrawNavRow(Rect row, int page, string title, ThingDef def = null)
        {
            if (selectedPage == page)
            {
                Widgets.DrawHighlightSelected(row);
                Widgets.DrawBoxSolid(new Rect(row.x, row.y + 5, 3, row.height - 10), Accent);
            }
            else Widgets.DrawHighlightIfMouseover(row);
            Rect icon = new Rect(row.x + 10, row.y + 11, 20, 20);
            if (def != null) GUI.DrawTexture(icon, AdvancedComplete(AdvancedPages[page - 7]) ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
            else if (page < 6) GUI.DrawTexture(icon, learned[page] ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
            else GUI.DrawTexture(icon, ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/Guide"));
            Label(new Rect(row.x + 36, row.y, row.width - 42, row.height), title,
                page == 6 ? Accent : Color.white, TextAnchor.MiddleLeft);
            if (Widgets.ButtonInvisible(row))
            { selectedPage = page; scroll = Vector2.zero; bodyHeight = 0; }
        }

        private void DrawAdvanced(float width, ref float y)
        {
            var page = SelectedAdvanced;
            Paragraph(width, ref y, ("MS_Guide_Advanced" + page.Key + "Body").Translate());
            DrawCheck(width, ref y, new CheckLine((AdvancedComplete(page) ? "MS_Guide_Learned" : "MS_Guide_NotLearned").Translate(), AdvancedComplete(page)));
            if (Map == null)
                DrawCheck(width, ref y, new CheckLine("MS_Guide_NoMap".Translate()));
            else
            {
                var buildings = AdvancedBuildings(page);
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedBuilt".Translate(buildings.Count), buildings.Count > 0));
                if (!ValidCore) DrawCheck(width, ref y, new CheckLine("MS_Guide_NoCore".Translate()));
                else
                {
                    var connected = buildings.Where(InSelectedNetwork).ToList();
                    DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedConnected".Translate(connected.Count), connected.Count > 0));
                    DrawCheck(width, ref y, new CheckLine("MS_NetworkStatus".Translate(StorageNetwork.StatusLabel(core.Network)), core.CanWork));
                    DrawAdvancedSettings(width, ref y, page, connected);
                }
            }
            if (page.Key != "Caravan")
            {
                y += 12;
                Paragraph(width, ref y, "MS_Guide_AdvancedTips".Translate(), Accent);
                Paragraph(width, ref y, ("MS_Guide_Advanced" + page.Key + "Tips").Translate(), Caption);
            }
        }

        private void DrawAdvancedSettings(float width, ref float y, AdvancedPage page, List<Thing> connected)
        {
            if (page.Key == "Construction")
            {
                DrawCheck(width, ref y, new CheckLine("MS_ConstructionSupplyUse".Translate(Building_ConstructionSupplyUnit.SupplyRadius)));
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedRepairCount".Translate(
                    connected.OfType<Building_ConstructionSupplyUnit>().Count(b => b.RepairMode))));
            }
            if (page.Key == "Fuel")
                DrawCheck(width, ref y, new CheckLine("MS_FuelSupply_Use".Translate(Building_FuelSupplier.SupplyRadius)));
            if (page.Key == "Shelf")
            {
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedFilterCount".Translate(
                    connected.OfType<Building_StorageSupplyShelf>().Count(b => b.FilterMode))));
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedColdCount".Translate(
                    connected.OfType<Building_StorageSupplyShelf>().Count(b => b.LowTemperatureMode))));
            }
            if (page.Key == "Transport")
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedDirectCount".Translate(
                    connected.OfType<Building_TransportSupplier>().Count(b => b.DirectSupply))));
            if (page.Key == "Workbench")
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedUnlockMode".Translate(
                    (MagicStorageMod.Settings.WorkbenchUnlockMode ? "MS_Guide_Enabled" : "MS_Guide_Disabled").Translate())));
            if (page.Key == "Gestator")
                DrawCheck(width, ref y, new CheckLine("MS_Guide_AdvancedStatusScope".Translate()));
        }

        private void DrawAdvancedActions(Rect rect)
        {
            var page = SelectedAdvanced;
            float width = (rect.width - 10) / 2;
            BuildButton(new Rect(rect.x, rect.y, width, rect.height), page.DefName);
            bool any = AdvancedBuildings(page).Any(t => t.Spawned && !t.Destroyed && t.Faction == Faction.OfPlayer);
            ActionButton(new Rect(rect.x + width + 10, rect.y, width, rect.height),
                "MS_Guide_AdvancedLocate", any, () => ChooseAdvancedBuilding(page));
        }

        private void ChooseAdvancedBuilding(AdvancedPage page)
        {
            // Re-query on click because the colony can keep running while the guide is open.
            RefreshAdvanced();
            var buildings = AdvancedBuildings(page).Where(t => t.Spawned && !t.Destroyed && t.Faction == Faction.OfPlayer)
                .OrderByDescending(InSelectedNetwork).ToList();
            if (buildings.Count == 1) { Locate(buildings[0]); return; }
            if (buildings.Count == 0) return;
            var options = buildings.Select(t => new FloatMenuOption(
                "MS_Guide_AdvancedTarget".Translate(t.LabelCap, t.Position.ToString(),
                    (InSelectedNetwork(t) ? "MS_Guide_AdvancedSameNetwork" : "MS_Guide_AdvancedOtherNetwork").Translate()),
                () => Locate(t))).ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
