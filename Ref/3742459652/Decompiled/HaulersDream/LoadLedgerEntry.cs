using System;
using System.Collections.Generic;
using HaulersDream.Core;
using Verse;

namespace HaulersDream;

public class LoadLedgerEntry : IExposable
{
	public class PawnClaimData : IExposable
	{
		public Pawn pawn;

		public Dictionary<ThingDef, int> claims = new Dictionary<ThingDef, int>();

		public PawnClaimData()
		{
		}

		public PawnClaimData(Pawn pawn, Dictionary<ThingDef, int> claims)
		{
			this.pawn = pawn;
			this.claims = claims;
		}

		public void ExposeData()
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Invalid comparison between Unknown and I4
			Scribe_References.Look<Pawn>(ref pawn, "pawn", false);
			Scribe_Collections.Look<ThingDef, int>(ref claims, "claims", (LookMode)4, (LookMode)1);
			if ((int)Scribe.mode == 2 && claims == null)
			{
				claims = new Dictionary<ThingDef, int>();
			}
		}
	}

	public Map map;

	public Dictionary<ThingDef, int> totalNeeded = new Dictionary<ThingDef, int>();

	public Dictionary<ThingDef, int> totalClaimed = new Dictionary<ThingDef, int>();

	public Dictionary<Pawn, Dictionary<ThingDef, int>> pawnClaims = new Dictionary<Pawn, Dictionary<ThingDef, int>>();

	private List<PawnClaimData> pawnClaimScribe;

	public bool IsInert
	{
		get
		{
			if (totalNeeded == null || totalNeeded.Count == 0)
			{
				if (totalClaimed != null)
				{
					return totalClaimed.Count == 0;
				}
				return true;
			}
			return false;
		}
	}

	public LoadLedgerEntry()
	{
	}

	public LoadLedgerEntry(Map map)
	{
		this.map = map;
	}

	public Dictionary<ThingDef, int> AvailableToClaim(Pawn asker)
	{
		return LoadLedger<ThingDef, Pawn>.AvailableToClaim(totalNeeded, totalClaimed, pawnClaims, asker);
	}

	public bool HasWork(Pawn asker)
	{
		return LoadLedger<ThingDef, Pawn>.HasWork(totalNeeded, totalClaimed, pawnClaims, asker);
	}

	public bool AnyClaimed()
	{
		return LoadLedger<ThingDef, Pawn>.AnyClaimed(totalClaimed);
	}

	public bool FullyClaimed()
	{
		return LoadLedger<ThingDef, Pawn>.FullyClaimed(totalNeeded, totalClaimed);
	}

	public void ApplyClaim(Pawn pawn, IReadOnlyDictionary<ThingDef, int> newPlan)
	{
		LoadLedger<ThingDef, Pawn>.ApplyClaim(totalClaimed, pawnClaims, pawn, newPlan);
	}

	public void Settle(Pawn pawn, ThingDef def, int deposited)
	{
		LoadLedger<ThingDef, Pawn>.Settle(totalNeeded, totalClaimed, pawnClaims, pawn, def, deposited);
	}

	public void Release(Pawn pawn)
	{
		LoadLedger<ThingDef, Pawn>.Release(totalClaimed, pawnClaims, pawn);
	}

	public void SetNeeded(Dictionary<ThingDef, int> needed)
	{
		totalNeeded = needed ?? new Dictionary<ThingDef, int>();
	}

	public void SetNeededFrom(Dictionary<ThingDef, int> source)
	{
		if (totalNeeded == null)
		{
			totalNeeded = new Dictionary<ThingDef, int>();
		}
		else
		{
			totalNeeded.Clear();
		}
		if (source == null)
		{
			return;
		}
		foreach (KeyValuePair<ThingDef, int> item in source)
		{
			totalNeeded[item.Key] = item.Value;
		}
	}

	public void ClearNeeded()
	{
		if (totalNeeded == null)
		{
			totalNeeded = new Dictionary<ThingDef, int>();
		}
		else
		{
			totalNeeded.Clear();
		}
	}

	public void ExposeData()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Invalid comparison between Unknown and I4
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Invalid comparison between Unknown and I4
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Invalid comparison between Unknown and I4
		Scribe_References.Look<Map>(ref map, "map", false);
		Scribe_Collections.Look<ThingDef, int>(ref totalNeeded, "totalNeeded", (LookMode)4, (LookMode)1);
		Scribe_Collections.Look<ThingDef, int>(ref totalClaimed, "totalClaimed", (LookMode)4, (LookMode)1);
		if ((int)Scribe.mode == 1)
		{
			pawnClaimScribe = new List<PawnClaimData>();
			foreach (KeyValuePair<Pawn, Dictionary<ThingDef, int>> pawnClaim in pawnClaims)
			{
				if (pawnClaim.Key != null && pawnClaim.Value != null && pawnClaim.Value.Count > 0)
				{
					pawnClaimScribe.Add(new PawnClaimData(pawnClaim.Key, pawnClaim.Value));
				}
			}
		}
		Scribe_Collections.Look<PawnClaimData>(ref pawnClaimScribe, "pawnClaims", (LookMode)2, Array.Empty<object>());
		if ((int)Scribe.mode == 2)
		{
			if (totalNeeded == null)
			{
				totalNeeded = new Dictionary<ThingDef, int>();
			}
			if (totalClaimed == null)
			{
				totalClaimed = new Dictionary<ThingDef, int>();
			}
		}
		if ((int)Scribe.mode != 4)
		{
			return;
		}
		pawnClaims = new Dictionary<Pawn, Dictionary<ThingDef, int>>();
		if (pawnClaimScribe != null)
		{
			foreach (PawnClaimData item in pawnClaimScribe)
			{
				if (item?.pawn != null && item.claims != null && item.claims.Count > 0)
				{
					pawnClaims[item.pawn] = item.claims;
				}
			}
		}
		pawnClaimScribe = null;
		totalClaimed = LoadLedger<ThingDef, Pawn>.RecomputeClaimed(pawnClaims);
	}
}
