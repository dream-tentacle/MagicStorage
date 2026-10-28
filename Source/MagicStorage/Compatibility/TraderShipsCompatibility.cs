using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    // Optional dependency: keep TraderShips' LandedShip ITrader instance and its delivery callbacks.
    // The trader lives on an impassable map building, so this is ground mode with the ship as the target.
    internal sealed class TraderShipsCompatibility : IStorageCompatibility
    {
        private static Type shipCompType, landedTraderType;
        private static FieldInfo tradeShipField;
        public string PackageId => "automatic.traderships";

        public void Apply(Harmony harmony)
        {
            Type comp = AccessTools.TypeByName("TraderShips.CompShip");
            Type trader = AccessTools.TypeByName("TraderShips.LandedShip");
            FieldInfo field = comp == null ? null : AccessTools.Field(comp, "tradeShip");
            MethodInfo menu = comp == null ? null : AccessTools.DeclaredMethod(comp, "CompFloatMenuOptions", new[] { typeof(Pawn) });
            if (comp == null || trader == null || field == null || menu == null ||
                !typeof(ThingComp).IsAssignableFrom(comp) || !typeof(ITrader).IsAssignableFrom(trader) ||
                !trader.IsAssignableFrom(field.FieldType))
                throw new InvalidOperationException("Trader ships landed trade API was not found.");
            shipCompType = comp;
            landedTraderType = trader;
            tradeShipField = field;
            harmony.Patch(menu, postfix: new HarmonyMethod(typeof(TraderShipsCompatibility), nameof(MenuOptions)));
        }

        internal static bool TryGetTrader(Thing target, out ITrader trader)
        {
            trader = null;
            if (shipCompType == null || !(target is ThingWithComps ship) || !target.Spawned || target.Destroyed) return false;
            foreach (ThingComp comp in ship.AllComps)
                if (shipCompType.IsInstanceOfType(comp))
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

        // The ship still owns stock, pricing and delivery; only the inventory scope is added.
        private static IEnumerable<FloatMenuOption> WithStorageOption(IEnumerable<FloatMenuOption> original, Thing ship, Pawn actor)
        {
            if (original != null) foreach (FloatMenuOption option in original) yield return option;
            if (actor == null || !actor.Spawned || ship == null || !ship.Spawned || ship.Map != actor.Map ||
                !TryGetTrader(ship, out ITrader trader) || !StorageTradeSession.HasCore(actor.Map)) yield break;
            string label = "MS_Trade_Command".Translate(ship.LabelShort);
            string reason = null;
            if (!trader.CanTradeNow) reason = "MS_Trade_TraderShipUnavailable".Translate();
            else if (!actor.CanReach(ship, PathEndMode.InteractionCell, Danger.Deadly)) reason = "NoPath".Translate();
            else if (actor.skills == null || actor.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                reason = "CannotPrioritizeWorkTypeDisabled".Translate(SkillDefOf.Social.LabelCap);
            else
            {
                AcceptanceReport report = actor.CanTradeWith(trader.Faction, trader.TraderKind);
                if (!report.Accepted)
                {
                    if (!string.IsNullOrWhiteSpace(report.Reason)) reason = report.Reason;
                    else if (trader.Faction != null && trader.Faction.HostileTo(actor.Faction))
                        reason = "MS_Trade_TraderShipHostile".Translate();
                    else reason = "MS_Trade_Fail_TradePermission".Translate();
                }
            }
            if (reason != null) { yield return new FloatMenuOption(label + ": " + reason, null); yield break; }
            Action action = () =>
            {
                if (!IsCurrentTrader(ship, trader)) return;
                Job job = JobMaker.MakeJob(StorageTradeDefOf.MS_TradeWithStorageTraderShip, ship);
                job.playerForced = true;
                actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            };
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, action, MenuOptionPriority.InitiateSocial), actor, ship);
        }
    }

    // Mirrors the ship's own trade job: same reservation, same interaction cell, same trader instance.
    public sealed class JobDriver_StorageTraderShipTrade : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !TraderShipsCompatibility.TryGetTrader(TargetThingA, out ITrader current) || !current.CanTradeNow);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil trade = ToilMaker.MakeToil("StorageTraderShipTrade");
            trade.initAction = () =>
            {
                Thing ship = TargetThingA;
                if (TraderShipsCompatibility.TryGetTrader(ship, out ITrader trader) && trader.CanTradeNow &&
                    StorageTradeSession.HasCore(pawn.Map))
                    StorageTradeSession.Open(pawn, trader, groundTarget: ship,
                        groundTargetUsable: () => TraderShipsCompatibility.IsCurrentTrader(ship, trader));
            };
            yield return trade;
        }
    }
}
