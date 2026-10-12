// Only game boundaries are stubbed; the supplier and exchange service are production code.
using System.Collections.Generic;
using MagicStorage;
using Verse;
namespace Verse
{
    public static class Gen
    {
        public static bool IsHashIntervalTick(this Thing thing, int interval) =>
            (Find.TickManager.TicksGame + thing.thingIDNumber) % interval == 0;
    }
    public partial class ThingDef { public float transportMass = 1f; }
    public partial class Thing
    {
        public string LabelCap => LabelNoCount;
        public float GetStatValue(object stat) => Stuff?.transportMass ?? def.transportMass;
    }
    public partial class Building
    {
        protected virtual void Tick() { }
        public void RunTransportTestTick() { Tick(); }
    }
    public partial struct Rot4
    {
        public IntVec3 FacingCell => value == 1 ? new IntVec3(1, 0) : value == 2 ? new IntVec3(0, -1) :
            value == 3 ? new IntVec3(-1, 0) : new IntVec3(0, 1);
    }
    public static partial class GenDraw
    {
        public static List<IntVec3> TransportFront;
        public static void DrawFieldEdges(List<IntVec3> cells) { TransportFront = cells; }
    }
}
namespace RimWorld
{
    public sealed class CompProperties_Interactable { public bool cooldownPreventsRefuel; }
    public sealed class CompInteractable : ThingComp
    {
        public bool OnCooldown;
        public CompProperties_Interactable Props = new CompProperties_Interactable();
    }
    public sealed class CompProperties_Refuelable
    {
        public float fuelCapacity = 100, FuelMultiplierCurrentDifficulty = 1, autoRefuelPercent = 0.3f;
        public bool atomicFueling, allowRefuelIfNotEmpty = true;
        public ThingFilter fuelFilter = new ThingFilter();
    }
    public sealed class CompRefuelable : ThingComp
    {
        public new Thing parent;
        public CompProperties_Refuelable Props = new CompProperties_Refuelable();
        public float Fuel, TargetFuelLevel = 100;
        public bool allowAutoRefuel = true;
        public bool IsFull => TargetFuelLevel - Fuel < Props.FuelMultiplierCurrentDifficulty;
        public bool ShouldAutoRefuelNowIgnoringFuelPct = true;
        public bool ShouldAutoRefuelNow => TargetFuelLevel > 0 && !IsFull &&
            Fuel / TargetFuelLevel <= Props.autoRefuelPercent && ShouldAutoRefuelNowIgnoringFuelPct;
        public int GetFuelCountToFullyRefuel() => (int)System.Math.Ceiling(
            (Props.atomicFueling ? Props.fuelCapacity : System.Math.Max(0, TargetFuelLevel - Fuel)) / Props.FuelMultiplierCurrentDifficulty);
        public int RefuelCalls;
        public void Refuel(List<Thing> things)
        {
            RefuelCalls++;
            int remaining = GetFuelCountToFullyRefuel();
            while (remaining > 0 && things.Count > 0)
            {
                Thing item = things[things.Count - 1]; things.RemoveAt(things.Count - 1);
                int count = System.Math.Min(remaining, item.stackCount);
                Fuel = System.Math.Min(Props.fuelCapacity, Fuel + count * Props.FuelMultiplierCurrentDifficulty);
                item.SplitOff(count).Destroy(); remaining -= count;
            }
        }
    }
    public static class StatDefOf { public static readonly object Mass = new object(); }
    public class TransferableOneWay { public List<Thing> things = new List<Thing>(); public virtual Thing AnyThing => things[0]; }
    public sealed class CompTransporter : ThingComp, IThingHolder
    {
        public new Thing parent;
        public float MassCapacity = 150;
        public ThingOwner innerContainer;
        public float MassUsage => (float)StorageTransportExchange.ContainerMass(innerContainer);
        public CompTransporter() { innerContainer = new ThingOwner(this); }
        public IThingHolder ParentHolder => parent as IThingHolder;
        public ThingOwner GetDirectlyHeldThings() => innerContainer;
        public void GetChildHolders(List<IThingHolder> children) { }
    }
}
namespace MagicStorage
{
    public sealed class Dialog_SupplierSupply { public Dialog_SupplierSupply(Building_TransportSupplier supplier) { } }
    public sealed class Dialog_TransportSupplier { public Dialog_TransportSupplier(Building_TransportSupplier supplier) { } }
}
