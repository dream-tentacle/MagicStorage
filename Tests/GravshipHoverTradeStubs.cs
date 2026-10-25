// Native and optional-mod boundaries modeled on the inspected 1.6/1.7 APIs.
// Production menu hooks, carried topology, API 2, reservations and storage deduction run unchanged.
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MagicStorage;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Verse
{
    public partial class Faction { public bool Hostile; public bool HostileTo(Faction other) => Hostile; }
    public partial class FactionDef { public bool permanentEnemy; }
    public struct IntVec2 { public int x, z; public IntVec2(int x, int z) { this.x = x; this.z = z; } }
    public partial class ThingDef { public IntVec2 size = new IntVec2(1, 1); }
    public partial struct Rot4
    {
        public Rot4 Rotated(RotationDirection direction) => new Rot4 { value = (value + (int)direction) % 4 };
        public static Rot4 East => new Rot4 { value = 1 };
        public static bool operator ==(Rot4 a, Rot4 b) => a.value == b.value;
        public static bool operator !=(Rot4 a, Rot4 b) => a.value != b.value;
        public override bool Equals(object obj) => obj is Rot4 other && this == other;
        public override int GetHashCode() => value;
    }
    public enum RotationDirection { None, Clockwise, Opposite, Counterclockwise }
    public static partial class GenAdj
    {
        public static IEnumerable<IntVec3> OccupiedRect(IntVec3 center, Rot4 rot, IntVec2 size)
        {
            if (rot.value % 2 != 0) { int width = size.x; size.x = size.z; size.z = width; }
            for (int x = 0; x < size.x; x++) for (int z = 0; z < size.z; z++)
                yield return new IntVec3(center.x - (size.x - 1) / 2 + x, center.z - (size.z - 1) / 2 + z);
        }
    }
    public class HoverStorageNode : Thing { public CompStorageNode Node; }
}
namespace RimWorld.Planet
{
    public class WorldObject
    {
        public bool Spawned = true;
        public int Tile;
        public Faction Faction = Faction.OfPlayer;
        public string Label = "settlement";
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual IEnumerable<Gizmo> GetGizmos() => new[] { new Command_Action { defaultLabel = "Native world command" } };
    }
    public class PositionData
    {
        public IntVec3 position;
        public RotationDirection relativeRotation;
        public PositionData(IntVec3 position, Rot4 rotation) { this.position = position; relativeRotation = (RotationDirection)rotation.value; }
        public struct Data { public IntVec3 local; public Rot4 rotation; }
    }
    public class Gravship : WorldObject
    {
        public int destinationTile;
        public Settlement Settlement;
        public readonly Dictionary<Thing, PositionData> Cargo = new Dictionary<Thing, PositionData>();
        private Dictionary<Thing, PositionData.Data> cachedPlacements;
        public Dictionary<Thing, PositionData.Data> ThingPlacements => cachedPlacements ??
            (cachedPlacements = Cargo.ToDictionary(pair => pair.Key, pair => new PositionData.Data
                { local = pair.Value.position, rotation = Rot4.North.Rotated(pair.Value.relativeRotation) }));
        public readonly List<Pawn> Pawns = new List<Pawn>();
        public IEnumerable<Thing> Things => Cargo.Keys;
        public bool ContainsThing(Thing thing) => Cargo.ContainsKey(thing);
        public bool ContainsPawn(Pawn pawn) => Pawns.Contains(pawn);
    }
    public class Settlement : WorldObject, ITrader
    {
        public bool HasMap, Available = true;
        public readonly ThingOwner Stock = new ThingOwner(null);
        public bool CanTradeNow => Available;
        public IEnumerable<Thing> Goods => Stock;
        public TraderKindDef TraderKind { get; } = new TraderKindDef();
        Faction ITrader.Faction => Faction;
        public void GiveSoldThingToTrader(Thing item, int count, Pawn actor) => item.SplitOff(count).Destroy();
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn actor) { }
    }
}
namespace RimWorld
{
    public partial class TradeDeal
    {
        private void AddToTradeables(Thing item, Transactor transactor)
        {
            var line = TransferableUtility.TradeableMatching(item, AllTradeables);
            if (line == null) { line = new Tradeable(); AllTradeables.Add(line); }
            line.AddThing(item, transactor);
        }
    }
}
namespace GravshipHoverTrade
{
    public static class GravshipCargo
    {
        public static Dictionary<Thing, PositionData> GetThings(Gravship ship) => ship.Cargo;
    }
    public static class GravshipHoverUtility
    {
        public static bool IsHoveringAtSettlement(Gravship ship, out Settlement settlement)
        { settlement = ship.Settlement; return ship.Spawned && ship.Tile == ship.destinationTile && settlement != null; }
        public static Pawn BestNegotiator(Gravship ship) => ship.Pawns.Where(p => p.Faction == Faction.OfPlayer && p.skills != null &&
            !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).FirstOrDefault();
        public static List<FloatMenuOption> ChoicesAtFor(Gravship ship)
        {
            var options = new List<FloatMenuOption>();
            if (IsHoveringAtSettlement(ship, out var settlement)) AddSettlementOptions(options, ship, settlement);
            return options;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddSettlementOptions(List<FloatMenuOption> options, Gravship ship, Settlement settlement)
        {
            options.Add(new FloatMenuOption("Native hover trade", () => Find.WindowStack.Add(
                new Dialog_Trade(BestNegotiator(ship), new GravshipTradeProxy(settlement, ship)))));
        }
    }
    public class GravshipTradeProxy : ITrader
    {
        public readonly Settlement Settlement;
        public readonly Gravship Ship;
        public GravshipTradeProxy(Settlement settlement, Gravship ship) { Settlement = settlement; Ship = ship; }
        public IEnumerable<Thing> Goods => Settlement.Goods;
        public bool CanTradeNow => Settlement.CanTradeNow;
        public Faction Faction => Settlement.Faction;
        public TraderKindDef TraderKind => Settlement.TraderKind;
        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn actor) => Ship.Things.Where(item => item.def.category != ThingCategory.Building).ToList();
        public void GiveSoldThingToTrader(Thing item, int count, Pawn actor)
        {
            item.SplitOff(count).Destroy();
            if (item.Destroyed) Ship.Cargo.Remove(item);
        }
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn actor) =>
            Ship.Cargo.Add(item.SplitOff(count), new PositionData(new IntVec3(0, 0), Rot4.North));
    }
    public static class Patch_TradeDeal_AddAllTradeables
    {
        public static int CargoFills;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Postfix(TradeDeal __instance)
        {
            if (!(TradeSession.trader is GravshipTradeProxy proxy)) return;
            CargoFills++;
            foreach (Thing item in proxy.ColonyThingsWillingToBuy(TradeSession.playerNegotiator))
                if (TradeUtility.PlayerSellableNow(item, TradeSession.trader))
                    AccessTools.Method(typeof(TradeDeal), "AddToTradeables").Invoke(__instance, new object[] { item, Transactor.Colony });
        }
    }
}
