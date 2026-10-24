using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // Optional dependency: keep CeleTech's ITrader instance and its credit/delivery callbacks.
    internal sealed class CeleTechTradeCompatibility : IStorageCompatibility
    {
        private static Type shuttleCompType, landedTraderType;
        private static FieldInfo tradeShipField;
        public string PackageId => "TOT.CeleTech.MKIII";

        public void Apply(Harmony harmony)
        {
            Type comp = AccessTools.TypeByName("CeleTech.Base.Comp_TraderShuttle");
            Type trader = AccessTools.TypeByName("CeleTech.Base.Landed_CMCTS");
            FieldInfo field = comp == null ? null : AccessTools.Field(comp, "tradeShip");
            MethodInfo menu = comp == null ? null : AccessTools.DeclaredMethod(comp, "CompFloatMenuOptions", new[] { typeof(Pawn) });
            if (comp == null || trader == null || field == null || menu == null ||
                !typeof(ThingComp).IsAssignableFrom(comp) || !typeof(ITrader).IsAssignableFrom(trader) ||
                !trader.IsAssignableFrom(field.FieldType))
                throw new InvalidOperationException("CeleTech landed trade shuttle API was not found.");
            shuttleCompType = comp;
            landedTraderType = trader;
            tradeShipField = field;
            harmony.Patch(menu, postfix: new HarmonyMethod(typeof(CeleTechTradeCompatibility), nameof(MenuOptions)));
        }

        internal static bool TryGetTrader(Thing target, out ITrader trader)
        {
            trader = null;
            if (shuttleCompType == null || !(target is ThingWithComps shuttle) || !target.Spawned || target.Destroyed) return false;
            foreach (ThingComp comp in shuttle.AllComps)
                if (shuttleCompType.IsInstanceOfType(comp))
                {
                    object value = tradeShipField.GetValue(comp);
                    if (value != null && landedTraderType.IsInstanceOfType(value)) trader = value as ITrader;
                    return trader != null;
                }
            return false;
        }

        internal static bool IsCurrentTrader(Thing target, ITrader trader) =>
            trader != null && trader.CanTradeNow && TryGetTrader(target, out ITrader current) && ReferenceEquals(current, trader);

        private static void MenuOptions(ThingComp __instance, Pawn __0, ref IEnumerable<FloatMenuOption> __result)
        {
            __result = WithStorageOption(__result, __instance.parent, __0);
        }

        private static IEnumerable<FloatMenuOption> WithStorageOption(IEnumerable<FloatMenuOption> original, Thing shuttle, Pawn actor)
        {
            if (original != null) foreach (FloatMenuOption option in original) yield return option;
            if (actor == null || !actor.Spawned || shuttle == null || shuttle.Map != actor.Map ||
                !TryGetTrader(shuttle, out ITrader trader) || !StorageTradeSession.HasCore(actor.Map)) yield break;
            string label = "MS_Trade_Command".Translate(shuttle.LabelShort);
            string reason = null;
            if (!trader.CanTradeNow) reason = "MS_Trade_CeleTechUnavailable".Translate();
            else if (!actor.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly)) reason = "NoPath".Translate();
            else if (actor.skills == null || actor.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                reason = "CannotPrioritizeWorkTypeDisabled".Translate(SkillDefOf.Social.LabelCap);
            else if (!actor.CanTradeWith(trader.Faction, trader.TraderKind).Accepted) reason = "MissingTitleAbility".Translate();
            if (reason != null) { yield return new FloatMenuOption(label + ": " + reason, null); yield break; }
            Action action = () =>
            {
                if (!IsCurrentTrader(shuttle, trader)) return;
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MS_TradeWithStorageCeleTech"), shuttle);
                job.playerForced = true;
                actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            };
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, action, MenuOptionPriority.InitiateSocial), actor, shuttle);
        }
    }

    public sealed class JobDriver_StorageCeleTechTrade : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !CeleTechTradeCompatibility.TryGetTrader(TargetThingA, out ITrader current) || !current.CanTradeNow);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil trade = ToilMaker.MakeToil("StorageCeleTechTrade");
            trade.initAction = () =>
            {
                Thing target = TargetThingA;
                if (CeleTechTradeCompatibility.TryGetTrader(target, out ITrader trader) && trader.CanTradeNow &&
                    StorageTradeSession.HasCore(pawn.Map))
                    StorageTradeSession.Open(pawn, trader, groundTarget: target,
                        groundTargetUsable: () => CeleTechTradeCompatibility.IsCurrentTrader(target, trader));
            };
            yield return trade;
        }
    }
}
