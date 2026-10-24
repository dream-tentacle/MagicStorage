using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Building_ConstructionSupplyUnit : Building
    {
        public static float SupplyRadius => MagicStorageMod.Settings.ConstructionSupplyRadius;
        private StorageRefreshTimer refreshTimer;
        private bool repairMode = true;
        internal int SupplyRefreshCount, RepairScanCount;
        internal int LastSupplyRefreshTick = -1, LastRepairScanTick = -1;
        public bool RepairMode => repairMode;
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && refreshTimer.IsDue(StorageRefreshTask.ConstructionSupply, Find.TickManager.TicksGame)) RefreshSupply();
        }

        internal void RefreshSupply()
        {
            SupplyRefreshCount++;
            LastSupplyRefreshTick = Find.TickManager.TicksGame;
            if (!Spawned) return;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (CanWork) ConstructionSupply.Deliver(this);
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            if (Spawned && newFaction != Faction.OfPlayer)
                Map.GetComponent<MapComponent_ConstructionRepair>().CancelFor(this);
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref repairMode, "repairMode", true);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "MS_ConstructionRepairMode".Translate(),
                defaultDesc = "MS_ConstructionRepairModeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/AutoRebuild"),
                isActive = () => repairMode,
                toggleAction = () =>
                {
                    repairMode = !repairMode;
                    if (!repairMode) Map.GetComponent<MapComponent_ConstructionRepair>().CancelFor(this);
                }
            };
            if (Prefs.DevMode)
                yield return new Command_Action
                {
                    defaultLabel = "DEV: 修理检查",
                    action = () =>
                    {
                        string report = MapComponent_ConstructionRepair.Diagnostics(this);
                        GUIUtility.systemCopyBuffer = report;
                        Find.WindowStack.Add(new Dialog_MessageBox("修理诊断已复制到剪贴板，可以回到对话直接粘贴。\n\n" + report));
                    }
                };
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned) Map.GetComponent<MapComponent_ConstructionRepair>().CancelFor(this);
            base.DeSpawn(mode);
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            DrawSupplyRadius(Position);
        }

        internal static void DrawSupplyRadius(IntVec3 center)
        {
            float radius = SupplyRadius;
            // NumCellsInRadius also rejects equality with the precomputed limit.
            if (radius < GenRadial.MaxRadialPatternRadius)
                GenDraw.DrawRadiusRing(center, radius);
        }

        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_ConstructionSupplyUse".Translate(SupplyRadius)).Trim();
    }

    public sealed class PlaceWorker_ConstructionSupply : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        { Building_ConstructionSupplyUnit.DrawSupplyRadius(center); }
    }
}
