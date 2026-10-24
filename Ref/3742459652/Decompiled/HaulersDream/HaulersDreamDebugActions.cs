using System.Collections.Generic;
using System.Text;
using HaulersDream.Core;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulersDream;

public static class HaulersDreamDebugActions
{
	private const string DiagVersion = "diag-v2";

	[DebugAction(/*Could not decode attribute arguments.*/)]
	private static void LogCarryState()
	{
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
		Pawn val = (Pawn)(object)((singleSelectedThing is Pawn) ? singleSelectedThing : null);
		if (val == null)
		{
			Messages.Message("Hauler's Dream: select a single pawn.", MessageTypeDefOf.RejectInput, false);
			return;
		}
		CompHauledToInventory comp = ((ThingWithComps)val).GetComp<CompHauledToInventory>();
		HaulersDreamSettings settings = HaulersDreamMod.Settings;
		float num = MassUtility.Capacity(val, (StringBuilder)null);
		float num2 = MassUtility.GearAndInventoryMass(val);
		float num3 = CarryMath.EffectiveCapacity(num, settings.carryLimitFraction);
		HashSet<Thing> hashSet = comp?.GetHashSet();
		ThingOwner<Thing> val2 = val.inventory?.innerContainer;
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(string.Format("{0}{1} {2}: max={3:0.#}kg current={4:0.#}kg limit={5:0.#}kg ", "[Hauler's Dream] ", "diag-v2", val, num, num2, num3) + $"encumbrance={CarryMath.EncumbranceFraction(num2, num):P0} eligible={YieldRouter.IsEligible(val)}");
		stringBuilder.AppendLine($"  settings: shareForCrafting={settings.shareForCrafting} shareForBuilding={settings.shareForBuilding} " + $"drafted={val.Drafted}");
		stringBuilder.AppendLine($"  yields: harvest={settings.yieldHarvest} logging={settings.yieldLogging} mining={settings.yieldMining} " + $"chunks={settings.yieldChunks} deepDrill={settings.yieldDeepDrill} deconstruct={settings.yieldDeconstruct} " + $"animals={settings.yieldAnimals} strip={settings.yieldStrip} uninstall={settings.yieldUninstall} " + $"fishing={settings.yieldFishing}");
		stringBuilder.AppendLine($"  TRACKED (tagged / shareable) x{hashSet?.Count ?? 0}:");
		if (hashSet != null)
		{
			foreach (Thing item in hashSet)
			{
				stringBuilder.AppendLine($"    - {((Def)(item?.def?)).defName} x{item?.stackCount} (stillInInventory={((val2 != null) ? new bool?(((ThingOwner)val2).Contains(item)) : ((bool?)null))}, reservable={item != null && ReservationUtility.CanReserve(val, LocalTargetInfo.op_Implicit(item), 1, -1, (ReservationLayerDef)null, false)})");
			}
		}
		stringBuilder.AppendLine($"  FULL INVENTORY x{((val2 != null) ? ((ThingOwner)val2).Count : 0)} (tagged? = counted for sharing):");
		if (val2 != null)
		{
			for (int i = 0; i < ((ThingOwner)val2).Count; i++)
			{
				stringBuilder.AppendLine($"    - {((Def)(val2[i]?.def?)).defName} x{val2[i]?.stackCount} (tagged={hashSet?.Contains(val2[i])})");
			}
		}
		Log.Message(stringBuilder.ToString().TrimEnd());
	}

	[DebugAction(/*Could not decode attribute arguments.*/)]
	private static void ForceUnload()
	{
		Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
		Pawn val = (Pawn)(object)((singleSelectedThing is Pawn) ? singleSelectedThing : null);
		if (val != null)
		{
			PawnUnloadChecker.CheckIfShouldUnload(val, forced: true);
		}
		else
		{
			Messages.Message("Hauler's Dream: select a single pawn.", MessageTypeDefOf.RejectInput, false);
		}
	}

