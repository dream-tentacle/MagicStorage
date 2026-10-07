using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // All native construction boundaries live here. Supply never performs construction work.
    internal static class ConstructionSite
    {
        // Invoke the virtual native factory, including Blueprint_Storage's settings/group
        // transfer. This is access to an existing method, not a Harmony patch.
        private static readonly MethodInfo MakeSolidThing = typeof(Blueprint).GetMethod("MakeSolidThing",
            BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool CanSupply(Thing target)
        {
            if (target == null || !target.Spawned || target.Destroyed || target.Faction != Faction.OfPlayer ||
                target.IsForbidden(Faction.OfPlayer) || target.IsBurning() ||
                (!(target is Blueprint_Build) && !(target is Frame))) return false;
            var buildDef = target.def.entityDefToBuild;
            if (!(buildDef is TerrainDef) && !(buildDef is ThingDef buildingDef && buildingDef.category == ThingCategory.Building)) return false;
            if (GenConstruct.FirstBlockingThing(target, null) != null) return false;
            // Native delivery first removes replaceable flooring and waits for attachment walls.
            if (target is Blueprint_Build && buildDef is TerrainDef && target.Map.terrainGrid.CanRemoveTopLayerAt(target.Position)) return false;
            if (buildDef is ThingDef def && def.building?.isAttachment == true)
            {
                Thing wall = GenConstruct.GetWallAttachedTo(target);
                if (wall == null || wall.def.IsBlueprint || wall.def.IsFrame) return false;
            }
            return true;
        }

        internal static int Needed(Thing target, ThingDef material) =>
            ((IHaulEnroute)target).GetSpaceRemainingWithEnroute(material);

        internal static Frame MakeFrame(Blueprint_Build blueprint)
        {
            if (!CanSupply(blueprint) || MakeSolidThing == null) return null;
            Map map = blueprint.Map;
            IntVec3 position = blueprint.Position;
            Rot4 rotation = blueprint.Rotation;
            Faction faction = blueprint.Faction;
            bool selected = Find.Selector.IsSelected(blueprint);
            var args = new object[] { false };
            var frame = (Frame)MakeSolidThing.Invoke(blueprint, args);
            if (blueprint.Graphic is Graphic_Random) frame.overrideGraphicIndex = blueprint.thingIDNumber;

            // Match Blueprint.TryReplaceWithSolidThing after its pawn-dependent checks.
            // The factory already transfers style, glower overrides and enroute reservations.
            GenSpawn.WipeExistingThings(position, rotation, frame.def, map, DestroyMode.Deconstruct);
            if (!blueprint.Destroyed) blueprint.Destroy();
            if (frame.def.CanHaveFaction) frame.SetFactionDirect(faction);
            GenSpawn.Spawn(frame, position, map, rotation);
            if (selected || (bool)args[0]) Find.Selector.Select(frame, playSound: false, forceDesignatorDeselect: false);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned) pawn.pather.NotifyThingTransformed(blueprint, frame);
            foreach (IntVec3 cell in frame.OccupiedRect())
                if (map.planManager.TryGetPlan(cell, out var plan)) plan.RemoveCell(cell);
            return frame;
        }
    }
}
