using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Building_StorageConduit : Building
    {
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().MarkDirty();
        }
    }

    public sealed class Graphic_StorageConduit : Graphic_Linked
    {
        public Graphic_StorageConduit() { }
        private Graphic_StorageConduit(Graphic graphic) : base(graphic) { }
        public override void Init(GraphicRequest req)
        {
            subGraphic = new Graphic_Single();
            subGraphic.Init(req);
            data = req.graphicData;
            path = req.path;
            color = req.color;
            colorTwo = req.colorTwo;
            drawSize = req.drawSize;
        }
        public override bool ShouldLinkWith(IntVec3 cell, Thing parent)
        {
            return parent.Spawned && cell.InBounds(parent.Map) &&
                parent.Map.GetComponent<MapComponent_StorageNetworks>().HasNodeAt(cell, parent);
        }
        public override Graphic GetColoredVersion(Shader shader, Color newColor, Color newColorTwo)
        { return new Graphic_StorageConduit(subGraphic.GetColoredVersion(shader, newColor, newColorTwo)); }
    }

    public sealed class PlaceWorker_StorageConduit : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot,
            Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            foreach (Thing existing in loc.GetThingList(map))
            {
                if (existing == thingToIgnore || existing == thing) continue;
                ThingDef built = existing.def.entityDefToBuild as ThingDef ?? existing.def;
                if (built.thingClass == typeof(Building_StorageConduit))
                    return "MS_ConduitAlreadyPresent".Translate();
            }
            return true;
        }
    }
}
