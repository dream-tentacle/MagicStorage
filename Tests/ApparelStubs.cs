// Original clothing evaluation and Wear execution remain engine boundaries.
// Tests link the actual adapter, job giver, reservation and retrieval code.
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Verse
{
    public enum Gender { None, Male, Female }
    public class StageFilter { public bool allowed = true; public bool Has(int stage) => allowed; }
    public class PawnMindState { public bool traderDismissed; public int nextApparelOptimizeTick; }
    public partial class Pawn
    {
        public PawnMindState mindState = new PawnMindState();
        public PawnOutfits outfits = new PawnOutfits();
        public Gender gender;
        public int DevelopmentalStage;
        public bool IsMutant, questLodger;
        public PawnMutant mutant = new PawnMutant();
        public bool IsQuestLodger() => questLodger;
        public Danger NormalMaxDanger() => Danger.Some;
    }
    public class PawnMutant { public MutantDef Def = new MutantDef(); }
    public class MutantDef { public bool disableApparel; }
    public class PawnOutfits { public ApparelPolicy CurrentApparelPolicy = new ApparelPolicy(); }
    public class ApparelPolicy { public ThingFilter filter = new ThingFilter(); }
    public partial class PawnApparel { public readonly HashSet<Apparel> Locked = new HashSet<Apparel>(); }
    public partial class Building { public IntVec3 InteractionCell => Position; }
    public class TileData { public int tile; }
    public partial class Map { public TileData TileInfo = new TileData(); }
    public static class GenLocalDate { public static int Twelfth(Pawn pawn) => 0; }
    public static class DebugViewSettings { public static bool debugApparelOptimize; }
}
namespace RimWorld
{
    public enum NeededWarmth { Any, Warm }
    public static class PawnApparelGenerator
    { public static NeededWarmth CalculateNeededWarmth(Pawn pawn, int tile, int twelfth) => NeededWarmth.Warm; }
    public partial class ApparelProperties
    { public Gender gender; public StageFilter developmentalStageFilter = new StageFilter(); public bool hasParts = true; }
    public partial class Apparel
    {
        public float testScore = 1f;
        public bool testBiocoded; public Pawn testOwner;
    }
    public static class CompBiocodable
    { public static bool IsBiocoded(Apparel item) => item.testBiocoded; public static bool IsBiocodedFor(Apparel item, Pawn pawn) => item.testOwner == pawn; }
    public static class ApparelUtility { public static bool HasPartsToWear(Pawn pawn, ThingDef def) => def.apparel.hasParts; }
    public interface IApparelSource : IThingHolder { Map Map { get; } bool ApparelSourceEnabled { get; } bool RemoveApparel(Apparel apparel); }
    public static class JobDefOf { public static readonly JobDef Wear = new JobDef(); public static readonly JobDef RemoveApparel = new JobDef(); }
    public class JobGiver_OptimizeApparel
    {
        private static NeededWarmth neededWarmth = NeededWarmth.Any;
        public Job NativeJob;
        public int NativeCalls;
        public static NeededWarmth CurrentWarmth => neededWarmth;
        public Job GiveJob(Pawn pawn) => TryGiveJob(pawn);
        protected virtual Job TryGiveJob(Pawn pawn)
        {
            NativeCalls++;
            if (Find.TickManager.TicksGame < pawn.mindState.nextApparelOptimizeTick) return null;
            if (NativeJob == null) pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame + 6000;
            return NativeJob;
        }
        public static float ApparelScoreRaw(Pawn pawn, Apparel item) => neededWarmth == NeededWarmth.Warm ? item.testScore : -1000f;
        public static float ApparelScoreGain(Pawn pawn, Apparel item, List<float> wornScores) => pawn.apparel.Locked.Count > 0 ? -1000f : ApparelScoreRaw(pawn, item);
    }
}
namespace Verse.AI
{
    public static partial class JobMaker
    { public static Job MakeJob(JobDef def, Thing a, Thing b, Thing c) => new Job { def = def, targetA = a, targetB = b, targetC = c }; }
    public sealed class NativeWearBoundary : JobDriver
    { protected override IEnumerable<Toil> MakeNewToils() { yield break; } }
}
