using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    internal sealed class GravshipHoverTradeCompatibility : IStorageCompatibility
    {
        private delegate bool Hovering(Gravship ship, out Settlement settlement);
        private static Hovering hovering;
        private static Func<Gravship, Pawn> bestNegotiator;
        private static Func<Gravship, Dictionary<Thing, PositionData>> getThings;
        private static ConstructorInfo traderConstructor;
        public string PackageId => "memer.gravshiphovertrade";

        public void Apply(Harmony harmony)
        {
            Type utility = AccessTools.TypeByName("GravshipHoverTrade.GravshipHoverUtility");
            Type proxy = AccessTools.TypeByName("GravshipHoverTrade.GravshipTradeProxy");
            Type cargoPatch = AccessTools.TypeByName("GravshipHoverTrade.Patch_TradeDeal_AddAllTradeables");
            Type cargoType = AccessTools.TypeByName("GravshipHoverTrade.GravshipCargo");
            MethodInfo menu = utility == null ? null : AccessTools.Method(utility, "AddSettlementOptions");
            MethodInfo hover = utility == null ? null : AccessTools.Method(utility, "IsHoveringAtSettlement");
            MethodInfo negotiator = utility == null ? null : AccessTools.Method(utility, "BestNegotiator");
            MethodInfo cargo = cargoPatch == null ? null : AccessTools.Method(cargoPatch, "Postfix");
            MethodInfo things = cargoType == null ? null : AccessTools.Method(cargoType, "GetThings");
            ConstructorInfo constructor = proxy == null ? null : AccessTools.Constructor(proxy, new[] { typeof(Settlement), typeof(Gravship) });
            if (menu == null || hover == null || negotiator == null || cargo == null || things == null || constructor == null || !typeof(ITrader).IsAssignableFrom(proxy))
                throw new InvalidOperationException("Gravship Hover Trade API was not found.");
            hovering = AccessTools.MethodDelegate<Hovering>(hover);
            bestNegotiator = AccessTools.MethodDelegate<Func<Gravship, Pawn>>(negotiator);
            getThings = AccessTools.MethodDelegate<Func<Gravship, Dictionary<Thing, PositionData>>>(things);
            traderConstructor = constructor;
            harmony.Patch(menu, postfix: new HarmonyMethod(typeof(GravshipHoverTradeCompatibility), nameof(Menu)));
            harmony.Patch(AccessTools.Method(typeof(WorldObject), nameof(WorldObject.GetGizmos)),
                postfix: new HarmonyMethod(typeof(GravshipHoverTradeCompatibility), nameof(Gizmos)));
            harmony.Patch(cargo, prefix: new HarmonyMethod(typeof(GravshipHoverTradeCompatibility), nameof(KeepStorageScope)));
        }

        private static bool KeepStorageScope(TradeDeal __0) => !StorageTradeSession.TryGet(__0, out _);

        private static void Menu(List<FloatMenuOption> __0, Gravship __1, Settlement __2)
        {
            Command_Action command = CommandFor(__1, __2);
            if (command != null) __0.Add(new FloatMenuOption(command.defaultLabel +
                (command.Disabled ? ": " + command.disabledReason : ""), command.Disabled ? null : command.action));
        }

        private static IEnumerable<Gizmo> Gizmos(IEnumerable<Gizmo> __result, WorldObject __instance)
        {
            if (__result != null) foreach (Gizmo gizmo in __result) yield return gizmo;
            if (__instance is Gravship ship && hovering(ship, out Settlement settlement))
            {
                Command_Action command = CommandFor(ship, settlement);
                if (command != null) yield return command;
            }
        }

        private static Command_Action CommandFor(Gravship ship, Settlement settlement)
        {
            if (!hovering(ship, out Settlement current) || current != settlement ||
                !ship.Things.OfType<Building_StorageUnit>().Any()) return null;
            Pawn actor = bestNegotiator(ship);
            var units = ConnectedUnits(ship, out var layout);
            Func<bool> valid = () => CanTrade(ship, settlement, actor) && LayoutUnchanged(ship, layout);
            var command = new Command_Action
            {
                defaultLabel = "MS_Trade_Command".Translate(settlement.Label),
                defaultDesc = "MS_Trade_PortableInventoryNotice".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade"),
                action = () =>
                {
                    if (!valid()) { Unavailable(); return; }
                    var result = StorageTradeApi.Open(new StorageTradeOptions
                    {
                        Negotiator = actor, Trader = (ITrader)traderConstructor.Invoke(new object[] { settlement, ship }),
                        Mode = StorageTradeMode.Portable, Units = units, IsStillValid = valid
                    });
                    if (!result.Opened) Unavailable();
                }
            };
            if (units.Length == 0) command.Disable("MS_Trade_HoverNoNetwork".Translate());
            else if (!CanTrade(ship, settlement, actor)) command.Disable("MS_Trade_HoverUnavailable".Translate());
            return command;
        }

        private static void Unavailable() => Messages.Message("MS_Trade_HoverUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);

        private static bool CanTrade(Gravship ship, Settlement settlement, Pawn actor) =>
            ship.Spawned && ship.Faction == Faction.OfPlayer && hovering(ship, out Settlement current) && current == settlement &&
            settlement.Spawned && !settlement.HasMap && settlement.Faction != null && settlement.Faction != Faction.OfPlayer &&
            !settlement.Faction.def.permanentEnemy && !settlement.Faction.HostileTo(Faction.OfPlayer) && settlement.CanTradeNow &&
            actor != null && !actor.Destroyed && !actor.Spawned && actor.Faction == Faction.OfPlayer && ship.ContainsPawn(actor) &&
            actor.skills != null && !actor.skills.GetSkill(SkillDefOf.Social).TotallyDisabled &&
            actor.CanTradeWith(settlement.Faction, settlement.TraderKind).Accepted;

        private static bool IsNode(Thing thing) => !thing.Destroyed && !thing.Spawned && thing.Faction == Faction.OfPlayer &&
            thing.TryGetComp<CompStorageNode>() != null;

        // Use the same occupied-cell/cardinal adjacency and one-core rule as maps, limited to carried buildings.
        private static Building_StorageUnit[] ConnectedUnits(Gravship ship, out Dictionary<Thing, PositionData.Data> layout)
        {
            // ThingPlacements caches until the ship rotates. Reading it here would freeze the pre-purchase landing list.
            layout = getThings(ship).Where(pair => IsNode(pair.Key)).ToDictionary(pair => pair.Key,
                pair => new PositionData.Data { local = pair.Value.position, rotation = Rot4.North.Rotated(pair.Value.relativeRotation) });
            var cells = layout.ToDictionary(pair => pair.Key,
                pair => GenAdj.OccupiedRect(pair.Value.local, pair.Value.rotation, pair.Key.def.size).ToArray());
            var grid = new Dictionary<IntVec3, List<Thing>>();
            foreach (var pair in cells)
                foreach (var cell in pair.Value)
                {
                    if (!grid.TryGetValue(cell, out var list)) grid.Add(cell, list = new List<Thing>());
                    list.Add(pair.Key);
                }
            var remaining = new HashSet<Thing>(layout.Keys);
            var result = new List<Building_StorageUnit>();
            while (remaining.Count > 0)
            {
                Thing root = remaining.First(); remaining.Remove(root);
                var queue = new Queue<Thing>(); queue.Enqueue(root);
                var units = new List<Building_StorageUnit>(); int cores = 0;
                void Visit(IntVec3 cell)
                {
                    if (grid.TryGetValue(cell, out var neighbors))
                        foreach (Thing neighbor in neighbors) if (remaining.Remove(neighbor)) queue.Enqueue(neighbor);
                }
                while (queue.Count > 0)
                {
                    Thing node = queue.Dequeue();
                    if (node is Building_StorageCore) cores++;
                    if (node is Building_StorageUnit unit) units.Add(unit);
                    foreach (var cell in cells[node])
                    {
                        Visit(cell);
                        foreach (var direction in GenAdj.CardinalDirections) Visit(cell + direction);
                    }
                }
                if (cores == 1) result.AddRange(units);
            }
            return result.OrderBy(unit => unit.thingIDNumber).ToArray();
        }

        private static bool LayoutUnchanged(Gravship ship, Dictionary<Thing, PositionData.Data> layout)
        {
            var current = getThings(ship);
            return current.Count(pair => IsNode(pair.Key)) == layout.Count && layout.All(pair =>
                IsNode(pair.Key) && current.TryGetValue(pair.Key, out var placement) &&
                placement.position == pair.Value.local && Rot4.North.Rotated(placement.relativeRotation) == pair.Value.rotation);
        }
    }
}
