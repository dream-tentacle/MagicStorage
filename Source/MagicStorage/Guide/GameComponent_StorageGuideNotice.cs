using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public sealed class GameComponent_StorageGuideNotice : GameComponent
    {
        private bool sent;

        public GameComponent_StorageGuideNotice(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sent, "storageGuideNoticeSent", false);
        }

        internal void Notify_CoreBuilt(Building_StorageCore core)
        {
            if (sent || core == null || !core.Spawned || core.Faction != Faction.OfPlayer) return;
            Find.LetterStack.ReceiveLetter("MS_Guide_FirstCoreTitle".Translate(),
                "MS_Guide_FirstCoreText".Translate(), LetterDefOf.NeutralEvent, core);
            sent = true;
        }
    }

    [StaticConstructorOnStartup]
    internal static class StorageGuideNoticePatches
    {
        static StorageGuideNoticePatches()
        {
            var harmony = new Harmony("mjcg.magicstorage.guide.notice");
            harmony.Patch(AccessTools.Method(typeof(Frame), nameof(Frame.CompleteConstruction)),
                prefix: new HarmonyMethod(typeof(StorageGuideNoticePatches), nameof(BeforeConstruction)),
                postfix: new HarmonyMethod(typeof(StorageGuideNoticePatches), nameof(AfterConstruction)));
        }

        // Capture the map before vanilla destroys the frame. Loading and reinstalling
        // a building do not complete a construction frame and cannot send this notice.
        private static void BeforeConstruction(Frame __instance, out Map __state)
        {
            __state = __instance.Faction == Faction.OfPlayer &&
                __instance.def.entityDefToBuild is ThingDef def &&
                def.thingClass == typeof(Building_StorageCore) ? __instance.Map : null;
        }

        private static void AfterConstruction(Frame __instance, Map __state)
        {
            if (__state == null || !__instance.Destroyed) return;
            var core = __instance.Position.GetFirstThing<Building_StorageCore>(__state);
            Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.Notify_CoreBuilt(core);
        }
    }
}
