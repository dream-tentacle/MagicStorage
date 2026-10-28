using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class CosmicWorkbenchRoomRoleTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception("Workbench room roles: " + message + "; expected " + expected + ", got " + actual); }

    internal static int Run()
    {
        RoomRoleDefOf.Workshop.Worker = new RoomRoleWorker_Workshop();
        RoomRoleDefOf.Laboratory.Worker = new RoomRoleWorker_Laboratory();
        var kitchen = DefDatabase<RoomRoleDef>.GetNamed("Kitchen"); kitchen.Worker = new RoomRoleWorker_Kitchen();
        Translation.Values["NotInRoomRole"] = "not in a {0}";
        Translation.Values["StatsReport_OptimalRoom"] = "Optimal room";
        var roles = CosmicWorkbenchRoomRoles.Choices.ToArray();
        Equal(3, roles.Length, "menu contains the three production room purposes");
        Equal(true, roles.SequenceEqual(new[] { RoomRoleDefOf.Workshop, kitchen, RoomRoleDefOf.Laboratory }), "menu lists workshop, kitchen and laboratory");
        var sharedDef = new ThingDef { building = new BuildingProperties { workTableRoomRole = RoomRoleDefOf.Workshop } };
        var room = new Room { Role = kitchen };
        var first = new Building_CosmicWorkbench { def = sharedDef, Spawned = true, TestRoom = room };
        var second = new Building_CosmicWorkbench { def = sharedDef, Spawned = true, TestRoom = room };
        room.ContainedAndAdjacentThings.Add(first); room.ContainedAndAdjacentThings.Add(second);
        Equal(RoomRoleDefOf.Workshop, first.PreferredRoomRole, "uninitialized bench defaults to workshop");
        Equal(54f, RoomRoleDefOf.Workshop.Worker.GetScore(room), "default benches keep vanilla workshop weights");
        first.SetPreferredRoomRole(kitchen); second.SetPreferredRoomRole(RoomRoleDefOf.Laboratory);
        Equal(kitchen, first.PreferredRoomRole, "first bench keeps its own kitchen selection");
        Equal(RoomRoleDefOf.Laboratory, second.PreferredRoomRole, "second bench keeps its own laboratory selection");
        Equal(RoomRoleDefOf.Workshop, sharedDef.building.workTableRoomRole, "selection never mutates the shared XML definition");
        Equal(0f, RoomRoleDefOf.Workshop.Worker.GetScore(room), "selected benches stop contributing to workshop score");
        Equal(28f, kitchen.Worker.GetScore(room), "kitchen selection contributes one vanilla kitchen weight");
        Equal(60f, RoomRoleDefOf.Laboratory.Worker.GetScore(room), "laboratory selection contributes one vanilla laboratory weight");
        room.ContainedAndAdjacentThings.Add(new Thing { def = new ThingDef { building = new BuildingProperties { workTableRoomRole = RoomRoleDefOf.Workshop } } });
        room.ContainedAndAdjacentThings.Add(new Thing { def = new ThingDef { NativeKitchen = true } });
        Equal(27f, RoomRoleDefOf.Workshop.Worker.GetScore(room), "ordinary workbench contribution remains intact");
        Equal(56f, kitchen.Worker.GetScore(room), "ordinary stove and selected bench both contribute");
        Equal(false, StatPart_WorkTableRoomRole.Applies(first), "matching selected room avoids penalty");
        Equal(true, StatPart_WorkTableRoomRole.Applies(second), "different selected room incurs penalty");
        var part = new StatPart_WorkTableRoomRole(); float speed = 1f;
        part.TransformValue(StatRequest.For(first), ref speed); Equal(1f, speed, "matching kitchen retains full speed");
        part.TransformValue(StatRequest.For(second), ref speed); Equal(.8f, speed, "mismatch retains configured 20 percent penalty");
        Equal("Not in a laboratory: x80%", part.ExplanationPart(StatRequest.For(second)), "stat explanation names selected room purpose");
        var report = new CompReportWorkSpeed { parent = second };
        Equal("Work speed: not in a laboratory", report.CompInspectStringExtra(), "inspection report names selected room purpose");
        var entries = sharedDef.building.SpecialDisplayStats(sharedDef, StatRequest.For(first)).ToList();
        Equal("unchanged", entries[0].ValueString, "unrelated info-card entry is retained");
        Equal("Kitchen", entries[1].ValueString, "instance info card displays selected optimal room");
        Equal("Workshop", sharedDef.building.SpecialDisplayStats(sharedDef, default(StatRequest)).Last().ValueString,
            "definition-only info card retains its workshop default");
        room.PsychologicallyOutdoors = true;
        Equal(false, StatPart_WorkTableRoomRole.Applies(second), "outdoor room behavior remains vanilla");
        room.PsychologicallyOutdoors = false;
        var ordinary = new Thing { def = sharedDef, TestRoom = room };
        Equal(true, StatPart_WorkTableRoomRole.Applies(ordinary), "ordinary benches keep their XML-based room requirement");

        var roomStat = new StatDef { parts = new System.Collections.Generic.List<StatPart> { part } };
        var unrelatedStat = new StatDef();
        DefDatabase<StatDef>.AllDefsListForReading.Add(roomStat); DefDatabase<StatDef>.AllDefsListForReading.Add(unrelatedStat);
        int dirty = room.DirtyNotifications;
        first.SetPreferredRoomRole(RoomRoleDefOf.Workshop);
        Equal(dirty + 1, room.DirtyNotifications, "changing selection invalidates cached room role");
        Equal(1, roomStat.Worker.Clears, "room-dependent stat cache is cleared");
        Equal(0, unrelatedStat.Worker.Clears, "unrelated stat cache remains intact");
        first.SetPreferredRoomRole(RoomRoleDefOf.Workshop);
        Equal(1, roomStat.Worker.Clears, "selecting the current purpose does not refresh again");
        first.SetPreferredRoomRole(new RoomRoleDef { defName = "Hospital" });
        Equal(RoomRoleDefOf.Workshop, first.PreferredRoomRole, "unrelated room purposes cannot enter the selection");
        first.SetPreferredRoomRole(kitchen);
        SaveAndLoad(first, kitchen);
        // Even if another mod makes the shared def qualify as a stove, count each
        // selected bench only once rather than adding its selection on top.
        sharedDef.NativeKitchen = true;
        Equal(56f, kitchen.Worker.GetScore(room), "native kitchen contribution is replaced rather than duplicated");
        DefDatabase<StatDef>.AllDefsListForReading.Clear();
        return checks;
    }

    private static void SaveAndLoad(Building_CosmicWorkbench bench, RoomRoleDef kitchen)
    {
        var oldBoundary = Scribe_Defs.Boundary; var oldMode = Scribe.mode;
        try
        {
            object saved = null;
            Scribe_Defs.Boundary = (key, value) => { if (key == "preferredRoomRole") saved = value; return value; };
            bench.ExposeData();
            Equal(kitchen, saved, "selected purpose is written through Scribe_Defs");
            var loaded = new Building_CosmicWorkbench(); Scribe.mode = LoadSaveMode.PostLoadInit;
            Scribe_Defs.Boundary = (key, value) => key == "preferredRoomRole" ? saved : value;
            loaded.ExposeData(); Equal(kitchen, loaded.PreferredRoomRole, "saved purpose is restored on its bench");
            Scribe_Defs.Boundary = (key, value) => key == "preferredRoomRole" ? null : value;
            loaded = new Building_CosmicWorkbench(); loaded.ExposeData();
            Equal(RoomRoleDefOf.Workshop, loaded.PreferredRoomRole, "missing saved purpose initializes to workshop");
            Equal(kitchen, bench.PreferredRoomRole, "loading a different bench never changes the original selection");
        }
        finally { Scribe_Defs.Boundary = oldBoundary; Scribe.mode = oldMode; }
    }
}
