using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Contents stay spawned. Native searches, reservations and pickup jobs see ordinary items.
    public sealed class Building_StorageSupplyShelf : Building, ISlotGroupParent
    {
        public const int SlotCount = 3;
        private List<ThingDef> selections = new List<ThingDef> { null, null, null };
        private List<int> restockLimits = new List<int> { 0, 0, 0 };
        private List<StorageShelfFilter> filters = new List<StorageShelfFilter>
            { new StorageShelfFilter(), new StorageShelfFilter(), new StorageShelfFilter() };
        private readonly SlotGroup slotGroup;
        private List<IntVec3> cells;
        private StorageSettings settings;
        private readonly StorageShelfRestockFailure[] restockFailures = new StorageShelfRestockFailure[SlotCount];
        internal StorageShelfRestockFailure RestockFailure(int slot) =>
            restockFailures[slot]?.IsCurrent == true ? restockFailures[slot] : null;
        internal void ClearRestockFailures() => Array.Clear(restockFailures, 0, SlotCount);
        internal void RecordRestockFailure(int slot, int current, StorageShelfFailureReason reason)
        {
            if (filters[slot].WarnWhenRestockFails && reason != StorageShelfFailureReason.None && current < RestockLimit(slot))
                restockFailures[slot] = new StorageShelfRestockFailure(this, slot, current, reason);
        }

        public Building_StorageSupplyShelf() { slotGroup = new SlotGroup(this); }
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        // This is an output buffer, not a destination for normal haulers.
        public bool HaulDestinationEnabled => false;
        public bool StorageTabVisible => false;
        public bool IgnoreStoredThingsBeauty => true;
        public string GroupingLabel => LabelCap;
        public int GroupingOrder => 0;
        public SlotGroup GetSlotGroup() => slotGroup;
        public string SlotYielderLabel() => LabelCap;
        public IEnumerable<IntVec3> AllSlotCells() => AllSlotCellsList();
        public List<IntVec3> AllSlotCellsList() => cells ?? (cells = new List<IntVec3> { Position });
        public ThingDef SelectedDef(int slot) => selections[slot];
        public StorageShelfFilter SlotFilter(int slot) => filters[slot];
        public bool AllowsStock(int slot, Thing item) => item != null && item.def == selections[slot] &&
            GetParentStoreSettings().AllowedToAccept(item) && filters[slot].Allows(item);
        public int RestockLimit(int slot) => Math.Max(0, Math.Min(restockLimits[slot], selections[slot]?.stackLimit ?? 0));
        public bool SetRestockLimit(int slot, int value)
        {
            if (slot < 0 || slot >= SlotCount || selections[slot] == null) return false;
            int limit = Math.Max(0, Math.Min(value, selections[slot].stackLimit));
            if (restockLimits[slot] != limit) restockFailures[slot] = null;
            restockLimits[slot] = limit;
            return true;
        }

        public bool CanSelect(ThingDef itemDef) => itemDef != null && itemDef.category == ThingCategory.Item &&
            itemDef.PlayerAcquirable && itemDef.virtualDefParent == null &&
            GetParentStoreSettings().filter.Allows(itemDef) &&
            !GetParentStoreSettings().filter.IsAlwaysDisallowedDueToSpecialFilters(itemDef);

        public bool SetSelection(int slot, ThingDef itemDef)
        {
            if (slot < 0 || slot >= SlotCount || (itemDef != null &&
                (!CanSelect(itemDef) || selections.Where((_, i) => i != slot).Contains(itemDef)))) return false;
            if (selections[slot] != itemDef)
            {
                restockLimits[slot] = itemDef?.stackLimit ?? 0;
                filters[slot] = new StorageShelfFilter();
                restockFailures[slot] = null;
            }
            selections[slot] = itemDef;
            RefreshSettings();
            return true;
        }

        // Accepts describes an existing stack's storage; HaulDestinationEnabled gates new hauling.
        public bool Accepts(Thing item) => item != null && Spawned && item.Spawned &&
            item.Map == Map && item.Position == Position && item.def.category == ThingCategory.Item;

        public StorageSettings GetStoreSettings()
        {
            if (settings == null) RefreshSettings();
            return settings;
        }
        public StorageSettings GetParentStoreSettings() =>
            def.building.fixedStorageSettings ?? StorageSettings.EverStorableFixedSettings();
        public void Notify_SettingsChanged()
        { if (Spawned) Map.listerHaulables.Notify_SlotGroupChanged(slotGroup); }
        public void Notify_ReceivedThing(Thing item) { }
        public void Notify_LostThing(Thing item) { }

        private void RefreshSettings()
        {
            if (settings == null) settings = new StorageSettings(this) { Priority = StoragePriority.Critical };
            settings.filter.SetDisallowAll();
            foreach (ThingDef itemDef in selections) if (itemDef != null) settings.filter.SetAllow(itemDef, true);
            Notify_SettingsChanged();
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned || Faction != Faction.OfPlayer) return;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            StorageShelfSupply.Process(this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            cells = new List<IntVec3> { Position };
            RefreshSettings();
            base.SpawnSetup(map, respawningAfterLoad);
        }
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref selections, "supplySelections", LookMode.Def);
            Scribe_Collections.Look(ref restockLimits, "restockLimits", LookMode.Value);
            Scribe_Collections.Look(ref filters, "supplyFilters", LookMode.Deep);
            StoragePriority priority = GetStoreSettings().Priority;
            Scribe_Values.Look(ref priority, "storagePriority", StoragePriority.Critical);
            if (priority != settings.Priority) settings.Priority = priority;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (selections == null) selections = new List<ThingDef>();
                while (selections.Count < SlotCount) selections.Add(null);
                if (selections.Count > SlotCount) selections.RemoveRange(SlotCount, selections.Count - SlotCount);
                if (restockLimits == null) restockLimits = new List<int>();
                while (restockLimits.Count < SlotCount) restockLimits.Add(0);
                if (restockLimits.Count > SlotCount) restockLimits.RemoveRange(SlotCount, restockLimits.Count - SlotCount);
                if (filters == null) filters = new List<StorageShelfFilter>();
                while (filters.Count < SlotCount) filters.Add(new StorageShelfFilter());
                if (filters.Count > SlotCount) filters.RemoveRange(SlotCount, filters.Count - SlotCount);
                for (int i = 0; i < SlotCount; i++) if (filters[i] == null) filters[i] = new StorageShelfFilter();
                var seen = new HashSet<ThingDef>();
                for (int i = 0; i < SlotCount; i++)
                    if (selections[i] != null && (!CanSelect(selections[i]) || !seen.Add(selections[i]))) selections[i] = null;
                for (int i = 0; i < SlotCount; i++) restockLimits[i] = RestockLimit(i);
                RefreshSettings();
            }
        }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Preserve excess stacks before the native shelf-removal code scatters them.
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
            cells = null;
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Faction == Faction.OfPlayer)
                yield return new Command_Action
                {
                    defaultLabel = "MS_Shelf_Configure".Translate(),
                    defaultDesc = "MS_Shelf_ConfigureDesc".Translate(),
                    icon = def.uiIcon,
                    action = () => Find.WindowStack.Add(new Dialog_StorageSupplyShelf(this))
                };
        }
        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_Shelf_Contents".Translate(slotGroup.HeldThingsCount, SlotCount) +
             "\n" + "MS_Shelf_Use".Translate()).Trim();
    }
}
