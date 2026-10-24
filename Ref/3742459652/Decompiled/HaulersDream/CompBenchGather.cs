using System.Collections.Generic;
using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

[StaticConstructorOnStartup]
public class CompBenchGather : ThingComp
{
	private static readonly Texture2D GatherIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Drop", false) ?? BaseContent.BadTex;

	public bool gatherIngredients = true;

	public override void PostExposeData()
	{
		((ThingComp)this).PostExposeData();
		Scribe_Values.Look<bool>(ref gatherIngredients, "haulersDreamBenchGatherIngredients", true, false);
	}

	public static bool Allows(Thing bench)
	{
		CompBenchGather compBenchGather = ((bench != null) ? ThingCompUtility.TryGetComp<CompBenchGather>(bench) : null);
		return BenchGatherPolicy.BenchAllowsGather(compBenchGather != null, compBenchGather?.gatherIngredients ?? true);
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (!(HaulersDreamMod.Settings?.showBenchGatherGizmo ?? false))
		{
			yield break;
		}
		ThingWithComps parent = base.parent;
		if (((parent != null) ? ((Thing)parent).Faction : null) != Faction.OfPlayerSilentFail || !BillRouteGate.IsRoutableBenchType((Thing)(object)base.parent))
		{
			yield break;
		}
		string text = TaggedString.op_Implicit(gatherIngredients ? Translator.Translate("HaulersDream.Gizmo.BenchGatherDescActive") : Translator.Translate("HaulersDream.Gizmo.BenchGatherDescInactive"));
		if (!GatherNotice.BenchSwitchGovernsPlainGather)
		{
			string text2 = GatherNotice.Text(GatherNotice.Current);
			if (!string.IsNullOrEmpty(text2))
			{
				text = text2 + "\n\n" + text;
			}
		}
		yield return (Gizmo)new Command_Toggle
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Gizmo.BenchGather")),
			defaultDesc = text,
			icon = (Texture)(object)GatherIcon,
			isActive = () => gatherIngredients,
			toggleAction = delegate
			{
				MultiplayerCompat.SetBenchGather((Thing)(object)base.parent, !gatherIngredients);
			}
		};
	}
}
