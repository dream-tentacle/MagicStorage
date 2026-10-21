using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // The native window owns the tabs, route planner, automatic supplies, estimates and warnings.
    public sealed class Dialog_StorageCaravan : Dialog_FormCaravan
    {
        internal readonly Building_CaravanTeleporter Teleporter;
        internal StorageNetwork Network { get; private set; }
        internal Map SourceMap { get; }
        private StorageItemFilterPanel itemFilters;
        private bool filtersWereEnabled;
        internal StorageItemFilterPanel ItemFilters => itemFilters ?? (itemFilters = new StorageItemFilterPanel(
            SourceMap, null, () => StorageNativeItemFilters.InvalidateCaravan(this),
            () => StorageNativeItemFilters.RebuildCaravan(this), () => StorageNativeItemFilters.InvalidateCaravan(this, true)));
        public override Vector2 InitialSize => MagicStorageMod.Settings.CaravanItemFilters
            ? new Vector2(Math.Min(1340f, UI.screenWidth), UI.screenHeight) : base.InitialSize;

        public Dialog_StorageCaravan(Building_CaravanTeleporter teleporter) : base(teleporter.Map)
        {
            Teleporter = teleporter;
            SourceMap = teleporter.Map;
        }

        internal bool Usable => Teleporter.Spawned && Teleporter.Map == SourceMap &&
            Teleporter.CanWork && Teleporter.Network == Network;

        public override void DoWindowContents(Rect inRect)
        {
            bool enabled = MagicStorageMod.Settings.CaravanItemFilters;
            if (enabled != filtersWereEnabled)
            { filtersWereEnabled = enabled; StorageNativeItemFilters.RebuildCaravan(this); }
            if (!enabled) { base.DoWindowContents(inRect); return; }
            float width = StorageItemFilterPanel.Width(inRect.width);
            ItemFilters.Draw(new Rect(inRect.x + 8f, inRect.y + 8f, width - 8f, inRect.height - 16f),
                transferables.Where(row => !(row.AnyThing is Pawn)).SelectMany(row => row.things));
            var body = new Rect(inRect.x + width + 16f, inRect.y, inRect.width - width - 16f, inRect.height);
            Widgets.BeginGroup(body);
            try { base.DoWindowContents(body.AtZero()); }
            finally { Widgets.EndGroup(); }
        }

        internal void FillNetworkItems()
        {
            SourceMap.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            Network = Teleporter.Network;
            // AddPawnsToTransferables also adds whatever each pawn is currently carrying.
            // Keep its pawn rows; all selectable cargo must originate in this network.
            transferables.RemoveAll(row => !(row.AnyThing is Pawn));
            if (!Usable) return;
            var stacks = new List<Thing>();
            Network.GetAllStacks(stacks);
            foreach (Thing item in stacks)
            {
                if (item.Destroyed || item.def.category != ThingCategory.Item || Network.AvailableToWithdraw(item) <= 0) continue;
                var candidates = MagicStorageMod.Settings.CaravanItemFilters
                    ? transferables.Where(candidate => StorageInventoryFilters.CanGroup(item, candidate.AnyThing,
                        MagicStorageMod.Settings.InventoryFilters)).ToList() : transferables;
                var row = TransferableUtility.TransferableMatching(item, candidates, TransferAsOneMode.PodsOrCaravanPacking);
                if (row == null)
                {
                    row = new Transferable_StorageCaravan(Network);
                    transferables.Add(row);
                }
                row.things.Add(item);
            }
        }

        internal bool ValidateSelection(List<Pawn> pawns)
        {
            SourceMap.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!Usable) return Reject("MS_Caravan_Unavailable");
            var allowed = new HashSet<Pawn>(AllSendablePawns(SourceMap, false));
            foreach (Pawn pawn in new List<Pawn>(allowed))
                if (pawn.carryTracker?.CarriedThing is Pawn carried) allowed.Add(carried);
            foreach (Pawn pawn in pawns)
                if (pawn.Dead || pawn.Destroyed || pawn.MapHeld != SourceMap || !allowed.Contains(pawn))
                    return Reject("MS_Caravan_PawnsChanged");
            foreach (TransferableOneWay row in transferables)
            {
                if (row.CountToTransfer <= 0 || row.AnyThing is Pawn) continue;
                if (!(row is Transferable_StorageCaravan cargo) || cargo.Network != Network ||
                    row.CountToTransfer > cargo.AvailableCount) return Reject("MS_Caravan_StockChanged");
            }
            return true;
        }

        internal static bool Reject(string key)
        {
            Messages.Message(key.Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }

    internal sealed class Transferable_StorageCaravan : TransferableOneWay
    {
        internal readonly StorageNetwork Network;
        internal Transferable_StorageCaravan(StorageNetwork network) { Network = network; }

        internal int AvailableCount
        {
            get
            {
                long count = 0;
                foreach (Thing item in things) count += Network.AvailableToWithdraw(item);
                return (int)Math.Min(int.MaxValue, count);
            }
        }

        public override int GetMaximumToTransfer() => AvailableCount;
    }
}
