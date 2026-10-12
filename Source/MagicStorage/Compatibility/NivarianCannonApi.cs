using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    // Cache the optional mod's members once. No compile-time dependency on its assembly.
    internal sealed class NivarianCannonApi
    {
        private const string Root = "Nivarian_Race.Code.";
        private readonly Type nodeType, lineType, lineListType;
        private readonly MethodInfo adjacent, covering, hops, load, fresh, reserved, group, place, cancel, rebuild, enqueue;
        private readonly PropertyInfo operational, capacity, maxPerShot;
        private readonly FieldInfo needKey, priority, keyTarget, keyDef, keySink, keyFuelIndex;
        private readonly FieldInfo lineDef, lineCount, lineOriginalCount, lineSink, lineFuelIndex, orderId;
        private readonly object failureReason;
        internal readonly MethodInfo Dispatch;

        internal NivarianCannonApi()
        {
            nodeType = RequireType("Comps.BuildingComps.CompCannonNode");
            Type inputType = RequireType("Comps.BuildingComps.CompCannonInput");
            Type network = RequireType("Logistics.CannonNetworkMapComponent");
            Type ledger = RequireType("Logistics.Orders.CannonOrderLedger");
            Type entry = RequireType("Logistics.Orders.OrderDemandCalculator+NeedEntry");
            Type key = RequireType("Logistics.Orders.ResKey");
            Type sink = RequireType("Comps.BuildingComps.CannonCargoSinkKind");
            Type reason = RequireType("Logistics.Orders.CannonOrderCloseReason");
            lineType = RequireType("Logistics.Orders.CannonCargoOrderLine");
            lineListType = typeof(List<>).MakeGenericType(lineType);
            Type order = RequireType("Logistics.Orders.CannonCargoOrder");
            Dispatch = Method(RequireType("Logistics.Orders.OrderController"), "DispatchDeficit",
                ledger, network, typeof(Map), entry, typeof(int));
            adjacent = Method(RequireType("Logistics.CannonLogisticsUtility"), "FillAdjacentCannons",
                typeof(Thing), typeof(List<>).MakeGenericType(nodeType));
            covering = Method(network, "FindCoveringCannon", typeof(Thing), nodeType);
            hops = Method(network, "GetHopDistance", nodeType, nodeType);
            load = Method(network, "AssignmentLoad", nodeType, typeof(int));
            fresh = Method(ledger, "FreshPoolOf", typeof(Thing), typeof(ThingDef), sink, typeof(int));
            reserved = Method(ledger, "ReservedOf", key);
            group = Method(ledger, "NextGroupId");
            place = Method(ledger, "PlaceOrder", typeof(Thing), lineListType, typeof(int), typeof(int), nodeType, inputType);
            cancel = Method(ledger, "Cancel", typeof(int), reason);
            rebuild = Method(ledger, "RebuildIndex");
            enqueue = Method(nodeType, "TryEnqueue", typeof(Thing), typeof(Thing), sink,
                typeof(int), typeof(int), typeof(int), typeof(bool));
            operational = Property(nodeType, "IsOperational", typeof(bool));
            capacity = Property(nodeType, "RemainingCapacity", typeof(int));
            maxPerShot = Property(nodeType, "MaxPerShot", typeof(int));
            needKey = Field(entry, "key", key); priority = Field(entry, "priority", typeof(int));
            keyTarget = Field(key, "destBuilding", typeof(Thing)); keyDef = Field(key, "def", typeof(ThingDef));
            keySink = Field(key, "sink", sink); keyFuelIndex = Field(key, "fuelCompIndex", typeof(int));
            lineDef = Field(lineType, "def", typeof(ThingDef)); lineCount = Field(lineType, "count", typeof(int));
            lineOriginalCount = Field(lineType, "originalCount", typeof(int)); lineSink = Field(lineType, "sink", sink);
            lineFuelIndex = Field(lineType, "fuelCompIndex", typeof(int)); orderId = Field(order, "orderId", typeof(int));
            failureReason = Enum.Parse(reason, "ExpiredOpen");
            if (Dispatch.ReturnType != typeof(void) || place.ReturnType != order || enqueue.ReturnType != typeof(bool))
                throw new InvalidOperationException("Unsupported Nivarian cannon method signature.");
        }

        internal sealed class Demand
        {
            internal object Key, Sink;
            internal Thing Target;
            internal ThingDef Def;
            internal int FuelIndex, Priority;
        }

        internal Demand ReadDemand(object entry)
        {
            object key = needKey.GetValue(entry);
            return new Demand { Key = key, Target = (Thing)keyTarget.GetValue(key), Def = (ThingDef)keyDef.GetValue(key),
                Sink = keySink.GetValue(key), FuelIndex = (int)keyFuelIndex.GetValue(key), Priority = (int)priority.GetValue(entry) };
        }

        internal IList Adjacent(Thing port)
        {
            var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(nodeType));
            adjacent.Invoke(null, new object[] { port, result });
            return result;
        }
        internal bool Operational(object node) => (bool)operational.GetValue(node, null);
        internal int Capacity(object node) => (int)capacity.GetValue(node, null);
        internal int ShotLimit(object node) => (int)maxPerShot.GetValue(node, null);
        internal Thing Parent(object node) => ((ThingComp)node).parent;
        internal object Covering(object network, Thing target) => covering.Invoke(network, new object[] { target, null });
        internal int Hops(object network, object source, object dest) => (int)hops.Invoke(network, new[] { source, dest });
        internal int Load(object network, object source) => (int)load.Invoke(network, new object[] { source, 0 });
        internal int Group(object ledger) => (int)group.Invoke(ledger, null);
        internal int AvailableDemand(object ledger, Demand demand) => Math.Max(0,
            (int)fresh.Invoke(ledger, new object[] { demand.Target, demand.Def, demand.Sink, demand.FuelIndex }) -
            (int)reserved.Invoke(ledger, new[] { demand.Key }));

        internal int PlaceOrder(object ledger, Demand demand, int count, int groupId, object source)
        {
            object line = Activator.CreateInstance(lineType);
            lineDef.SetValue(line, demand.Def); lineCount.SetValue(line, count); lineOriginalCount.SetValue(line, count);
            lineSink.SetValue(line, demand.Sink); lineFuelIndex.SetValue(line, demand.FuelIndex);
            var lines = (IList)Activator.CreateInstance(lineListType); lines.Add(line);
            // No ground intake return point: later cancelled cargo uses Nivarian's safe drop handling.
            object order = place.Invoke(ledger, new object[] { demand.Target, lines, demand.Priority, groupId, source, null });
            return (int)orderId.GetValue(order);
        }
        internal bool Enqueue(object node, Thing item, Demand demand, int id, int groupId) =>
            (bool)enqueue.Invoke(node, new object[] { item, demand.Target, demand.Sink, demand.FuelIndex, id, groupId, false });
        internal void Cancel(object ledger, int id)
        {
            try { cancel.Invoke(ledger, new[] { (object)id, failureReason }); }
            finally { rebuild.Invoke(ledger, null); }
        }

        private static Type RequireType(string name) => AccessTools.TypeByName(Root + name) ??
            throw new TypeLoadException(Root + name);
        private static MethodInfo Method(Type type, string name, params Type[] args) => AccessTools.Method(type, name, args) ??
            throw new MissingMethodException(type.FullName, name);
        private static FieldInfo Field(Type type, string name, Type valueType)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null || field.FieldType != valueType) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        private static PropertyInfo Property(Type type, string name, Type valueType)
        {
            PropertyInfo property = AccessTools.Property(type, name);
            if (property == null || property.PropertyType != valueType) throw new MissingMemberException(type.FullName, name);
            return property;
        }
    }
}
