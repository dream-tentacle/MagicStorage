using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

internal static class StorageBeggarGiftTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string text)
    { checks++; if (!Equals(expected, actual)) throw new Exception(text + ": expected " + expected + ", got " + actual); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Building_StorageCore Core = new Building_StorageCore();
        internal readonly Building_StorageUnit Unit = new Building_StorageUnit();
        internal readonly ThingDef Def = new ThingDef { stackLimit = 1000, label = "silver" };
        internal readonly Pawn Actor, Receiver;
        internal readonly Lord Lord;
        internal MapComponent_StorageNetworks Manager => Map.GetComponent<MapComponent_StorageNetworks>();
        internal Fixture(int amount = 100, bool traveling = false)
        {
            Thing.BeforePaymentTestSplit = null; QuestUtility.BeforeSignal = null;
            Map.component = new MapComponent_StorageNetworks(Map);
            Register(Core, 0); Register(Unit, 1); Manager.EnsureCurrent();
            Actor = new Pawn { Map = Map, Spawned = true, Faction = Faction.OfPlayer, Position = new IntVec3(90, 0) };
            Receiver = new Pawn { Map = Map, Spawned = true, Faction = new Faction(), Position = new IntVec3(150, 0) };
            Lord = new Lord { Receiver = Receiver, Def = Def, Amount = amount };
            Lord.CurLordToil = traveling ? (LordToil)new LordToil_TravelAndWaitForItems
                { target = Receiver, requestedThingDef = Def, requestedThingCount = amount } :
                new LordToil_WaitForItems { target = Receiver, requestedThingDef = Def, requestedThingCount = amount };
            Receiver.lord = Lord;
        }
        internal void Register(Thing thing, int x)
        {
            thing.Map = Map; thing.Position = new IntVec3(x, 0); thing.Spawned = true; thing.Faction = Faction.OfPlayer;
            var node = new CompStorageNode { parent = thing };
            if (thing is Building_StorageCore core) { core.node = node; Map.listerBuildings.allBuildingsColonist.Add(core); }
            if (thing is Building_StorageUnit unit) unit.node = node;
            Manager.Register(node);
        }
        internal Thing Stock(int count, Building_StorageUnit unit = null)
        {
            var item = new Thing { def = Def, stackCount = count };
            Equal(true, (unit ?? Unit).Inventory.Contents.TryAdd(item, false), "stock added"); return item;
        }
        internal FloatMenuOption[] Options(Pawn receiver = null) =>
            GiveItemsToPawnUtility.GetFloatMenuOptionsForPawn(receiver ?? Receiver, Actor, Def, Lord.Amount).ToArray();
        internal FloatMenuOption Gift => Options().Last();
        internal int Received => Receiver.inventory.innerContainer.Where(t => t.def == Def).Sum(t => t.stackCount);
    }

    public static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StorageBeggarGifts).TypeHandle);
            Translation.Values["MS_Beggar_Give"] = "Magic gift {0} {1}";
            ImmediateAndRemaining(); MultipleNetworksAndReservations(); StaleMenus(); Eligibility();
            ExtractionFailure(); RejectedDelivery(); DuplicateAndReentrant(); MovingReceiverMenu();
            Console.WriteLine("PASS: " + checks + " beggar gift assertions (production Harmony patch, storage indexes and transfers; native quest/lord boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ImmediateAndRemaining()
    {
        foreach (bool traveling in new[] { false, true })
        {
            var f = new Fixture(traveling: traveling); f.Stock(120);
            f.Receiver.inventory.innerContainer.TryAdd(new Thing { def = f.Def, stackCount = 25 });
            var oldJob = new Job(); f.Actor.jobs.curJob = oldJob; IntVec3 position = f.Actor.Position;
            f.Map.reachability.Unreachable.Add(f.Receiver); f.Map.reachability.Unreachable.Add(f.Core);
            int signals = QuestUtility.ReceivedSignals, native = GiveItemsToPawnUtility.NativeClicks;
            var options = f.Options(); Equal(2, options.Length, "native and storage options coexist");
            options[0].action(); Equal(native + 1, GiveItemsToPawnUtility.NativeClicks, "native menu unchanged");
            Equal("Magic gift 75 silver", options[1].Label, "menu shows outstanding demand");
            options[1].action();
            Equal(45L, f.Core.Network.GetTotalCount(f.Def), "only remaining 75 deducted");
            Equal(100, f.Received, "real inventory receives complete demand");
            Equal(position, f.Actor.Position, "actor never moves"); Equal(oldJob, f.Actor.jobs.curJob, "actor keeps current job");
            Equal(signals + 1, QuestUtility.ReceivedSignals, "native receipt signal sent once");
            Equal(f.Actor, QuestUtility.LastGiver, "signal includes original giver");
            Equal(f.Receiver, QuestUtility.LastReceiver, "signal includes receiver");
            Equal(f.Receiver.questTags, QuestUtility.LastTags, "signal uses actual quest tags");
            Equal(1, f.Lord.Completions, "native completion reevaluated immediately");
            Equal(1, f.Lord.EndedPendingDeliveries, "pending delivery jobs end before more can arrive");
            Equal(1, f.Options().Length, "completed receiver has no magic gift option");
        }
    }

    private static void MultipleNetworksAndReservations()
    {
        var f = new Fixture(); var first = f.Stock(80); var claim = new object();
        f.Core.Network.ReserveOutgoing(claim, first, 30);
        var core = new Building_StorageCore(); var unit = new Building_StorageUnit();
        f.Register(core, 50); f.Register(unit, 51); f.Manager.EnsureCurrent(); f.Stock(70, unit);
        f.Gift.action();
        Equal(100, f.Received, "delivery spans networks");
        Equal(30L, f.Core.Network.GetTotalCount(f.Def), "reserved first-network stock preserved");
        Equal(20L, core.Network.GetTotalCount(f.Def), "second network provides shortfall");
        Equal(true, f.Core.Network.HasOutgoingReservation(claim, first, 30), "foreign reservation retained");
        f.Core.Network.ReleaseOutgoing(claim);
    }

    private static void StaleMenus()
    {
        var f = new Fixture(); var stock = f.Stock(120); var menu = f.Gift;
        var claim = new object(); f.Core.Network.ReserveOutgoing(claim, stock, 30);
        int signals = QuestUtility.ReceivedSignals; menu.action();
        Equal(120L, f.Core.Network.GetTotalCount(f.Def), "stale insufficient stock not deducted");
        Equal(0, f.Received, "no partial gift for insufficient stock");
        Equal(signals, QuestUtility.ReceivedSignals, "failed gift emits no receipt");
        Equal(true, f.Gift.action == null, "insufficient menu disabled"); f.Core.Network.ReleaseOutgoing(claim);
        menu = f.Gift; f.Receiver.inventory.innerContainer.TryAdd(new Thing { def = f.Def, stackCount = 60 }); menu.action();
        Equal(80L, f.Core.Network.GetTotalCount(f.Def), "click recalculates remaining demand"); Equal(100, f.Received, "no overpayment from stale count");
        f = new Fixture(); f.Stock(100); menu = f.Gift; f.Lord.CurLordToil = new LordToil(); menu.action();
        Equal(100L, f.Core.Network.GetTotalCount(f.Def), "departed requester cannot be gifted from stale menu");
        f = new Fixture(); f.Stock(100); menu = f.Gift; f.Core.Faction = new Faction(); menu.action();
        Equal(100L, f.Unit.Inventory.GetTotalCount(f.Def), "stale core ownership rejected");
    }

    private static void Eligibility()
    {
        var f = new Fixture(); f.Stock(100);
        var other = new Pawn { Map = f.Map, Spawned = true, Faction = f.Receiver.Faction, lord = f.Lord };
        Equal(1, f.Options(other).Length, "only designated group receiver gets option");
        f.Lord.LordJob = new object(); Equal(1, f.Options().Length, "unrelated lord untouched");
        f.Lord.LordJob = new LordJob_BegForItems(); f.Receiver.Faction.Hostile = true;
        Equal(1, f.Options().Length, "hostile receiver excluded"); f.Receiver.Faction.Hostile = false;
        f.Actor.Map = new Map(); Equal(1, f.Options().Length, "cross-map actor excluded"); f.Actor.Map = f.Map;
        f.Map.Fog.Add(f.Core.Position); Equal(1, f.Options().Length, "unavailable core excluded"); f.Map.Fog.Clear();
        f.Stock(50); Equal(2, f.Options().Length, "core requires no orbital beacon");
    }

    private static void ExtractionFailure()
    {
        foreach (bool throws in new[] { false, true })
        {
            var f = new Fixture(); var first = f.Stock(60); var second = f.Stock(60); var menu = f.Gift;
            Thing.BeforePaymentTestSplit = item =>
            {
                if (item != second) return;
                if (throws) throw new InvalidOperationException("injected split failure");
                f.Map.Fog.Add(f.Core.Position);
            };
            try { menu.action(); } finally { Thing.BeforePaymentTestSplit = null; f.Map.Fog.Clear(); }
            Equal(120L, f.Core.Network.GetTotalCount(f.Def), "failed staged gift restores all unreceived stock");
            Equal(0, f.Received, "failed staging gives nothing");
            Equal(120, f.Unit.Inventory.Contents.Sum(item => f.Core.Network.AvailableToWithdraw(item)),
                "all reservations released even when rollback merges the original stacks");
            f.Gift.action(); Equal(100, f.Received, "subsequent gift succeeds after cleanup");
        }
    }

    private sealed class RefusingOwner : ThingOwner
    {
        internal RefusingOwner() : base(null) { }
        public override bool TryAdd(Thing item, bool merge = true) => false;
    }

    private static void RejectedDelivery()
    {
        var f = new Fixture(); f.Stock(120); f.Receiver.inventory.innerContainer = new RefusingOwner();
        int signals = QuestUtility.ReceivedSignals; f.Gift.action();
        Equal(120L, f.Core.Network.GetTotalCount(f.Def), "receiver refusal restores extracted goods");
        Equal(0, f.Received, "refusing receiver receives nothing"); Equal(signals, QuestUtility.ReceivedSignals, "refusal never completes quest");
    }

    private static void DuplicateAndReentrant()
    {
        var f = new Fixture(); f.Stock(250); var menu = f.Gift; int signals = QuestUtility.ReceivedSignals;
        QuestUtility.BeforeSignal = () => menu.action();
        try { menu.action(); menu.action(); } finally { QuestUtility.BeforeSignal = null; }
        Equal(150L, f.Core.Network.GetTotalCount(f.Def), "reentrant and duplicate clicks deduct once");
        Equal(100, f.Received, "reentrant clicks never duplicate receipt");
        Equal(signals + 1, QuestUtility.ReceivedSignals, "one receipt signal"); Equal(1, f.Lord.Completions, "one departure transition");
    }

    private static void MovingReceiverMenu()
    {
        foreach (bool traveling in new[] { false, true })
        {
            var f = new Fixture(traveling: traveling); f.Stock(150);
            var option = f.Gift;
            var menu = new BeggarMenuRevalidationBoundary(f.Receiver.Position, position =>
                f.Receiver.Spawned && f.Receiver.Map == f.Actor.Map && f.Receiver.Position == position ?
                    f.Options() : new FloatMenuOption[0]);
            Equal(true, menu.StillValid(option), "gift initially available in map menu");
            f.Receiver.Position = new IntVec3(155, 2);
            Equal(true, menu.StillValid(option), "gift stays clickable after receiver leaves clicked cell");
            f.Receiver.Spawned = false;
            Equal(false, menu.StillValid(option), "receiver leaving map still invalidates menu");
            f.Receiver.Spawned = true;
            f.Receiver.Faction.Hostile = true;
            Equal(false, menu.StillValid(option), "hostile receiver still invalidates menu");
            f.Receiver.Faction.Hostile = false;
            if (traveling)
                f.Lord.CurLordToil = new LordToil_WaitForItems
                    { target = f.Receiver, requestedThingDef = f.Def, requestedThingCount = f.Lord.Amount };
            Equal(true, menu.StillValid(option), "menu survives transition from traveling to waiting");
            option.action();
            Equal(100, f.Received, "click after movement delivers to the same receiver");
            Equal(50L, f.Core.Network.GetTotalCount(f.Def), "click after movement deducts once");
            Equal(false, menu.StillValid(option), "completed request invalidates existing menu");
        }
    }
}
