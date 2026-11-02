using System.Collections.Generic;
using Verse;
using RimWorld;

namespace MagicStorage
{
    internal sealed class Dialog_RenameCosmicWorkbench : Dialog_Rename<Building_CosmicWorkbench>
    {
        internal Dialog_RenameCosmicWorkbench(Building_CosmicWorkbench bench) : base(bench) { }
        protected override AcceptanceReport NameIsValid(string name) => !string.IsNullOrWhiteSpace(name);
    }

    public sealed class Building_CosmicWorkbench : Building, IBillGiver, IRenameable
    {
        private List<WorkTypeDef> allowedWorkTypes;
        private RoomRoleDef preferredRoomRole;
        private BillStack billStack;
        private string customName;
        public override string LabelNoCount => customName ?? base.LabelNoCount;
        public string RenamableLabel { get => LabelNoCount; set => customName = value?.Trim(); }
        public string BaseLabel => base.LabelNoCount;
        public string InspectLabel => LabelNoCount;
        internal string SelectionLabel => LabelShortCap + " (" + Position.x + ", " + Position.z + ")";
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        public RoomRoleDef PreferredRoomRole => preferredRoomRole ?? (preferredRoomRole = RoomRoleDefOf.Workshop);

        internal void SetPreferredRoomRole(RoomRoleDef role)
        {
            if (!CosmicWorkbenchRoomRoles.Supports(role) || role == PreferredRoomRole) return;
            preferredRoomRole = role;
            CosmicWorkbenchRoomRoles.Refresh(this);
        }

        // Recipe callbacks receive the actual bench, while orders remain core-owned.
        // Give generic bill readers a stable empty stack and disable vanilla bill work.
        BillStack IBillGiver.BillStack => billStack ?? (billStack = new BillStack(this));
        IEnumerable<IntVec3> IBillGiver.IngredientStackCells => GenAdj.CellsOccupiedBy(this);
        bool IBillGiver.CurrentlyUsableForBills() => false;
        bool IBillGiver.UsableForBillsAfterFueling() => false;
        void IBillGiver.Notify_BillDeleted(Bill bill) { }

        private void EnsureWorkTypes()
        {
            // Null means unconfigured; an empty list is the player's explicit choice.
            if (allowedWorkTypes == null) allowedWorkTypes = new List<WorkTypeDef>(CraftingRecipeCatalog.WorkTypes);
        }
        internal bool AllowsWorkType(WorkTypeDef type)
        {
            EnsureWorkTypes();
            return type != null && allowedWorkTypes.Contains(type);
        }
        internal void SetWorkTypeAllowed(WorkTypeDef type, bool allowed)
        {
            EnsureWorkTypes();
            if (type == null) return;
            if (allowed) { if (!allowedWorkTypes.Contains(type)) allowedWorkTypes.Add(type); }
            else allowedWorkTypes.Remove(type);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Collections.Look(ref allowedWorkTypes, "allowedWorkTypes", LookMode.Def);
            preferredRoomRole = PreferredRoomRole;
            Scribe_Defs.Look(ref preferredRoomRole, "preferredRoomRole");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureWorkTypes();
                allowedWorkTypes.RemoveAll(type => type == null);
                preferredRoomRole = PreferredRoomRole;
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Rename".Translate(),
                icon = TexButton.Rename,
                action = () => Find.WindowStack.Add(new Dialog_RenameCosmicWorkbench(this))
            };
            yield return new Command_Action
            {
                defaultLabel = "MS_Craft_AllowedWorkTypes".Translate(),
                defaultDesc = "MS_Craft_AllowedWorkTypesDesc".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/Inspect"),
                action = () =>
                {
                    if (Spawned && Faction == Faction.OfPlayer)
                        Find.WindowStack.Add(new Dialog_CosmicWorkbenchWorkTypes(this));
                }
            };
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!CanWork) yield break;
            yield return new Command_Action
            {
                defaultLabel = "MS_Craft_Open".Translate(),
                hotKey = StorageKeyBindingDefOf.MS_ProductionOrders,
                defaultDesc = "MS_Craft_OpenDesc".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/CraftingList"),
                action = () =>
                {
                    if (!Spawned || Faction != Faction.OfPlayer) return;
                    Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                    if (CanWork) Find.WindowStack.Add(new Dialog_CosmicCrafting(Network.Core, this));
                }
            };
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        { base.SpawnSetup(map, respawningAfterLoad); map.GetComponent<MapComponent_CosmicCrafting>().Register(this); }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        { Map.GetComponent<MapComponent_CosmicCrafting>().Unregister(this); base.DeSpawn(mode); }
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }
    }
}
