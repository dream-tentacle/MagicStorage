using HaulersDream.Core;
using RimWorld;
using Verse;

namespace HaulersDream;

public class StatPart_MechCarryWeightCE : StatPart
{
	public override void TransformValue(StatRequest req, ref float val)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		if (TryGetMechCarryWeight(req, out var weight))
		{
			val = weight;
		}
	}

	public override string ExplanationPart(StatRequest req)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		if (TryGetMechCarryWeight(req, out var weight))
		{
			return TaggedString.op_Implicit(Translator.Translate("HaulersDream.Stat.MechCarryCapacity") + ": " + weight.ToString("0.##"));
		}
		return null;
	}

	private static bool TryGetMechCarryWeight(StatRequest req, out float weight)
	{
		weight = 0f;
		Thing thing = ((StatRequest)(ref req)).Thing;
		Pawn val = (Pawn)(object)((thing is Pawn) ? thing : null);
		if (val == null || val.Dead)
		{
			return false;
		}
		RaceProperties raceProps = val.RaceProps;
		if (raceProps == null || !raceProps.IsMechanoid)
		{
			return false;
		}
		if (((Thing)val).Faction == null || !((Thing)val).Faction.IsPlayer)
		{
			return false;
		}
		float num = HaulersDreamMod.Settings?.mechHaulMultiplier ?? 1f;
		if (!CarryCapacityPolicy.CeMechCarryOverrideActive(num))
		{
			return false;
		}
		float statValue = StatExtension.GetStatValue((Thing)(object)val, StatDefOf.CarryingCapacity, true, -1);
		weight = statValue * num;
		return weight > 0f;
	}
}
