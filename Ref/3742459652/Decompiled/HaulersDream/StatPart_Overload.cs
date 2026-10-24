using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public class StatPart_Overload : StatPart
{
	public override void TransformValue(StatRequest req, ref float val)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		if (TryGetFactor(req, out var factor))
		{
			val *= factor;
		}
	}

	public override string ExplanationPart(StatRequest req)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (TryGetFactor(req, out var factor))
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Stat.Overload") + ": x" + GenText.ToStringPercent(factor));
		}
		return null;
	}

	private static bool TryGetFactor(StatRequest req, out float factor)
	{
		factor = 1f;
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null)
		{
			return false;
		}
		if (settings.overloadLevel <= 0)
		{
			return false;
		}
		Thing thing = ((StatRequest)(ref req)).Thing;
		Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
		if (val == null || val.Dead || val.RaceProps == null)
		{
			return false;
		}
		if (!OverloadPolicy.AppliesSpeedPenalty(settings.strictCarryWeight, settings.overloadLevel, CECompat.IsActive, val.RaceProps.Humanlike, val.RaceProps.IsMechanoid, ((Thing)val).Faction != null && ((Thing)val).Faction.IsPlayer, val.Drafted))
		{
			return false;
		}
		PawnMass pawnMass = PawnMassCache.MassInfo(val);
		float capacity = pawnMass.Capacity;
		if (capacity <= 0f)
		{
			return false;
		}
		float num = pawnMass.CurrentMass / capacity;
		if (num <= 1f)
		{
			return false;
		}
		factor = OverloadTuning.SpeedFactor(settings.overloadLevel, num);
		return factor < 1f;
	}
}
