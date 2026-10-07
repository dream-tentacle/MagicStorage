using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class StorageShelfFilter : IExposable
    {
        public QualityRange Quality = QualityRange.All;
        public FloatRange HitPoints = new FloatRange(0f, 1f);
        public bool AnyMaterial = true;
        public bool WarnWhenRestockFails;
        public List<ThingDef> Materials = new List<ThingDef>();
        private readonly ThingFilter nativeFilter = new ThingFilter();
        private ThingDef filteredDef;

        public bool Allows(Thing item)
        {
            if (item == null || item.Destroyed) return false;
            Thing inner = item.GetInnerIfMinified();
            if (inner == null) return false;
            if (filteredDef != inner.def)
            {
                nativeFilter.SetDisallowAll();
                nativeFilter.SetAllow(inner.def, true);
                filteredDef = inner.def;
            }
            nativeFilter.AllowedQualityLevels = Quality;
            nativeFilter.AllowedHitPointsPercents = HitPoints;
            return nativeFilter.Allows(inner) &&
                (!inner.def.MadeFromStuff || AnyMaterial || Materials.Contains(inner.Stuff));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Quality, "quality", QualityRange.All);
            Scribe_Values.Look(ref HitPoints, "hitPoints", new FloatRange(0f, 1f));
            Scribe_Values.Look(ref AnyMaterial, "anyMaterial", true);
            Scribe_Values.Look(ref WarnWhenRestockFails, "warnWhenRestockFails", false);
            Scribe_Collections.Look(ref Materials, "materials", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Materials == null) Materials = new List<ThingDef>();
                Materials.RemoveAll(material => material == null);
            }
        }
    }
}
