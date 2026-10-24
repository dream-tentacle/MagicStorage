using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulersDream;

public static class Global
{
	[ThreadStatic]
	public static bool IsExecutingManagedUnload;

	[ThreadStatic]
	public static bool IsExecutingManagedPortalUnload;

	private static bool reflectionInit;

	private static FieldInfo countToTransferField;

	private static FieldInfo editBufferField;

	private static void EnsureReflection()
	{
		if (!reflectionInit)
		{
			reflectionInit = true;
			countToTransferField = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");
			editBufferField = AccessTools.Field(typeof(Transferable), "editBuffer");
		}
	}

	public static bool ForceTo(TransferableOneWay tr, int value)
	{
		if (tr == null)
		{
			return false;
		}
		((Transferable)tr).ForceTo(value);
		return true;
	}

	public static bool ForceToViaReflection(TransferableOneWay tr, int value)
	{
		EnsureReflection();
		if (tr == null || countToTransferField == null)
		{
			return false;
		}
		countToTransferField.SetValue(tr, value);
		if (editBufferField != null)
		{
			editBufferField.SetValue(tr, value.ToString());
		}
		return true;
	}

	public static TransferableOneWay FindBestMatchFor(Thing deposited, List<TransferableOneWay> leftToLoad)
	{
		return FindBestMatchFor(deposited, deposited?.stackCount ?? 0, leftToLoad);
	}

	public static TransferableOneWay FindBestMatchFor(Thing deposited, int depositedCount, List<TransferableOneWay> leftToLoad)
	{
		if (deposited?.def == null || leftToLoad == null || leftToLoad.Count == 0)
		{
			return null;
		}
		return TransferableUtility.TransferableMatchingDesperate(deposited, leftToLoad, (TransferAsOneMode)1);
	}
}
