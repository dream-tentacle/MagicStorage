using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageShelfDrawing
    {
        static StorageShelfDrawing()
        {
            var harmony = new Harmony("mjcg.magicstorage.shelfDrawing");
            harmony.Patch(AccessTools.Method(typeof(SectionLayer_ThingsGeneral), "TakePrintFrom"),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(PrintItem)));
            // This non-virtual entry point also covers items with custom DrawAt overrides.
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DynamicDrawPhase)),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(DrawItem)));
            harmony.Patch(AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), new[] { typeof(Thing) }),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(ItemCenter)));
            harmony.Patch(AccessTools.Method(typeof(GenMapUI), nameof(GenMapUI.LabelDrawPosFor), new[] { typeof(Thing), typeof(float) }),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(LabelOffset)));
        }

        internal static bool TryGetShelf(Thing item, out Building_StorageSupplyShelf shelf, out int slot)
        {
            shelf = null;
            slot = -1;
            if (!MagicStorageMod.Settings.TidySupplyShelf || item == null || !item.Spawned ||
                item.def.category != ThingCategory.Item) return false;
            shelf = item.Map.haulDestinationManager.SlotGroupParentAt(item.Position) as Building_StorageSupplyShelf;
            if (shelf == null) return false;
            slot = shelf.StockSlot(item);
            if (slot >= 0 && shelf.StockAt(slot) == item) return true;
            shelf.ResolveStockSlots(new System.Collections.Generic.List<Thing>(shelf.GetSlotGroup().HeldThings));
            slot = shelf.StockSlot(item);
            if (slot >= 0) return true;
            // Reserved/forbidden leftovers may temporarily outlive a changed selection.
            // They remain visible without rearranging the configured stock.
            slot = item.thingIDNumber % shelf.SlotCount;
            return true;
        }

        internal static Vector3 Center(Thing item, Building_StorageSupplyShelf shelf, int slot)
        {
            Vector3 center = shelf.Position.ToVector3Shifted() + StorageShelfLayout.Offset(slot, shelf.SlotCount);
            // Draw in front of the shelf, with the lower row on top at overlaps.
            center.y = Math.Max(item.def.Altitude, shelf.def.Altitude) + 0.02f +
                slot * 0.008f / Math.Max(1, shelf.SlotCount - 1);
            return center;
        }

        private static bool ItemCenter(Thing t, ref Vector3 __result)
        {
            if (!TryGetShelf(t, out var shelf, out int slot)) return true;
            __result = Center(t, shelf, slot);
            return false;
        }

        private static void LabelOffset(Thing thing, ref float worldOffsetZ)
        {
            if (TryGetShelf(thing, out var shelf, out _)) worldOffsetZ = -StorageShelfLayout.ItemSizeFor(shelf.SlotCount) / 2f;
        }

        private static Graphic DisplayGraphic(Thing item)
        {
            Graphic graphic = item.Graphic;
            // Keep the actual stack-count/random variant and its rotation rules.
            if (graphic is Graphic_StackCount stack) graphic = stack.SubGraphicFor(item);
            if (graphic is Graphic_Random random) graphic = random.SubGraphicFor(item);
            return graphic;
        }

        private static void Geometry(Graphic graphic, Rot4 rotation, int slotCount, out Vector2 size, out float angle, out bool flip)
        {
            size = graphic.drawSize;
            flip = !graphic.ShouldDrawRotated && ((rotation == Rot4.West && graphic.WestFlipped) ||
                (rotation == Rot4.East && graphic.EastFlipped));
            if (!graphic.ShouldDrawRotated && rotation.IsHorizontal) size = new Vector2(size.y, size.x);
            angle = graphic.ShouldDrawRotated ? rotation.AsAngle + graphic.DrawRotatedExtraAngleOffset : 0f;
            if (graphic.ShouldDrawRotated && ((rotation == Rot4.West && graphic.WestFlipped) ||
                (rotation == Rot4.East && graphic.EastFlipped))) angle += 180f;
            if (flip && graphic.data != null) angle += graphic.data.flipExtraRotation;
            size = StorageShelfLayout.FitSize(size, angle, slotCount);
        }

        internal static bool PrintItem(SectionLayer_ThingsGeneral __instance, Thing t)
        {
            if (!TryGetShelf(t, out var shelf, out int slot)) return true;
            if (t.def.dontPrint) return false;
            Graphic graphic = DisplayGraphic(t);
            Geometry(graphic, t.Rotation, shelf.SlotCount, out Vector2 size, out float angle, out bool flip);
            Material material = graphic.MatAt(t.Rotation, t);
            Graphic.TryGetTextureAtlasReplacementInfo(material, t.def.category.ToAtlasGroup(), flip, true,
                out material, out Vector2[] uvs, out Color32 color);
            Printer_Plane.PrintPlane(__instance, Center(t, shelf, slot), size, material, angle, flip, uvs,
                new[] { color, color, color, color });
            return false;
        }

        internal static bool DrawItem(Thing __instance, DrawPhase phase)
        {
            if (phase != DrawPhase.Draw || !TryGetShelf(__instance, out var shelf, out int slot)) return true;
            // MapMeshAndRealTime items already have their main graphic in the section mesh.
            if (__instance.def.drawerType == DrawerType.RealtimeOnly)
            {
                Graphic graphic = DisplayGraphic(__instance);
                Geometry(graphic, __instance.Rotation, shelf.SlotCount, out Vector2 size, out float angle, out bool flip);
                Graphics.DrawMesh(flip ? MeshPool.GridPlaneFlip(size) : MeshPool.GridPlane(size),
                    Center(__instance, shelf, slot), Quaternion.AngleAxis(angle, Vector3.up),
                    graphic.MatAt(__instance.Rotation, __instance), 0);
            }
            return false;
        }

        internal static void RefreshAll()
        {
            foreach (Map map in Find.Maps)
                foreach (Thing thing in map.listerThings.AllThings)
                    if (thing is Building_StorageSupplyShelf)
                        map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things, true, false);
        }
    }
}
