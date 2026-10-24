using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace Verse
{
    public partial class Thing { internal static System.Action<Thing> BeforeMonolynTestSplit; }
    public class BuildableDef { }
    public partial class ThingDef : BuildableDef
    { public string defName = "FixtureMaterial"; public List<ThingDefCountClass> costList; }
    public partial class Map
    {
        private RimWorld.ResourceCounter counter;
        public RimWorld.ResourceCounter resourceCounter => counter ?? (counter = new RimWorld.ResourceCounter(this));
    }
    public static class MonolynThingExtensions
    { public static void Destroy(this Thing item, DestroyMode mode) { item.Destroy(); } }
    public class EffecterDef { public Effecter Spawn() => new Effecter(); }
    public class Effecter
    {
        public static int Triggers, Cleanups;
        public static bool ThrowOnTrigger;
        public static TargetInfo LastTarget;
        public void Trigger(TargetInfo a, TargetInfo b, int tick)
        { Triggers++; LastTarget = a; if (ThrowOnTrigger) throw new System.Exception("Injected effect failure"); }
        public void Cleanup() { Cleanups++; }
    }
}
namespace ASEL
{
    public class Building_MonolynProducer : Building, IThingHolder
    {
        public bool working = true, pause;
        public List<ThingDefCountClass> requiredCost = new List<ThingDefCountClass>();
        public ThingOwner innerContainer;
        public int OriginalTicks;
        public Building_MonolynProducer() { innerContainer = new ThingOwner(this); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override void Tick() { OriginalTicks++; }
        public void RunTick() { Tick(); }
        public new IThingHolder ParentHolder => null;
        public ThingOwner GetDirectlyHeldThings() => innerContainer;
        public void GetChildHolders(List<IThingHolder> children) { }
    }
    public class Building_Crucible : Building_MonolynProducer
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override void Tick() { base.Tick(); }
    }
    public static class MNEffecterDefOf { public static EffecterDef LanternTransfer = new EffecterDef(); }
    public class PlaceWorker_LightBuilder
    {
        public static int OriginalCalls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool DestroyResources(Map map, BuildableDef checkingDef) { OriginalCalls++; return false; }
    }
}
