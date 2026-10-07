// Controlled vanilla boundary. The real Harmony postfix and private-method delegate
// run against this counter; freshness/fog decisions are supplied by the fixture.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace Verse
{
    public partial class ThingDef { public bool CountAsResource = true; }
    public partial class Map
    {
        public readonly List<ThingDef> ResourceDefs = new List<ThingDef>();
        public readonly List<Thing> NativeStoredResources = new List<Thing>();
        public readonly HashSet<Thing> ExcludedResources = new HashSet<Thing>();
    }
}
namespace RimWorld
{
    public sealed class ResourceCounter
    {
        private Map map;
        public int EligibilityChecks;
        public Dictionary<ThingDef, int> AllCountedAmounts { get; } = new Dictionary<ThingDef, int>();
        public ResourceCounter(Map map) { this.map = map; }
        public int GetCount(ThingDef def) => AllCountedAmounts.TryGetValue(def, out int count) ? count : 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateResourceCounts()
        {
            AllCountedAmounts.Clear();
            foreach (ThingDef def in map.ResourceDefs) AllCountedAmounts.Add(def, 0);
            foreach (Thing stored in map.NativeStoredResources)
            {
                Thing item = stored.GetInnerIfMinified();
                if (item.def.CountAsResource && ShouldCount(item)) AllCountedAmounts[item.def] += item.stackCount;
            }
        }
        private bool ShouldCount(Thing item)
        { EligibilityChecks++; return !map.ExcludedResources.Contains(item); }
    }
}
