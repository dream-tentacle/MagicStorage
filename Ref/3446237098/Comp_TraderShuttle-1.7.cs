using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CeleTech.Base;

[StaticConstructorOnStartup]
public class Comp_TraderShuttle : ThingComp
{
	private const float CreditSilverPerPoint = 10f;

	private const float CreditBuyWeight = 0.65f;

	private const float CreditDiminishKnee = 800f;

	public Landed_CMCTS tradeShip;

	public bool mustCrash;

	private static readonly Texture2D SendAwayTexture = ContentFinder<Texture2D>.Get("UI/SendAway", true);

	private static Material LightTexture = MaterialPool.MatFrom("Things/Skyfaller/TradeShuttle_Light", ShaderDatabase.MoteGlow);

	private static Vector3 vec = new Vector3(5f, 0f, 5f);

	public CompProperties_TraderShuttle Props => base.props as CompProperties_TraderShuttle;

	public override void PostExposeData()
	{
		Scribe_Deep.Look<Landed_CMCTS>(ref tradeShip, "ship", Array.Empty<object>());
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		((ThingComp)this).PostSpawnSetup(respawningAfterLoad);
		if (!respawningAfterLoad && !mustCrash)
		{
			if (Props.soundThud != null)
			{
				SoundStarter.PlayOneShot(Props.soundThud, SoundInfo.op_Implicit((Thing)(object)base.parent));
			}
			string text = TaggedString.op_Implicit(Translator.Translate("TOT_TraderShuttleArrivalLabel"));
			string text2 = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("TOT_TraderShuttleArrivalText", NamedArgument.op_Implicit(((PassingShip)tradeShip).name)));
			Find.LetterStack.ReceiveLetter(TaggedString.op_Implicit(text), TaggedString.op_Implicit(text2), LetterDefOf.PositiveEvent, LookTargets.op_Implicit((Thing)(object)base.parent), (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null, 0, true);
		}
	}

	private int CalcTradeCredits(Landed_CMCTS ship, out float soldSilver, out float boughtSilver, out float effectiveSilver)
	{
		soldSilver = Mathf.Max(0f, ship?.soldSilverTotal ?? 0f);
		boughtSilver = Mathf.Max(0f, ship?.boughtSilverTotal ?? 0f);
		effectiveSilver = soldSilver + boughtSilver * 0.65f;
		float num = effectiveSilver / 10f;
		float num2 = 800f * Mathf.Log(1f + num / 800f);
		float tradePointFactor = Settings_CMC_Main.Settings_CMC.TradePointFactor;
		float num3 = num2 * tradePointFactor * 2.3f + ship.creditPointRemainder;
		int num4 = Mathf.FloorToInt(num3);
		ship.creditPointRemainder = num3 - (float)num4;
		return Mathf.Max(0, num4);
	}

	public override void CompTick()
	{
		if (((PassingShip)tradeShip).Departed)
		{
			if (((Thing)base.parent).Spawned)
			{
				SendAway();
			}
		}
		else
		{
			((PassingShip)tradeShip).PassingShipTick();
		}
	}

	public override string CompInspectStringExtra()
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(((Def)((TradeShip)tradeShip).def).LabelCap + "\n" + TranslatorFormattedStringExtensions.Translate("UTSLeavingIn", NamedArgument.op_Implicit(GenDate.ToStringTicksToPeriod(((PassingShip)tradeShip).ticksUntilDeparture, true, false, true, true, false))));
	}

	public override string TransformLabel(string label)
	{
		return ((PassingShip)tradeShip).name;
	}

	public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
	{
		bool flag = false;
		if (totalDamageDealt > 2000f)
		{
			flag = true;
		}
		else if ((double)((Thing)base.parent).HitPoints <= (double)((Thing)base.parent).MaxHitPoints * 0.5)
		{
			flag = true;
		}
		if (flag)
		{
			SendAway();
		}
	}

	public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn negotiator)
	{
		string text = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("TradeWith", NamedArgument.op_Implicit(((PassingShip)tradeShip).GetCallLabel())));
		yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, (Action)delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			Job val = JobMaker.MakeJob(CMC_Def.CMCTS_TradeWithShip, LocalTargetInfo.op_Implicit((Thing)(object)base.parent));
			negotiator.jobs.TryTakeOrderedJob(val, (JobTag?)(JobTag)0, false);
		}, (MenuOptionPriority)7, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0), negotiator, LocalTargetInfo.op_Implicit((Thing)(object)base.parent), "ReservedBy", (ReservationLayerDef)null);
	}

	private Faction GetFaction(TraderKindDef trader)
	{
		return null;
	}

	public void GenerateInternalTradeShip(Map map, TraderKindDef traderKindDef = null)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if (traderKindDef == null)
		{
			traderKindDef = GenCollection.RandomElementByWeightWithFallback<TraderKindDef>(DefDatabase<TraderKindDef>.AllDefs, (Func<TraderKindDef, float>)((TraderKindDef x) => x.CalculatedCommonality), (TraderKindDef)null);
		}
		tradeShip = new Landed_CMCTS(map, traderKindDef, GetFaction(traderKindDef));
		((PassingShip)tradeShip).passingShipManager = map.passingShipManager;
		((PassingShip)tradeShip).name = TaggedString.op_Implicit(Translator.Translate("CMC_TradeShipName"));
		((TradeShip)tradeShip).GenerateThings();
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("CMC_TraderShipsSendAway")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("CMC_TraderShipsSendAwayDesc")),
			action = SendAway,
			icon = (Texture)(object)SendAwayTexture
		};
	}

	private void SendAway()
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		if (!((Thing)base.parent).Spawned)
		{
			Log.Error("Tried to send " + ((object)base.parent)?.ToString() + " away, but it's unspawned.");
			return;
		}
		int num = 0;
		if (tradeShip != null)
		{
			num = CalcTradeCredits(tradeShip, out var _, out var _, out var _);
		}
		if (num > 0)
		{
			GameComponent_CeleTech.Instance.CurrentPoint += num;
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("TOT_PointsAdded", NamedArgument.op_Implicit(num.ToString("F0")), NamedArgument.op_Implicit(GameComponent_CeleTech.Instance.CurrentPoint.ToString("F0")))), MessageTypeDefOf.PositiveEvent, true);
		}
		Map map = ((Thing)base.parent).Map;
		IntVec3 position = ((Thing)base.parent).Position;
		((Entity)base.parent).DeSpawn((DestroyMode)0);
		Thing obj = ThingMaker.MakeThing(Props.takeoffAnimation, (ThingDef)null);
		Thing obj2 = ((obj is Skyfaller) ? obj : null);
		if (!((Skyfaller)obj2).innerContainer.TryAdd((Thing)(object)base.parent, true))
		{
			Log.Error("Could not add " + Gen.ToStringSafe<Thing>((Thing)(object)base.parent) + " to a skyfaller.");
			((Thing)base.parent).Destroy((DestroyMode)8);
		}
		GenSpawn.Spawn(obj2, position, map, (WipeMode)0);
	}

	public override void PostDraw()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		((ThingComp)this).PostDraw();
		Matrix4x4 val = default(Matrix4x4);
		Vector3 val2 = ((Thing)base.parent).DrawPos + Altitudes.AltIncVect + ((Thing)base.parent).def.graphicData.drawOffset;
		val2.y = Altitudes.AltitudeFor((AltitudeLayer)15);
		((Matrix4x4)(ref val)).SetTRS(val2, Quaternion.identity, vec);
		Graphics.DrawMesh(MeshPool.plane10, val, LightTexture, 0);
	}
}
