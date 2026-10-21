// Native UI, route planning and world creation are controlled boundaries.
// Actual Harmony patches, network inventory, claims, withdrawal and rollback are linked unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace Verse
{
    public partial class Map { public PlanetTile Tile = new PlanetTile(1); public List<Pawn> CaravanCandidates = new List<Pawn>(); }
    public partial class Thing { public bool IsForbidden(Faction faction) => forbidden; }
    public partial class Pawn
    {
        public Pawn() { def.category = ThingCategory.Pawn; }
    }
    public static class Messages
    {
        public static string Last;
        public static void Message(string text, object type, bool historical) { Last = text; }
    }
    public enum OpportunityType { GoodToKnow }
    public static class LessonAutoActivator { public static void TeachOpportunity(object concept, OpportunityType type) { } }
    public struct ThingCount
    {
        public Thing Thing; public int Count;
        public ThingCount(Thing thing, int count) { Thing = thing; Count = count; }
    }
}
namespace RimWorld
{
    public static class MessageTypeDefOf { public static readonly object RejectInput = new object(); }
    public static class ConceptDefOf { public static readonly object MechsInCaravans = new object(); }
    public enum IgnorePawnsInventoryMode { DontIgnore, IgnoreIfAssignedToUnload }
    public partial class TransferableOneWay
    {
        public int CountToTransfer { get; set; }
        public ThingDef ThingDef => AnyThing.def;
        public bool HasAnyThing => things.Count != 0;
        public int MaxCount
        {
            [MethodImpl(MethodImplOptions.NoInlining)] get { return things.Sum(item => item.stackCount); }
        }
        public virtual int GetMaximumToTransfer() => MaxCount;
        public void AdjustTo(int count) { CountToTransfer = Math.Max(0, Math.Min(count, GetMaximumToTransfer())); }
    }
    public static partial class TransferableUtility
    {
        public static T TransferableMatching<T>(Thing item, List<T> rows, TransferAsOneMode mode) where T : TransferableOneWay =>
            rows.Find(row => TransferAsOne(item, row.AnyThing, mode));
        public static List<Pawn> GetPawnsFromTransferables(List<TransferableOneWay> rows) =>
            rows.Where(row => row.AnyThing is Pawn).SelectMany(row => row.things.Take(row.CountToTransfer).Cast<Pawn>()).ToList();
    }
    public static class CollectionsMassCalculator
    {
        public static float MassUsage(List<ThingCount> counts, IgnorePawnsInventoryMode mode) =>
            counts.Sum(entry => entry.Thing is Pawn pawn ? pawn.inventory.innerContainer.Sum(item => item.GetStatValue(null) * item.stackCount) :
                entry.Thing.GetStatValue(null) * entry.Count);
    }
    public class Dialog_FormCaravan : Window
    {
        private readonly Map map;
        private PlanetTile startingTile = new PlanetTile(2), destinationTile = new PlanetTile(3);
        public List<TransferableOneWay> transferables;
        public bool forcePause = true, Closed;
        public static int NativeItemFills, NativeDepartures, NativeChecks;
        public float MassCapacity = 500;
        public Dialog_FormCaravan(Map map) { this.map = map; }
        public void PostOpen() { CalculateAndRecacheTransferables(); }
        public void Reset() { CalculateAndRecacheTransferables(); }
        private void CalculateAndRecacheTransferables()
        {
            transferables = new List<TransferableOneWay>();
            foreach (Pawn pawn in AllSendablePawns(map, false))
            {
                Add(pawn);
                if (pawn.carryTracker.CarriedThing != null) Add(pawn.carryTracker.CarriedThing);
            }
            AddItemsToTransferables();
        }
        private void Add(Thing item) { var row = new TransferableOneWay(); row.things.Add(item); transferables.Add(row); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddItemsToTransferables() { NativeItemFills++; foreach (Thing item in map.Ground) Add(item); }
        public static List<Pawn> AllSendablePawns(Map map, bool reform) => new List<Pawn>(map.CaravanCandidates);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool CheckForErrors(List<Pawn> pawns)
        {
            NativeChecks++;
            return destinationTile.Valid && startingTile.Valid && pawns.Any(pawn => pawn.IsColonist && !pawn.Downed) &&
                CollectionsMassCalculator.MassUsage(SelectedCounts(), IgnoreInventoryMode) <= MassCapacity;
        }
        private List<ThingCount> SelectedCounts() => transferables.SelectMany(row =>
        {
            int remaining = row.CountToTransfer;
            return row.things.Select(item => { int count = Math.Min(remaining, item.stackCount); remaining -= count; return new ThingCount(item, count); });
        }).Where(count => count.Count > 0).ToList();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryFormAndSendCaravan() { NativeDepartures++; return CheckForErrors(TransferableUtility.GetPawnsFromTransferables(transferables)); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool DebugTryFormCaravanInstantly() { NativeDepartures++; return true; }
        private IgnorePawnsInventoryMode IgnoreInventoryMode
        {
            [MethodImpl(MethodImplOptions.NoInlining)] get { return IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload; }
        }
        public bool SendForTest() => TryFormAndSendCaravan();
        public bool InstantForTest() => DebugTryFormCaravanInstantly();
        public IgnorePawnsInventoryMode InventoryModeForTest() => IgnoreInventoryMode;
        public void ClearRouteForTest() { destinationTile = PlanetTile.Invalid; }
        public void Close(bool doCloseSound = true) { Closed = true; }
    }
}
namespace RimWorld.Planet
{
    public sealed class PlanetLayerDef { public bool canFormCaravans = true; }
    public struct PlanetTile
    {
        public int tile; public static readonly PlanetTile Invalid = new PlanetTile(-1);
        public PlanetTile(int tile) { this.tile = tile; }
        public bool Valid => tile >= 0;
        public PlanetLayerDef LayerDef => Layer;
        public static readonly PlanetLayerDef Layer = new PlanetLayerDef();
    }
    public class Caravan { public List<Pawn> Pawns; public PlanetTile Tile, Direction, Destination; }
    public static class GenWorldClosest
    {
        public static bool Passable = true;
        public static bool TryFindClosestPassableTile(PlanetTile root, out PlanetTile tile) { tile = root; return Passable; }
    }
    public static class CaravanInventoryUtility
    {
        public static int Calls; public static Action OnChoose;
        public static Pawn FindPawnToMoveInventoryTo(Thing item, List<Pawn> candidates, List<Pawn> ignored)
        { Calls++; OnChoose?.Invoke(); return candidates[0]; }
    }
    public static class CaravanExitMapUtility
    {
        public static Caravan Last;
        public static bool ReturnNull, ThrowAfterDeparture;
        public static Caravan ExitMapAndCreateCaravan(IEnumerable<Pawn> pawns, Faction faction, PlanetTile from, PlanetTile direction, PlanetTile destination)
        {
            if (ReturnNull) return null;
            Last = new Caravan { Pawns = pawns.ToList(), Tile = from, Direction = direction, Destination = destination };
            foreach (Pawn pawn in Last.Pawns) { pawn.Spawned = false; pawn.Map = null; }
            if (ThrowAfterDeparture) throw new InvalidOperationException("Injected native departure failure");
            return Last;
        }
    }
}
