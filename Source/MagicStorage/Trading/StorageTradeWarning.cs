using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class GameComponent_StorageTradeWarning : GameComponent
    {
        internal bool Suppressed;

        public GameComponent_StorageTradeWarning(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Suppressed, "suppressVanillaTradeWarning", false);
        }
    }

    [StaticConstructorOnStartup]
    internal static class StorageTradeWarningPatches
    {
        static StorageTradeWarningPatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.trading.warning");
            harmony.Patch(AccessTools.DeclaredMethod(typeof(FloatMenuOptionProvider_Trade), "GetOptionsFor",
                new[] { typeof(Pawn), typeof(FloatMenuContext) }),
                postfix: new HarmonyMethod(typeof(StorageTradeWarningPatches), nameof(TradeOptions)));
            harmony.Patch(AccessTools.Method(typeof(PassingShip), nameof(PassingShip.CommFloatMenuOption)),
                postfix: new HarmonyMethod(typeof(StorageTradeWarningPatches), nameof(OrbitalOption)));
        }

        private static void TradeOptions(FloatMenuContext context, ref IEnumerable<FloatMenuOption> __result)
        {
            __result = WrapTradeOption(__result, context.map);
        }

        private static IEnumerable<FloatMenuOption> WrapTradeOption(IEnumerable<FloatMenuOption> options, Map map)
        {
            // The native provider yields trade (or its disabled reason) first, then dismissal.
            bool first = true;
            foreach (FloatMenuOption option in options)
            {
                if (first) Wrap(option, map);
                first = false;
                yield return option;
            }
        }

        private static void OrbitalOption(PassingShip __instance, Building_CommsConsole console, Pawn negotiator,
            FloatMenuOption __result)
        {
            if (__instance is TradeShip ship && negotiator.CanTradeWith(ship.Faction, ship.TraderKind).Accepted &&
                Building_OrbitalTradeBeacon.AllPowered(console.Map).Any())
                Wrap(__result, console.Map);
        }

        private static void Wrap(FloatMenuOption option, Map map)
        {
            if (option?.action == null) return;
            Action original = option.action;
            option.action = () =>
            {
                var state = Current.Game?.GetComponent<GameComponent_StorageTradeWarning>();
                if (state == null || state.Suppressed || !StorageTradeSession.HasCore(map))
                {
                    original();
                    return;
                }
                // Native message boxes place button B on the left and button A on the right.
                Find.WindowStack.Add(new Dialog_MessageBox("MS_Trade_VanillaWarning".Translate(),
                    buttonAText: "MS_Trade_DoNotShowAgain".Translate(),
                    buttonAAction: () =>
                    {
                        state.Suppressed = true;
                        original();
                    },
                    buttonBText: "OK".Translate(),
                    buttonBAction: original,
                    acceptAction: original,
                    cancelAction: () => { }));
            };
        }
    }
}
