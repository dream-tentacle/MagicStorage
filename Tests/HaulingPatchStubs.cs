// Controlled native job boundary. No hauling patches are installed by the mod.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace Verse
{
    public class ModContentPack { }
    public class Mod { public Mod(ModContentPack content) { } }
    public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); public int ColonistsSpawnedCount = 1; }
}

namespace RimWorld
{
    public sealed class DefOfAttribute : Attribute { }
    public static class DefOfHelper { public static void EnsureInitializedInCtor(Type type) { } }
    public enum StoragePriority { Unstored, Low, Normal, Important, Critical }
    public enum HaulMode { ToContainer, ToCellStorage }
    public interface IHaulDestination { }
    public static class StoreUtility
    {
        public static IHaulDestination Destination;
        public static int SearchCount;
        public static StoragePriority CurrentStoragePriorityOf(Thing item, bool forced) => StoragePriority.Unstored;
        public static bool TryFindBestBetterStorageFor(Thing item, Pawn pawn, Map map,
            StoragePriority priority, object faction, out IntVec3 cell, out IHaulDestination destination)
        {
            SearchCount++;
            cell = new IntVec3(); destination = Destination;
            return destination != null;
        }
    }
}

namespace Verse.AI
{
    public class JobDef
    {
        public Verse.DefModExtension extension;
        public T GetModExtension<T>() where T : Verse.DefModExtension => extension as T;
    }
    public static class JobMaker
    {
        public static Job MakeJob(JobDef def, Thing item, Thing destination = null) =>
            new Job { def = def, targetA = item, targetB = destination };
        public static Job MakeJob(JobDef def, LocalTargetInfo a) => new Job { def = def, targetA = a.Thing };
        public static Job MakeJob(JobDef def, Thing a, LocalTargetInfo b) => new Job { def = def, targetA = a, targetB = b.Thing, targetBCell = b };
        public static Job MakeJob(JobDef def, LocalTargetInfo a, LocalTargetInfo b, LocalTargetInfo c) => new Job { def = def, targetA = a.Thing, targetB = b.Thing, targetBCell = b, targetC = c };
    }
    public static class HaulAIUtility
    {
        public static int OriginalCalls;
        public static readonly Job VanillaJob = new Job();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Job HaulToStorageJob(Pawn p, Thing t, bool forced)
        {
            OriginalCalls++;
            return VanillaJob;
        }
    }
}