	[DebugAction(/*Could not decode attribute arguments.*/)]
	private static void VerifyLedgerRoundTrip()
	{
		Map currentMap = Find.CurrentMap;
		object obj;
		if (currentMap == null)
		{
			obj = null;
		}
		else
		{
			MapPawns mapPawns = currentMap.mapPawns;
			obj = ((mapPawns != null) ? mapPawns.FreeColonistsSpawned : null);
		}
		List<Pawn> list = (List<Pawn>)obj;
		if (list == null || list.Count == 0)
		{
			HDLog.Msg("ledger round-trip: SKIPPED — no free colonists on the current map to key the synthetic ledger.");
			return;
		}
		Pawn val = list[0];
		Pawn val2 = ((list.Count > 1) ? list[1] : list[0]);
		Pawn val3 = ((list.Count > 2) ? list[2] : list[0]);
		ThingDef steel = ThingDefOf.Steel;
		ThingDef woodLog = ThingDefOf.WoodLog;
		ThingDef silver = ThingDefOf.Silver;
		LoadLedgerEntry loadLedgerEntry = new LoadLedgerEntry(currentMap);
		loadLedgerEntry.SetNeeded(new Dictionary<ThingDef, int>
		{
			{ steel, 300 },
			{ woodLog, 120 },
			{ silver, 50 }
		});
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("[Hauler's Dream] diag-v2 ledger save/load round-trip:");
		bool flag = true;
		loadLedgerEntry.ApplyClaim(val, new Dictionary<ThingDef, int>
		{
			{ steel, 120 },
			{ woodLog, 40 }
		});
		loadLedgerEntry.ApplyClaim(val2, new Dictionary<ThingDef, int> { { steel, 80 } });
		if (val3 != val && val3 != val2)
		{
			loadLedgerEntry.ApplyClaim(val3, new Dictionary<ThingDef, int> { { silver, 50 } });
		}
		flag &= Check(loadLedgerEntry, "after claims", stringBuilder);
		loadLedgerEntry.Settle(val, steel, 70);
		flag &= Check(loadLedgerEntry, "after partial settle", stringBuilder);
		loadLedgerEntry.ApplyClaim(val2, new Dictionary<ThingDef, int>
		{
			{ steel, 30 },
			{ woodLog, 20 }
		});
		flag &= Check(loadLedgerEntry, "after re-plan", stringBuilder);
		loadLedgerEntry.Release(val);
		flag &= Check(loadLedgerEntry, "after release", stringBuilder);
		Dictionary<ThingDef, int> b = LoadLedger<ThingDef, Pawn>.RecomputeClaimed(loadLedgerEntry.pawnClaims);
		bool flag2 = DictsEqual(loadLedgerEntry.totalClaimed, b);
		stringBuilder.AppendLine("  PostLoadInit RecomputeClaimed reproduces live totalClaimed: " + (flag2 ? "PASS" : "FAIL"));
		flag = flag && flag2;
		stringBuilder.Append("  RESULT: " + (flag ? "PASS — claim invariant holds and survives scribe recompute." : "FAIL — see lines above."));
		Log.Message(stringBuilder.ToString());
	}

	private static bool Check(LoadLedgerEntry entry, string step, StringBuilder sb)
	{
		Dictionary<ThingDef, int> dictionary = LoadLedger<ThingDef, Pawn>.RecomputeClaimed(entry.pawnClaims);
		bool flag = DictsEqual(entry.totalClaimed, dictionary);
		sb.AppendLine("  " + step + ": claimed=" + Fmt(entry.totalClaimed) + " expected(Σ pawnClaims)=" + Fmt(dictionary) + " -> " + (flag ? "PASS" : "FAIL"));
		return flag;
	}

	private static bool DictsEqual(Dictionary<ThingDef, int> a, Dictionary<ThingDef, int> b)
	{
		int num = a?.Count ?? 0;
		int num2 = b?.Count ?? 0;
		if (num != num2)
		{
			return false;
		}
		if (a == null)
		{
			return true;
		}
		foreach (KeyValuePair<ThingDef, int> item in a)
		{
			if (!b.TryGetValue(item.Key, out var value) || value != item.Value)
			{
				return false;
			}
		}
		return true;
	}

	private static string Fmt(Dictionary<ThingDef, int> d)
	{
		if (d == null || d.Count == 0)
		{
			return "{}";
		}
		StringBuilder stringBuilder = new StringBuilder("{");
		bool flag = true;
		foreach (KeyValuePair<ThingDef, int> item in d)
		{
			if (!flag)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append($"{((Def)(item.Key?)).defName}={item.Value}");
			flag = false;
		}
		return stringBuilder.Append("}").ToString();
	}
}
