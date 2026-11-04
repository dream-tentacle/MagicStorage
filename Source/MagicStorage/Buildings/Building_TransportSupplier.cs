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
        public const int DefaultTargetLimit = 75;
        private bool directSupply;
        private bool genericContainerSupply;
        private Thing genericContainerTarget;
        private int genericContainerIndex = -1;
        private List<ThingDef> selections = new List<ThingDef> { null, null, null };
        private List<int> targetLimits = new List<int> { DefaultTargetLimit, DefaultTargetLimit, DefaultTargetLimit };
        // Session-only snapshot, independent of both the source and pasted suppliers.
        private static SupplierPreset copiedPreset;

        private sealed class SupplierPreset
        {
            internal readonly bool DirectSupply;
            internal readonly bool GenericContainerSupply;
            internal readonly List<ThingDef> Selections;
            internal readonly List<int> TargetLimits;

            internal SupplierPreset(Building_TransportSupplier supplier)
            {
                DirectSupply = supplier.directSupply;
                GenericContainerSupply = supplier.genericContainerSupply;
                Selections = new List<ThingDef>(supplier.selections);
                TargetLimits = new List<int>(supplier.targetLimits);
            }
        }

        private void CopyPreset() => copiedPreset = new SupplierPreset(this);

        private void PastePreset()
        {
            SupplierPreset preset = copiedPreset;
            if (preset == null) return;
            if (directSupply != preset.DirectSupply && !TrySwitchMode())
            {
                Find.WindowStack.Add(new Dialog_MessageBox("MS_Transport_ModeBlocked".Translate(),
                    title: "MS_Transport_PastePreset".Translate()));
                return;
            }
            // Replace all slots together so swapped selections cannot conflict.
            selections = new List<ThingDef>(preset.Selections);
            targetLimits = new List<int>(preset.TargetLimits);
            genericContainerSupply = preset.GenericContainerSupply;
        }

        public int TargetLimit(int slot) => targetLimits[slot];
        public void SetTargetLimit(int slot, int value)
        {
            if (slot >= 0 && slot < SlotCount) targetLimits[slot] = Math.Max(0, value);
        }

        // Recount the real destination before each transfer, including stock from other suppliers.
        // Subtract with a wide accumulator so large modded stacks cannot overflow into free space.
        internal int TargetDeficit(ThingOwner target, int slot)
        {
            long remaining = TargetLimit(slot);
            if (remaining <= 0 || selections[slot] == null) return 0;
            foreach (Thing existing in target)
            {
                if (existing.Destroyed || existing.def != selections[slot]) continue;
                remaining -= existing.stackCount;
                if (remaining <= 0) return 0;
            }
            return (int)remaining;
        }

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
                if (target != null) return Math.Max(150f, StorageTransportGroups.Capacity(target));
                Pawn vehicle = VehicleFrameworkCompatibility.FrontVehicle(this);
                return vehicle == null ? 150f : Math.Max(150f, VehicleFrameworkCompatibility.Capacity(vehicle));
            }
        }
        internal double StoredMass => StorageTransportExchange.ContainerMass(Contents);

        internal List<CompRefuelable> FrontRefuelables()
        {
            var result = new List<CompRefuelable>();
            if (!Spawned || !FrontCell.InBounds(Map)) return result;
            foreach (Thing thing in FrontCell.GetThingList(Map))
            {
                if (thing == this || !thing.Spawned || thing.Destroyed || thing.Faction != Faction ||
                    !thing.OccupiedRect().Contains(FrontCell)) continue;
                result.AddRange(StorageFuelSupply.FuelComponents(thing));
            }
            return result;
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
            for (int slot = 0; slot < SlotCount; slot++)
            {
                ThingDef itemDef = selections[slot];
                if (itemDef == null || TargetLimit(slot) == 0) continue;
                stacks.Clear(); network.GetStacks(itemDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (!CanWork || Network != network || !GenericTargetValid(target, container)) return;
                    int deficit = TargetDeficit(container, slot);
                    if (deficit == 0) break;
                    int count = Math.Min(network.AvailableToWithdraw(item), deficit);
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
                Pawn vehicle = VehicleFrameworkCompatibility.FrontVehicle(this);
                if (vehicle != null) { VehicleFrameworkCompatibility.Supply(this, vehicle); return; }
                List<CompRefuelable> fuels = FrontRefuelables();
                if (fuels.Count > 0)
                {
                    StorageNetwork network = Network;
                    foreach (CompRefuelable fuel in fuels)
                    {
                        if (!Spawned || Faction != Faction.OfPlayer ||
                            (directSupply && (!CanWork || Network != network))) break;
                        if (fuel.parent.Map != Map || fuel.parent.Faction != Faction ||
                            !fuel.parent.OccupiedRect().Contains(FrontCell)) continue;
                        SupplyFuel(fuel, immediate);
                    }
                }
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
            // Fill each type to its target inventory limit before the next priority.
            for (int slot = 0; slot < SlotCount; slot++)
            {
                ThingDef itemDef = selections[slot];
                if (itemDef == null || TargetLimit(slot) == 0) continue;
                stacks.Clear(); network.GetStacks(itemDef, stacks);
                foreach (Thing item in stacks)
                {
                    if (!CanWork || Network != network || !transporter.parent.Spawned || transporter.parent.Destroyed) return;
                    int deficit = TargetDeficit(transporter.innerContainer, slot);
                    if (deficit == 0) break;
                    int count = Math.Min(deficit, Math.Min(network.AvailableToWithdraw(item),
                        StorageTransportExchange.CountThatFits(item, StorageTransportGroups.FreeMass(transporter))));
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
                var fuel = Contents.Where(t => StoredFilterGuard.Allows(target.Props.fuelFilter, t)).ToList();
                if (fuel.Count == 0 || (target.Props.atomicFueling && fuel.Sum(t => (long)t.stackCount) < needed)) return;
                target.Refuel(fuel);
                return;
            }
            // Fuel has its own target level; a zero item limit still explicitly pauses that fuel.
            StorageFuelSupply.Deliver(this, Network, target,
                selections.Where((def, slot) => def != null && TargetLimit(slot) > 0), immediate);
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
            Scribe_Collections.Look(ref targetLimits, "transportTargetLimits", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Contents.dontTickContents = true;
                if (targetLimits == null) targetLimits = new List<int>();
                while (targetLimits.Count < SlotCount) targetLimits.Add(DefaultTargetLimit);
                if (targetLimits.Count > SlotCount) targetLimits.RemoveRange(SlotCount, targetLimits.Count - SlotCount);
                for (int i = 0; i < SlotCount; i++) targetLimits[i] = Math.Max(0, targetLimits[i]);
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
                    if (FrontTransporter() != null || FrontRefuelables().Count > 0 || VehicleFrameworkCompatibility.FrontVehicle(this) != null)
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
                yield return new Command_Action
                {
                    defaultLabel = "MS_Transport_CopyPreset".Translate(),
                    hotKey = StorageKeyBindingDefOf.MS_ShelfCopyPreset,
                    defaultDesc = "MS_Transport_CopyPresetDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/CopySettings"),
                    // Keep supplier commands separate from shelf commands in mixed selections.
                    groupKey = 0x4D535443,
                    alsoClickIfOtherInGroupClicked = false,
                    action = CopyPreset
                };
                var paste = new Command_Action
                {
                    defaultLabel = "MS_Transport_PastePreset".Translate(),
                    hotKey = StorageKeyBindingDefOf.MS_ShelfPastePreset,
                    defaultDesc = "MS_Transport_PastePresetDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/PasteSettings"),
                    groupKey = 0x4D535450,
                    alsoClickIfOtherInGroupClicked = true,
                    action = PastePreset
                };
                if (copiedPreset == null) paste.Disable("MS_Transport_NeedCopiedPreset".Translate());
                else if (directSupply != copiedPreset.DirectSupply && Contents.Count > 0)
                    paste.Disable("MS_Transport_ModeBlocked".Translate());
                yield return paste;
                var immediate = new Command_Action
                {
                    defaultLabel = "MS_FuelSupply_Immediate".Translate(),
                    defaultDesc = "MS_Transport_ImmediateDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/SupplyNow"),
                    action = () => SupplyTransporter(true)
                };
                if (directSupply && !CanWork) immediate.Disable(StorageNetwork.StatusLabel(Network));
                else if (!directSupply && Contents.Count == 0) immediate.Disable("MS_Transport_ImmediateEmpty".Translate());
                else if (FrontTransporter() == null && FrontRefuelables().Count == 0 && VehicleFrameworkCompatibility.FrontVehicle(this) == null)
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
                Pawn vehicle = VehicleFrameworkCompatibility.FrontVehicle(this);
                List<CompRefuelable> fuels = FrontRefuelables();
                if (vehicle != null) status = "MS_Transport_Target".Translate(vehicle.LabelCap,
                    VehicleFrameworkCompatibility.Usage(vehicle).ToString("0.##"), VehicleFrameworkCompatibility.Capacity(vehicle).ToString("0.##"));
                else if (fuels.Count > 0) status = string.Join("\n", fuels.Select((fuel, index) =>
                    "MS_Transport_FuelTarget".Translate(
                        fuel.parent.LabelCap.ToString() + (fuels.Count > 1 ? " #" + (index + 1) : ""),
                        fuel.Fuel.ToString("0.##"), fuel.TargetFuelLevel.ToString("0.##")).ToString()));
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
