using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageShelfDrawing
    {
        private static readonly Dictionary<Type, bool> customDrawers = new Dictionary<Type, bool>();

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
            harmony.Patch(AccessTools.Method(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI)),
                transpiler: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(FilterGUIOverlays)));
            harmony.Patch(AccessTools.Method(typeof(OverlayDrawer), nameof(OverlayDrawer.DrawOverlay)),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(QueueOverlay)));
        }

        internal static bool TryGetShelf(Thing item, out Building_StorageSupplyShelf shelf, out int slot)
        {
            shelf = null;
            slot = -1;
            if (item == null || !item.Spawned ||
                item.def.category != ThingCategory.Item) return false;
            shelf = item.Map.haulDestinationManager.SlotGroupParentAt(item.Position) as Building_StorageSupplyShelf;
            if (shelf == null) return false;
            // Hidden shelves bypass both the tidy-storage setting and stock-slot resolution.
            if (StorageShelfVisibility.HidesContents(shelf)) return true;
            if (!MagicStorageMod.Settings.TidySupplyShelf) return false;
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
            if (!TryGetShelf(t, out var shelf, out int slot) || StorageShelfVisibility.HidesContents(shelf)) return true;
            __result = Center(t, shelf, slot);
            return false;
        }

        private static void LabelOffset(Thing thing, ref float worldOffsetZ)
        {
            if (TryGetShelf(thing, out var shelf, out _) && !StorageShelfVisibility.HidesContents(shelf))
                worldOffsetZ = -StorageShelfLayout.ItemSizeFor(shelf.SlotCount) / 2f;
        }

        private static bool QueueOverlay(Thing t) => !StorageShelfVisibility.IsHidden(t);

        private static IEnumerable<CodeInstruction> FilterGUIOverlays(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(Thing), nameof(Thing.DrawGUIOverlay));
            MethodInfo filtered = AccessTools.Method(typeof(StorageShelfVisibility), nameof(StorageShelfVisibility.DrawGUIOverlay));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && Equals(instruction.operand, original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = filtered;
                }
                yield return instruction;
            }
        }

        private static Graphic DisplayGraphic(Thing item)
        {
            Graphic graphic = item.Graphic;
            // Keep the actual stack-count/random variant and its rotation rules.
            if (graphic is Graphic_StackCount stack) graphic = stack.SubGraphicFor(item);
            if (graphic is Graphic_Random random) graphic = random.SubGraphicFor(item);
            return graphic;
        }

        private static bool UsesPlaceholder(Thing item)
        {
            if (item.def.graphicData == null) return true;
            Type type = item.GetType();
            if (!customDrawers.TryGetValue(type, out bool custom))
            {
                custom = !OrdinaryMethod(AccessTools.Method(type, nameof(Thing.DynamicDrawPhaseAt),
                    new[] { typeof(DrawPhase), typeof(Vector3), typeof(bool) })) ||
                    !OrdinaryMethod(AccessTools.Method(type, "DrawAt", new[] { typeof(Vector3), typeof(bool) })) ||
                    !OrdinaryMethod(AccessTools.Method(type, nameof(Thing.Print), new[] { typeof(SectionLayer) }));
                customDrawers.Add(type, custom);
            }
            if (custom) return true;
            Graphic graphic = item.Graphic;
            return graphic == null || ReferenceEquals(graphic, BaseContent.BadGraphic);
        }

        private static bool OrdinaryMethod(MethodInfo method) => method != null &&
            (method.DeclaringType == typeof(Thing) || method.DeclaringType == typeof(ThingWithComps));

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
            // The original method's catch cannot catch exceptions thrown by a prefix.
            try
            {
                return PrintShelfItem(__instance, t);
            }
            catch (Exception ex)
            {
                ReportDrawingError(t, "StaticPrint", "Retry vanilla printing with its exception handler", ex);
                return true;
            }
        }

        private static void ReportDrawingError(Thing item, string stage, string recovery, Exception error)
        {
            string defName = item?.def?.defName ?? "<unknown>";
            Type type = item?.GetType();
            string typeName = type?.FullName ?? "<unknown>";
            var source = item?.def?.modContentPack;
            // Deduplicate per stage and item definition/type, not globally: a failure
            // for one mod's item must not suppress diagnostics for another item.
            string key = "MagicStorage.ShelfDrawing|" + stage + "|" + defName + "|" + typeName;
            Log.ErrorOnce("[MagicStorage] Shelf item rendering failed." +
                " Stage=" + stage + "; DefName=" + defName +
                "; RuntimeType=" + typeName + "; RuntimeAssembly=" + (type?.Assembly.GetName().Name ?? "<unknown>") +
                "; DefinitionMod=" + (source?.Name ?? "<unknown>") +
                "; DefinitionPackageId=" + (source?.PackageId ?? "<unknown>") +
                "; Recovery=" + recovery + ".\n" +
                "Source metadata identifies the item, not the mod responsible for the error. " +
                "A MagicStorage frame or Harmony patch listing alone does not establish the cause; " +
                "inspect the exception and full stack trace (including possible compatibility issues).\n" + error,
                key.GetHashCode());
        }

        private static bool PreventsParentDrawing(Thing item)
        {
            if (item is ThingWithComps withComps)
                foreach (ThingComp comp in withComps.AllComps)
                    if (comp.DontDrawParent()) return true;
            return false;
        }

        private static bool PrintShelfItem(SectionLayer_ThingsGeneral layer, Thing t)
        {
            if (!TryGetShelf(t, out var shelf, out int slot)) return true;
            if (StorageShelfVisibility.HidesContents(shelf)) return false;
            if (UsesPlaceholder(t))
            {
                if (t.def.drawerType == DrawerType.MapMeshOnly || t.def.drawerType == DrawerType.MapMeshAndRealTime)
                    PrintPlaceholder(layer, t, shelf, slot);
                return false;
            }
            if (!t.def.dontPrint && !PreventsParentDrawing(t))
                PrintGraphic(layer, t, shelf, slot);
            if (t is ThingWithComps withComps)
                foreach (ThingComp comp in withComps.AllComps)
                    comp.PostPrintOnto(layer);
            return false;
        }

        private static void PrintGraphic(SectionLayer layer, Thing t, Building_StorageSupplyShelf shelf, int slot)
        {
            Graphic graphic = DisplayGraphic(t);
            Geometry(graphic, t.Rotation, shelf.SlotCount, out Vector2 size, out float angle, out bool flip);
            Material material = graphic.MatAt(t.Rotation, t);
            Graphic.TryGetTextureAtlasReplacementInfo(material, t.def.category.ToAtlasGroup(), flip, true,
                out material, out Vector2[] uvs, out Color32 color);
            Printer_Plane.PrintPlane(layer, Center(t, shelf, slot), size, material, angle, flip, uvs,
                new[] { color, color, color, color });
        }

        internal static bool DrawItem(Thing __instance, DrawPhase phase)
        {
            if (phase != DrawPhase.Draw) return !StorageShelfVisibility.IsHidden(__instance);
            if (!TryGetShelf(__instance, out var shelf, out int slot)) return true;
            if (StorageShelfVisibility.HidesContents(shelf)) return false;
            if (UsesPlaceholder(__instance))
            {
                if (__instance.def.drawerType == DrawerType.RealtimeOnly)
                    DrawPlaceholder(__instance, shelf, slot);
                return false;
            }
            if (!PreventsParentDrawing(__instance) && __instance.def.drawerType == DrawerType.RealtimeOnly)
                DrawFallbackGraphic(__instance, shelf, slot);
            // Match ThingWithComps.DrawAt: all DrawAt callbacks precede PostDraw.
            if (__instance is ThingWithComps withComps)
            {
                foreach (ThingComp comp in withComps.AllComps)
                    comp.DrawAt(Center(__instance, shelf, slot), false);
                foreach (ThingComp comp in withComps.AllComps)
                    comp.PostDraw();
            }
            return false;
        }

        private static void DrawFallbackGraphic(Thing item, Building_StorageSupplyShelf shelf, int slot)
        {
            try
            {
                Graphic graphic = DisplayGraphic(item);
                if (graphic != null && !ReferenceEquals(graphic, BaseContent.BadGraphic))
                {
                    Geometry(graphic, item.Rotation, shelf.SlotCount, out Vector2 size, out float angle, out bool flip);
                    Graphics.DrawMesh(flip ? MeshPool.GridPlaneFlip(size) : MeshPool.GridPlane(size),
                        Center(item, shelf, slot), Quaternion.AngleAxis(angle, Vector3.up),
                        graphic.MatAt(item.Rotation, item), 0);
                    return;
                }
            }
            catch (Exception ex)
            {
                ReportDrawingError(item, "FallbackGraphicResolutionAndDraw", "Draw placeholder", ex);
            }
            DrawPlaceholder(item, shelf, slot);
        }

        private static Material PlaceholderMaterial(Building_StorageSupplyShelf shelf, out Vector2 size)
        {
            Texture2D texture = ContentFinder<Texture2D>.Get("UI/MagicStorageMissingItem");
            size = StorageShelfLayout.FitSize(new Vector2(texture.width, texture.height), 0f, shelf.SlotCount);
            return MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, Color.white));
        }

        private static void PrintPlaceholder(SectionLayer layer, Thing item, Building_StorageSupplyShelf shelf, int slot)
        {
            Material material = PlaceholderMaterial(shelf, out Vector2 size);
            Graphic.TryGetTextureAtlasReplacementInfo(material, item.def.category.ToAtlasGroup(), false, true,
                out material, out Vector2[] uvs, out Color32 color);
            Printer_Plane.PrintPlane(layer, Center(item, shelf, slot), size, material, 0f, false, uvs,
                new[] { color, color, color, color });
        }

        private static void DrawPlaceholder(Thing item, Building_StorageSupplyShelf shelf, int slot)
        {
            Material material = PlaceholderMaterial(shelf, out Vector2 size);
            Graphics.DrawMesh(MeshPool.GridPlane(size), Center(item, shelf, slot),
                Quaternion.AngleAxis(0f, Vector3.up), material, 0);
        }

        internal static void RefreshAll()
        {
            foreach (Map map in Find.Maps)
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing is Building_StorageSupplyShelf)
                        map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things, true, false);
                }
        }
    }
}
