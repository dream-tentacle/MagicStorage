using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageNetworkDrawing
    {
        private const string AtlasPath = "Things/Special/Power/TransmitterAtlas";
        private static readonly Material BuiltMaterial = MaterialPool.MatFrom(AtlasPath, ShaderDatabase.MetaOverlay,
            new Color(0.3f, 0.85f, 1f), 3600);
        private static readonly Material PlannedMaterial = MaterialPool.MatFrom(AtlasPath, ShaderDatabase.MetaOverlay,
            new Color(0.7f, 0.85f, 1f, 0.45f), 3600);

        internal static bool ShouldDraw(Map map)
        {
            if (map != Find.CurrentMap) return false;
            if (Find.DesignatorManager.SelectedDesignator is Designator_Place placing &&
                StorageConnectionQuery.IsStorage(placing.PlacingDef as ThingDef)) return true;
            foreach (var selected in Find.Selector.SelectedObjectsListForReading)
                if (selected is Thing thing && thing.Spawned && thing.Map == map &&
                    StorageConnectionQuery.IsStorage(thing.def)) return true;
            return false;
        }

        internal static void Dirty(Map map, CellRect rect)
        {
            if (map == null) return;
            foreach (var cell in rect)
                if (cell.InBounds(map))
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Things, true, false);
        }

        internal static void Print(SectionLayer layer, Thing thing)
        {
            if (!StorageConnectionQuery.IsStorage(thing.def) || (thing.Faction != null && thing.Faction != Faction.OfPlayer)) return;
            bool planned = StorageConnectionQuery.IsPlan(thing);
            var map = thing.Map;
            foreach (var cell in thing.OccupiedRect())
            {
                // Overlapping conduit/building footprints get a single overlay tile.
                if (cell.Fogged(map) || StorageConnectionQuery.NodeAt(map, cell, thing.Faction, true) != thing) continue;
                var links = StorageConnectionQuery.LinksAt(map, cell, thing.Faction, planned);
                var material = MaterialAtlasPool.SubMaterialFromAtlas(planned ? PlannedMaterial : BuiltMaterial, links);
                Printer_Plane.PrintPlane(layer, cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MapDataOverlay), Vector2.one, material);
            }
        }

        internal static void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostColor, Thing installing)
        {
            var map = Find.CurrentMap;
            if (map == null || StorageConnectionQuery.IsConduit(def)) return;
            var rect = GenAdj.OccupiedRect(center, rot, def.size);
            var root = MaterialPool.MatFrom(AtlasPath, ShaderDatabase.MetaOverlay, ghostColor, 3600);
            foreach (var cell in rect)
            {
                if (!cell.InBounds(map) || cell.Fogged(map)) continue;
                var links = StorageConnectionQuery.LinksAt(map, cell, Faction.OfPlayer, true, rect, installing);
                Graphics.DrawMesh(MeshPool.plane10, cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MapDataOverlay),
                    Quaternion.identity, MaterialAtlasPool.SubMaterialFromAtlas(root, links), 0);
            }
        }
    }

    // Verse discovers SectionLayer subclasses itself. Meshes regenerate on local changes,
    // while visibility follows the active build/install tool or selected storage facility.
    public sealed class SectionLayer_StorageNetwork : SectionLayer_Things
    {
        public SectionLayer_StorageNetwork(Section section) : base(section)
        {
            requireAddToMapMesh = false;
            relevantChangeTypes = (ulong)MapMeshFlagDefOf.Things | (ulong)MapMeshFlagDefOf.Buildings | (ulong)MapMeshFlagDefOf.FogOfWar;
        }
        public override void DrawLayer()
        { if (StorageNetworkDrawing.ShouldDraw(Map)) base.DrawLayer(); }
        protected override void TakePrintFrom(Thing thing) => StorageNetworkDrawing.Print(this, thing);
    }
}
