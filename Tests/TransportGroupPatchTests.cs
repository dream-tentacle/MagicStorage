// Execute the production Harmony patches against controlled game boundaries.
// The launch boundary retains the inspected 1.6/1.7 order of checks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class TransportGroupPatchTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StorageTransportGroupPatches).TypeHandle);
            var map = new Map(); var def = new object();
            var heavy = Pod(map, def, 299); var light = Pod(map, def, 1);
            var cargo = new object(); heavy.innerContainer.Add(cargo);
            Find.Selector.SelectedObjectsListForReading.AddRange(new object[] { heavy.parent, light.parent });
            var commands = heavy.CompGetGizmosExtra().Concat(light.CompGetGizmosExtra()).OfType<Command_Action>().ToList();
            Check(commands.Count == 1, "multi-selection exposes a single group command");
            commands[0].action();
            Check(heavy.groupID >= 0 && heavy.groupID == light.groupID, "button groups the selected pods");
            Check(heavy.innerContainer.Single() == cargo, "button preserves loaded item identity");
            var heavyLaunch = (CompLaunchable_TransportPod)heavy.Launchable;
            var lightLaunch = (CompLaunchable_TransportPod)light.Launchable;
            Check(heavyLaunch.CanLaunch().Accepted, "patched native launch accepts the 299 kg pod");
            Check(lightLaunch.CanLaunch().Accepted, "patched native launch accepts the 1 kg pod");
            Check(heavy.CompInspectStringExtra().Contains("MS_Transport_GroupMass:300,300"), "inspect string shows shared mass");
            Check(heavy.CompGetGizmosExtra().OfType<Command_Action>().Single().disabled, "existing group disables regrouping");
            light.MassUsage = 2;
            Check(!heavyLaunch.CanLaunch().Accepted, "301 kg group cannot launch from heavy pod");
            var rejected = lightLaunch.CanLaunch();
            Check(!rejected.Accepted && rejected.Reason.Contains("MassUsageString:301,300"),
                "301 kg group cannot launch from light pod and reports group totals");
            light.MassUsage = 1;
            heavyLaunch.HasFuel = false;
            Check(heavyLaunch.CanLaunch().Reason == "CommandLaunchGroupFailNoFuel", "fuel rejection is preserved");
            heavyLaunch.HasFuel = true; heavyLaunch.UnderRoof = true;
            Check(heavyLaunch.CanLaunch().Reason == "CommandLaunchGroupFailUnderRoof", "roof rejection is preserved");
            heavyLaunch.UnderRoof = false; heavyLaunch.Cooldown = true;
            Check(heavyLaunch.CanLaunch().Reason == "CommandLaunchGroupCooldown", "cooldown rejection is preserved");
            heavyLaunch.Cooldown = false;
            light.parent.Spawned = false;
            Check(!heavyLaunch.CanLaunch().Accepted, "removed pod's capacity cannot be used for launch");
            var ordinary = Pod(new Map(), def, 200); ordinary.Launchable = new CompLaunchable { Transporter = ordinary };
            ordinary.groupID = 10;
            Check(!ordinary.Launchable.CanLaunch().Accepted, "other transporters keep native individual limits");
            Check(!ordinary.CompGetGizmosExtra().Any(), "other transporters do not receive the pod button");
            var solo = Pod(new Map(), def, 1);
            Check(!solo.Launchable.CanLaunch().Accepted, "cargo alone never establishes a group automatically");
            Find.Selector.SelectedObjectsListForReading.Clear();
            solo.CompGetGizmosExtra().OfType<Command_Action>().Single().action();
            Check(solo.Launchable.CanLaunch().Accepted, "single-pod grouping also works");
            LauncherAdjacency();
            Console.WriteLine("PASS: " + checks + " transport-group assertions (production Harmony patches executed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static CompTransporter Pod(Map map, object def, float mass)
    {
        var thing = new ThingWithComps { Map = map, def = def, Faction = Faction.OfPlayer, Spawned = true };
        var pod = new CompTransporter { parent = thing, MassUsage = mass };
        pod.Launchable = new CompLaunchable_TransportPod { Transporter = pod };
        thing.Comps.Add(pod); map.Pods.Add(pod); return pod;
    }

    private static void LauncherAdjacency()
    {
        foreach (bool reverse in new[] { false, true })
        {
            var map = new Map(); var def = new object();
            var first = Pod(map, def, 1); var last = Pod(map, def, 1);
            Connect(first, map, 0); Connect(last, map, 1);
            GroupButton(reverse ? new[] { last, first } : new[] { first, last }).action();
            Check(first.groupID >= 0 && first.groupID == last.groupID,
                "adjacent launchers group successfully in either selection order");
        }

        var chain = new Map(); var chainDef = new object();
        var a = Pod(chain, chainDef, 1); var b = Pod(chain, chainDef, 1);
        Connect(a, chain, 0); Connect(b, chain, 2);
        chain.Ports.Add(new IntVec3(1, 0), new Building());
        GroupButton(a, b).action();
        Check(a.groupID >= 0 && a.groupID == b.groupID,
            "an unselected intermediate launcher connects the selected launchers");

        var gap = new Map(); var gapDef = new object();
        a = Pod(gap, gapDef, 1); b = Pod(gap, gapDef, 1);
        Connect(a, gap, 0); Connect(b, gap, 2);
        Messages.Last = null;
        GroupButton(a, b).action();
        Check(a.groupID == -1 && b.groupID == -1, "disconnected launchers remain ungrouped");
        Check(Messages.Last == "MessageTransportersNotAdjacent", "disconnected launchers retain native rejection");

        gap.Ports.Add(new IntVec3(1, 0), new Building());
        gap.reachability.Reachable = false;
        Messages.Last = null;
        GroupButton(a, b).action();
        Check(a.groupID == -1 && b.groupID == -1, "unreachable pods remain ungrouped");
        Check(Messages.Last == "MessageTransporterUnreachable", "native reachability rejection is preserved");
    }

    private static Command_Action GroupButton(params CompTransporter[] pods)
    {
        Find.Selector.SelectedObjectsListForReading.Clear();
        Find.Selector.SelectedObjectsListForReading.AddRange(pods.Select(pod => (object)pod.parent));
        return pods[0].CompGetGizmosExtra().OfType<Command_Action>().Single();
    }

    private static void Connect(CompTransporter pod, Map map, int x)
    {
        var port = new Building { Map = map, Position = new IntVec3(x, 0) };
        map.Ports.Add(port.Position, port);
        ((CompLaunchable_TransportPod)pod.Launchable).FuelingPortSource = new CompRefuelable { parent = port };
    }
}

namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class StaticConstructorOnStartupAttribute : Attribute { }
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
    }
    public class Thing
    {
        public bool Spawned, Destroyed;
        public Faction Faction;
        public object def;
        public Map Map;
        public IntVec3 Position;
        public readonly List<object> Comps = new List<object>();
        public T TryGetComp<T>() where T : class => Comps.OfType<T>().FirstOrDefault();
    }
    public class ThingWithComps : Thing { }
    public class Building : ThingWithComps { }
    public class Map
    {
        public readonly List<CompTransporter> Pods = new List<CompTransporter>();
        public readonly Dictionary<IntVec3, Building> Ports = new Dictionary<IntVec3, Building>();
        public FloodFiller floodFiller;
        public Reachability reachability = new Reachability();
        public Map() { floodFiller = new FloodFiller(this); }
    }
    public class FloodFiller
    {
        public FloodFiller(Map map) { }
        // Preserve both native overloads: a bool-returning callback requests early termination.
        public void FloodFill(IntVec3 start, Predicate<IntVec3> pass, Action<IntVec3> visit) =>
            FloodFill(start, pass, cell => { visit(cell); return false; });
        public void FloodFill(IntVec3 start, Predicate<IntVec3> pass, Func<IntVec3, bool> visit)
        {
            if (!pass(start)) return;
            var pending = new Queue<IntVec3>(); var seen = new HashSet<IntVec3>();
            pending.Enqueue(start); seen.Add(start);
            while (pending.Count > 0)
            {
                IntVec3 cell = pending.Dequeue();
                if (visit(cell)) return;
                foreach (var next in new[] { new IntVec3(cell.x + 1, cell.z), new IntVec3(cell.x - 1, cell.z),
                    new IntVec3(cell.x, cell.z + 1), new IntVec3(cell.x, cell.z - 1) })
                    if (seen.Add(next) && pass(next)) pending.Enqueue(next);
            }
        }
    }
    public class Reachability
    {
        public bool Reachable = true;
        public bool CanReach(IntVec3 start, Thing target, Verse.AI.PathEndMode end, Verse.AI.TraverseParms parms) => Reachable;
    }
    public static class Find { public static Selector Selector = new Selector(); }
    public class Selector { public readonly List<object> SelectedObjectsListForReading = new List<object>(); }
    public class Gizmo { }
    public class Command_Action : Gizmo
    {
        public string defaultLabel, defaultDesc;
        public object icon;
        public bool groupable, disabled;
        public Action action;
        public void Disable(string reason) { disabled = true; }
    }
    public static class Translation
    { public static string Translate(this string key, params object[] args) => args.Length == 0 ? key : key + ":" + string.Join(",", args); }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public string Reason;
        public static implicit operator AcceptanceReport(bool value) => new AcceptanceReport { Accepted = value };
        public static implicit operator AcceptanceReport(string reason) => new AcceptanceReport { Reason = reason };
    }
    public static class Messages
    {
        public static string Last;
        public static void Message(string message, Thing target, object kind, bool historical) { Last = message; }
    }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch }
    public enum TraverseMode { PassDoors }
    public struct TraverseParms { public static TraverseParms For(TraverseMode mode) => new TraverseParms(); }
}
namespace RimWorld
{
    public static class MessageTypeDefOf { public static object RejectInput = new object(); }
    public class CompRefuelable { public ThingWithComps parent; }
    public static class FuelingPortUtility
    {
        public static bool AnyFuelingPortGiverAt(IntVec3 cell, Map map) => map.Ports.ContainsKey(cell);
        public static Building FuelingPortGiverAt(IntVec3 cell, Map map) =>
            map.Ports.TryGetValue(cell, out Building port) ? port : null;
    }
    public class CompTransporter
    {
        public ThingWithComps parent;
        public int groupID = -1;
        public bool Groupable => true;
        public object Shuttle => null;
        public CompLaunchable Launchable;
        public float MassUsage { get; set; }
        public float MassCapacity { get; set; } = 150;
        public bool OverMassCapacity => MassUsage > MassCapacity;
        public bool LoadingInProgressOrReadyToLaunch => groupID >= 0;
        public readonly List<object> innerContainer = new List<object>();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IEnumerable<Gizmo> CompGetGizmosExtra() => new Gizmo[0];
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string CompInspectStringExtra() => "Native contents";
    }
    public class CompLaunchable
    {
        public static readonly object LaunchCommandTex = new object();
        public CompTransporter Transporter;
        public bool UnderRoof, HasFuel = true, Cooldown;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual AcceptanceReport CanLaunch(float? overrideFuelLevel = null)
        {
            if (!Transporter.LoadingInProgressOrReadyToLaunch) return "CommandLaunchGroupFailNotLoaded".Translate();
            if (UnderRoof) return "CommandLaunchGroupFailUnderRoof".Translate();
            if (!HasFuel) return "CommandLaunchGroupFailNoFuel".Translate();
            if (Transporter.OverMassCapacity)
                return "CommandLaunchGroupFailOverMassCapacity".Translate() + ": " +
                    "MassUsageString".Translate(Transporter.MassUsage.ToString("F0"), Transporter.MassCapacity.ToString("F0"));
            if (Cooldown) return "CommandLaunchGroupCooldown".Translate();
            return true;
        }
    }
    public class CompLaunchable_TransportPod : CompLaunchable { public CompRefuelable FuelingPortSource; }
    public static class TransporterUtility
    {
        private static int nextID;
        public static int InitiateLoading(IEnumerable<CompTransporter> pods)
        { int id = nextID++; foreach (var pod in pods) pod.groupID = id; return id; }
        public static void GetTransportersInGroup(int id, Map map, List<CompTransporter> pods)
        { pods.Clear(); pods.AddRange(map.Pods.Where(pod => pod.parent.Spawned && pod.groupID == id)); }
    }
}
