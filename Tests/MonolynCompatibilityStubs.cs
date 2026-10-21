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
    public static class MNEffecterDefOf { public static EffecterDef LanternTransfer = new EffecterDef(); }
    public class PlaceWorker_LightBuilder
    {
        public static int OriginalCalls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool DestroyResources(Map map, BuildableDef checkingDef) { OriginalCalls++; return false; }
    }
}
