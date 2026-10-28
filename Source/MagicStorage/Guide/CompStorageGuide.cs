using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class CompProperties_StorageGuide : CompProperties
    {
        public CompProperties_StorageGuide() { compClass = typeof(CompStorageGuide); }
    }

    // Cores expose the shared tutorial; learning belongs to the current save.
    public sealed class CompStorageGuide : ThingComp
    {
        private GameComponent_StorageGuideNotice Progress => Current.Game?.GetComponent<GameComponent_StorageGuideNotice>();
        internal bool Learned(int step) => Progress?.Learned(step) == true;
        internal void Complete(int step) => Progress?.Complete(step, parent as Building_StorageCore);

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
