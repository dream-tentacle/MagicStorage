using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MagicStorage
{
    internal static class StorageCaravanTransfer
    {
        private sealed class Move
        {
            internal Thing Item, Received, Delivered;
            internal ThingOwner Source;
            internal int Count;
            internal bool Forbidden;
        }

        internal static bool Send(Dialog_StorageCaravan dialog, List<Pawn> pawns,
            PlanetTile directionTile, PlanetTile destinationTile)
        {
            if (!dialog.ValidateSelection(pawns)) return false;
            Map map = dialog.SourceMap;
            if (!map.Tile.LayerDef.canFormCaravans || !GenWorldClosest.TryFindClosestPassableTile(map.Tile, out _))
                return Dialog_StorageCaravan.Reject("MS_Caravan_LayerUnavailable");
            StorageNetwork network = dialog.Network;
            var moves = new List<Move>();
            var selected = new List<ThingCount>();
            var seen = new HashSet<Thing>();
            foreach (Pawn pawn in pawns) selected.Add(new ThingCount(pawn, 1));
            foreach (TransferableOneWay row in dialog.transferables)
            {
                if (row.AnyThing is Pawn) continue;
                int remaining = row.CountToTransfer;
                foreach (Thing item in row.things)
                {
                    int count = Math.Min(remaining, network.AvailableToWithdraw(item));
                    if (count > 0)
                    {
                        if (!seen.Add(item)) return Dialog_StorageCaravan.Reject("MS_Caravan_StockChanged");
                        moves.Add(new Move { Item = item, Source = item.holdingOwner, Count = count,
                            Forbidden = item.IsForbidden(Faction.OfPlayer) });
                        selected.Add(new ThingCount(item, count));
                        remaining -= count;
                    }
                    if (remaining == 0) break;
                }
                if (remaining != 0) return Dialog_StorageCaravan.Reject("MS_Caravan_StockChanged");
            }
            // Recheck the actual stacks we can withdraw, including retained pawn inventories.
            if (CollectionsMassCalculator.MassUsage(selected, IgnorePawnsInventoryMode.DontIgnore) > dialog.MassCapacity)
                return Dialog_StorageCaravan.Reject("TooBigCaravanMassUsage");
            var manager = map.GetComponent<MapComponent_StorageNetworks>();
            StorageRecoveryBatch staging = manager.CreateRecovery(dialog.Teleporter.Position);
            bool departing = false;
            try
            {
                // Each physical stack has its own claim; reserve everything before collecting.
                foreach (Move move in moves)
                    if (network.ReserveOutgoing(move, move.Item, move.Count, dialog.Teleporter,
                        usable: () => dialog.Usable) != move.Count)
                        return Dialog_StorageCaravan.Reject("MS_Caravan_StockChanged");
                foreach (Move move in moves)
                    if (!network.TryWithdrawReserved(move, move.Item, move.Count, staging.Contents,
                        out move.Received).Complete) return Dialog_StorageCaravan.Reject("MS_Caravan_StockChanged");
                foreach (Move move in moves)
                {
                    // Use the same inventory recipient selection as native instant/reformed caravans.
                    Pawn recipient = CaravanInventoryUtility.FindPawnToMoveInventoryTo(move.Received, pawns, null);
                    if (recipient?.inventory == null || StorageTransfer.Move(staging.Contents,
                        recipient.inventory.innerContainer, move.Received, move.Count, map,
                        dialog.Teleporter.Position, out move.Delivered, false) != move.Count)
                        return Dialog_StorageCaravan.Reject("MS_Caravan_TransferFailed");
                    move.Received.SetForbidden(false, false);
                }
                if (!dialog.Usable) return Dialog_StorageCaravan.Reject("MS_Caravan_Unavailable");
                foreach (Pawn pawn in pawns)
                {
                    // ExitMap destroys carried cargo; AddPawn also automatically takes carried pawns.
                    // Put both on the map first so only the selected pawns and network cargo depart.
                    if (pawn.carryTracker?.CarriedThing != null &&
                        !pawn.carryTracker.TryDropCarriedThing(pawn.PositionHeld, ThingPlaceMode.Near, out _))
                        return Dialog_StorageCaravan.Reject("MS_Caravan_CannotDropCarried");
                }
                // Past this point native callbacks may already have transferred pawns to the world.
                // Never remove their cargo in an exception handler once departure has started.
                departing = true;
                Caravan caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(pawns, Faction.OfPlayer,
                    map.Tile, directionTile, destinationTile);
                if (caravan == null)
                {
                    departing = false;
                    return Dialog_StorageCaravan.Reject("MS_Caravan_TransferFailed");
                }
                if (ModsConfig.BiotechActive && pawns.Exists(pawn => pawn.RaceProps.IsMechanoid))
                    LessonAutoActivator.TeachOpportunity(ConceptDefOf.MechsInCaravans, OpportunityType.GoodToKnow);
                return true;
            }
            catch (Exception error)
            {
                Log.Error("[MagicStorage] Caravan teleport failed: " + error);
                if (departing)
                {
                    // Do not leave a retryable send button after an irreversible native callback.
                    dialog.Close(doCloseSound: false);
                }
                return Dialog_StorageCaravan.Reject("MS_Caravan_TransferFailed");
            }
            finally
            {
                foreach (Move move in moves) network.ReleaseOutgoing(move);
                if (!departing)
                    foreach (Move move in moves)
                    {
                        Restore(move.Received, move, staging.Contents, map, dialog.Teleporter.Position);
                        // A limited recipient may have accepted a split piece before refusing the rest.
                        if (move.Delivered != move.Received)
                            Restore(move.Delivered, move, staging.Contents, map, dialog.Teleporter.Position);
                    }
                manager.ReleaseRecovery(staging);
            }
        }

        private static void Restore(Thing item, Move move, ThingOwner recovery, Map map, IntVec3 cell)
        {
            if (item == null || item.Destroyed || item.holdingOwner == null || item.holdingOwner == move.Source) return;
            item.SetForbidden(move.Forbidden, false);
            // A partial withdrawal leaves its original stack in a possibly full storage unit.
            // Merge back into that exact stack so rollback needs no extra slot and touches no other claim.
            Thing original = move.Source.Contains(move.Item) && move.Item != item ? move.Item : null;
            StorageTransfer.Move(item.holdingOwner, move.Source, item, item.stackCount, map, cell,
                original != null, original);
            // Preserve any failed restoration in the map's saved recovery buffer.
            if (!item.Destroyed && item.holdingOwner != null && item.holdingOwner != move.Source && item.holdingOwner != recovery)
                StorageTransfer.Move(item.holdingOwner, recovery, item, item.stackCount, map, cell, false);
        }
    }
}
