using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using MapParent = RimWorld.Planet.MapParent;

namespace MagicStorage
{
    internal static class StorageCaravanWorldGizmos
    {
        private static readonly FieldInfo settlementsField = AccessTools.Field(typeof(WorldGizmoUtility), "settlements");

        internal static IEnumerable<CodeInstruction> InsertCommands(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            MethodInfo getCaravan = AccessTools.Method(typeof(WorldGizmoUtility), nameof(WorldGizmoUtility.TryGetCaravanGizmo));
            MethodInfo add = AccessTools.Method(typeof(List<object>), nameof(List<object>.Add));
            bool foundCaravan = false;
            for (int i = 0; i < code.Count; i++)
            {
                if (Equals(code[i].operand, getCaravan)) foundCaravan = true;
                if (!foundCaravan || !Equals(code[i].operand, add)) continue;
                CodeInstruction loadGizmo = code[i - 1];
                if (loadGizmo.opcode != OpCodes.Ldloc && loadGizmo.opcode != OpCodes.Ldloc_S &&
                    loadGizmo.opcode != OpCodes.Ldloc_0 && loadGizmo.opcode != OpCodes.Ldloc_1 &&
                    loadGizmo.opcode != OpCodes.Ldloc_2 && loadGizmo.opcode != OpCodes.Ldloc_3)
                    throw new InvalidOperationException("[MagicStorage] Native caravan gizmo local could not be identified.");
                // Append to the same collection immediately after the original command is added.
                // The native branch and tile-picker checks also govern this call.
                code.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(loadGizmo.opcode, loadGizmo.operand),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(WorldGizmoUtility), "tmpObjectsList")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StorageCaravanWorldGizmos), nameof(AppendCommands)))
                });
                return code;
            }
            throw new InvalidOperationException("[MagicStorage] Native caravan gizmo insertion point could not be found.");
        }

        private static void AppendCommands(Gizmo original, List<object> objects)
        {
            if (!(original is Command_Action command) || command.defaultLabel != "CommandFormCaravan".Translate()) return;
            Map map = SourceMap();
            if (map != null) objects.AddRange(CommandsFor(map));
        }

        private static Map SourceMap()
        {
            // Match TryGetCaravanGizmo's source selection, including its no-selection fallback.
            PlanetTile tile = Find.WorldSelector.SelectedTile;
            Settlement settlement = null;
            foreach (WorldObject selected in Find.WorldSelector.SelectedObjects)
            {
                if (selected is Settlement candidate) settlement = candidate;
                if (!tile.Valid) tile = selected.Tile;
            }
            settlement = settlement ?? Find.WorldObjects.SettlementAt(tile);
            if (settlement != null) return settlement.Faction == Faction.OfPlayer ? settlement.Map : null;
            MapParent parent = Find.WorldObjects.MapParentAt(tile);
            FormCaravanComp forming = parent?.GetComponent<FormCaravanComp>();
            if (forming != null && parent.HasMap && parent.Map.mapPawns.ColonistCount > 0 && !forming.Reform)
                return parent.Map;
            if (tile.Valid) return null;
            var settlements = (List<MapParent>)settlementsField.GetValue(null);
            return settlements.Count > 0 ? settlements[0].Map : null;
        }

        internal static IEnumerable<Command_Action> CommandsFor(Map map)
        {
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            manager.EnsureCurrent();
            foreach (Building_StorageCore core in manager.StorageCores.OrderBy(core => core.thingIDNumber))
            {
                if (!core.Spawned || core.Map != map || core.Faction != Faction.OfPlayer ||
                    !core.CanWork || core.Network.Core != core) continue;
                Building_CaravanTeleporter teleporter = FindTeleporter(core);
                var command = new Command_Action
                {
                    defaultLabel = "MS_Caravan_WorldOpen".Translate(),
                    defaultDesc = "MS_Caravan_OpenDesc".Translate() + "\n\n" +
                        "MS_Caravan_CorePosition".Translate(core.Position.x, core.Position.z),
                    icon = FormCaravanComp.FormCaravanCommand,
                    // Identical labels must remain separate when a map has several networks.
                    groupable = false,
                    action = () => Open(core, map)
                };
                if (teleporter == null) command.Disable("MS_Caravan_NoTeleporter".Translate());
                else if (!map.Tile.LayerDef.canFormCaravans) command.Disable("MS_Caravan_LayerUnavailable".Translate());
                yield return command;
            }
        }

        private static Building_CaravanTeleporter FindTeleporter(Building_StorageCore core)
        {
            Building_CaravanTeleporter result = null;
            foreach (CompStorageNode node in core.Network.Members)
                if (node.parent is Building_CaravanTeleporter terminal && terminal.CanWork &&
                    terminal.Map == core.Map && terminal.Network == core.Network &&
                    (result == null || terminal.thingIDNumber < result.thingIDNumber)) result = terminal;
            return result;
        }

        private static void Open(Building_StorageCore core, Map map)
        {
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (!core.Spawned || core.Map != map || core.Faction != Faction.OfPlayer || !core.CanWork || core.Network.Core != core)
            { Dialog_StorageCaravan.Reject("MS_Caravan_Unavailable"); return; }
            Building_CaravanTeleporter teleporter = FindTeleporter(core);
            if (teleporter == null)
            { Dialog_StorageCaravan.Reject("MS_Caravan_NoTeleporter"); return; }
            if (!map.Tile.LayerDef.canFormCaravans)
            { Dialog_StorageCaravan.Reject("MS_Caravan_LayerUnavailable"); return; }
            Find.WindowStack.Add(new Dialog_StorageCaravan(teleporter));
        }
    }
}
