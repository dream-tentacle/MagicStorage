using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace MagicStorage
{
    internal static class StorageFoodWorkUtility
    {
        // Reuse the vanilla room-wide prisoner demand calculation, including other prisoners.
        private static readonly Func<Pawn, bool> FoodInPrisonRoom = (Func<Pawn, bool>)Delegate.CreateDelegate(
            typeof(Func<Pawn, bool>), AccessTools.Method(typeof(WorkGiver_Warden_DeliverFood), "FoodAvailableInRoomTo"));

        internal static bool WardenCanCare(Pawn pawn, Pawn prisoner, bool forced) => prisoner != null &&
            prisoner.IsPrisonerOfColony && prisoner.guest.PrisonerIsSecure && prisoner.Spawned &&
            !prisoner.InAggroMentalState && !prisoner.IsForbidden(pawn) && !prisoner.IsFormingCaravan() &&
            pawn.CanReserveAndReach(prisoner, PathEndMode.OnCell, pawn.NormalMaxDanger(), 1, -1, null, forced);

        internal static bool PatientEligible(Pawn getter, Pawn patient, WorkGiverDef def, bool forced)
        {
            if (patient == null || getter == patient || (def.feedHumanlikesOnly && !patient.RaceProps.Humanlike) ||
                (def.feedAnimalsOnly && !patient.IsAnimal) || patient.DevelopmentalStage.Baby() ||
                !FeedPatientUtility.IsHungry(patient) || !FeedPatientUtility.ShouldBeFed(patient) ||
                (WardenFeedUtility.ShouldBeFed(patient) && !getter.IsColonyMech)) return false;
            return getter.CanReserveAndReach(patient, PathEndMode.Touch, Danger.Deadly, 1, -1, null, forced);
        }

        internal static bool StillNeeded(Pawn pawn, StorageFoodPurpose purpose, LocalTargetInfo target, bool forced = false)
        {
            if (purpose == StorageFoodPurpose.Eat) return pawn.needs?.food != null;
            if (purpose == StorageFoodPurpose.Binge) return pawn.InMentalState &&
                pawn.MentalStateDef?.defName == "Binging_Food";
            if (purpose == StorageFoodPurpose.Pack)
                return pawn.needs?.food != null && JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn) <= 0.4f && !MassUtility.IsOverEncumbered(pawn);
            if (purpose == StorageFoodPurpose.Gathering)
                return pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage <= 0.9f && pawn.mindState.duty != null &&
                    target.IsValid && pawn.mindState.duty.focus.Cell == target.Cell;
            Thing destination = target.Thing;
            if (destination == null || destination.Destroyed || !destination.Spawned || destination.Map != pawn.Map ||
                destination.IsForbidden(pawn) || !pawn.CanReserveAndReach(destination, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced)) return false;
            if (StorageFoodRequest.IsDevice(purpose)) return DeviceNeedsFood(destination, purpose, forced);
            Pawn recipient = target.Pawn;
            if (recipient == null || recipient.Dead || recipient.needs?.food == null) return false;
            if (purpose == StorageFoodPurpose.FeedPatient)
                return !recipient.DevelopmentalStage.Baby() && FeedPatientUtility.IsHungry(recipient) &&
                    FeedPatientUtility.ShouldBeFed(recipient) && (!WardenFeedUtility.ShouldBeFed(recipient) || pawn.IsColonyMech);
            if (purpose == StorageFoodPurpose.WardenFeed)
                return WardenCanCare(pawn, recipient, forced) && WardenFeedUtility.ShouldBeFed(recipient) && FeedPatientUtility.IsHungry(recipient);
            if (purpose == StorageFoodPurpose.Deliver)
                return WardenCanCare(pawn, recipient, forced) && recipient.guest.CanBeBroughtFood &&
                    recipient.Position.IsInPrisonCell(recipient.Map) && FeedPatientUtility.IsHungry(recipient) &&
                    !WardenFeedUtility.ShouldBeFed(recipient) && !FoodInPrisonRoom(recipient);
            if (purpose == StorageFoodPurpose.BottleFeed)
                return ChildcareUtility.CanSuckle(recipient, out _) && ChildcareUtility.CanSuckleNow(recipient, out _) &&
                    ChildcareUtility.CanFeedBaby(pawn, recipient, out _) && (forced || ChildcareUtility.WantsSuckle(recipient, out _));
            if (StorageFoodRequest.IsAnimalWork(purpose))
            {
                if (!recipient.IsAnimal || !WorkGiver_InteractAnimal.CanInteractWithAnimal(pawn, recipient, out _, forced)) return false;
                if (purpose == StorageFoodPurpose.Train)
                    return recipient.Faction == pawn.Faction && recipient.training?.NextTrainableToTrain() != null && !TrainableUtility.TrainedTooRecently(recipient);
                return TameUtility.CanTame(recipient) && !TameUtility.TriedToTameTooRecently(recipient) &&
                    pawn.Map.designationManager.DesignationOn(recipient, DesignationDefOf.Tame) != null;
            }
            return false;
        }

        internal static bool DeviceNeedsFood(Thing device, StorageFoodPurpose purpose, bool forced)
        {
            if (device.IsBurning() || device.Map.designationManager.DesignationOn(device, DesignationDefOf.Deconstruct) != null) return false;
            if (purpose == StorageFoodPurpose.GrowthVat)
                return ModsConfig.BiotechActive && device is Building_GrowthVat vat && vat.NutritionNeeded > 2.5f;
            if (purpose == StorageFoodPurpose.Biosculpter)
            {
                var pod = device.TryGetComp<CompBiosculpterPod>();
                return ModsConfig.IdeologyActive && pod != null && pod.PowerOn && pod.State == BiosculpterPodState.LoadingNutrition &&
                    (forced || pod.autoLoadNutrition) && pod.RequiredNutritionRemaining > 0f;
            }
            if (!(device is ISlotGroupParent) || device.def != ThingDefOf.Hopper) return false;
            foreach (Thing item in device.Position.GetThingList(device.Map))
                if (Building_NutrientPasteDispenser.IsAcceptableFeedstock(item.def) && (float)item.stackCount / item.def.stackLimit > 0.35f) return false;
            Thing first = device.Position.GetFirstItem(device.Map);
            return first == null || Building_NutrientPasteDispenser.IsAcceptableFeedstock(first.def);
        }

        internal static bool DeviceAccepts(Pawn pawn, Thing device, Thing food, StorageFoodPurpose purpose)
        {
            if (device == null) return false;
            if (purpose == StorageFoodPurpose.GrowthVat)
                return device is Building_GrowthVat vat && vat.CanAcceptNutrition(food) && food.GetStatValue(StatDefOf.Nutrition) <= vat.NutritionNeeded;
            if (purpose == StorageFoodPurpose.Biosculpter) return device.TryGetComp<CompBiosculpterPod>()?.CanAcceptNutrition(food) == true;
            if (!(device is ISlotGroupParent hopper) || !hopper.GetStoreSettings().AllowedToAccept(food) ||
                !Building_NutrientPasteDispenser.IsAcceptableFeedstock(food.def) ||
                (food.def.ingestible.preferability != FoodPreferability.RawBad && food.def.ingestible.preferability != FoodPreferability.RawTasty)) return false;
            Thing first = device.Position.GetFirstItem(device.Map);
            // Honor storage priority so a hopper does not repeatedly exchange food with the core.
            var unit = food.holdingOwner?.Owner as Building_StorageUnit;
            StorageNetwork network = unit?.GetComp<CompStorageNode>()?.Network;
            return (first == null || first.CanStackWith(food)) && network?.Core != null &&
                (int)hopper.GetStoreSettings().Priority > (int)network.Core.Settings.Priority;
        }

        internal static int DeviceCount(Thing device, Thing food, StorageFoodPurpose purpose)
        {
            if (purpose == StorageFoodPurpose.Hopper)
                return food.def.stackLimit - (device.Position.GetFirstItem(device.Map)?.stackCount ?? 0);
            float needed = purpose == StorageFoodPurpose.GrowthVat ? ((Building_GrowthVat)device).NutritionNeeded :
                device.TryGetComp<CompBiosculpterPod>().RequiredNutritionRemaining;
            int count = (int)Math.Ceiling(needed / food.GetStatValue(StatDefOf.Nutrition));
            return Math.Min(count, device.TryGetInnerInteractableThingOwner()?.GetCountCanAccept(food) ?? 0);
        }
    }
}
