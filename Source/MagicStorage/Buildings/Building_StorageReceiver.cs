using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // A real map storage cell: native haulers own pickup, reservations and delivery.
    // The ten stacks remain spawned until the completed placement is processed later.
    public sealed class Building_StorageReceiver : Building, ISlotGroupParent
    {
        private const int MaxAbsorbRadius = 3;
        private readonly SlotGroup slotGroup;
        private List<IntVec3> cells;
        private readonly StorageSettings disabledSettings;
        private StorageSettings observedSettings;
        private StoragePriority observedPriority;
        private StorageRefreshTimer refreshTimer;
        // 0 = its own cell only. The range widens absorption only: the slot group must stay a
        // single cell, or it would fight the stockpile zones the player puts around it.
        private int absorbRadius;

        public Building_StorageReceiver()
        {
            slotGroup = new SlotGroup(this);
            // Priority changes sort the map's haul destinations. Initialize before
            // spawning so a settings lookup during sorting cannot reenter it.
            disabledSettings = new StorageSettings(this);
            disabledSettings.filter.SetDisallowAll();
            disabledSettings.Priority = StoragePriority.Unstored;
        }
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        public bool HaulDestinationEnabled => CanWork;
        public bool StorageTabVisible => false;
        public bool IgnoreStoredThingsBeauty => true;
        public string GroupingLabel => LabelCap;
        public int GroupingOrder => 0;
        public bool Accepts(Thing item) => CanWork && Network.GetCountCanAccept(item) > 0;
        public SlotGroup GetSlotGroup() => slotGroup;
        public string SlotYielderLabel() => LabelCap;
        public IEnumerable<IntVec3> AllSlotCells() => AllSlotCellsList();
        public List<IntVec3> AllSlotCellsList() => cells ?? (cells = new List<IntVec3> { Position });
        public int AbsorbRadius => absorbRadius;

        public StorageSettings GetStoreSettings() => CanWork ? Network.Core.Settings : disabledSettings;

        public StorageSettings GetParentStoreSettings() => StorageSettings.EverStorableFixedSettings();
        public void Notify_SettingsChanged()
        {
            if (!Spawned) return;
            Map.listerHaulables.Notify_SlotGroupChanged(slotGroup);
        }

        public void Notify_ReceivedThing(Thing item)
        {
            // SpawnSetup/GenPlace may still be operating on this object. Never take it here.
        }
        public void Notify_LostThing(Thing item) { }

        internal void ProcessIncoming()
        {
            if (!Spawned || !refreshTimer.IsDue(StorageRefreshTask.Receiver, Find.TickManager.TicksGame)) return;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            StorageSettings settings = GetStoreSettings();
            if (settings != observedSettings || settings.Priority != observedPriority)
            {
                observedSettings = settings;
                observedPriority = settings.Priority;
                Map.haulDestinationManager.Notify_HaulDestinationChangedPriority();
                Notify_SettingsChanged();
            }
            if (!CanWork) return;
            // Retry waiting stacks, including native merges which do not spawn a new Thing.
            List<Thing> items = CollectAbsorbable();
            foreach (Thing item in items)
            {
                if (!CanWork) break;
                if (!item.Spawned || item.Map != Map || !InAbsorbRange(item.Position) ||
                    item.IsForbidden(Faction) || Map.reservationManager.IsReservedByAnyoneOf(item, Faction)) continue;
                IntVec3 source = item.Position;
                var result = StorageReception.Transfer(Network, item, Map, source);
                if (result.Transferred <= 0) continue;
                Network?.Core?.GetComp<CompStorageGuide>()?.Complete(4);
                if (Spawned && source != Position && MagicStorageMod.Settings.ShowStorageReceiverBeams)
                {
                    var beam = DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ConstructionSupplyBeam") ??
                        DefDatabase<ThingDef>.GetNamedSilentFail("MS_Mote_ProductionSupplyBeamFallback");
                    if (beam != null)
                        MoteMaker.MakeInteractionOverlay(beam, new TargetInfo(source, Map), new TargetInfo(Position, Map),
                            default, this.TrueCenter() - Position.ToVector3Shifted());
                }
            }
        }

        // Its own cell keeps the native slot-group semantics. A wider range is a plain map
        // scan, so surrounding stockpile zones stay independent haul destinations.
        private List<Thing> CollectAbsorbable()
        {
            List<Thing> result = new List<Thing>();
            if (absorbRadius <= 0) { result.AddRange(slotGroup.HeldThings); return result; }
            Map map = Map;
            List<IntVec3> scan = RangeCells();
            for (int c = 0; c < scan.Count; c++)
            {
                List<Thing> things = map.thingGrid.ThingsListAt(scan[c]);
                for (int i = 0; i < things.Count; i++)
                    if (things[i].def.EverStorable(false)) result.Add(things[i]);
            }
            return result;
        }

        private List<IntVec3> RangeCells()
        {
            List<IntVec3> result = new List<IntVec3>();
            IntVec3 origin = Position;
            for (int x = origin.x - absorbRadius; x <= origin.x + absorbRadius; x++)
                for (int z = origin.z - absorbRadius; z <= origin.z + absorbRadius; z++)
                {
                    IntVec3 cell = new IntVec3(x, origin.y, z);
                    if (cell.InBounds(Map)) result.Add(cell);
                }
            return result;
        }

        private bool InAbsorbRange(IntVec3 cell) =>
            absorbRadius <= 0 ? cell == Position :
            cell.y == Position.y && Mathf.Abs(cell.x - Position.x) <= absorbRadius &&
            Mathf.Abs(cell.z - Position.z) <= absorbRadius;

        private static string RangeOptionLabel(int radius) =>
            radius <= 0 ? "MS_ReceiverRange_This".Translate().ToString()
                        : "MS_ReceiverRange_Within".Translate(radius).ToString();

        private void ChooseAbsorbRange()
        {
            var options = new List<FloatMenuOption>();
            for (int radius = 0; radius <= MaxAbsorbRadius; radius++)
            {
                int chosen = radius;
                var option = FloatMenuOption.CheckboxLabeled(RangeOptionLabel(radius),
                    () => { absorbRadius = chosen; }, chosen == absorbRadius);
                option.tooltip = "MS_ReceiverRangeDesc".Translate();
                options.Add(option);
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = "MS_ReceiverRange".Translate(RangeOptionLabel(absorbRadius)),
                defaultDesc = "MS_ReceiverRangeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/ReceiverRange"),
                action = ChooseAbsorbRange
            };
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (!Spawned || absorbRadius <= 0) return;
            GenDraw.DrawFieldEdges(RangeCells(), new Color(0.45f, 1f, 0.45f, 0.6f));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref absorbRadius, "absorbRadius", 0);
            if (absorbRadius < 0) absorbRadius = 0;
            else if (absorbRadius > MaxAbsorbRadius) absorbRadius = MaxAbsorbRadius;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            cells = new List<IntVec3> { Position };
            base.SpawnSetup(map, respawningAfterLoad);
            refreshTimer.Start(Find.TickManager.TicksGame);
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Preserve the real stacks before native shelf removal scatters excess items.
            // Failed placement stays in the existing saved recovery queue.
            Map map = Map;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            var batch = manager.CreateRecovery(Position);
            try
            {
                foreach (Thing item in new List<Thing>(slotGroup.HeldThings))
                {
                    item.DeSpawn();
                    if (!batch.Contents.TryAdd(item, false)) manager.PreserveLooseThing(item, Position);
                }
                base.DeSpawn(mode);
            }
            finally { manager.ReleaseRecovery(batch); }
        }

        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_ReceiverCapacity".Translate(Spawned ? slotGroup.HeldThingsCount : 0, MaxItemsInCell) +
             "\n" + "MS_ReceiverUse".Translate()).Trim();
    }
}
