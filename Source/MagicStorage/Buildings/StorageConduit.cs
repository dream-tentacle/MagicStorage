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
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }
    }

    public sealed class Graphic_StorageConduit : Graphic_Linked
    {
        public Graphic_StorageConduit() { }
        private Graphic_StorageConduit(Graphic graphic) : base(graphic)
        { path = graphic.path; color = graphic.color; colorTwo = graphic.colorTwo; drawSize = graphic.drawSize; }
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
                StorageConnectionQuery.NodeAt(parent.Map, cell, parent.Faction, true) != null;
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (thing == null)
            {
                // GhostUtility recognizes vanilla linked graphics through linkType, which
                // would double-wrap this custom graphic. Explicitly use the same icon ghost.
                GraphicDatabase.Get<Graphic_Single>(thingDef.uiIconPath, ShaderTypeDefOf.EdgeDetect.Shader, drawSize, color)
                    .DrawWorker(loc, rot, thingDef, null, extraRotation);
                return;
            }
            base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            base.Print(layer, thing, extraRotation);
            if (StorageConnectionQuery.IsPlan(thing)) return;
            // Like Graphic_LinkedTransmitter: extend the ground cable into the neighboring
            // transmitting facility. Its whole occupied area appears in the overlay layer.
            foreach (var direction in GenAdj.CardinalDirections)
            {
                var cell = thing.Position + direction;
                var neighbor = StorageConnectionQuery.NodeAt(thing.Map, cell, thing.Faction, false);
                if (neighbor == null || StorageConnectionQuery.IsConduit(neighbor.def)) continue;
                Printer_Plane.PrintPlane(layer, cell.ToVector3ShiftedWithAltitude(AltitudeLayer.Conduits),
                    Vector2.one, LinkedDrawMatFrom(thing, cell), extraRotation);
            }
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
