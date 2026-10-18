using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // The native window owns the tabs, route planner, automatic supplies, estimates and warnings.
    public sealed class Dialog_StorageCaravan : Dialog_FormCaravan
    {
        internal readonly Building_CaravanTeleporter Teleporter;
        internal StorageNetwork Network { get; private set; }
        internal Map SourceMap { get; }

        public Dialog_StorageCaravan(Building_CaravanTeleporter teleporter) : base(teleporter.Map)
        {
            Teleporter = teleporter;
            SourceMap = teleporter.Map;
        }

        internal bool Usable => Teleporter.Spawned && Teleporter.Map == SourceMap &&
            Teleporter.CanWork && Teleporter.Network == Network;

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
                var row = TransferableUtility.TransferableMatching(item, transferables, TransferAsOneMode.PodsOrCaravanPacking);
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
