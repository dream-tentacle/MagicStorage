using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Alert_CannotUnloadInventory : Alert_Critical
{
	private enum UnloadFault
	{
		None,
		InFlight,
		Stranded
	}

	private List<Pawn> lastCulprits = new List<Pawn>();

	private bool anyNoStorage;

	private readonly Dictionary<Pawn, int> problemSince = new Dictionary<Pawn, int>();

	private static List<Pawn> tmpDeadKeys = new List<Pawn>();

	private const int GraceTicks = 2500;

	private int lastComputeTick = -99999;

	private AlertReport cachedReport = AlertReport.Inactive;

	private const int RecomputeInterval = 60;

	public override bool DoMessage => false;

	public Alert_CannotUnloadInventory()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		((Alert)this).defaultLabel = TaggedString.op_Implicit(Translator.Translate("HaulersDream.Alert.CannotUnload"));
		((Alert)this).defaultExplanation = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Alert.CannotUnloadDesc.Stuck", NamedArgument.op_Implicit("")));
	}

	public override string GetLabel()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Alert.CannotUnload", NamedArgument.op_Implicit(lastCulprits.Count)));
	}

	public override TaggedString GetExplanation()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		if (lastCulprits.Count == 0)
		{
			return TaggedString.op_Implicit("");
		}
		List<string> list = new List<string>();
		for (int i = 0; i < lastCulprits.Count; i++)
		{
			list.Add(((Entity)lastCulprits[i]).LabelShortCap);
		}
		return TranslatorFormattedStringExtensions.Translate(anyNoStorage ? "HaulersDream.Alert.CannotUnloadDesc.NoStorage" : "HaulersDream.Alert.CannotUnloadDesc.Stuck", NamedArgument.op_Implicit(GenText.ToLineList((IList<string>)list, "  - ")));
	}

	public override AlertReport GetReport()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		if (settings == null || !settings.alertCannotUnload || Find.Maps == null || Find.TickManager == null)
		{
			return AlertReport.Inactive;
		}
		int ticksGame = Find.TickManager.TicksGame;
		if (lastComputeTick >= 0 && ticksGame - lastComputeTick < 60)
		{
			return cachedReport;
		}
		lastComputeTick = ticksGame;
		try
		{
			cachedReport = ComputeReport(settings, ticksGame, StuckTicksFor(settings));
		}
		catch (Exception ex)
		{
			HDLog.ErrOnce("Alert_CannotUnloadInventory.GetReport threw; returning the cached report to keep the HUD alive. This is a bug — please report this stack trace:\n" + ex, 1212457447);
		}
		return cachedReport;
	}

	private static int StuckTicksFor(HaulersDreamSettings s)
	{
		return Mathf.Max(2500, Mathf.RoundToInt(s.alertStuckHours * 2500f));
	}

	private AlertReport ComputeReport(HaulersDreamSettings s, int now, int stuckTicks)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		List<Pawn> list = new List<Pawn>();
		bool flag = false;
		List<Map> maps = Find.Maps;
		for (int i = 0; i < maps.Count; i++)
		{
			List<Pawn> list2 = maps[i].mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int j = 0; j < list2.Count; j++)
			{
				Pawn val = list2[j];
				bool noStorage;
				UnloadFault unloadFault = Evaluate(val, s, now, stuckTicks, out noStorage);
				if (unloadFault == UnloadFault.None)
				{
					problemSince.Remove(val);
					continue;
				}
				if (!problemSince.TryGetValue(val, out var value))
				{
					value = now;
					problemSince[val] = value;
				}
				if (unloadFault == UnloadFault.Stranded && now - value >= 2500)
				{
					list.Add(val);
					if (noStorage)
					{
						flag = true;
					}
				}
			}
		}
		PruneDeadDebounceKeys();
		lastCulprits = list;
		anyNoStorage = flag;
		if (list.Count <= 0)
		{
			return AlertReport.Inactive;
		}
		return AlertReport.CulpritsAre(list);
	}

	private void PruneDeadDebounceKeys()
	{
		if (problemSince.Count == 0)
		{
			return;
		}
		tmpDeadKeys.Clear();
		foreach (KeyValuePair<Pawn, int> item in problemSince)
		{
			if (item.Key == null || ((Thing)item.Key).Destroyed || !((Thing)item.Key).Spawned)
			{
				tmpDeadKeys.Add(item.Key);
			}
		}
		for (int i = 0; i < tmpDeadKeys.Count; i++)
		{
			problemSince.Remove(tmpDeadKeys[i]);
		}
		tmpDeadKeys.Clear();
	}

	private static UnloadFault Evaluate(Pawn p, HaulersDreamSettings s, int now, int stuckTicks, out bool noStorage)
	{
		noStorage = false;
		ThingOwner<Thing> val = p?.inventory?.innerContainer;
		if (val == null || ((Thing)p).Map == null)
		{
			return UnloadFault.None;
		}
		CompHauledToInventory comp = ((ThingWithComps)p).GetComp<CompHauledToInventory>();
		if ((comp == null || comp.PeekHashSet().Count == 0) && !YieldRouter.IsEligible(p))
		{
			return UnloadFault.None;
		}
		if (p.Drafted)
		{
			return UnloadFault.None;
		}
		if (!MapGate.ShouldUnloadToStorage(((Thing)p).Map))
		{
			if (!s.enableOnNonHomeMaps || !s.markForUnload || !s.autoDivertToPackAnimal)
			{
				return UnloadFault.None;
			}
			if (!PackAnimalLoad.HasDepositableSurplus(p))
			{
				return UnloadFault.None;
			}
			if (PackAnimalLoad.HasLoadJob(p))
			{
				return UnloadFault.InFlight;
			}
			if (PackAnimalLoad.FindCarrier(p) == null)
			{
				return UnloadFault.None;
			}
			if (comp != null)
			{
				foreach (Thing item in comp.PeekHashSet())
				{
					if (item != null && !item.Destroyed && ((ThingOwner)val).Contains(item) && InventorySurplus.SurplusOf(p, item) > 0 && now - comp.FirstTaggedTick(item) > stuckTicks)
					{
						return UnloadFault.Stranded;
					}
				}
			}
			return UnloadFault.None;
		}
		bool flag = false;
		for (int i = 0; i < ((ThingOwner)val).Count; i++)
		{
			Thing val2 = val[i];
			if (val2 != null && InventorySurplus.SurplusOf(p, val2) > 0 && !InventorySurplus.HasUnloadDestination(p, val2))
			{
				noStorage = true;
				flag = true;
				break;
			}
		}
		if (!flag && s.markForUnload && comp != null)
		{
			foreach (Thing item2 in comp.PeekHashSet())
			{
				if (item2 != null && !item2.Destroyed && ((ThingOwner)val).Contains(item2) && InventorySurplus.SurplusOf(p, item2) > 0 && now - comp.FirstTaggedTick(item2) > stuckTicks)
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return UnloadFault.None;
		}
		JobDef curJobDef = p.CurJobDef;
		if ((curJobDef != null && HdJobDefSets.InTransitLoadJobs.Contains(curJobDef)) || PawnUnloadChecker.HasQueuedUnload(p))
		{
			return UnloadFault.InFlight;
		}
		return UnloadFault.Stranded;
	}
}
