// Controlled native boundaries. The iterator retains the inspected 1.6/1.7
// selection rules; both runtimes execute the unchanged production Harmony prefix.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public sealed class ThingDef
    { public bool selectable = true, containedItemsSelectable, containedPawnsSelectable; }
    public class Thing
    { public ThingDef def = new ThingDef(); public bool PlayerOwned = true; }
    public sealed class Pawn : Thing { public Thing CarriedThing; }
    public sealed class MinifiedThing : Thing { }
    public interface IThingHolder { List<Thing> GetDirectlyHeldThings(); }
    public class ThingWithComps : Thing { public readonly List<IThingHolder> Comps = new List<IThingHolder>(); }
    public class Building : ThingWithComps { }
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
}
namespace RimWorld
{
    using Verse;
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
