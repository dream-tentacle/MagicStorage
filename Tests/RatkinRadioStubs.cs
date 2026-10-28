// The inspected 1.6/1.7 boundary, including delayed opening and the original active-trade self-lock.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Verse
{
    public static partial class Widgets
    {
        internal static bool ClickRadioStorage, RadioStorageActive;
        internal static int RadioStorageDraws;
        public static bool ButtonText(UnityEngine.Rect rect, string label, bool drawBackground = true,
            bool doMouseoverSound = true, bool active = true)
        {
            RadioStorageDraws++;
            RadioStorageActive = active;
            bool clicked = ClickRadioStorage; ClickRadioStorage = false;
            return active && clicked;
        }
    }
}

namespace RatkinUnderground
{
    public static class RKU_DialogueManager
    {
        internal static int TradeDialogues;
        public static void TriggerDialogueEvents(Dialog_RKU_Radio radio, string triggerType = "")
        { if (triggerType == "trade") TradeDialogues++; }
    }
    public sealed class RKU_RadioGameComponent
    {
        public bool canTrade = true, isWaitingForTrade;
        public bool CanTradeNow => canTrade && !isWaitingForTrade;
    }

    public sealed class Dialog_RKU_Radio : Window, ITrader
    {
        public Thing radio;
        private bool tradeReady = true, tradeInProgress, hasTraded;
        public readonly RKU_RadioGameComponent Component = new RKU_RadioGameComponent();
        public readonly List<Thing> Stock = new List<Thing>(), pendingCargo = new List<Thing>();
        public Pawn Actor;
        public Action PendingOpen;
        public int GeneratedStocks, DeliveryLetters;
        public bool InProgress => tradeInProgress;
        public bool HasTraded => hasTraded;
        public TraderKindDef TraderKind { get; } = new TraderKindDef();
        public Faction Faction { get; } = new Faction();
        public IEnumerable<Thing> Goods => Stock;
        public RKU_RadioGameComponent GetRadioComponent() => Component;
        public bool CanTradeNow
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get { return (tradeReady || GetRadioComponent().CanTradeNow) && !tradeInProgress; }
        }
        public void Start() { OpenTradeWindow(); tradeReady = false; }
        public void SetReady(bool value) { tradeReady = value; }
        public void StartStorage()
        {
            Widgets.ClickRadioStorage = true;
            DoWindowContents(new UnityEngine.Rect(0, 0, 764, 564));
            Widgets.ClickRadioStorage = false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void DoWindowContents(UnityEngine.Rect rect) { }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OpenTradeWindow()
        {
            tradeInProgress = true;
            hasTraded = false;
            GeneratedStocks++;
            PendingOpen = () => Find.WindowStack.Add(new Dialog_Trade(Actor, this, false));
        }
        public void GiveSoldThingToTrader(Thing item, int count, Pawn actor)
        { item.SplitOff(count).Destroy(DestroyMode.Vanish); hasTraded = true; }
        public void GiveSoldThingToPlayer(Thing item, int count, Pawn actor)
        { pendingCargo.Add(item.SplitOff(count)); hasTraded = true; }
        public void EndRadio()
        {
            if (pendingCargo.Count > 0) DeliveryLetters++;
            if (hasTraded) Component.canTrade = false;
            tradeInProgress = false;
            Find.WindowStack.TryRemove(this, false);
        }
    }
}
