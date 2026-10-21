using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageTransportGroupPatches
    {
        static StorageTransportGroupPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.transportgroups");
            harmony.Patch(AccessTools.Method(typeof(CompTransporter), nameof(CompTransporter.CompGetGizmosExtra)),
                postfix: new HarmonyMethod(typeof(StorageTransportGroupPatches), nameof(AddGroupCommand)));
            harmony.Patch(AccessTools.Method(typeof(CompTransporter), nameof(CompTransporter.CompInspectStringExtra)),
                postfix: new HarmonyMethod(typeof(StorageTransportGroupPatches), nameof(ShowGroupMass)));
            harmony.Patch(AccessTools.Method(typeof(CompLaunchable), nameof(CompLaunchable.CanLaunch)),
                transpiler: new HarmonyMethod(typeof(StorageTransportGroupPatches), nameof(UseGroupMass)));
        }

        private static void AddGroupCommand(CompTransporter __instance, ref IEnumerable<Gizmo> __result) =>
            __result = WithGroupCommand(__instance, __result);

        private static List<CompTransporter> SelectedPods(CompTransporter source)
        {
            var pods = new List<CompTransporter>();
            foreach (object selected in Find.Selector.SelectedObjectsListForReading)
            {
                if (!(selected is Thing thing) || thing.def != source.parent.def || thing.Map != source.parent.Map) continue;
                var pod = thing.TryGetComp<CompTransporter>();
                if (StorageTransportGroups.IsPlayerPod(pod)) pods.Add(pod);
            }
            if (!pods.Contains(source)) pods.Insert(0, source);
            return pods;
        }

        private static IEnumerable<Gizmo> WithGroupCommand(CompTransporter source, IEnumerable<Gizmo> original)
        {
            foreach (var gizmo in original) yield return gizmo;
            if (!StorageTransportGroups.IsPlayerPod(source)) yield break;
            var pods = SelectedPods(source);
            // A multi-selection gets one explicit command for the entire selection.
            if (pods[0] != source) yield break;
            var command = new Command_Action
            {
                defaultLabel = "MS_Transport_CreateGroup".Translate(),
                defaultDesc = "MS_Transport_CreateGroupDesc".Translate(pods.Count),
                icon = CompLaunchable.LaunchCommandTex,
                groupable = false,
                action = () => CreateGroup(source)
            };
            if (pods.Any(pod => pod.groupID >= 0))
                command.Disable("MS_Transport_GroupAlreadyAssigned".Translate());
            yield return command;
        }

        private static void CreateGroup(CompTransporter source)
        {
            if (!StorageTransportGroups.IsPlayerPod(source)) return;
            var pods = SelectedPods(source);
            // Match the native multi-pod loading command's launch-pad and reachability rules.
            var launchable = (CompLaunchable_TransportPod)source.Launchable;
            ThingWithComps port = launchable.FuelingPortSource?.parent;
            if (port != null && pods.Count > 1)
            {
                var ports = new HashSet<Building>();
                Map map = source.parent.Map;
                map.floodFiller.FloodFill(port.Position,
                    cell => FuelingPortUtility.AnyFuelingPortGiverAt(cell, map),
                    cell =>
                    {
                        // A bool-returning lambda selects FloodFill's early-stop overload.
                        ports.Add(FuelingPortUtility.FuelingPortGiverAt(cell, map));
                    });
                foreach (var pod in pods)
                {
                    ThingWithComps otherPort = ((CompLaunchable_TransportPod)pod.Launchable).FuelingPortSource?.parent;
                    if (otherPort != null && !ports.Contains(otherPort))
                    {
                        Messages.Message("MessageTransportersNotAdjacent".Translate(), pod.parent,
                            MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }
                }
            }
            foreach (var pod in pods)
            {
                if (pod == source || source.parent.Map.reachability.CanReach(source.parent.Position, pod.parent,
                    PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))) continue;
                Messages.Message("MessageTransporterUnreachable".Translate(), pod.parent,
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            if (!StorageTransportGroups.TryCreate(pods, out string error))
                Messages.Message(error.Translate(), source.parent, MessageTypeDefOf.RejectInput, historical: false);
        }

        private static void ShowGroupMass(CompTransporter __instance, ref string __result)
        {
            if (StorageTransportGroups.TryGetMass(__instance, out float usage, out float capacity))
                __result += "\n" + "MS_Transport_GroupMass".Translate(usage.ToString("0.##"), capacity.ToString("0.##"));
        }

        internal static IEnumerable<CodeInstruction> UseGroupMass(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var properties = new[] { nameof(CompTransporter.OverMassCapacity), nameof(CompTransporter.MassUsage), nameof(CompTransporter.MassCapacity) };
            var replacements = new[] { nameof(StorageTransportGroups.OverCapacity), nameof(StorageTransportGroups.Usage), nameof(StorageTransportGroups.Capacity) };
            for (int i = 0; i < properties.Length; i++)
            {
                var getter = AccessTools.PropertyGetter(typeof(CompTransporter), properties[i]);
                int replaced = 0;
                foreach (var instruction in code)
                {
                    if (!instruction.Calls(getter)) continue;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(StorageTransportGroups), replacements[i]);
                    replaced++;
                }
                if (replaced == 0)
                    throw new InvalidOperationException("[MagicStorage] Native launch mass check not found: " + properties[i]);
            }
            // Only the mass checks and their displayed numbers change; all other launch rules stay native.
            return code;
        }
    }
}
