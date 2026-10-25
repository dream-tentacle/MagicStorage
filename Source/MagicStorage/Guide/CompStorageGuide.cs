using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class CompProperties_StorageGuide : CompProperties
    {
        public CompProperties_StorageGuide() { compClass = typeof(CompStorageGuide); }
    }

    // Learning belongs to this core and survives network rebuilds and minification.
    public sealed class CompStorageGuide : ThingComp
    {
        private int learned;
        internal bool Learned(int step) => (learned & (1 << step)) != 0;
        internal void Complete(int step) { learned |= 1 << step; }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref learned, "storageGuideLearned", 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;
            var concept = DefDatabase<ConceptDef>.GetNamedSilentFail("MS_StorageGuide");
            if (concept != null) LessonAutoActivator.TeachOpportunity(concept, OpportunityType.GoodToKnow);
            yield return new Command_Action
            {
                Order = 10000f,
                defaultLabel = "MS_Guide_Title".Translate(),
                defaultDesc = "MS_Guide_Description".Translate(),
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Gizmos/MagicStorage/Guide"),
                action = () => Find.WindowStack.Add(new Dialog_StorageGuide((Building_StorageCore)parent))
            };
        }
    }
}
