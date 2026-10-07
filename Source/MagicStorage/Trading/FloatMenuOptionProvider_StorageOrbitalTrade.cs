using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class FloatMenuOptionProvider_StorageOrbitalTrade : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!(clickedThing is Building_CommsConsole console) || !StorageTradeSession.HasCore(context.map)) yield break;
            Pawn actor = context.FirstSelectedPawn;
            foreach (ICommunicable target in console.GetCommTargets(actor))
            {
                if (!(target is TradeShip ship) || !ship.CanTradeNow) continue;
                string label = "MS_Trade_OrbitalCommand".Translate(ship.GetCallLabel());
                string reason = null;
                if (!actor.CanReach(console, PathEndMode.InteractionCell, Danger.Some)) reason = "NoPath".Translate();
                else if (context.map.gameConditionManager.ElectricityDisabled(context.map)) reason = "CannotUseSolarFlare".Translate();
                else if (!console.CanUseCommsNow) reason = "CannotUseNoPower".Translate();
                else if (!actor.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                    reason = "IncapableOfCapacity".Translate(PawnCapacityDefOf.Talking.label, actor.Named("PAWN"));
                else if (!actor.CanTradeWith(ship.Faction, ship.TraderKind).Accepted) reason = "MissingTitleAbility".Translate();
                else if (!StorageTradeSession.HasOrbitalCore(context.map)) reason = "MS_Trade_NoCoveredCore".Translate();
                if (reason != null) { yield return new FloatMenuOption(label + ": " + reason, null); continue; }
                TradeShip selected = ship;
                Action action = () =>
                {
                    Job job = JobMaker.MakeJob(StorageTradeDefOf.MS_TradeWithStorageOrbital, console);
                    job.commTarget = selected;
                    actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.OpeningComms, KnowledgeAmount.Total);
                };
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action,
                    MenuOptionPriority.InitiateSocial), actor, console);
            }
        }
    }

    public sealed class JobDriver_StorageOrbitalTrade : JobDriver
    {
        private Building_CommsConsole Console => (Building_CommsConsole)TargetThingA;
        private TradeShip Ship => job.commTarget as TradeShip;
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Console.CanUseCommsNow || Ship == null || !Ship.CanTradeNow);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell);
            Toil open = ToilMaker.MakeToil("StorageOrbitalTrade");
            open.initAction = () =>
            {
                if (Console.CanUseCommsNow && Ship?.CanTradeNow == true && StorageTradeSession.HasOrbitalCore(pawn.Map))
                {
                    StorageTradeSession.Open(pawn, Ship, Console);
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(System.Linq.Enumerable.OfType<Pawn>(Ship.Goods),
                        "LetterRelatedPawnsTradeShip".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent);
                }
            };
            yield return open;
        }
    }
}
