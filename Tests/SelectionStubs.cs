// Controlled native boundaries. The iterator retains the inspected 1.6/1.7
// selection rules; both runtimes execute the production Harmony selection patches.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Verse
{
    public enum ThingCategory { Item, Building, Pawn }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public sealed class ThingDef
    { public bool selectable = true, containedItemsSelectable, containedPawnsSelectable; public ThingCategory category; }
    public class Thing
    { public ThingDef def = new ThingDef(); public bool PlayerOwned = true, Spawned; public Map Map; public IntVec3 Position; }
    public struct IntVec3 { public int x; public IntVec3(int x) { this.x = x; } }
    public class Map { public RimWorld.HaulDestinationManager haulDestinationManager = new RimWorld.HaulDestinationManager(); }
    public sealed class Pawn : Thing { public Thing CarriedThing; }
    public sealed class MinifiedThing : Thing { }
    public interface IThingHolder { List<Thing> GetDirectlyHeldThings(); }
    public class ThingWithComps : Thing { public readonly List<IThingHolder> Comps = new List<IThingHolder>(); }
    public class Building : ThingWithComps { }
    public class Selector
    {
        public readonly List<object> Candidates = new List<object>();
        public object Selected;
        public IEnumerable<object> QueryUnderMouse() => SelectableObjectsUnderMouse();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IEnumerable<object> SelectableObjectsUnderMouse() { return Candidates; }
        public void Select(object thing) { Selected = thing; }
    }
    public class TestContainer : Building, IThingHolder
    {
        public readonly List<Thing> Contents = new List<Thing>();
        public int Reads;
        public List<Thing> GetDirectlyHeldThings() { Reads++; return Contents; }
    }
}
namespace MagicStorage
{
    public sealed class Building_StorageUnit : Verse.TestContainer { }
    public sealed class Building_StorageCore : Verse.TestContainer { }
    public sealed class Building_TransportSupplier : Verse.TestContainer { }
    public sealed class Building_StorageApparelAdapter : Verse.TestContainer { }
    public sealed class Building_StorageSupplyShelf : Verse.Building { }
}
namespace UnityEngine
{
    public enum EventType { MouseUp, MouseDown, Repaint }
    public class Event { public static Event current; public EventType rawType; public int button; }
    public struct Rect { }
}
namespace RimWorld
{
    using Verse;
    public class HaulDestinationManager
    {
        public readonly Dictionary<IntVec3, Building> Parents = new Dictionary<IntVec3, Building>();
        public Building SlotGroupParentAt(IntVec3 cell) => Parents.TryGetValue(cell, out Building parent) ? parent : null;
    }
    public static class ThingSelectionUtility
    {
        public static readonly List<Thing> Candidates = new List<Thing>();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IEnumerable<Thing> MultiSelectableThingsInScreenRectDistinct(UnityEngine.Rect rect) => Candidates;
    }
    public static class ContainingSelectionUtility
    {
        public static int Visits;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IEnumerable<Thing> SelectableContainedThings(Thing container)
        {
            if (container is Pawn pawn)
            {
                if (pawn.CarriedThing != null && CanSelect(pawn.CarriedThing, container)) yield return pawn.CarriedThing;
            }
            else
            {
                if (container is MinifiedThing || !container.PlayerOwned) yield break;
                if (container is IThingHolder holder)
                {
                    foreach (Thing item in holder.GetDirectlyHeldThings())
                    {
                        Visits++;
                        if (CanSelect(item, container)) yield return item;
                    }
                }
                else if (container is ThingWithComps withComps)
                {
                    foreach (IThingHolder comp in withComps.Comps)
                        foreach (Thing item in comp.GetDirectlyHeldThings())
                        {
                            Visits++;
                            if (CanSelect(item, container)) yield return item;
                        }
                }
            }
        }

        private static bool CanSelect(Thing item, Thing container)
        {
            if (!item.def.selectable) return false;
            return item is Pawn ? container.def.containedPawnsSelectable : container.def.containedItemsSelectable;
        }
    }
}
