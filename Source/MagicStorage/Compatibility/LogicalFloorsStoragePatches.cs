using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    // All extra storage rules live here and are installed only for Logical Floors.
    internal static class LogicalFloorsStoragePatches
    {
        private static LogicalFloorsApi api;

        internal static void Install(Harmony harmony, LogicalFloorsApi resolved, MethodInfo route)
        {
            api = resolved;
            foreach (string method in new[] { "Process", "TryReturnAll", "RemoveStock", "Reserved" })
                harmony.Patch(AccessTools.Method(typeof(StorageShelfSupply), method),
                    prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndScope)),
                    transpiler: method == "TryReturnAll" ? Hook(nameof(PlacementCalls)) : new HarmonyMethod(route));
            harmony.Patch(AccessTools.Method(typeof(StorageShelfSupply), "PlaceStock"),
                prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndScope)), transpiler: Hook(nameof(PlacementCalls)));
            harmony.Patch(AccessTools.Method(typeof(Building_StorageSupplyShelf), "IsShelfStock"), postfix: Hook(nameof(ShelfStock)));
            harmony.Patch(AccessTools.Method(typeof(Building_StorageSupplyShelf), "Accepts"), postfix: Hook(nameof(ShelfStock)));
            harmony.Patch(AccessTools.Method(typeof(Building_StorageSupplyShelf), "Notify_SettingsChanged"),
                prefix: Hook(nameof(BeginShelfInstance)), finalizer: Hook(nameof(EndScope)), transpiler: new HarmonyMethod(route));
            harmony.Patch(AccessTools.Method(typeof(StorageShelfHaulingPatches), "SkipShelfMerge"),
                prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndScope)), transpiler: new HarmonyMethod(route));
            harmony.Patch(AccessTools.Method(typeof(StorageShelfHaulingPatches), "CanPickUpDuplicate"),
                prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndScope)), transpiler: new HarmonyMethod(route));
            harmony.Patch(AccessTools.Method(typeof(StorageShelfTemperaturePatches), "AdjustItemTemperature"),
                prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndScope)), transpiler: new HarmonyMethod(route));
            // Click and drag selection share this predicate. Resolve storage on the item's
            // floor when it is evaluated, including during deferred candidate enumeration.
            harmony.Patch(AccessTools.Method(typeof(StorageSelectionPatches), "IsSupplyShelfItem"),
                prefix: Hook(nameof(BeginShelf)), finalizer: Hook(nameof(EndScope)), transpiler: new HarmonyMethod(route));

            harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), "CollectAdjacent"), prefix: Hook(nameof(CollectAdjacent)));
            harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), "EnqueueAt"), prefix: Hook(nameof(EnqueueAt)));
            harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), "Rebuild"), transpiler: Hook(nameof(RebuildCalls)));
            harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), "HasNodeAt"), postfix: Hook(nameof(HasNodeAt)));
            foreach (string method in new[] { "Register", "Unregister" })
                harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), method),
                    prefix: Hook(nameof(BeginNode)), finalizer: Hook(nameof(EndScope)));
            harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), "NotifyFactionChanged"),
                prefix: Hook(nameof(BeginMapThing)), finalizer: Hook(nameof(EndScope)));
            harmony.Patch(AccessTools.Method(typeof(MapComponent_StorageNetworks), "DirtyDrawing"), transpiler: new HarmonyMethod(route));

            Type drawing = AccessTools.TypeByName("MagicStorage.StorageShelfDrawing");
            MethodInfo findShelf = drawing == null ? null : AccessTools.Method(drawing, "TryGetShelf");
            if (findShelf != null)
                harmony.Patch(findShelf, prefix: Hook(nameof(BeginShelfDrawing)),
                    finalizer: Hook(nameof(EndScope)), transpiler: new HarmonyMethod(route));
            foreach (string name in new[] { "MagicStorage.StorageNetworkDrawing", "MagicStorage.Graphic_StorageConduit" })
            {
                Type type = AccessTools.TypeByName(name);
                if (type == null) continue;
                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                {
                    if (method.Name != "Print" && method.Name != "ShouldLinkWith" && method.Name != "Dirty") continue;
                    bool owner = method.Name != "Dirty";
                    harmony.Patch(method, prefix: owner ? Hook(method.Name == "ShouldLinkWith" ? nameof(BeginStorageLink) : nameof(BeginStorageDrawing)) : null,
                        finalizer: owner ? Hook(nameof(EndScope)) : null, transpiler: new HarmonyMethod(route));
                }
            }
        }

        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(LogicalFloorsStoragePatches), name)
            { priority = Priority.Last };
        private static void BeginShelf(Thing __0, out IDisposable __state) => __state = api.Enter(__0);
        private static void BeginShelfInstance(Thing __instance, out IDisposable __state) => __state = api.Enter(__instance);
        private static bool BeginShelfDrawing(Thing __0, ref Building_StorageSupplyShelf __1, ref int __2,
            ref bool __result, out IDisposable __state)
        {
            __state = null;
            if (MagicStorageMod.Settings.TidySupplyShelf && __0?.def?.category == ThingCategory.Item &&
                api.TryEnter(__0, out __state)) return true;
            __1 = null;
            __2 = -1;
            __result = false;
            return false;
        }

        private static bool BeginStorageDrawing(Thing __1, out IDisposable __state)
        {
            __state = null;
            return StorageConnectionQuery.IsStorage(__1?.def) && api.TryEnter(__1, out __state);
        }

        private static bool BeginStorageLink(Thing __1, ref bool __result, out IDisposable __state)
        {
            if (BeginStorageDrawing(__1, out __state)) return true;
            __result = false;
            return false;
        }
        private static void BeginNode(Map ___map, CompStorageNode __0, out IDisposable __state) =>
            __state = api.EnterFloor(___map, api.GetFloor(__0.parent));
        private static void BeginMapThing(Map ___map, Thing __0, out IDisposable __state) =>
            __state = api.EnterFloor(___map, api.GetFloor(__0));
        private static void EndScope(IDisposable __state) => __state?.Dispose();
        private static void ShelfStock(Thing __0, Building_StorageSupplyShelf __instance, ref bool __result)
        { if (__result) __result = api.SameFloor(__0, __instance); }

        private static bool Connected(CompStorageNode a, CompStorageNode b) =>
            a.parent.Faction == b.parent.Faction && (api.SameFloor(a.parent, b.parent) ||
            a.parent is Building_StorageConduit && b.parent is Building_StorageConduit && a.parent.Position == b.parent.Position);

        private static bool CollectAdjacent(Dictionary<IntVec3, List<CompStorageNode>> ___grid,
            IntVec3 __0, CompStorageNode __1, HashSet<StorageNetwork> __2)
        {
            if (___grid.TryGetValue(__0, out var nodes))
                foreach (CompStorageNode node in nodes)
                    if (node != __1 && node.Network != null && Connected(__1, node)) __2.Add(node.Network);
            return false;
        }

        private static bool EnqueueAt(Dictionary<IntVec3, List<CompStorageNode>> ___grid,
            IntVec3 __0, CompStorageNode __1, HashSet<CompStorageNode> __2, Queue<CompStorageNode> __3)
        {
            if (___grid.TryGetValue(__0, out var nodes))
                foreach (CompStorageNode node in nodes)
                    if (Connected(__1, node) && __2.Add(node)) __3.Enqueue(node);
            return false;
        }

        private static void HasNodeAt(Dictionary<IntVec3, List<CompStorageNode>> ___grid, IntVec3 __0, Thing __1, ref bool __result)
        {
            if (!__result) return;
            __result = false;
            if (___grid.TryGetValue(__0, out var nodes))
                foreach (CompStorageNode node in nodes)
                    if (node.parent.Faction == __1.Faction && api.SameFloor(node.parent, __1)) { __result = true; return; }
        }

        // Keep the native graph traversal; only its edge checks need the current
        // dequeued node instead of the component's root when floors differ.
        private static IEnumerable<CodeInstruction> RebuildCalls(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo add = AccessTools.Method(typeof(StorageNetwork), "AddNode");
            MethodInfo enqueue = AccessTools.Method(typeof(MapComponent_StorageNetworks), "EnqueueAt");
            CodeInstruction nodeLoad = null;
            int adds = 0, calls = 0;
            for (int i = 1; i < code.Count; i++)
                if (code[i].Calls(add)) { nodeLoad = code[i - 1]; adds++; }
            if (adds != 1 || nodeLoad == null || !LoadsLocal(nodeLoad))
                throw new InvalidOperationException("Logical Floors could not recognize storage graph traversal.");
            for (int i = 3; i < code.Count; i++)
                if (code[i].Calls(enqueue))
                {
                    if (!LoadsLocal(code[i - 3])) throw new InvalidOperationException("Logical Floors could not recognize storage graph edge arguments.");
                    code[i - 3].opcode = nodeLoad.opcode; code[i - 3].operand = nodeLoad.operand; calls++;
                }
            if (calls != 2) throw new InvalidOperationException("Logical Floors expected two storage graph edge calls.");
            return code;
        }

        private static bool LoadsLocal(CodeInstruction instruction) => instruction.opcode == OpCodes.Ldloc ||
            instruction.opcode == OpCodes.Ldloc_S || instruction.opcode == OpCodes.Ldloc_0 ||
            instruction.opcode == OpCodes.Ldloc_1 || instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Ldloc_3;

        // Rewrite only these calls in the shelf methods, never the global native APIs.
        private static IEnumerable<CodeInstruction> PlacementCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>();
            MethodInfo drop = AccessTools.Method(typeof(ThingOwner), "TryDrop", new[] { typeof(Thing), typeof(IntVec3),
                typeof(Map), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>), typeof(Predicate<IntVec3>), typeof(bool) });
            MethodInfo spawn = AccessTools.Method(typeof(GenSpawn), "Spawn", new[] { typeof(Thing), typeof(IntVec3), typeof(Map),
                typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) });
            int drops = 0, spawns = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                bool isDrop = instruction.Calls(drop), isSpawn = instruction.Calls(spawn);
                if (isDrop || isSpawn)
                {
                    var shelf = new CodeInstruction(OpCodes.Ldarg_0);
                    shelf.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    shelf.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    code.Add(shelf);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(LogicalFloorsStoragePatches), isDrop ? nameof(DropAtShelf) : nameof(SpawnAtShelf));
                    if (isDrop) drops++; else spawns++;
                }
                code.Add(instruction);
            }
            if (spawns != 1 || drops != (__originalMethod.Name == "PlaceStock" ? 1 : 0))
                throw new InvalidOperationException("Logical Floors could not recognize shelf placement calls in " + __originalMethod.Name);
            return code;
        }

        private static bool DropAtShelf(ThingOwner holder, Thing item, IntVec3 cell, Map map, ThingPlaceMode mode,
            out Thing result, Action<Thing, int> placedAction, Predicate<IntVec3> validator, bool sound, Building_StorageSupplyShelf shelf) =>
            api.TryDrop(holder, item, shelf, out result);
        private static Thing SpawnAtShelf(Thing item, IntVec3 cell, Map map, Rot4 rotation, WipeMode wipeMode,
            bool respawningAfterLoad, bool forbidLeavings, Building_StorageSupplyShelf shelf) =>
            api.SpawnDistinct(item, shelf);
    }
}
