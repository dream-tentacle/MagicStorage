using System;
using System.Collections.Generic;
using RavenRace.Features.Operator;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RavenRace;

[StaticConstructorOnStartup]
public class CompFusangRadio : ThingComp
{
	private static Texture2D cachedCommandIcon;

	private Texture2D CommandIcon
	{
		get
		{
			if ((Texture)(object)cachedCommandIcon == (Texture)null)
			{
				ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail("CommsConsole");
				if (namedSilentFail != null)
				{
					cachedCommandIcon = ((BuildableDef)namedSilentFail).uiIcon;
				}
				if ((Texture)(object)cachedCommandIcon == (Texture)null)
				{
					cachedCommandIcon = BaseContent.BadTex;
				}
			}
			return cachedCommandIcon;
		}
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		CompPowerTrader comp = base.parent.GetComp<CompPowerTrader>();
		bool num = comp == null || comp.PowerOn;
		Command_Action val = new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("RavenRace_OpenFusangRadio")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("RavenRace_OpenFusangRadioDesc")),
			icon = (Texture)(object)CommandIcon,
			action = delegate
			{
				//IL_001a: Unknown result type (might be due to invalid IL or missing references)
				if (FindUser() != null)
				{
					Dialog_FusangComm.Open((Thing)(object)base.parent);
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("RavenRace_NoColonistCanReach")), LookTargets.op_Implicit((Thing)(object)base.parent), MessageTypeDefOf.RejectInput, true);
				}
			}
		};
		if (!num)
		{
			((Gizmo)val).Disable(TaggedString.op_Implicit(Translator.Translate("NoPower")));
		}
		yield return (Gizmo)(object)val;
		if (!DebugSettings.ShowDevGizmos || ((Thing)base.parent).Faction != Faction.OfPlayer)
		{
			yield break;
		}
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Label")),
			defaultDesc = TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Description")),
			action = delegate
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				WorldComponent_OperatorManager.ChangeFavorability(100, bypassCap: true);
				Messages.Message(TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Message")), MessageTypeDefOf.PositiveEvent, true);
			}
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Label_3")),
			action = delegate
			{
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				FusangResourceManager.Add(FusangResourceType.Resources, 100000);
				FusangResourceManager.Add(FusangResourceType.Military, 100000);
				FusangResourceManager.Add(FusangResourceType.Intel, 100000);
				FusangResourceManager.Add(FusangResourceType.Influence, 100000);
				Messages.Message(TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Message_2")), MessageTypeDefOf.TaskCompletion, true);
			}
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("RavenRace_Dev_ResetCooldown")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("RavenRace_Dev_ResetCooldownDesc")),
			action = delegate
			{
				//IL_0032: Unknown result type (might be due to invalid IL or missing references)
				World world = Find.World;
				WorldComponent_Fusang worldComponent_Fusang = ((world != null) ? world.GetComponent<WorldComponent_Fusang>() : null);
				if (worldComponent_Fusang != null)
				{
					worldComponent_Fusang.lastTradeTick = Find.TickManager.TicksGame - worldComponent_Fusang.tradeCooldownTicks;
					Messages.Message(TaggedString.op_Implicit(Translator.Translate("RavenRace_Dev_ResetCooldown_Message")), MessageTypeDefOf.TaskCompletion, true);
				}
			}
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Label_4")),
			defaultDesc = TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Description_3")),
			action = delegate
			{
				//IL_006e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0053: Unknown result type (might be due to invalid IL or missing references)
				//IL_0038: Unknown result type (might be due to invalid IL or missing references)
				IncidentDef namedSilentFail = DefDatabase<IncidentDef>.GetNamedSilentFail("FallenLeavesQuest");
				if (namedSilentFail != null)
				{
					IncidentParms val2 = StorytellerUtility.DefaultParmsNow(namedSilentFail.category, (IIncidentTarget)(object)((Thing)base.parent).Map);
					if (namedSilentFail.Worker.TryExecute(val2))
					{
						Messages.Message(TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Message_3")), MessageTypeDefOf.PositiveEvent, true);
					}
					else
					{
						Messages.Message(TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Message_4")), MessageTypeDefOf.RejectInput, true);
					}
				}
				else
				{
					Messages.Message(TaggedString.op_Implicit(RavenText.Key("RavenRace_Fusang_CompFusangRadio_Message_5")), MessageTypeDefOf.RejectInput, true);
				}
			}
		};
	}

	public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
	{
		if (!ReachabilityUtility.CanReach(selPawn, LocalTargetInfo.op_Implicit((Thing)(object)base.parent), (PathEndMode)4, (Danger)3, false, false, (TraverseMode)0))
		{
			yield return new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("CannotUseNoPath")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield break;
		}
		CompPowerTrader comp = base.parent.GetComp<CompPowerTrader>();
		if (comp != null && !comp.PowerOn)
		{
			yield return new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("CannotUseNoPower")), (Action)null, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
			yield break;
		}
		yield return new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("RavenRace_OpenFusangRadio")), (Action)delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			Job val = JobMaker.MakeJob(RavenDefOf.Raven_Job_UseFusangRadio, LocalTargetInfo.op_Implicit((Thing)(object)base.parent));
			selPawn.jobs.TryTakeOrderedJob(val, (JobTag?)(JobTag)0, false);
		}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0);
	}

	private Pawn FindUser()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		if (((Thing)base.parent).Map == null)
		{
			return null;
		}
		foreach (Pawn item in ((Thing)base.parent).Map.mapPawns.FreeColonistsSpawned)
		{
			if (!item.Dead && !item.Downed && ReachabilityUtility.CanReach(item, LocalTargetInfo.op_Implicit((Thing)(object)base.parent), (PathEndMode)4, (Danger)3, false, false, (TraverseMode)0))
			{
				return item;
			}
		}
		return null;
	}
}
