using System.Collections.Generic;
using Verse;

namespace HaulersDream;

internal static class SelfPickupClaims
{
	private static readonly Dictionary<Thing, Pawn> owners;

	static SelfPickupClaims()
	{
		owners = new Dictionary<Thing, Pawn>();
		CacheRegistry.Register(Clear);
	}

	internal static void Clear()
	{
		owners.Clear();
	}

	internal static bool Claim(Thing t, Pawn pawn)
	{
		if (t == null || pawn == null)
		{
			return false;
		}
		CompHauledToInventory comp = ((ThingWithComps)pawn).GetComp<CompHauledToInventory>();
		if (comp == null)
		{
			return false;
		}
		if (owners.TryGetValue(t, out var value))
		{
			if (value == pawn)
			{
				return true;
			}
			if (IsUsableOwner(value) && !IsCloser(pawn, value, t))
			{
				return false;
			}
			((ThingWithComps)value).GetComp<CompHauledToInventory>()?.pendingSelfPickups.Remove(t);
		}
		owners[t] = pawn;
		if (!comp.pendingSelfPickups.Contains(t))
		{
			comp.pendingSelfPickups.Add(t);
		}
		return true;
	}

	internal static void Release(Thing t, Pawn pawn)
	{
		if (t != null && owners.TryGetValue(t, out var value) && value == pawn)
		{
			owners.Remove(t);
		}
	}

	private static bool IsUsableOwner(Pawn owner)
	{
		if (owner != null && ((Thing)owner).Spawned)
		{
			return !owner.Dead;
		}
		return false;
	}

	private static bool IsCloser(Pawn a, Pawn b, Thing t)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 val = ((Thing)a).Position - t.Position;
		int lengthHorizontalSquared = ((IntVec3)(ref val)).LengthHorizontalSquared;
		val = ((Thing)b).Position - t.Position;
		return lengthHorizontalSquared < ((IntVec3)(ref val)).LengthHorizontalSquared;
	}
}
