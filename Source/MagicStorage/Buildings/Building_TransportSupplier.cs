using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class Building_TransportSupplier : Building, IThingHolder, IThingHolderTickable, ISuspendableThingHolder
    {
        private StorageRefreshTimer refreshTimer;
        public const int SlotCount = 3;
        private bool directSupply;
        private bool genericContainerSupply;
        private Thing genericContainerTarget;
        private int genericContainerIndex = -1;
        private List<ThingDef> selections = new List<ThingDef> { null, null, null };
        public bool DirectSupply => directSupply;
        public bool GenericContainerSupply => genericContainerSupply;
        public ThingDef SelectedDef(int slot) => selections[slot];
        public bool CanSelect(ThingDef itemDef) => itemDef != null && itemDef.category == ThingCategory.Item &&
            itemDef.PlayerAcquirable && itemDef.virtualDefParent == null &&
            StorageSettings.EverStorableFixedSettings().filter.Allows(itemDef) &&
            !StorageSettings.EverStorableFixedSettings().filter.IsAlwaysDisallowedDueToSpecialFilters(itemDef);
        public bool SetSelection(int slot, ThingDef itemDef)
        {
            if (slot < 0 || slot >= SlotCount || (itemDef != null &&
                (!CanSelect(itemDef) || selections.Where((_, i) => i != slot).Contains(itemDef)))) return false;
            selections[slot] = itemDef;
            return true;
        }
        internal bool TrySwitchMode()
        {
            if (Contents.Count != 0) return false;
            directSupply = !directSupply;
            return true;
        }
        private ThingOwner<Thing> contents;
        internal ThingOwner<Thing> Contents => contents ?? (contents = NewContainer());
        private ThingOwner<Thing> NewContainer() => new ThingOwner<Thing>(this, false, LookMode.Deep) { dontTickContents = true };
        public bool ShouldTickContents => false;
        public bool IsContentsSuspended => true;
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true;
        public IntVec3 FrontCell => Position + Rotation.FacingCell;
        public ThingOwner GetDirectlyHeldThings() => Contents;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, Contents);

        internal CompTransporter FrontTransporter()
        {
            if (!Spawned || !FrontCell.InBounds(Map)) return null;
            foreach (Thing thing in FrontCell.GetThingList(Map))
            {
                if (thing == this || !thing.Spawned || thing.Destroyed || !thing.OccupiedRect().Contains(FrontCell)) continue;
                CompTransporter transporter = thing.TryGetComp<CompTransporter>();
                if (transporter != null) return transporter;
            }
            return null;
        }

        internal float TransferCapacity
        {
            get
            {
                CompTransporter target = FrontTransporter();
                return Math.Max(150f, target == null ? 150f : StorageTransportGroups.Capacity(target));
            }
        }
        internal double StoredMass => StorageTransportExchange.ContainerMass(Contents);

        internal CompRefuelable FrontRefuelable()
        {
            if (!Spawned || !FrontCell.InBounds(Map)) return null;
            foreach (Thing thing in FrontCell.GetThingList(Map))
            {
                if (thing == this || !thing.Spawned || thing.Destroyed || thing.Faction != Faction ||
                    !thing.OccupiedRect().Contains(FrontCell)) continue;
                CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
                if (refuelable != null) return refuelable;
            }
            return null;
        }

        private static List<ThingOwner> GenericContainers(Thing target)
        {
            var containers = new List<ThingOwner>();
            AddGenericContainer(target as IThingHolder, containers);
            if (target is ThingWithComps withComps)
                foreach (ThingComp comp in withComps.AllComps)
                    AddGenericContainer(comp as IThingHolder, containers);
            return containers;
        }

        private static void AddGenericContainer(IThingHolder holder, List<ThingOwner> containers)
        {
            ThingOwner container = holder?.GetDirectlyHeldThings();
            if (container != null && !containers.Contains(container)) containers.Add(container);
        }

        internal Thing FrontGenericTarget(out List<ThingOwner> containers)
        {
            containers = null;
            if (!genericContainerSupply || !Spawned || !FrontCell.InBounds(Map)) return null;
            foreach (Thing thing in FrontCell.GetThingList(Map))
            {
                if (thing == this || !thing.Spawned || thing.Destroyed || thing.Faction != Faction ||
                    thing.def.category != ThingCategory.Building || !thing.OccupiedRect().Contains(FrontCell)) continue;
                var candidates = GenericContainers(thing);
                if (candidates.Count == 0) continue;
                containers = candidates;
                return thing;
            }
            return null;
        }

        private int SelectedGenericIndex(Thing target, List<ThingOwner> containers)
        {
            if (target == genericContainerTarget && genericContainerIndex >= 0 && genericContainerIndex < containers.Count)
                return genericContainerIndex;
            return containers.Count == 1 ? 0 : -1;
        }

        internal bool SelectGenericContainer(Thing target, int index)
        {
            if (FrontGenericTarget(out var containers) != target || containers == null || index < 0 || index >= containers.Count)
                return false;
            genericContainerTarget = target;
            genericContainerIndex = index;
            return true;
        }

        private static string GenericContainerLabel(ThingOwner container, int index)
        {
            string contents = container.Count == 0 ? "MS_Transport_GenericEmpty".Translate().ToString() :
                string.Join(", ", container.Take(3).Select(item => item.LabelCap + " x" + item.stackCount));
            if (container.Count > 3) contents += ", ...";
            return "MS_Transport_GenericContainer".Translate(index + 1, contents);
        }

        private void ChooseGenericContainer()
        {
            Thing target = FrontGenericTarget(out var containers);
            if (target == null) return;
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < containers.Count; i++)
            {
                int index = i;
                ThingOwner container = containers[i];
                options.Add(new FloatMenuOption(GenericContainerLabel(container, index), () =>
                {
                    if (FrontGenericTarget(out var current) == target && index < current.Count && current[index] == container)
                        SelectGenericContainer(target, index);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private bool GenericTargetValid(Thing target, ThingOwner container)
        {
            if (!genericContainerSupply || !Spawned || !target.Spawned || target.Destroyed || target.Map != Map ||
                target.Faction != Faction || !target.OccupiedRect().Contains(FrontCell)) return false;
            var containers = GenericContainers(target);
            int index = SelectedGenericIndex(target, containers);
            return index >= 0 && containers[index] == container;
        }

        private void SupplyGenericContainer()
        {
            Thing target = FrontGenericTarget(out var containers);
            if (target == null) return;
            int index = SelectedGenericIndex(target, containers);
            if (index < 0) return;
            ThingOwner container = containers[index];
            if (!directSupply)
            {
                foreach (Thing item in new List<Thing>(Contents))
                {
                    if (!GenericTargetValid(target, container)) return;
                    if (!item.Destroyed && Contents.Contains(item))
                        StorageTransfer.Move(Contents, container, item, item.stackCount, Map, Position);
                }
                return;
            }
            StorageNetwork network = Network;
            var stacks = new List<Thing>();
            foreach (ThingDef itemDef in selections)
            {
                if (itemDef == null) continue;
                stacks.Clear(); network.GetStacks(itemDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (!CanWork || Network != network || !GenericTargetValid(target, container)) return;
                    int count = network.AvailableToWithdraw(item);
                    if (count > 0) network.TryTransferTo(item, count, container);
                }
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && Faction == Faction.OfPlayer && refreshTimer.IsDue(StorageRefreshTask.TransportSupply, Find.TickManager.TicksGame))
                SupplyTransporter();
        }

        internal void SupplyTransporter(bool immediate = false)
        {
            if (!Spawned || Faction != Faction.OfPlayer) return;
            if (directSupply)
            {
                Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                if (!CanWork) return;
            }
            else if (Contents.Count == 0) return;
            CompTransporter transporter = FrontTransporter();
            if (transporter == null)
            {
                CompRefuelable fuel = FrontRefuelable();
                if (fuel != null) SupplyFuel(fuel, immediate);
                else SupplyGenericContainer();
                return;
            }
            if (directSupply) { SupplyFromNetwork(transporter); return; }
            // ThingOwner sends the native added/merged notifications and invalidates mass caches.
            foreach (Thing item in new List<Thing>(Contents))
            {
                if (!transporter.parent.Spawned || transporter.parent.Destroyed) break;
                int count = StorageTransportExchange.CountThatFits(item, StorageTransportGroups.FreeMass(transporter));
                if (count > 0)
                    StorageTransfer.Move(Contents, transporter.innerContainer, item, count, Map, Position);
            }
        }

        private void SupplyFromNetwork(CompTransporter transporter)
        {
            StorageNetwork network = Network;
            var stacks = new List<Thing>();
            // Exhaust each selected type before considering the next priority.
            foreach (ThingDef itemDef in selections)
            {
                if (itemDef == null) continue;
                stacks.Clear(); network.GetStacks(itemDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (!CanWork || Network != network || !transporter.parent.Spawned || transporter.parent.Destroyed) return;
                    int count = Math.Min(network.AvailableToWithdraw(item),
                        StorageTransportExchange.CountThatFits(item, StorageTransportGroups.FreeMass(transporter)));
                    if (count > 0) network.TryTransferTo(item, count, transporter.innerContainer);
                }
            }
        }

        private void SupplyFuel(CompRefuelable target, bool immediate)
        {
            if (!StorageFuelSupply.NeedsFuel(target, immediate)) return;
            int needed = target.GetFuelCountToFullyRefuel();
            if (!directSupply)
            {
                var fuel = Contents.Where(t => !t.Destroyed && target.Props.fuelFilter.Allows(t)).ToList();
                if (fuel.Count == 0 || (target.Props.atomicFueling && fuel.Sum(t => (long)t.stackCount) < needed)) return;
                target.Refuel(fuel);
                return;
            }
            StorageFuelSupply.Deliver(this, Network, target, selections, immediate);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "transportSupplyContents", this);
            Scribe_Values.Look(ref directSupply, "transportDirectSupply", false);
            Scribe_Values.Look(ref genericContainerSupply, "transportGenericContainerSupply", false);
            Scribe_References.Look(ref genericContainerTarget, "transportGenericContainerTarget");
            Scribe_Values.Look(ref genericContainerIndex, "transportGenericContainerIndex", -1);
            Scribe_Collections.Look(ref selections, "transportSupplySelections", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Contents.dontTickContents = true;
                if (selections == null) selections = new List<ThingDef>();
                while (selections.Count < SlotCount) selections.Add(null);
                if (selections.Count > SlotCount) selections.RemoveRange(SlotCount, selections.Count - SlotCount);
                var seen = new HashSet<ThingDef>();
                for (int i = 0; i < SlotCount; i++)
                    if (selections[i] != null && (!CanSelect(selections[i]) || !seen.Add(selections[i]))) selections[i] = null;
            }
        }

        public override void SetFaction(Faction faction, Pawn recruiter = null)
        {
            base.SetFaction(faction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = MapHeld;
            IntVec3 cell = PositionHeld;
            StorageRecoveryBatch batch = null;
            if (Contents.Count > 0)
            {
                if (map == null)
                {
                    Log.Warning("[MagicStorage] Refused to destroy a loaded transport supplier without a map.");
                    return;
                }
                batch = map.GetComponent<MapComponent_StorageNetworks>().CreateRecovery(cell);
                while (Contents.Count > 0)
                {
                    Thing item = Contents[Contents.Count - 1];
                    if (StorageTransfer.Move(Contents, batch.Contents, item, item.stackCount, map, cell, false) == 0)
                    {
                        map.GetComponent<MapComponent_StorageNetworks>().ReleaseRecovery(batch);
                        Log.Error("[MagicStorage] Transport supplier destruction stopped because its contents could not be released.");
                        return;
                    }
                }
            }
            base.Destroy(mode);
            if (batch != null) map.GetComponent<MapComponent_StorageNetworks>().ReleaseRecovery(batch);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Spawned && Faction == Faction.OfPlayer)
            {
                var toggle = new Command_SupplyMode(this)
                {
                    defaultLabel = "MS_Transport_SwitchMode".Translate(),
                    defaultDesc = "MS_Transport_SwitchModeDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/SwitchSupplyMode"),
                    action = () => TrySwitchMode()
                };
                if (Contents.Count > 0) toggle.Disable("MS_Transport_ModeBlocked".Translate());
                yield return toggle;
                yield return new Command_Toggle
                {
                    defaultLabel = "MS_Transport_GenericSupply".Translate(),
                    defaultDesc = "MS_Transport_GenericSupplyDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/ConfigureSupply"),
                    isActive = () => genericContainerSupply,
                    toggleAction = () => genericContainerSupply = !genericContainerSupply
                };
                if (genericContainerSupply)
                {
                    var choose = new Command_Action
                    {
                        defaultLabel = "MS_Transport_ChooseContainer".Translate(),
                        defaultDesc = "MS_Transport_ChooseContainerDesc".Translate(),
                        icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/TransferItems"),
                        action = ChooseGenericContainer
                    };
                    if (FrontTransporter() != null || FrontRefuelable() != null)
                        choose.Disable("MS_Transport_SpecializedTarget".Translate());
                    else if (FrontGenericTarget(out _) == null) choose.Disable("MS_Transport_NoGenericTarget".Translate());
                    yield return choose;
                }
                yield return new Command_Action
                {
                    defaultLabel = (directSupply ? "MS_Transport_Configure" : "MS_Transport_Open").Translate(),
                    defaultDesc = (directSupply ? "MS_Transport_DirectHint" : "MS_Transport_OpenDesc").Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get(directSupply
                        ? "UI/Gizmos/MagicStorage/ConfigureSupply"
                        : "UI/Gizmos/MagicStorage/TransferItems"),
                    action = () =>
                    {
                        if (directSupply) Find.WindowStack.Add(new Dialog_SupplierSupply(this));
                        else Find.WindowStack.Add(new Dialog_TransportSupplier(this));
                    }
                };
                var immediate = new Command_Action
                {
                    defaultLabel = "MS_FuelSupply_Immediate".Translate(),
                    defaultDesc = "MS_Transport_ImmediateDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/SupplyNow"),
                    action = () => SupplyTransporter(true)
                };
                if (directSupply && !CanWork) immediate.Disable(StorageNetwork.StatusLabel(Network));
                else if (!directSupply && Contents.Count == 0) immediate.Disable("MS_Transport_ImmediateEmpty".Translate());
                else if (FrontTransporter() == null && FrontRefuelable() == null)
                {
                    Thing generic = FrontGenericTarget(out var containers);
                    if (generic == null) immediate.Disable((genericContainerSupply ? "MS_Transport_NoGenericTarget" : "MS_Transport_NoTarget").Translate());
                    else if (SelectedGenericIndex(generic, containers) < 0) immediate.Disable("MS_Transport_SelectContainerFirst".Translate());
                }
                yield return immediate;
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (Spawned && FrontCell.InBounds(Map)) GenDraw.DrawFieldEdges(new List<IntVec3> { FrontCell });
        }

        public override string GetInspectString()
        {
            CompTransporter target = FrontTransporter();
            string status = target == null ? "MS_Transport_NoTarget".Translate().ToString() :
                "MS_Transport_Target".Translate(target.parent.LabelCap, target.MassUsage.ToString("0.##"), target.MassCapacity.ToString("0.##")).ToString();
            if (target != null && StorageTransportGroups.TryGetMass(target, out float usage, out float capacity))
                status += "\n" + "MS_Transport_GroupMass".Translate(usage.ToString("0.##"), capacity.ToString("0.##"));
            if (target == null)
            {
                CompRefuelable fuel = FrontRefuelable();
                if (fuel != null) status = "MS_Transport_FuelTarget".Translate(fuel.parent.LabelCap,
                    fuel.Fuel.ToString("0.##"), fuel.TargetFuelLevel.ToString("0.##"));
                else if (genericContainerSupply)
                {
                    Thing generic = FrontGenericTarget(out var containers);
                    status = "MS_Transport_NoGenericTarget".Translate();
                    if (generic != null)
                    {
                        int index = SelectedGenericIndex(generic, containers);
                        status = index < 0 ? "MS_Transport_SelectContainerFirst".Translate().ToString() :
                            "MS_Transport_GenericTarget".Translate(generic.LabelCap, GenericContainerLabel(containers[index], index)).ToString();
                    }
                }
            }
            string mode = (directSupply ? "MS_Transport_DirectMode" : "MS_Transport_ManualMode").Translate();
            return (base.GetInspectString() + "\n" + mode + "\n" +
                "MS_Transport_Contents".Translate(Contents.Count, StoredMass.ToString("0.##")) + "\n" + status).Trim();
        }
    }

    internal sealed class Command_SupplyMode : Command_Action
    {
        private readonly Building_TransportSupplier supplier;

        internal Command_SupplyMode(Building_TransportSupplier supplier) { this.supplier = supplier; }

        public override void DrawIcon(UnityEngine.Rect rect, UnityEngine.Material buttonMat, GizmoRenderParms parms)
        {
            iconAngle = supplier.DirectSupply ? (UnityEngine.Time.realtimeSinceStartup % 12f) * 30f : 0f;
            base.DrawIcon(rect, buttonMat, parms);
        }
    }

    public sealed class PlaceWorker_TransportSupplier : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, UnityEngine.Color ghostCol, Thing thing = null)
        { GenDraw.DrawFieldEdges(new List<IntVec3> { center + rot.FacingCell }); }
    }
}
