using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    public sealed class FloatMenuOptionProvider_StorageTrade : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn == null || !clickedPawn.CanTradeNow || !StorageTradeSession.HasCore(context.map)) yield break;
            Pawn actor = context.FirstSelectedPawn;
            string label = "MS_Trade_Command".Translate(clickedPawn.LabelShort);
            string reason = null;
            if (!actor.CanReach(clickedPawn, PathEndMode.OnCell, Danger.Deadly)) reason = "NoPath".Translate();
            else if (actor.skills.GetSkill(SkillDefOf.Social).TotallyDisabled) reason = "CannotPrioritizeWorkTypeDisabled".Translate(SkillDefOf.Social.LabelCap);
            else if (clickedPawn.mindState.traderDismissed) reason = "TraderDismissed".Translate();
            else if (!actor.CanTradeWith(clickedPawn.Faction, clickedPawn.TraderKind).Accepted) reason = "MissingTitleAbility".Translate();
            if (reason != null) { yield return new FloatMenuOption(label + ": " + reason, null); yield break; }
            Action action = () =>
            {
                Job job = JobMaker.MakeJob(StorageTradeDefOf.MS_TradeWithStorage, clickedPawn);
                job.playerForced = true;
                actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.InteractingWithTraders, KnowledgeAmount.Total);
            };
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action,
                MenuOptionPriority.InitiateSocial, null, clickedPawn), actor, clickedPawn);
        }
    }

    [DefOf]
    internal static class StorageTradeDefOf
    {
        public static JobDef MS_TradeWithStorage = null;
        public static JobDef MS_TradeWithStorageOrbital = null;
        static StorageTradeDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(StorageTradeDefOf)); }
    }

    public sealed class JobDriver_StorageTrade : JobDriver
    {
        private Pawn Trader => (Pawn)TargetThingA;
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(Trader, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(() => !Trader.CanTradeNow);
            Toil trade = ToilMaker.MakeToil("StorageTrade");
            trade.initAction = () =>
            {
                if (Trader.CanTradeNow && StorageTradeSession.HasCore(pawn.Map)) StorageTradeSession.Open(pawn, Trader);
            };
            yield return trade;
        }
    }
}
