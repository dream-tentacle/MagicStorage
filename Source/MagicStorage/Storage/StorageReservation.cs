using System;
using Verse;

namespace MagicStorage
{
    internal enum StorageReservationState { Reserved, Collecting, Collected, Released, Invalid }
    internal enum StorageReservationFailure { NodeUnavailable, NetworkUnavailable, ItemUnavailable, InsufficientCount, PolicyChanged }

    internal interface IStorageReservationClient
    {
        void OnReservationInvalidated(StorageReservation reservation, StorageReservationFailure reason);
    }

    // Also used after loading: the network manager knows no concrete job driver types.
    internal interface IStorageNetworkClient
    {
        void OnStorageNetworksRebuilt();
    }

    internal sealed class StorageReservation
    {
        internal object Owner;
        internal Thing Item;
        internal int Count;
        internal Building_StorageUnit Source;
        internal Building_StorageCore Core;
        internal Thing Endpoint;
        internal IStorageReservationClient Client;
        internal Func<bool> Usable;
        internal StorageReservationState State = StorageReservationState.Reserved;
        internal StorageReservationFailure Failure;
    }
}
