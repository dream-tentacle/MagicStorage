using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredients
{
	[ThreadStatic]
	public static Pawn CurrentWorker;

	[ThreadStatic]
	public static bool AddedThisSearch;

	private static void Prefix(Pawn pawn)
	{
		CurrentWorker = pawn;
		AddedThisSearch = false;
	}

	private static void Finalizer()
	{
		CurrentWorker = null;
		AddedThisSearch = false;
	}
}
