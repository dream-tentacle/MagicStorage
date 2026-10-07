using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // No IHaulSource: supply shelves place real items outside the unit containers.
    public sealed class Building_StorageCore : Building, IStoreSettingsParent, IThingHolder, IThingHolderTickable, ISuspendableThingHolder
    {
        private StorageSettings settings;
        private StorageSettings fixedSettings;
        private CraftingOrderList crafting = new CraftingOrderList();
        private ThingOwner<Thing> directlyHeldThings;
        public CraftingOrderList Crafting => crafting;
        public bool ShouldTickContents => false;
        public bool IsContentsSuspended => true;
        // Vanilla selection enumerates this without a null check. Materials belong to
        // child batches, so expose a stable empty direct container for this building.
        public ThingOwner GetDirectlyHeldThings() => directlyHeldThings ??
            (directlyHeldThings = new ThingOwner<Thing>(this, false, LookMode.Deep) { dontTickContents = true });
        public void GetChildHolders(List<IThingHolder> children)
        { foreach (var order in Crafting.Orders) if (order.Batch != null) children.Add(order.Batch); }
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Network != null && Network.CanWork;
        public bool StorageTabVisible => true;

        public StorageSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = new StorageSettings(this);
                    settings.filter.SetDisallowAll();
                }
                return settings;
            }
        }

        public StorageSettings GetStoreSettings() => Settings;
        public StorageSettings GetParentStoreSettings()
        {
            if (fixedSettings == null)
            {
                fixedSettings = new StorageSettings();
                fixedSettings.filter = ThingFilter.CreateOnlyEverStorableThingFilter();
            }
            return fixedSettings;
        }

        public void Notify_SettingsChanged()
        {
            // Existing inventory is retained; receivers use this shared filter.
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyReceiverSettingsChanged();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref crafting, "cosmicCrafting");
            Scribe_Deep.Look(ref settings, "storageSettings", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (crafting == null) crafting = new CraftingOrderList();
                var initialized = Settings;
                initialized.owner = this;
                if (initialized.filter == null)
                {
                    initialized.filter = new ThingFilter(Notify_SettingsChanged);
                    initialized.filter.SetDisallowAll();
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        { base.SpawnSetup(map, respawningAfterLoad); map.GetComponent<MapComponent_CosmicCrafting>().Register(this); }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        { Map.GetComponent<MapComponent_CosmicCrafting>().Unregister(this); base.DeSpawn(mode); }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!Crafting.CancelAll(false))
            { Log.Error("[MagicStorage] Core destruction stopped because crafting items could not be released."); return; }
            base.Destroy(mode);
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (Network != null && Network.Status != StorageNetworkStatus.Rebuilding)
                text += "\n" + "MS_NetworkCapacity".Translate(Network.UnitCount, Network.UsedSlots, Network.SlotCapacity);
            return (text + "\n" + "MS_ManualWithdrawalOnly".Translate()).Trim();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Spawned && Faction == Faction.OfPlayer)
                yield return new Command_Action
                {
                    defaultLabel = "MS_Craft_Open".Translate(),
                    defaultDesc = "MS_Craft_OpenDesc".Translate(),
                    icon = def.uiIcon,
                    action = () => Find.WindowStack.Add(new Dialog_CosmicCrafting(this))
                };
            if (Spawned && Faction == Faction.OfPlayer)
                yield return new Command_Action
                {
                    defaultLabel = "MS_UI_Open".Translate(),
                    defaultDesc = "MS_UI_OpenDesc".Translate(),
                    icon = def.uiIcon,
                    action = () => Find.WindowStack.Add(new Dialog_StorageInventory(this))
                };
        }
    }
}
