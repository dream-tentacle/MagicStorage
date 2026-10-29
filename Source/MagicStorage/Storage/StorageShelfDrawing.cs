using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
        private static readonly Dictionary<Shader, Shader> mapShaders = new Dictionary<Shader, Shader>();
        private static readonly HashSet<Shader> unmappedShaders = new HashSet<Shader>();
        private static readonly Vector2 IconResolution = new Vector2(64f, 64f);
        private static readonly ConditionalWeakTable<Thing, object> iconDrawables = new ConditionalWeakTable<Thing, object>();

        static StorageShelfDrawing()
        {
            var harmony = new Harmony("mjcg.magicstorage.shelfDrawing");
            harmony.Patch(AccessTools.Method(typeof(SectionLayer_ThingsGeneral), "TakePrintFrom"),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(PrintItem)));
            // This non-virtual entry point also covers items with custom DrawAt overrides.
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DynamicDrawPhase)),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(DrawItem)));
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn)),
                prefix: new HarmonyMethod(typeof(StorageShelfDrawing), nameof(ReleaseIconDrawable)));
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

        private static bool UsesIcon(Thing item)
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

        private static Shader MapShader(Shader uiShader, bool masked)
        {
            if (mapShaders.TryGetValue(uiShader, out var shader)) return shader;
            if (!unmappedShaders.Contains(uiShader))
            {
                foreach (var def in DefDatabase<ShaderTypeDef>.AllDefsListForReading)
                {
                    shader = def.Shader;
                    if (ShaderDatabase.TryGetUIShader(shader, out var ui) && ui == uiShader)
                    {
                        mapShaders.Add(uiShader, shader);
                        return shader;
                    }
                }
                unmappedShaders.Add(uiShader);
            }
            // Unregistered UI shaders still need a map-compatible shader. Keep the
            // native material request's mask, colors, secondary texture and parameters.
            return masked ? ShaderDatabase.CutoutComplex : ShaderDatabase.Transparent;
        }

        private static Material IconMaterial(Texture texture, Color color, Material source)
        {
            if (source != null && MaterialPool.TryGetRequestForMat(source, out var request))
            {
                request.shader = MapShader(request.shader, request.maskTex != null);
                request.mainTex = texture;
                request.color *= color;
                request.colorTwo *= color;
                return MaterialPool.MatFrom(request);
            }
            return MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, color));
        }

        private static void RegisterIconDrawable(Thing item)
        {
            // Vanilla SpawnSetup excludes pure map-mesh items from dynamic drawing.
            if (item.def.drawerType != DrawerType.MapMeshOnly || iconDrawables.TryGetValue(item, out _)) return;
            item.Map.dynamicDrawManager.RegisterDrawable(item);
            iconDrawables.Add(item, new object());
        }

        private static void ReleaseIconDrawable(Thing __instance)
        {
            if (!iconDrawables.TryGetValue(__instance, out _)) return;
            __instance.Map?.dynamicDrawManager.DeRegisterDrawable(__instance);
            iconDrawables.Remove(__instance);
        }

        private static bool DrawIcon(Thing item, Building_StorageSupplyShelf shelf, int slot)
        {
            // The same resolver used by trade knows about portraits, body textures,
            // minified things and mod UI icons. No corpse/race-specific logic here.
            Texture texture = Widgets.GetIconFor(item, IconResolution, null, false,
                out float scale, out float angle, out Vector2 proportions, out Color color, out Material source);
            if (texture == null || texture == BaseContent.BadTex) return false;
            // Trade uses texture proportions when the original thing has no graphic
            // data, including square portrait render textures for human corpses.
            if (item.def.graphicData == null) proportions = new Vector2(texture.width, texture.height);
            Vector2 size = StorageShelfLayout.FitSize(proportions, angle, shelf.SlotCount);
            if (!float.IsNaN(scale) && !float.IsInfinity(scale) && scale > 0f)
                size *= Math.Min(1f, scale);
            Graphics.DrawMesh(MeshPool.GridPlane(size), Center(item, shelf, slot),
                Quaternion.AngleAxis(angle, Vector3.up), IconMaterial(texture, color, source), 0);
            return true;
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
            if (StorageShelfVisibility.HidesContents(shelf)) return false;
            if (t.def.dontPrint) return false;
            // Resolve thumbnails only on the main draw phase. Portrait textures may
            // expire or change, so never bake them into the static section mesh.
            if (UsesIcon(t)) { RegisterIconDrawable(t); return false; }
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
            if (phase != DrawPhase.Draw) return !StorageShelfVisibility.IsHidden(__instance);
            if (!TryGetShelf(__instance, out var shelf, out int slot)) return true;
            if (StorageShelfVisibility.HidesContents(shelf)) return false;
            if (UsesIcon(__instance)) return !DrawIcon(__instance, shelf, slot);
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
                {
                    if (!MagicStorageMod.Settings.TidySupplyShelf) ReleaseIconDrawable(thing);
                    if (thing is Building_StorageSupplyShelf)
                        map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things, true, false);
                }
        }
    }
}
