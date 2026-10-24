using HaulersDream.Core;
using Verse.AI;

namespace HaulersDream;

public static class PickupPause
{
	public static Toil MakeToil(TargetIndex stackInd, PickupDelayContext context)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		int num = PickupDelayPolicy.TicksPerStack(settings?.pickupDelayTicks ?? 0);
		if (num <= 0 || !PickupDelayPolicy.ShouldPause(context, settings?.pickupDelayOnHauling ?? false, settings?.pickupDelayOnLoading ?? false, settings?.pickupDelayOnDirectHarvest ?? false))
		{
			return Toils_General.Label();
		}
		Toil obj = Toils_General.Wait(num, stackInd);
		ToilEffects.WithProgressBarToilDelay(obj, stackInd, false, -0.5f);
		return obj;
	}
}
