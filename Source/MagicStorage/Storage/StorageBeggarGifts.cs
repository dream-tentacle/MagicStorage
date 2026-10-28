using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StorageBeggarGifts
    {
        private static readonly HashSet<Pawn> delivering = new HashSet<Pawn>();

        static StorageBeggarGifts()
        {
            new Harmony("mjcg.magicstorage.beggars").Patch(
                AccessTools.Method(typeof(GiveItemsToPawnUtility), nameof(GiveItemsToPawnUtility.GetFloatMenuOptionsForPawn)),
                postfix: new HarmonyMethod(typeof(StorageBeggarGifts), nameof(AddOptions)));
        }

        private static void AddOptions(Pawn requester, Pawn current, ThingDef requestedThingDef, int countRequired,
            ref IEnumerable<FloatMenuOption> __result)
        {
            __result = WithStorage(__result, requester, current, requestedThingDef, countRequired);
        }

        private static bool Waiting(Pawn receiver, Pawn actor, ThingDef def, int amount, Lord expected)
        {
            if (receiver == null || actor == null || def == null || amount <= 0 ||
                !receiver.Spawned || receiver.Destroyed || receiver.Dead || receiver.inventory == null ||
                !actor.Spawned || actor.Destroyed || actor.Dead || actor.Downed ||
                actor.Faction != Faction.OfPlayer || actor.Map != receiver.Map ||
                receiver.Faction == Faction.OfPlayer || receiver.HostileTo(actor)) return false;
            Lord lord = receiver.GetLord();
            if (lord == null || (expected != null && lord != expected) || !(lord.LordJob is LordJob_BegForItems)) return false;
            if (lord.CurLordToil is LordToil_WaitForItems wait)
                return wait.target == receiver && wait.requestedThingDef == def && wait.requestedThingCount == amount;
            if (lord.CurLordToil is LordToil_TravelAndWaitForItems travel)
                return travel.target == receiver && travel.requestedThingDef == def && travel.requestedThingCount == amount;
            return false;
        }

        private static IEnumerable<FloatMenuOption> WithStorage(IEnumerable<FloatMenuOption> original,
            Pawn receiver, Pawn actor, ThingDef def, int amount)
        {
            foreach (FloatMenuOption option in original) yield return option;
            if (!Waiting(receiver, actor, def, amount, null)) yield break;
            int remaining = GiveItemsToPawnUtility.GetCountRemaining(receiver, def, amount);
            if (remaining <= 0) yield break;
            List<StorageNetwork> networks = Networks(receiver.Map);
            if (networks.Count == 0) yield break;
            long available = 0;
            foreach (StorageNetwork network in networks)
                foreach (Thing item in Stocks(network, def)) available += network.AvailableToWithdraw(item);
            string label = "MS_Beggar_Give".Translate(remaining, def.label, receiver.Named("RECEIVER"));
            if (available < remaining)
                yield return new FloatMenuOption(label + ": " + "MS_Beggar_Insufficient".Translate(available, remaining), null);
            else
            {
                Lord lord = receiver.GetLord();
                // Map menus revalidate at the original click cell unless a target
                // is bound. Follow the beggar as the native give option does.
                yield return new FloatMenuOption(label, () => Give(receiver, actor, def, amount, lord))
                    { revalidateClickTarget = receiver };
            }
        }

        private static List<StorageNetwork> Networks(Map map)
        {
            map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            var result = new List<StorageNetwork>();
            var seen = new HashSet<StorageNetwork>();
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
                if (building is Building_StorageCore core && StorageTradeSession.PaymentCoreEligible(core, map) &&
                    seen.Add(core.Network)) result.Add(core.Network);
            return result;
        }

        private static List<Thing> Stocks(StorageNetwork network, ThingDef def)
        {
            var result = new List<Thing>();
            network.GetStacks(def, result);
            result.RemoveAll(item => item == null || item.Destroyed || network.AvailableToWithdraw(item) <= 0);
            result.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            return result;
        }

        private static void Reject() => Messages.Message("MS_Beggar_Unavailable".Translate(), MessageTypeDefOf.RejectInput, false);

        private static void Give(Pawn receiver, Pawn actor, ThingDef def, int amount, Lord lord)
        {
            if (!delivering.Add(receiver)) return;
            try
            {
                if (!Waiting(receiver, actor, def, amount, lord)) { Reject(); return; }
                Map map = receiver.Map;
                int remaining = GiveItemsToPawnUtility.GetCountRemaining(receiver, def, amount);
                if (remaining <= 0) return;
                using (var transfer = new StorageMaterialTransaction(map, receiver.Position))
                {
                    int needed = remaining;
                    foreach (StorageNetwork network in Networks(map))
                    {
                        foreach (Thing item in Stocks(network, def))
                        {
                            int count = Math.Min(needed, network.AvailableToWithdraw(item));
                            if (count <= 0) continue;
                            if (!transfer.Add(item, count, network, network.Core)) { Reject(); return; }
                            needed -= count;
                            if (needed == 0) break;
                        }
                        if (needed == 0) break;
                    }
                    // Prepare the entire remainder before giving any items. A stale menu
                    // or a failed reservation returns all unreceived goods to storage.
                    if (needed != 0 || !transfer.TryStage() || !Waiting(receiver, actor, def, amount, lord) ||
                        GiveItemsToPawnUtility.GetCountRemaining(receiver, def, amount) != remaining)
                    { Reject(); return; }
                    foreach (var request in transfer.Requests)
                        if (!StorageTradeSession.PaymentCoreEligible(request.Network.Core, map)) { Reject(); return; }
                    foreach (var request in transfer.Requests)
                    {
                        if (!Waiting(receiver, actor, def, amount, lord)) { Reject(); return; }
                        transfer.Deliver(request, item => StorageTransfer.Move(item.holdingOwner,
                            receiver.inventory.innerContainer, item, item.stackCount, map, receiver.Position) == request.Count);
                        if (request.DeliveredCount != request.Count) { Reject(); return; }
                    }
                }
                if (GiveItemsToPawnUtility.GetCountRemaining(receiver, def, amount) > 0) { Reject(); return; }
                try
                {
                    // Match the native delivery signal, including Odyssey discoveries.
                    QuestUtility.SendQuestTargetSignals(receiver.questTags, "ReceivedItems", actor.Named("GIVER"), receiver.Named("RECEIVER"));
                }
                finally
                {
                    // Evaluate the native completion transition immediately. It ends
                    // pending deliveries before another hauler can give excess items.
                    lord.ReceiveMemo("MagicStorage.ItemsReceived");
                }
            }
            catch (Exception error)
            {
                Log.Error("[MagicStorage] Beggar gift failed: " + error);
                Reject();
            }
            finally { delivering.Remove(receiver); }
        }
    }
}
