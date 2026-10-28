using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageBuildMaterialPatches
    {
        // The transpiler rewrites two call operands inside Designator_Build.ProcessInput and bails
        // out when another mod's transpiler changed the instruction stream. The prefix below is the
        // conflict-proof alternative: it replaces the whole method when a compatibility option asks
        // for it, so a broken instruction match can never block construction again.
        private const string HarmonyId = "mjcg.magicstorage.buildmaterials";

        private static readonly FieldInfo stuffDefField = AccessTools.Field(typeof(Designator_Build), "stuffDef");
        private static readonly FieldInfo writeStuffField = AccessTools.Field(typeof(Designator_Build), "writeStuff");
        private static readonly MethodInfo checkCanInteract = AccessTools.Method(typeof(Designator), "CheckCanInteract");

        // First so this fix is not preempted: in Harmony a prefix that returns false stops every
        // later prefix from running, so a lower priority could be silently bypassed.
        private static readonly HarmonyMethod prefixPatch =
            new HarmonyMethod(typeof(StorageBuildMaterialPatches), nameof(UseStoredMaterials)) { priority = Priority.First };
        private static readonly HarmonyMethod transpilerPatch =
            new HarmonyMethod(typeof(StorageBuildMaterialPatches), nameof(IncludeStoredMaterials)) { priority = Priority.First };

        private static readonly List<ThingDef> storedOrder = new List<ThingDef>();
        private static readonly HashSet<ThingDef> storedLookup = new HashSet<ThingDef>();

        static StorageBuildMaterialPatches()
        {
            MethodInfo target = AccessTools.Method(typeof(Designator_Build), nameof(Designator_Build.ProcessInput));
            if (target == null)
            {
                Log.Error("[MagicStorage] Designator_Build.ProcessInput was not found; build material compatibility is disabled.");
                return;
            }
            if (stuffDefField == null || writeStuffField == null || checkCanInteract == null)
                Log.Warning("[MagicStorage] Designator_Build internals changed; force-compat build materials will fall back to vanilla.");
            new Harmony(HarmonyId).Patch(target, prefix: prefixPatch, transpiler: transpilerPatch);
        }

        // Full replacement of Designator_Build.ProcessInput, copied from vanilla, with two
        // compatibility modes layered on top:
        //   AllowAllMaterials         -> every stuff def is offered, nothing is counted.
        //   ForceCompatBuildMaterials -> offer stuffs that exist on the map or in magic storage.
        // With both options off it returns true and vanilla keeps running (through the transpiler).
        internal static bool UseStoredMaterials(Designator_Build __instance, Event ev)
        {
            try
            {
                MagicStorageSettings settings = MagicStorageMod.Settings;
                bool allMaterials = settings.AllowAllMaterials;
                if (!allMaterials && !settings.ForceCompatBuildMaterials) return true;
                if (stuffDefField == null || writeStuffField == null) return true;

                // Vanilla returns early when the designator may not be interacted with; let it.
                if (!CanInteract(__instance)) return true;

                if (!(__instance.PlacingDef is ThingDef building) || !building.MadeFromStuff) return true;
                Map map = __instance.Map;
                if (map == null) return true;

                if (allMaterials) ClearStoredMaterials();
                else CollectStoredMaterials(map);

                IEnumerable<ThingDef> sources = allMaterials
                    ? (IEnumerable<ThingDef>)DefDatabase<ThingDef>.AllDefsListForReading
                    : CandidateDefs(map);

                Designator_Build designator = __instance;
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (ThingDef item in from d in sources
                                          orderby d.stuffProps?.commonality ?? float.PositiveInfinity descending, d.BaseMarketValue
                                          select d)
                {
                    if (!item.IsStuff || !item.stuffProps.CanMake(building) ||
                        !(DebugSettings.godMode || allMaterials || HasMaterialSource(map, item))) continue;

                    ThingDef localStuffDef = item;
                    string str = ((designator.sourcePrecept == null)
                        ? GenLabel.ThingLabel(building, localStuffDef)
                        : ((string)"ThingMadeOfStuffLabel".Translate(localStuffDef.LabelAsStuff, designator.sourcePrecept.Label)));
                    str = str.CapitalizeFirst();
                    FloatMenuOption floatMenuOption = new FloatMenuOption(str, delegate
                    {
                        BaseProcessInput(designator, ev);
                        Find.DesignatorManager.Select(designator);
                        stuffDefField.SetValue(designator, localStuffDef);
                        writeStuffField.SetValue(designator, true);
                    }, item);
                    floatMenuOption.tutorTag = "SelectStuff-" + building.defName + "-" + localStuffDef.defName;
                    list.Add(floatMenuOption);
                }
                if (list.Count == 0)
                {
                    Messages.Message("NoStuffsToBuildWith".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                FloatMenu floatMenu = new FloatMenu(list);
                floatMenu.onCloseCallback = delegate
                {
                    writeStuffField.SetValue(designator, true);
                };
                Find.WindowStack.Add(floatMenu);
                Find.DesignatorManager.Select(designator);
                return false;
            }
            catch (Exception exception)
            {
                Log.Error("[MagicStorage] Build material compatibility failed; falling back to vanilla. " + exception);
                return true;
            }
        }

        // Designator.ProcessInput, which Designator_Build.ProcessInput reaches through base.
        private static void BaseProcessInput(Designator designator, Event ev)
        {
            if (!CanInteract(designator)) return;
            SoundDef sound = designator.CurActivateSound;
            if (sound != null) sound.PlayOneShotOnCamera();
            Find.DesignatorManager.Select(designator);
        }

        // Command.ProcessInput only plays the activate sound, which BaseProcessInput does above.
        private static bool CanInteract(Designator designator)
            => checkCanInteract == null || (bool)checkCanInteract.Invoke(designator, null);

        private static bool HasMaterialSource(Map map, ThingDef def)
            => map.listerThings.ThingsOfDef(def).Count > 0 || storedLookup.Contains(def);

        // Vanilla candidate source, plus stuff defs that only exist inside magic storage.
        private static IEnumerable<ThingDef> CandidateDefs(Map map)
        {
            List<ThingDef> result = new List<ThingDef>(map.resourceCounter.AllCountedAmounts.Keys);
            if (storedOrder.Count == 0) return result;
            HashSet<ThingDef> seen = new HashSet<ThingDef>(result);
            for (int i = 0; i < storedOrder.Count; i++)
                if (seen.Add(storedOrder[i])) result.Add(storedOrder[i]);
            return result;
        }

        private static void ClearStoredMaterials()
        {
            storedOrder.Clear();
            storedLookup.Clear();
        }

        private static void CollectStoredMaterials(Map map)
        {
            ClearStoredMaterials();
            MapComponent_StorageNetworks manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return;
            foreach (Building_StorageUnit unit in manager.StorageUnits)
            {
                // Match stored-resource ownership and visibility, without requiring power,
                // a working network, unreserved stock, or enough material for construction.
                if (!unit.Spawned || unit.Destroyed || unit.Map != map || unit.Faction != Faction.OfPlayer ||
                    unit.Position.Fogged(map)) continue;
                foreach (ThingDef def in unit.Inventory.IndexedDefs)
                {
                    if (storedLookup.Contains(def) || !HasStoredStack(unit, def)) continue;
                    storedLookup.Add(def);
                    storedOrder.Add(def);
                }
            }
        }

        private static bool HasStoredStack(Building_StorageUnit unit, ThingDef def)
        {
            Thing stored = unit.Inventory.StackAt(def, 0);
            return stored != null && !stored.Destroyed && stored.stackCount > 0 &&
                stored.holdingOwner == unit.Inventory.Contents;
        }

        // Kept as the fallback path for when both compatibility options are off.
        internal static IEnumerable<CodeInstruction> IncludeStoredMaterials(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo query = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsOfDef),
                new[] { typeof(ThingDef) });
            MethodInfo count = AccessTools.PropertyGetter(typeof(List<Thing>), nameof(List<Thing>.Count));
            MethodInfo keys = AccessTools.PropertyGetter(typeof(Dictionary<ThingDef, int>), nameof(Dictionary<ThingDef, int>.Keys));
            int match = -1, matches = 0, keysMatch = -1, keysMatches = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].Calls(keys)) { keysMatch = i; keysMatches++; }
                if (!code[i].Calls(query)) continue;
                match = i;
                matches++;
            }
            int next = match + 1;
            while (next < code.Count && code[next].opcode == OpCodes.Nop) next++;
            if (keysMatches != 1 || matches != 1 || next >= code.Count || !code[next].Calls(count))
            {
                Log.Error("[MagicStorage] Build material query did not match exactly one resource Keys and ThingsOfDef().Count call; leaving ProcessInput unchanged. Enable \"Force compatibility: build materials\" if construction is blocked.");
                return code;
            }

            // Keep native legality checks, ordering and menu actions. Only change the
            // candidate source and presence test, preserving labels and exception boundaries.
            code[keysMatch].opcode = OpCodes.Call;
            code[keysMatch].operand = AccessTools.Method(typeof(StorageBuildMaterialPatches), nameof(MaterialDefsForBuild));
            code[match].opcode = OpCodes.Call;
            code[match].operand = AccessTools.Method(typeof(StorageBuildMaterialPatches), nameof(MaterialCountForBuild));
            code[next].opcode = OpCodes.Nop;
            code[next].operand = null;
            return code;
        }

        private static IEnumerable<ThingDef> MaterialDefsForBuild(Dictionary<ThingDef, int> counts)
        {
            if (MagicStorageMod.Settings.AllowAllMaterials) return DefDatabase<ThingDef>.AllDefsListForReading;
            return counts.Keys;
        }

        private static int MaterialCountForBuild(ListerThings lister, ThingDef def) =>
            MagicStorageMod.Settings.AllowAllMaterials ? 1 : ThingsOfDefForBuild(lister, def).Count;

        private static List<Thing> ThingsOfDefForBuild(ListerThings lister, ThingDef def)
        {
            List<Thing> ground = lister.ThingsOfDef(def);
            if (ground.Count > 0) return ground;

            // Designator.Map also resolves Find.CurrentMap. This replacement is confined
            // to ProcessInput; ordinary map queries must never expose container contents.
            Map map = Find.CurrentMap;
            if (map == null || !ReferenceEquals(lister, map.listerThings)) return ground;
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            if (manager == null) return ground;
            foreach (Building_StorageUnit unit in manager.StorageUnits)
            {
                // Match stored-resource ownership and visibility, without requiring power,
                // a working network, unreserved stock, or enough material for construction.
                if (!unit.Spawned || unit.Destroyed || unit.Map != map || unit.Faction != Faction.OfPlayer ||
                    unit.Position.Fogged(map) || unit.Inventory.GetTotalCount(def) <= 0) continue;
                Thing stored = unit.Inventory.StackAt(def, 0);
                if (stored == null || stored.Destroyed || stored.stackCount <= 0 ||
                    stored.holdingOwner != unit.Inventory.Contents) continue;
                // The guarded call site only checks Count. One real stack establishes
                // presence without copying the inventory or altering the map's own list.
                return new List<Thing> { stored };
            }
            return ground;
        }
    }
}
