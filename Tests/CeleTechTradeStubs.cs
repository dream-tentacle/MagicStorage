// Only optional-mod and native menu/job boundaries are modeled. Production
// compatibility, Harmony hooks, trade sessions, reservations and delivery are linked.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Verse
{
    public partial class Thing { public string LabelShort => "trade shuttle"; }
    public enum MenuOptionPriority { InitiateSocial }
    public class FloatMenuOption
    {
        public string Label;
        public Action action;
        public FloatMenuOption(string label, Action action, MenuOptionPriority priority = MenuOptionPriority.InitiateSocial)
        { Label = label; this.action = action; }
    }
    public static class FloatMenuUtility
    {
        public static FloatMenuOption DecoratePrioritizedTask(FloatMenuOption option, Pawn pawn, LocalTargetInfo target) => option;
    }
    public static class CeleTechMenuBoundary
    {
        public static bool CanReach(this Pawn pawn, Thing target, PathEndMode end, Danger danger) =>
            pawn.Map.reachability.CanReach(pawn.Position, target, end, TraverseMode.PassDoors, danger);
    }
}
namespace RimWorld
{
    public static class SkillDefOf { public static readonly SkillDef Social = new SkillDef(); }
}
namespace Verse.AI
{
    public enum JobTag { Misc }
    public partial class Pawn_JobTracker
    {
        public bool TryTakeOrderedJob(Job job, JobTag tag) { curJob = job; return true; }
    }
    public static class CeleTechJobBoundary
    { public static T FailOnDespawnedOrNull<T>(this T value, TargetIndex target) => value; }
}
namespace CeleTech.Base
{
    public sealed class Comp_TraderShuttle : ThingComp
    {
        public Landed_CMCTS tradeShip;
        public int NativeClicks;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn negotiator) =>
            new[] { new FloatMenuOption("Native shuttle trade", () => NativeClicks++) };
    }

    public sealed class Landed_CMCTS : TradeShip, ITrader
    {
        public float boughtSilverTotal, soldSilverTotal;
        public int TraderDeliveries, PlayerDeliveries;
        void ITrader.GiveSoldThingToTrader(Thing item, int count, Pawn negotiator)
        {
            TraderDeliveries++;
            if (item.def == ThingDefOf.Silver) boughtSilverTotal += count;
            base.GiveSoldThingToTrader(item, count, negotiator);
        }
        void ITrader.GiveSoldThingToPlayer(Thing item, int count, Pawn negotiator)
        {
            PlayerDeliveries++;
            if (item.def == ThingDefOf.Silver) soldSilverTotal += count;
            Thing received = item.SplitOff(count);
            received.Spawned = true; received.Map = negotiator.Map; received.Position = negotiator.Position;
            negotiator.Map.Ground.Add(received);
        }
    }
}
