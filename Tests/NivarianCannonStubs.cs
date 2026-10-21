// Shape of the inspected Nivarian API only. Actual Harmony patch and storage code are linked into tests.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using Verse;
using Nivarian_Race.Code.Comps.BuildingComps;
using Nivarian_Race.Code.Logistics.Orders;

namespace RimWorld
{
    public class CompPowerTrader : ThingComp { public bool PowerOn = true; }
    public class CompFlickable : ThingComp { public bool SwitchIsOn = true; }
}
namespace Nivarian_Race.Code.Comps.BuildingComps
{
    public enum CannonCargoSinkKind { Ingredient, Fuel }
    public class CompCannonInput : ThingComp { }
    public class CompCannonNode : ThingComp, IThingHolder
    {
        public readonly ThingOwner Contents;
        public readonly List<CannonCargoOrder> Accepted = new List<CannonCargoOrder>();
        public bool Powered = true, Reject, Throw;
        public bool ThrowAfterAccept, ReturnFalseAfterAccept, ReturnTrueWithoutAccept;
        public int AcceptLimit = int.MaxValue;
        public int CapacityLimit = 75, ShotLimit = 75, Distance, Load;
        public bool IsOperational => Powered && parent.Spawned;
        public int RemainingCapacity => Math.Max(0, CapacityLimit - Contents.Sum(item => item.stackCount));
        public int MaxPerShot => ShotLimit;
        public Action BeforeAccept;
        public CompCannonNode() { Contents = new ThingOwner(this); }
        public bool TryEnqueue(Thing item, Thing target, CannonCargoSinkKind sink, int fuelCompIndex,
            int orderId, int parentOrderId, bool isReturn)
        {
            BeforeAccept?.Invoke();
            if (Throw) throw new InvalidOperationException("fixture enqueue failure");
            if (Reject) return false;
            if (ReturnTrueWithoutAccept) return true;
            int count = Math.Min(item.stackCount, AcceptLimit);
            if (StorageTransfer.Move(item.holdingOwner, Contents, item, count, parent.Map, parent.Position, false) != count) return false;
            var order = CannonOrderLedger.Current.ById(orderId);
            order.Stocked = true; Accepted.Add(order);
            if (ThrowAfterAccept) throw new InvalidOperationException("fixture post-acceptance failure");
            return !ReturnFalseAfterAccept;
        }
        public IThingHolder ParentHolder => parent.Map;
        public ThingOwner GetDirectlyHeldThings() => Contents;
        public void GetChildHolders(List<IThingHolder> list) { }
    }
}
namespace Nivarian_Race.Code.Logistics
{
    public class CannonNetworkMapComponent
    {
        public object Destination;
        public int GetHopDistance(CompCannonNode source, CompCannonNode dest) => source.Distance;
        public int AssignmentLoad(CompCannonNode source, int plannedExtra = 0) => source.Load + plannedExtra;
        public CompCannonNode FindCoveringCannon(Thing target, CompCannonNode preferLocal = null) => (CompCannonNode)Destination;
    }
    public static class CannonLogisticsUtility
    {
        public static readonly List<CompCannonNode> Nodes = new List<CompCannonNode>();
        public static void FillAdjacentCannons(Thing port, List<CompCannonNode> result)
        {
            result.Clear();
            foreach (var node in Nodes)
                if (node.parent.Spawned && node.parent.Map == port.Map &&
                    Math.Abs(node.parent.Position.x - port.Position.x) <= 1 &&
                    Math.Abs(node.parent.Position.z - port.Position.z) <= 1 && node.parent.Position != port.Position)
                    result.Add(node);
        }
    }
}
namespace Nivarian_Race.Code.Logistics.Orders
{
    public struct ResKey { public Thing destBuilding; public ThingDef def; public CannonCargoSinkKind sink; public int fuelCompIndex; }
    public class OrderDemandCalculator
    { public struct NeedEntry { public ResKey key; public int need, priority; } }
    public enum CannonOrderCloseReason { ExpiredOpen }
    public class CannonCargoOrderLine
    { public ThingDef def; public int count, originalCount; public CannonCargoSinkKind sink; public int fuelCompIndex = -1; }
    public class CannonCargoOrder
    {
        public int orderId, parentOrderId;
        public Thing target;
        public List<CannonCargoOrderLine> lines;
        public bool Cancelled, Stocked;
    }
    public class CannonOrderLedger
    {
        public static CannonOrderLedger Current;
        public readonly List<CannonCargoOrder> Orders = new List<CannonCargoOrder>();
        public int Need = 100, GroundCalls, GroundDeficit, IndexRebuilds;
        public Action OnPlace;
        public int NextGroupId() => Orders.Count + 1;
        public int FreshPoolOf(Thing target, ThingDef def, CannonCargoSinkKind sink, int index) => target.Spawned ? Need : 0;
        public int ReservedOf(ResKey key) => Orders.Where(order => !order.Cancelled && order.target == key.destBuilding)
            .SelectMany(order => order.lines).Where(line => line.def == key.def && line.sink == key.sink && line.fuelCompIndex == key.fuelCompIndex).Sum(line => line.count);
        public CannonCargoOrder PlaceOrder(Thing target, List<CannonCargoOrderLine> lines, int priority, int groupId = 0,
            CompCannonNode assignedCannon = null, CompCannonInput returnIntake = null)
        {
            OnPlace?.Invoke();
            var order = new CannonCargoOrder { orderId = Orders.Count + 1, parentOrderId = groupId, target = target, lines = lines };
            Orders.Add(order); return order;
        }
        public CannonCargoOrder ById(int id) => Orders.Find(order => order.orderId == id);
        public bool Cancel(int id, CannonOrderCloseReason reason) { ById(id).Cancelled = true; return true; }
        public void RebuildIndex() { IndexRebuilds++; }
    }
    public static class OrderController
    {
        public static void Match(CannonOrderLedger ledger, Nivarian_Race.Code.Logistics.CannonNetworkMapComponent network,
            Map map, OrderDemandCalculator.NeedEntry entry, int deficit)
        { CannonOrderLedger.Current = ledger; DispatchDeficit(ledger, network, map, entry, deficit); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DispatchDeficit(CannonOrderLedger ledger, Nivarian_Race.Code.Logistics.CannonNetworkMapComponent network,
            Map map, OrderDemandCalculator.NeedEntry entry, int deficit)
        { ledger.GroundCalls++; ledger.GroundDeficit += deficit; }
    }
}
