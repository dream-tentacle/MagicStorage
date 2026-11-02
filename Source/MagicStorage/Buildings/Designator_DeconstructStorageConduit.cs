using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Designator_DeconstructStorageConduit : Designator_Deconstruct
    {
        public Designator_DeconstructStorageConduit()
        {
            defaultLabel = "MS_DeconstructStorageConduit".Translate();
            defaultDesc = "MS_DeconstructStorageConduitDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/DeconstructConduit");
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (!(t is Building_StorageConduit)) return false;
            return base.CanDesignateThing(t);
        }
    }
}
