// Controlled vanilla room scoring, Scribe and stat boundaries. The actual
// workbench property and Harmony patches are linked from production sources.
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;

namespace Verse
{
    public partial class RoomRoleDef { public RoomRoleWorker Worker; }
    public static partial class DefDatabase<T> where T : class
    { public static readonly List<T> AllDefsListForReading = new List<T>(); }
    public class Room
    {
        public readonly List<Thing> ContainedAndAdjacentThings = new List<Thing>();
        public RoomRoleDef Role;
        public bool PsychologicallyOutdoors;
        public int DirtyNotifications;
        public void Notify_ContainedThingSpawnedOrDespawned(Thing thing) { DirtyNotifications++; }
    }
    public partial class Thing { public Room TestRoom; }
    public static class RoomBoundary { public static Room GetRoom(this Thing thing) => thing.TestRoom; }
    public partial class ThingDef { public bool NativeKitchen; }
    public abstract class RoomRoleWorker
    {
        public abstract float GetScore(Room room);
        public abstract float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef buildingDef);
    }
    public partial class BuildingProperties
    {
        public RoomRoleDef workTableRoomRole;
        public float workTableNotInRoomRoleFactor = .8f;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef parentDef, StatRequest req) => new[]
        {
            new StatDrawEntry(null, "Other", "unchanged", "", 100),
            new StatDrawEntry(null, "StatsReport_OptimalRoom".Translate(), workTableRoomRole.LabelCap, "", 4850)
        };
    }
    public static class RoomRoleFormatting { public static string ToStringPercent(this float value) => (value * 100f) + "%"; }
}
namespace RimWorld
{
    using Verse;
    public class RoomRoleWorker_Workshop : RoomRoleWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override float GetScore(Room room) => room.ContainedAndAdjacentThings.Sum(t => GetScoreDeltaIfBuildingPlaced(room, t.def));
        public override float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef def) => def.building?.workTableRoomRole == RoomRoleDefOf.Workshop ? 27f : 0f;
    }
    public class RoomRoleWorker_Laboratory : RoomRoleWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override float GetScore(Room room) => room.ContainedAndAdjacentThings.Sum(t => GetScoreDeltaIfBuildingPlaced(room, t.def));
        public override float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef def) => def.building?.workTableRoomRole == RoomRoleDefOf.Laboratory ? 60f : 0f;
    }
    public class RoomRoleWorker_Kitchen : RoomRoleWorker
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override float GetScore(Room room) => room.ContainedAndAdjacentThings.Sum(t => GetScoreDeltaIfBuildingPlaced(room, t.def));
        public override float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef def) => def.NativeKitchen ? 28f : 0f;
    }
    public class StatPart { }
    public struct StatRequest
    {
        public Thing Thing;
        public bool HasThing => Thing != null;
        public static StatRequest For(Thing thing) => new StatRequest { Thing = thing };
    }
    public class StatPart_WorkTableRoomRole : StatPart
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Applies(Thing parent)
        {
            Room room = parent.GetRoom();
            return parent.def.building?.workTableRoomRole != null && room != null && !room.PsychologicallyOutdoors && room.Role != parent.def.building.workTableRoomRole;
        }
        public void TransformValue(StatRequest req, ref float value)
        { if (req.HasThing && Applies(req.Thing)) value *= req.Thing.def.building.workTableNotInRoomRoleFactor; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string ExplanationPart(StatRequest req) => !req.HasThing || !Applies(req.Thing) ? null :
            "NotInRoomRole".Translate(req.Thing.def.building.workTableRoomRole.label).CapitalizeFirst() + ": x" + req.Thing.def.building.workTableNotInRoomRoleFactor.ToStringPercent();
    }
    public class StatWorker
    {
        public int Clears;
        public void TryClearCache() { Clears++; }
    }
    public class StatDef
    {
        public List<StatPart> parts;
        public readonly StatWorker Worker = new StatWorker();
    }
    public class CompReportWorkSpeed : ThingComp
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string CompInspectStringExtra() => "Work speed: " + (StatPart_WorkTableRoomRole.Applies(parent)
            ? "NotInRoomRole".Translate(parent.def.building.workTableRoomRole.label) : "100%");
    }
    public class StatDrawEntry
    {
        public object category;
        public StatDef stat;
        private readonly string label;
        public string LabelCap => label.CapitalizeFirst();
        public string ValueString { get; }
        public int DisplayPriorityWithinCategory { get; }
        public StatDrawEntry(object category, string label, string value, string report, int priority)
        { this.category = category; this.label = label; ValueString = value; DisplayPriorityWithinCategory = priority; }
    }
}
