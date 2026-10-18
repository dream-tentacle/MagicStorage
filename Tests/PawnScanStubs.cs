// Controlled native boundaries: getters and holder collection match the inspected
// 1.6/1.7 call shape. Tests execute the unchanged production Harmony hooks.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public class Thing { }
    public sealed class Pawn : Thing { public bool Dead; }
    public interface IThingHolder
    {
        List<Thing> GetDirectlyHeldThings();
        void GetChildHolders(List<IThingHolder> children);
    }
    public class TestVanillaHolder : IThingHolder
    {
        public readonly List<Thing> Contents = new List<Thing>();
        public readonly List<IThingHolder> Children = new List<IThingHolder>();
        public int Reads;
        public List<Thing> GetDirectlyHeldThings() { Reads++; return Contents; }
        public void GetChildHolders(List<IThingHolder> children) { children.AddRange(Children); }
    }
    public sealed class Map
    {
        public readonly List<IThingHolder> Holders = new List<IThingHolder>();
        public Action BeforeCollect;
        public readonly MapPawns mapPawns;
        public Map() { mapPawns = new MapPawns(this); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void GetChildHolders(List<IThingHolder> outChildren)
        { BeforeCollect?.Invoke(); outChildren.AddRange(Holders); }
    }
    public sealed class MapPawns
    {
        private readonly Map map;
        private readonly List<Pawn> allPawnsUnspawnedResult = new List<Pawn>();
        public MapPawns(Map map) { this.map = map; }
        public List<Pawn> AllPawnsUnspawned
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                allPawnsUnspawnedResult.Clear();
                var holders = new List<IThingHolder>();
                map.GetChildHolders(holders);
                var things = new List<Thing>();
                foreach (IThingHolder holder in holders)
                {
                    ThingOwnerUtility.GetAllThingsRecursively(holder, things);
                    foreach (Thing thing in things)
                        if (thing is Pawn pawn && !pawn.Dead) allPawnsUnspawnedResult.Add(pawn);
                }
                return allPawnsUnspawnedResult;
            }
        }
    }
    public static class ThingOwnerUtility
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetAllThingsRecursively(IThingHolder holder, List<Thing> outThings,
            bool allowUnreal = true, Predicate<IThingHolder> passCheck = null)
        {
            outThings.Clear();
            if (passCheck != null && !passCheck(holder)) return;
            var stack = new Stack<IThingHolder>(); stack.Push(holder);
            while (stack.Count > 0)
            {
                IThingHolder current = stack.Pop();
                outThings.AddRange(current.GetDirectlyHeldThings());
                var children = new List<IThingHolder>(); current.GetChildHolders(children);
                foreach (IThingHolder child in children)
                    if (passCheck == null || passCheck(child)) stack.Push(child);
            }
        }
    }
}
namespace MagicStorage
{
    public sealed class TestStorageHolder : Verse.TestVanillaHolder { }
}
namespace MagicStorage.TestsContainers
{
    public sealed class TestStorageHolder : Verse.TestVanillaHolder { }
}
namespace MagicStorageOther
{
    public sealed class TestUnrelatedHolder : Verse.TestVanillaHolder { }
}
