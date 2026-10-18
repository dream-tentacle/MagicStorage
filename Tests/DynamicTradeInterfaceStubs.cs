// Optional mod boundaries modeled on the inspected 1.6 DLL. The production
// compatibility module and Harmony hooks run unchanged against these methods.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace DynamicTradeInterface.Patches
{
    internal static class Dialog_TradePatches
    {
        internal static bool Replace = true;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Prefix(Dialog_Trade __instance, bool ___giftsOnly)
        {
            if (!Replace) return;
            var window = new UserInterface.Window_DynamicTrade(___giftsOnly);
            if (Find.WindowStack.TryRemove(__instance, false)) Find.WindowStack.Add(window);
        }
    }
}

namespace DynamicTradeInterface.UserInterface
{
    public sealed class Window_DynamicTrade : Window
    {
        internal static bool FailPreOpen, FailPostClose;
        internal readonly Dictionary<ThingDef, int> ColonyCounts = new Dictionary<ThingDef, int>();
        internal readonly Dictionary<ThingDef, int> TraderCounts = new Dictionary<ThingDef, int>();
        internal int Flashes;
        public Window_DynamicTrade(bool giftOnly = false) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void PreOpen()
        {
            if (FailPreOpen) throw new InvalidOperationException("Controlled dynamic window open failure");
            foreach (var line in TradeSession.deal.AllTradeables.Where(t => !t.IsCurrency))
            {
                int colony = line.CountHeldBy(Transactor.Colony), trader = line.CountHeldBy(Transactor.Trader);
                if (colony > 0) ColonyCounts[line.ThingDef] = colony;
                if (trader > 0) TraderCounts[line.ThingDef] = trader;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void PostClose()
        {
            if (FailPostClose) throw new InvalidOperationException("Controlled dynamic window close failure");
        }
        public void FlashSilver() { Flashes++; }
        internal bool Accept()
        {
            if (!TradeSession.deal.TryExecute(out _)) return false;
            Find.WindowStack.TryRemove(this, false); return true;
        }
    }
}
