// Only native menu, quest signal and lord boundaries are modeled here.
// The Harmony patch, stock indexes, reservations and transfers are production code.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI.Group;

namespace Verse
{
    // Native FloatMenuMap.StillValid checks options at the target's current
    // position, or at the initial click position when no target was bound.
    internal sealed class BeggarMenuRevalidationBoundary
    {
        private readonly IntVec3 clickPosition;
        private readonly Func<IntVec3, IEnumerable<FloatMenuOption>> optionsAt;
        internal BeggarMenuRevalidationBoundary(IntVec3 clickPosition, Func<IntVec3, IEnumerable<FloatMenuOption>> optionsAt)
        { this.clickPosition = clickPosition; this.optionsAt = optionsAt; }
        internal bool StillValid(FloatMenuOption option)
        {
            Thing target = option.revalidateClickTarget;
            if (target != null && !target.Spawned) return false;
            return optionsAt(target?.Position ?? clickPosition).Any(current => current.Label == option.Label && current.action != null);
        }
    }

    public partial class Pawn
    {
        public Lord lord;
        public List<string> questTags = new List<string> { "Quest.Receiver" };
        public Lord GetLord() => lord;
        public bool HostileTo(Pawn other) => Faction?.HostileTo(other.Faction) == true;
    }
}
namespace Verse.AI.Group
{
    public class LordToil { }
    public class Lord
    {
        public object LordJob = new RimWorld.LordJob_BegForItems();
        public LordToil CurLordToil;
        public Pawn Receiver;
        public ThingDef Def;
        public int Amount, Completions, EndedPendingDeliveries;
        public void ReceiveMemo(string memo)
        {
            if (RimWorld.GiveItemsToPawnUtility.GetCountRemaining(Receiver, Def, Amount) > 0) return;
            // Native Trigger_Custom evaluates on every signal, including a memo.
            CurLordToil = new LordToil(); Completions++; EndedPendingDeliveries++;
        }
    }
}
namespace RimWorld
{
    public class LordJob_BegForItems { }
    public class LordToil_WaitForItems : LordToil
    {
        public Pawn target;
        public ThingDef requestedThingDef;
        public int requestedThingCount;
    }
    public class LordToil_TravelAndWaitForItems : LordToil
    {
        public Pawn target;
        public ThingDef requestedThingDef;
        public int requestedThingCount;
    }
    public static class GiveItemsToPawnUtility
    {
        public static int NativeClicks;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IEnumerable<FloatMenuOption> GetFloatMenuOptionsForPawn(Pawn requester, Pawn current,
            ThingDef requestedThingDef, int countRequired) =>
            new[] { new FloatMenuOption("Native gift", () => NativeClicks++) };
        public static int GetCountRemaining(Pawn pawn, ThingDef requestedThingDef, int requestedThingCount) =>
            requestedThingCount - pawn.inventory.innerContainer.Where(t => t.def == requestedThingDef).Sum(t => t.stackCount);
    }
    public static class QuestUtility
    {
        public static int ReceivedSignals;
        public static List<string> LastTags;
        public static Pawn LastGiver, LastReceiver;
        public static Action BeforeSignal;
        public static void SendQuestTargetSignals(List<string> questTags, string signalPart, Pawn giver, Pawn receiver)
        {
            if (signalPart != "ReceivedItems") throw new Exception("Unexpected signal: " + signalPart);
            BeforeSignal?.Invoke(); ReceivedSignals++; LastTags = questTags; LastGiver = giver; LastReceiver = receiver;
        }
    }
}
