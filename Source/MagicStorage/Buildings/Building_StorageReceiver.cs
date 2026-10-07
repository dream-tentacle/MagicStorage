using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // A real map storage cell: native haulers own pickup, reservations and delivery.
    // The ten stacks remain spawned until the completed placement is processed later.
    public sealed class Building_StorageReceiver : Building, ISlotGroupParent
    {
        private readonly SlotGroup slotGroup;
        private List<IntVec3> cells;
        private StorageSettings disabledSettings;
        private StorageSettings observedSettings;
        private StoragePriority observedPriority;
        private int nextTransferTick;

        public Building_StorageReceiver() { slotGroup = new SlotGroup(this); }
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

        public StorageSettings GetStoreSettings()
        {
            if (CanWork) return Network.Core.Settings;
            if (disabledSettings == null)
            {
                disabledSettings = new StorageSettings(this);
                disabledSettings.filter.SetDisallowAll();
                disabledSettings.Priority = StoragePriority.Unstored;
            }
            return disabledSettings;
        }

        public StorageSettings GetParentStoreSettings() => StorageSettings.EverStorableFixedSettings();
        public void Notify_SettingsChanged()
        {
            if (!Spawned) return;
            Map.listerHaulables.Notify_SlotGroupChanged(slotGroup);
            nextTransferTick = Find.TickManager.TicksGame + 1;
        }

        public void Notify_ReceivedThing(Thing item)
        {
            // SpawnSetup/GenPlace may still be operating on this object. Never take it here.
            nextTransferTick = Find.TickManager.TicksGame + 1;
        }
        public void Notify_LostThing(Thing item) { }

        internal void ProcessIncoming()
        {
            if (!Spawned) return;
            StorageSettings settings = GetStoreSettings();
            if (settings != observedSettings || settings.Priority != observedPriority)
            {
                observedSettings = settings;
                observedPriority = settings.Priority;
                Map.haulDestinationManager.Notify_HaulDestinationChangedPriority();
                Notify_SettingsChanged();
            }
            if (!CanWork || Find.TickManager.TicksGame < nextTransferTick) return;
            // Retry waiting stacks, including native merges which do not spawn a new Thing.
            nextTransferTick = Find.TickManager.TicksGame + 60;
            var items = new List<Thing>(slotGroup.HeldThings);
            foreach (Thing item in items)
            {
                if (!CanWork) break;
                if (!item.Spawned || item.Map != Map || item.Position != Position ||
                    item.IsForbidden(Faction) || Map.reservationManager.IsReservedByAnyoneOf(item, Faction)) continue;
                StorageReception.Transfer(Network, item, Map, Position);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            cells = new List<IntVec3> { Position };
            base.SpawnSetup(map, respawningAfterLoad);
            nextTransferTick = Find.TickManager.TicksGame + 1;
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
            (base.GetInspectString() + "\n" + "MS_ReceiverCapacity".Translate(slotGroup.HeldThingsCount, MaxItemsInCell) +
             "\n" + "MS_ReceiverUse".Translate()).Trim();
    }
}
