using System.Collections.Generic;
using Verse;
namespace Verse
{
    public interface ISuspendableThingHolder { bool IsContentsSuspended { get; } }
    public static class ThingOwnerUtility
    { public static void AppendThingHoldersFromThings(List<IThingHolder> list, ThingOwner owner) { } }
    public partial class ThingDef { public bool tradeNeverStack, IsWeapon, IsApparel; }
    public partial class Thing
    {
        public int HitPoints = 100;
        public string LabelNoCount => variant ?? "item";
        public Thing GetInnerIfMinified() => this is MinifiedThing minified ? minified.InnerThing : this;
        public void SetForbidden(bool value, bool warn = true) { forbidden = value; }
        public T TryGetComp<T>() where T : class => new RimWorld.CompForbiddable { parent = this } as T;
    }
    public class ThingComp { }
    public class ThingWithComps : Thing { public List<ThingComp> AllComps = new List<ThingComp>(); }
    public partial class Map
    {
        public int DropBudget = int.MaxValue;
        public List<Thing> Ground = new List<Thing>();
    }
    public partial struct IntVec3
    {
        public bool InBounds(Map map) => true;
        public List<Thing> GetThingList(Map map) => map.Ground;
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
        public static bool TransferAsOne(Thing a, Thing b, TransferAsOneMode mode)
        {
            Calls++; LastMode = mode;
            return a == b || (!a.def.tradeNeverStack && a.CanStackWith(b));
        }
    }
    public class CompForbiddable : ThingComp
    { public Thing parent; public bool Forbidden => parent.forbidden; }
}
