using RimWorld;
using Verse;

namespace MagicStorage
{
    // Visual connectivity only. Plans never enter MapComponent_StorageNetworks.
    internal static class StorageConnectionQuery
    {
        internal static ThingDef BuiltDef(ThingDef def) => def?.entityDefToBuild as ThingDef ?? def;

        internal static bool IsStorage(ThingDef def)
        {
            var built = BuiltDef(def);
            if (built?.comps == null) return false;
            foreach (var comp in built.comps)
                if (comp is CompProperties_StorageNode) return true;
            return false;
        }

        internal static bool IsConduit(ThingDef def) => BuiltDef(def)?.thingClass == typeof(Building_StorageConduit);
        internal static bool IsPlan(Thing thing) => thing.def.IsBlueprint || thing.def.IsFrame;

        internal static Thing NodeAt(Map map, IntVec3 cell, Faction faction, bool includePlans, Thing ignore = null)
        {
            if (map == null || !cell.InBounds(map)) return null;
            Thing plan = null;
            foreach (var thing in cell.GetThingList(map))
            {
                if (thing == ignore || !thing.Spawned || thing.Destroyed || thing.Faction != faction || !IsStorage(thing.def)) continue;
                if (!IsPlan(thing)) return thing;
                if (includePlans && plan == null) plan = thing;
            }
            return plan;
        }

        internal static LinkDirections LinksAt(Map map, IntVec3 cell, Faction faction, bool includePlans,
            CellRect? preview = null, Thing ignore = null)
        {
            int links = 0;
            for (int i = 0; i < 4; i++)
            {
                var adjacent = cell + GenAdj.CardinalDirections[i];
                if (adjacent.InBounds(map) &&
                    ((preview.HasValue && preview.Value.Contains(adjacent)) || NodeAt(map, adjacent, faction, includePlans, ignore) != null))
                    links |= 1 << i;
            }
            return (LinkDirections)links;
        }
    }
}
