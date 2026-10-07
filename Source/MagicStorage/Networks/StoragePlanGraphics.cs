using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class StoragePlanGraphics
    {
        static StoragePlanGraphics() { Initialize(); }

        internal static void Initialize()
        {
            // Implied blueprints/frames already exist at this stage. Only our plans receive
            // a visual notification comp; adding the operational storage node would connect
            // unfinished buildings to the working network.
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if ((!def.IsBlueprint && !def.IsFrame) || !StorageConnectionQuery.IsStorage(def)) continue;
                if (def.comps == null) def.comps = new List<CompProperties>();
                if (!def.comps.Exists(p => p.compClass == typeof(CompStoragePlanGraphics)))
                    def.comps.Add(new CompProperties(typeof(CompStoragePlanGraphics)));
            }
        }
    }

    public sealed class CompStoragePlanGraphics : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            StorageNetworkDrawing.Dirty(parent.Map, parent.OccupiedRect());
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            StorageNetworkDrawing.Dirty(map, parent.OccupiedRect());
            base.PostDeSpawn(map, mode);
        }
    }
}
