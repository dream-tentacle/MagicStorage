using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // No IHaulSource: automated withdrawals use dedicated outlets.
    public sealed class Building_StorageCore : Building, IStoreSettingsParent
    {
        private StorageSettings settings;
        private StorageSettings fixedSettings;
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
            Scribe_Deep.Look(ref settings, "storageSettings", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                var initialized = Settings;
                initialized.owner = this;
                if (initialized.filter == null)
                {
                    initialized.filter = new ThingFilter(Notify_SettingsChanged);
                    initialized.filter.SetDisallowAll();
                }
            }
        }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().MarkDirty();
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
                    defaultLabel = "MS_UI_Open".Translate(),
                    defaultDesc = "MS_UI_OpenDesc".Translate(),
                    icon = def.uiIcon,
                    action = () => Find.WindowStack.Add(new Dialog_StorageInventory(this))
                };
        }
    }
}
