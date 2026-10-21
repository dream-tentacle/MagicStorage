using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal static class StorageTransportGroups
    {
        internal static bool IsPlayerPod(CompTransporter transporter) =>
            transporter?.parent != null && transporter.parent.Spawned && !transporter.parent.Destroyed &&
            transporter.parent.Faction == Faction.OfPlayer && transporter.Groupable &&
            transporter.Launchable is CompLaunchable_TransportPod && transporter.Shuttle == null;

        internal static bool TryCreate(IList<CompTransporter> pods, out string error)
        {
            error = "MS_Transport_GroupInvalid";
            if (pods == null || pods.Count == 0) return false;
            var first = pods[0];
            var unique = new HashSet<CompTransporter>();
            foreach (var pod in pods)
            {
                if (!IsPlayerPod(pod) || pod.parent.Map != first.parent.Map ||
                    pod.parent.def != first.parent.def || !unique.Add(pod)) return false;
                if (pod.groupID >= 0)
                { error = "MS_Transport_GroupAlreadyAssigned"; return false; }
            }
            // Unlike the loading dialog, this entry point only assigns group IDs.
            // Contents, outstanding orders and mass-capacity overrides remain untouched.
            TransporterUtility.InitiateLoading(pods);
            error = null;
            return true;
        }

        internal static bool TryGetMass(CompTransporter transporter, out float usage, out float capacity)
        {
            usage = 0f; capacity = 0f;
            if (!IsPlayerPod(transporter) || transporter.groupID < 0) return false;
            var pods = new List<CompTransporter>();
            TransporterUtility.GetTransportersInGroup(transporter.groupID, transporter.parent.Map, pods);
            if (!pods.Contains(transporter)) return false;
            foreach (var pod in pods)
            {
                // Keep quest shuttles and other transporters on their native capacity rules.
                if (!IsPlayerPod(pod)) return false;
                usage += pod.MassUsage;
                capacity += pod.MassCapacity;
            }
            return true;
        }

        internal static float Capacity(CompTransporter transporter) =>
            TryGetMass(transporter, out _, out float capacity) ? capacity : transporter.MassCapacity;

        internal static float Usage(CompTransporter transporter) =>
            TryGetMass(transporter, out float usage, out _) ? usage : transporter.MassUsage;

        internal static float FreeMass(CompTransporter transporter) =>
            TryGetMass(transporter, out float usage, out float capacity) ? capacity - usage :
                transporter.MassCapacity - transporter.MassUsage;

        internal static bool OverCapacity(CompTransporter transporter) =>
            TryGetMass(transporter, out float usage, out float capacity) ? usage > capacity :
                transporter.OverMassCapacity;
    }
}
