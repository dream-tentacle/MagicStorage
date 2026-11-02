using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    internal enum StorageGuideStage { Basics, Advanced, Completed }

    public sealed class GameComponent_StorageGuideNotice : GameComponent
    {
        private bool sent;
        private bool transportPodNoticeSent;
        private bool shuttleNoticeSent;
        private bool vehicleNoticeSent;
        internal bool VehicleNoticeSent => vehicleNoticeSent;
        private int learned;
        private int advancedLearned;
        private StorageGuideStage stage;
        private bool basicRewardSent;
        private bool advancedRewardSent;
        internal static readonly string[] AdvancedDefNames =
        {
            "MS_CosmicWorkbench", "MS_CosmicMechGestator", "MS_StorageSupplyShelf",
            "MS_ConstructionSupplyUnit", "MS_StorageApparelAdapter", "MS_FuelSupplier",
            "MS_TransportSupplier", "MS_CaravanTeleporter", "MS_ProductionOutputPort"
        };

        internal StorageGuideStage Stage => stage;
        internal static bool IsBasicBuilding(string defName) => defName == "MS_ConstructionSupplyUnit";
        internal bool Learned(int step) => step >= 0 && step < 6 && (learned & (1 << step)) != 0;
        internal bool AdvancedLearned(int step) => (advancedLearned & (1 << step)) != 0;

        public GameComponent_StorageGuideNotice(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sent, "storageGuideNoticeSent", false);
            Scribe_Values.Look(ref transportPodNoticeSent, "storageTransportPodNoticeSent", false);
            Scribe_Values.Look(ref shuttleNoticeSent, "storageShuttleNoticeSent", false);
            Scribe_Values.Look(ref vehicleNoticeSent, "storageVehicleNoticeSent", false);
            Scribe_Values.Look(ref learned, "storageGuideLearned", 0);
            Scribe_Values.Look(ref advancedLearned, "storageGuideAdvancedLearned", 0);
            Scribe_Values.Look(ref stage, "storageGuideStage", StorageGuideStage.Basics);
            Scribe_Values.Look(ref basicRewardSent, "storageGuideBasicRewardSent", false);
            Scribe_Values.Look(ref advancedRewardSent, "storageGuideAdvancedRewardSent", false);
        }

        internal void Complete(int step, Building_StorageCore core)
        {
            if (!ValidCore(core) || step < 0 || step >= 6) return;
            learned |= 1 << step;
            UpdateStage(core);
        }

        internal void CompleteAdvanced(int step, Building_StorageCore core)
        {
            if (!ValidCore(core) || step < 0 || step >= AdvancedDefNames.Length) return;
            advancedLearned |= 1 << step;
            UpdateStage(core);
        }

        private static bool ValidCore(Building_StorageCore core) =>
            core != null && core.Spawned && !core.Destroyed && core.Faction == Faction.OfPlayer;

        private void UpdateStage(Building_StorageCore core)
        {
            if ((learned & 63) != 63) return;
            for (int i = 0; i < AdvancedDefNames.Length; i++)
                if (IsBasicBuilding(AdvancedDefNames[i]) && !AdvancedLearned(i)) return;
            if (stage == StorageGuideStage.Basics) stage = StorageGuideStage.Advanced;
            if (!basicRewardSent)
            {
                SendReward(core.Map, 100, 5, "MS_ConstructionSupplyUnit", 1, "MS_StorageSupplyShelf", "Basics");
                basicRewardSent = true;
            }
            for (int i = 0; i < AdvancedDefNames.Length; i++)
                if (!IsBasicBuilding(AdvancedDefNames[i]) &&
                    DefDatabase<ThingDef>.GetNamedSilentFail(AdvancedDefNames[i]) != null && !AdvancedLearned(i)) return;
            stage = StorageGuideStage.Completed;
            if (!advancedRewardSent)
            {
                SendReward(core.Map, 200, 10, "MS_CosmicWorkbench", 3, "MS_HighStackSupplyShelf", "Advanced");
                advancedRewardSent = true;
            }
        }

        private static void SendReward(Map map, int steel, int components, string buildingDefName,
            int buildingCount, string shelfDefName, string stageKey)
        {
            // Match vanilla quest rewards: one group produces one incoming pod.
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            var rewards = new List<Thing>();
            AddReward(rewards, ThingDefOf.Steel, steel);
            AddReward(rewards, ThingDefOf.ComponentIndustrial, components);
            AddBuildingReward(rewards, buildingDefName, buildingCount);
            AddBuildingReward(rewards, shelfDefName, 1);
            DropPodUtility.DropThingGroupsNear(dropSpot, map, new List<List<Thing>> { rewards },
                openDelay: 110, instaDrop: false, leaveSlag: false, canRoofPunch: false,
                forbid: false, allowFogged: false);
            Find.LetterStack.ReceiveLetter(("MS_Guide_" + stageKey + "CompleteTitle").Translate(),
                ("MS_Guide_" + stageKey + "CompleteText").Translate(), LetterDefOf.PositiveEvent,
                new TargetInfo(rewards[0].PositionHeld, map));
        }

        private static void AddBuildingReward(List<Thing> rewards, string defName, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Thing building = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
                building.SetFaction(Faction.OfPlayer);
                rewards.Add(building.MakeMinified());
            }
        }

        private static void AddReward(List<Thing> rewards, ThingDef def, int count)
        {
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = System.Math.Min(count, def.stackLimit);
                rewards.Add(thing);
                count -= thing.stackCount;
            }
        }

        internal void Notify_TransportPodBuilt(Thing pod)
        {
            if (transportPodNoticeSent || !StorageTransportGroups.IsPlayerPod(pod.TryGetComp<CompTransporter>())) return;
            Find.LetterStack.ReceiveLetter("MS_Transport_FirstPodTitle".Translate(),
                "MS_Transport_FirstPodText".Translate(), LetterDefOf.NeutralEvent, pod);
            transportPodNoticeSent = true;
        }

        internal void Notify_ShuttleBuilt(Thing shuttle)
        {
            if (shuttleNoticeSent || shuttle == null || !shuttle.Spawned || shuttle.Destroyed ||
                shuttle.Faction != Faction.OfPlayer || shuttle.def.defName != "PassengerShuttle") return;
            Find.LetterStack.ReceiveLetter("MS_Transport_FirstShuttleTitle".Translate(),
                "MS_Transport_FirstShuttleText".Translate(), LetterDefOf.NeutralEvent, shuttle);
            shuttleNoticeSent = true;
        }

        internal void Notify_VehicleBuilt(Map map, IntVec3 position)
        {
            if (vehicleNoticeSent || map == null) return;
            Find.LetterStack.ReceiveLetter("MS_Transport_FirstVehicleTitle".Translate(),
                "MS_Transport_FirstVehicleText".Translate(), LetterDefOf.NeutralEvent,
                new TargetInfo(position, map));
            vehicleNoticeSent = true;
        }

        internal void Notify_CoreBuilt(Building_StorageCore core)
        {
            if (sent || stage != StorageGuideStage.Basics || !ValidCore(core)) return;
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
                (def.thingClass == typeof(Building_StorageCore) || def.HasComp(typeof(CompTransporter)))
                ? __instance.Map : null;
        }

        private static void AfterConstruction(Frame __instance, Map __state)
        {
            if (__state == null || !__instance.Destroyed) return;
            var core = __instance.Position.GetFirstThing<Building_StorageCore>(__state);
            Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.Notify_CoreBuilt(core);
            foreach (Thing thing in __instance.Position.GetThingList(__state))
            {
                if (thing.def != __instance.def.entityDefToBuild) continue;
                Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.Notify_TransportPodBuilt(thing);
                Current.Game?.GetComponent<GameComponent_StorageGuideNotice>()?.Notify_ShuttleBuilt(thing);
                break;
            }
        }
    }
}
