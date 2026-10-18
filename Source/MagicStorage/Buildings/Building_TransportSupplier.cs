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
        private List<ThingDef> selections = new List<ThingDef> { null, null, null };
        public bool DirectSupply => directSupply;
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

        internal float TransferCapacity => Math.Max(150f, FrontTransporter()?.MassCapacity ?? 150f);
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
            if (transporter == null) { SupplyFuel(FrontRefuelable(), immediate); return; }
            if (directSupply) { SupplyFromNetwork(transporter); return; }
            // ThingOwner sends the native added/merged notifications and invalidates mass caches.
            foreach (Thing item in new List<Thing>(Contents))
            {
                if (!transporter.parent.Spawned || transporter.parent.Destroyed) break;
                int count = StorageTransportExchange.CountThatFits(item, transporter.MassCapacity - transporter.MassUsage);
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
                        StorageTransportExchange.CountThatFits(item, transporter.MassCapacity - transporter.MassUsage));
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
                var toggle = new Command_Action
                {
                    defaultLabel = "MS_Transport_SwitchMode".Translate(),
                    defaultDesc = "MS_Transport_SwitchModeDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/SwitchSupplyMode"),
                    action = () => TrySwitchMode()
                };
                if (Contents.Count > 0) toggle.Disable("MS_Transport_ModeBlocked".Translate());
                yield return toggle;
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
                else if (FrontTransporter() == null && FrontRefuelable() == null) immediate.Disable("MS_Transport_NoTarget".Translate());
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
            if (target == null)
            {
                CompRefuelable fuel = FrontRefuelable();
                if (fuel != null) status = "MS_Transport_FuelTarget".Translate(fuel.parent.LabelCap,
                    fuel.Fuel.ToString("0.##"), fuel.TargetFuelLevel.ToString("0.##"));
            }
            string mode = (directSupply ? "MS_Transport_DirectMode" : "MS_Transport_ManualMode").Translate();
            return (base.GetInspectString() + "\n" + mode + "\n" +
                "MS_Transport_Contents".Translate(Contents.Count, StoredMass.ToString("0.##")) + "\n" + status).Trim();
        }
    }

    public sealed class PlaceWorker_TransportSupplier : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, UnityEngine.Color ghostCol, Thing thing = null)
        { GenDraw.DrawFieldEdges(new List<IntVec3> { center + rot.FacingCell }); }
    }
}
