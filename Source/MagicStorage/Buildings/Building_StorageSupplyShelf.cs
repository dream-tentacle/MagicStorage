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
        public int SlotCount
        {
            get { EnsureSlotCapacity(); return selections.Count; }
        }
        // Session-only clipboard; never serialized or tied to a source building.
        private static ShelfPreset copiedPreset;

        private sealed class ShelfPreset
        {
            internal readonly List<ThingDef> Selections;
            internal readonly List<int> RestockLimits;
            internal readonly List<StorageShelfFilter> Filters;
            internal readonly StoragePriority Priority;
            internal readonly bool FilterMode;
            internal readonly ThingFilter SharedFilter = new ThingFilter();
            internal readonly List<int> FilterLimits;

            internal ShelfPreset(Building_StorageSupplyShelf shelf)
            {
                shelf.EnsureSlotCapacity();
                Selections = new List<ThingDef>(shelf.selections);
                RestockLimits = Enumerable.Range(0, shelf.SlotCount).Select(shelf.RestockLimit).ToList();
                Filters = shelf.filters.Select(filter => filter.Clone()).ToList();
                Priority = shelf.GetStoreSettings().Priority;
                FilterMode = shelf.FilterMode;
                SharedFilter.CopyAllowancesFrom(shelf.SharedFilter);
                FilterLimits = new List<int>(shelf.filterLimits);
            }
        }

        private void CopyPreset() => copiedPreset = new ShelfPreset(this);

        private void PastePreset()
        {
            if (copiedPreset == null) return;
            if (filterMode != copiedPreset.FilterMode && !TrySetFilterMode(copiedPreset.FilterMode)) return;
            // Replace all slots together so swapped selections cannot conflict.
            selections = new List<ThingDef>(copiedPreset.Selections);
            restockLimits = new List<int>(copiedPreset.RestockLimits);
            filters = copiedPreset.Filters.Select(filter => filter.Clone()).ToList();
            SharedFilter.CopyAllowancesFrom(copiedPreset.SharedFilter);
            filterLimits = new List<int>(copiedPreset.FilterLimits);
            EnsureSlotCapacity();
            GetStoreSettings().Priority = copiedPreset.Priority;
            ClearRestockFailures();
            RefreshSettings();
        }

        private StorageRefreshTimer refreshTimer;
        private List<ThingDef> selections = new List<ThingDef>();
        private List<int> restockLimits = new List<int>();
        private List<StorageShelfFilter> filters = new List<StorageShelfFilter>();
        private readonly SlotGroup slotGroup;
        private List<IntVec3> cells;
        private StorageSettings settings;
        private bool filterMode;
        private ThingFilter sharedFilter;
        private List<int> filterLimits = new List<int>();
        private List<Thing> slotStocks = new List<Thing>();

        private void EnsureSlotCapacity()
        {
            int count = Math.Max(1, def?.building?.maxItemsInCell ?? 3);
            ResizeSlots(ref selections, count, () => null);
            ResizeSlots(ref restockLimits, count, () => 0);
            ResizeSlots(ref filters, count, () => new StorageShelfFilter());
            ResizeSlots(ref filterLimits, count, () => 75);
            ResizeSlots(ref slotStocks, count, () => null);
            if (restockFailures.Length != count) Array.Resize(ref restockFailures, count);
        }

        private static void ResizeSlots<T>(ref List<T> list, int count, Func<T> create)
        {
            if (list == null) list = new List<T>();
            while (list.Count < count) list.Add(create());
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }

        public bool FilterMode => filterMode;
        public ThingFilter SharedFilter => sharedFilter ?? (sharedFilter = new ThingFilter(RefreshSettings));
        public int FilterLimit(int slot) { EnsureSlotCapacity(); return Math.Max(0, filterLimits[slot]); }
        public void SetFilterLimit(int slot, int value)
        { if (slot >= 0 && slot < SlotCount) filterLimits[slot] = Math.Max(0, value); }
        internal int SupplyLimit(int slot, ThingDef itemDef) =>
            filterMode ? Math.Min(FilterLimit(slot), itemDef?.stackLimit ?? 0) : RestockLimit(slot);

        // Keep real stack references: identical definitions may occupy independent positions.
        public Thing StockAt(int slot)
        { EnsureSlotCapacity(); return IsShelfStock(slotStocks[slot]) ? slotStocks[slot] : null; }
        internal void SetStock(int slot, Thing item) { slotStocks[slot] = item; }
        private bool IsShelfStock(Thing item) => item != null && !item.Destroyed && item.Spawned &&
            Spawned && item.Map == Map && item.Position == Position;
        internal void ResolveStockSlots(List<Thing> items)
        {
            var assigned = new HashSet<Thing>();
            for (int i = 0; i < SlotCount; i++)
                if (!IsShelfStock(slotStocks[i]) || !assigned.Add(slotStocks[i])) slotStocks[i] = null;
            for (int i = 0; i < SlotCount; i++)
                if (slotStocks[i] == null)
                {
                    Thing item = items.Find(t => !assigned.Contains(t) && AllowsStock(i, t));
                    if (item == null && !filterMode)
                        item = items.Find(t => !assigned.Contains(t) && t.def == selections[i]);
                    if (item != null) { slotStocks[i] = item; assigned.Add(item); }
                }
        }
        internal int StockSlot(Thing item) => slotStocks.IndexOf(item);

        public bool TrySetFilterMode(bool value)
        {
            if (value == filterMode) return true;
            if (!StorageShelfSupply.TryReturnAll(this))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("MS_Shelf_ModeBlocked".Translate(),
                    title: "MS_Shelf_ModeBlockedTitle".Translate()));
                return false;
            }
            filterMode = value;
            slotStocks = Enumerable.Repeat<Thing>(null, SlotCount).ToList();
            ClearRestockFailures();
            RefreshSettings();
            return true;
        }

        private void SetFoodFilter()
        {
            if (!TrySetFilterMode(true)) return;
            var foodFilter = new ThingFilter();
            foodFilter.SetAllow(ThingCategoryDefOf.Foods, true);
            SharedFilter.CopyAllowancesFrom(foodFilter);
        }

        private StorageShelfRestockFailure[] restockFailures = new StorageShelfRestockFailure[0];
        internal StorageShelfRestockFailure RestockFailure(int slot)
        { EnsureSlotCapacity(); return restockFailures[slot]?.IsCurrent == true ? restockFailures[slot] : null; }
        internal void ClearRestockFailures() { EnsureSlotCapacity(); Array.Clear(restockFailures, 0, restockFailures.Length); }
        internal void RecordRestockFailure(int slot, int current, StorageShelfFailureReason reason)
        {
            if (!filterMode && filters[slot].WarnWhenRestockFails && reason != StorageShelfFailureReason.None && current < RestockLimit(slot))
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
        public ThingDef SelectedDef(int slot) { EnsureSlotCapacity(); return selections[slot]; }
        public StorageShelfFilter SlotFilter(int slot) { EnsureSlotCapacity(); return filters[slot]; }
        public bool AllowsStock(int slot, Thing item)
        {
            EnsureSlotCapacity();
            return item != null && GetParentStoreSettings().AllowedToAccept(item) &&
                (filterMode ? SharedFilter.Allows(item) : item.def == selections[slot] && filters[slot].Allows(item));
        }
        public int RestockLimit(int slot)
        { EnsureSlotCapacity(); return Math.Max(0, Math.Min(restockLimits[slot], selections[slot]?.stackLimit ?? 0)); }
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
            if (slot < 0 || slot >= SlotCount || (itemDef != null && !CanSelect(itemDef))) return false;
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
        {
            if (!Spawned) return;
            Map.listerHaulables.Notify_SlotGroupChanged(slotGroup);
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
        }
        public void Notify_ReceivedThing(Thing item) { }
        public void Notify_LostThing(Thing item) { }

        private void RefreshSettings()
        {
            EnsureSlotCapacity();
            if (settings == null) settings = new StorageSettings(this) { Priority = StoragePriority.Critical };
            settings.filter.SetDisallowAll();
            if (filterMode) settings.filter.CopyAllowancesFrom(SharedFilter);
            else foreach (ThingDef itemDef in selections) if (itemDef != null) settings.filter.SetAllow(itemDef, true);
            Notify_SettingsChanged();
        }

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && refreshTimer.IsDue(StorageRefreshTask.ShelfRestock, Find.TickManager.TicksGame)) RefreshSupply();
        }

        internal void RefreshSupply()
        {
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
            Scribe_Values.Look(ref filterMode, "filterMode", false);
            if (sharedFilter == null) sharedFilter = new ThingFilter(RefreshSettings);
            Scribe_Deep.Look(ref sharedFilter, "sharedSupplyFilter", (Action)RefreshSettings);
            Scribe_Collections.Look(ref filterLimits, "filterRestockLimits", LookMode.Value);
            Scribe_Collections.Look(ref slotStocks, "supplySlotStocks", LookMode.Reference);
            StoragePriority priority = GetStoreSettings().Priority;
            Scribe_Values.Look(ref priority, "storagePriority", StoragePriority.Critical);
            if (priority != settings.Priority) settings.Priority = priority;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureSlotCapacity();
                for (int i = 0; i < SlotCount; i++)
                {
                    if (filters[i] == null) filters[i] = new StorageShelfFilter();
                    if (selections[i] != null && !CanSelect(selections[i])) selections[i] = null;
                    restockLimits[i] = RestockLimit(i);
                    filterLimits[i] = Math.Max(0, filterLimits[i]);
                }
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
            {
                yield return new Command_Action
                {
                    defaultLabel = "MS_Shelf_Configure".Translate(),
                    defaultDesc = "MS_Shelf_ConfigureDesc".Translate(SlotCount),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/ConfigureShelf"),
                    action = () =>
                    {
                        if (filterMode) Find.WindowStack.Add(new Dialog_StorageShelfSharedFilter(this));
                        else Find.WindowStack.Add(new Dialog_StorageSupplyShelf(this));
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "MS_Shelf_CopyPreset".Translate(),
                    defaultDesc = "MS_Shelf_CopyPresetDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/CopySettings"),
                    alsoClickIfOtherInGroupClicked = false,
                    action = CopyPreset
                };
                var paste = new Command_Action
                {
                    defaultLabel = "MS_Shelf_PastePreset".Translate(),
                    defaultDesc = "MS_Shelf_PastePresetDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/PasteSettings"),
                    alsoClickIfOtherInGroupClicked = true,
                    action = PastePreset
                };
                if (copiedPreset == null) paste.Disable("MS_Shelf_NeedCopiedPreset".Translate());
                yield return paste;
                yield return new Command_Toggle
                {
                    defaultLabel = "MS_Shelf_FilterMode".Translate(),
                    defaultDesc = "MS_Shelf_FilterModeDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/DesirePower"),
                    isActive = () => filterMode,
                    toggleAction = () => TrySetFilterMode(!filterMode)
                };
                yield return new Command_Action
                {
                    defaultLabel = "MS_Shelf_QuickFood".Translate(),
                    defaultDesc = "MS_Shelf_QuickFoodDesc".Translate(),
                    icon = DefDatabase<ThingDef>.GetNamed("MealLavish").uiIcon,
                    action = SetFoodFilter
                };
                if (Spawned && Find.Selector.NumSelected == 1)
                {
                    foreach (Thing item in slotGroup.HeldThings)
                    {
                        Gizmo select = ContainingSelectionUtility.CreateSelectStorageGizmo(
                            "CommandSelectStoredThing".Translate(item), "CommandSelectStoredThingDesc".Translate(),
                            item, item, groupable: false);
                        if (select != null) yield return select;
                    }
                }
            }
        }
        public override string GetInspectString() =>
            (base.GetInspectString() + "\n" + "MS_Shelf_Contents".Translate(slotGroup.HeldThingsCount, SlotCount) +
             "\n" + "MS_Shelf_Use".Translate()).Trim();
    }
}
