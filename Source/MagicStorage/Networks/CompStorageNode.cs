using System;
using Verse;

namespace MagicStorage
{
    public sealed class CompProperties_StorageNode : CompProperties
    {
        public int slotCapacity = 64;
        public CompProperties_StorageNode() { compClass = typeof(CompStorageNode); }
    }

    public sealed class CompStorageNode : ThingComp
    {
        public StorageNetwork Network { get; internal set; }
        public int SlotCapacity => Math.Max(0, ((CompProperties_StorageNode)props).slotCapacity);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map.GetComponent<MapComponent_StorageNetworks>().Register(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            map.GetComponent<MapComponent_StorageNetworks>().Unregister(this);
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            return "MS_NetworkStatus".Translate(StorageNetwork.StatusLabel(Network));
        }
    }
}
