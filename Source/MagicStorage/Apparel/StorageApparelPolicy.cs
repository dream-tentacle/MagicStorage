using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal interface IStorageApparelPolicy
    {
        bool CanOptimize(Pawn pawn);
        bool Allows(Pawn pawn, Apparel apparel);
        IDisposable BeginScoring(Pawn pawn);
        float RawScore(Pawn pawn, Apparel apparel);
        float ScoreGain(Pawn pawn, Apparel apparel, List<float> wornScores);
    }

    internal sealed class VanillaStorageApparelPolicy : IStorageApparelPolicy
    {
        // Vanilla's public scoring methods read this shared context. Scope it to this
        // pawn even when vanilla found no map apparel and skipped initialization.
        private static readonly FieldInfo warmth = AccessTools.Field(typeof(JobGiver_OptimizeApparel), "neededWarmth");
        public bool CanOptimize(Pawn pawn) => pawn.Spawned && pawn.Faction == Faction.OfPlayer &&
            pawn.outfits != null && pawn.apparel != null && !pawn.IsQuestLodger() &&
            !(pawn.IsMutant && pawn.mutant.Def.disableApparel);
        public bool Allows(Pawn pawn, Apparel apparel) => apparel != null && !apparel.Destroyed &&
            pawn.outfits.CurrentApparelPolicy.filter.Allows(apparel) && !apparel.IsForbidden(pawn) &&
            !apparel.IsBurning() && (apparel.def.apparel.gender == Gender.None || apparel.def.apparel.gender == pawn.gender) &&
            (!CompBiocodable.IsBiocoded(apparel) || CompBiocodable.IsBiocodedFor(apparel, pawn)) &&
            ApparelUtility.HasPartsToWear(pawn, apparel.def) &&
            apparel.def.apparel.developmentalStageFilter.Has(pawn.DevelopmentalStage);
        public IDisposable BeginScoring(Pawn pawn)
        {
            object previous = warmth.GetValue(null);
            warmth.SetValue(null, PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.TileInfo.tile, GenLocalDate.Twelfth(pawn)));
            return new WarmthScope(previous);
        }
        public float RawScore(Pawn pawn, Apparel apparel) => JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, apparel);
        public float ScoreGain(Pawn pawn, Apparel apparel, List<float> wornScores) => JobGiver_OptimizeApparel.ApparelScoreGain(pawn, apparel, wornScores);
        private sealed class WarmthScope : IDisposable
        {
            private readonly object previous;
            internal WarmthScope(object previous) { this.previous = previous; }
            public void Dispose() { warmth.SetValue(null, previous); }
        }
    }

    internal static class StorageApparelServices
    {
        internal static IStorageApparelPolicy Policy = new VanillaStorageApparelPolicy();
        internal static bool CanAccess(Pawn pawn, Building_StorageApparelAdapter adapter) =>
            adapter != null && adapter.Spawned && adapter.Map == pawn.Map && adapter.Faction == pawn.Faction &&
            adapter.CanWork && !adapter.IsForbidden(pawn) && !adapter.IsBurning() &&
            adapter.InteractionCell.Standable(adapter.Map) && !adapter.InteractionCell.IsForbidden(pawn);
    }
}
