using System.Collections.Generic;
using System.Linq;
using Verse;
namespace Verse
{
    public interface ISuspendableThingHolder { bool IsContentsSuspended { get; } }
    public static class ThingOwnerUtility
    { public static void AppendThingHoldersFromThings(List<IThingHolder> list, ThingOwner owner) { } }
    public partial class ThingDef { public bool tradeNeverStack, IsWeapon, IsApparel; public ModContentPack modContentPack; }
    public class Corpse : Thing { public Pawn InnerPawn; }
    public partial class Thing
    {
        public int HitPoints = 100;
        public virtual string LabelNoCount => variant ?? "item";
        public static int InnerLookups;
        public Thing GetInnerIfMinified()
        { InnerLookups++; return this is MinifiedThing minified ? minified.InnerThing : this; }
        public void SetForbidden(bool value, bool warn = true) { forbidden = value; }
        public object TestExtraComp;
        public T TryGetComp<T>() where T : class =>
            TestExtraComp as T ?? (this as ThingWithComps)?.AllComps.Find(comp => comp is T) as T ?? new RimWorld.CompForbiddable { parent = this } as T;
    }
    public class ThingComp { public Thing parent; }
    public class ThingWithComps : Thing { public List<ThingComp> AllComps = new List<ThingComp>(); }
    public partial class Map
    {
        public int DropBudget = int.MaxValue;
        public List<Thing> Ground = new List<Thing>();
        public bool UseCellThingLists;
    }
    public partial struct IntVec3
    {
        public bool InBounds(Map map) => true;
        public List<Thing> GetThingList(Map map)
        {
            IntVec3 cell = this;
            return map.UseCellThingLists
                ? map.Ground.FindAll(thing => thing.Spawned && thing.OccupiedRect().Contains(cell)) : map.Ground;
        }
    }
}
namespace RimWorld
{
    public enum TransferAsOneMode { Normal, PodsOrCaravanPacking, InactiveTradeable }
    // Controlled native grouping boundary, not an emulation of every game component.
    public static partial class TransferableUtility
    {
        public static int Calls;
        public static TransferAsOneMode LastMode;
        public static System.Func<Thing, Thing, bool> GroupingOverride;
        public static bool TransferAsOne(Thing a, Thing b, TransferAsOneMode mode)
        {
            Calls++; LastMode = mode;
            if (GroupingOverride != null) return GroupingOverride(a, b);
            return a == b || (!a.def.tradeNeverStack && a.CanStackWith(b));
        }
    }
    public class CompForbiddable : ThingComp
    { public new Thing parent; public bool Forbidden => parent.forbidden; }
}
