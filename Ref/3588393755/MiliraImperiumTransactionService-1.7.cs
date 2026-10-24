using System;
using System.Collections.Generic;
using AriandelLibrary;
using Milira;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MiliraImperium;

public static class MiliraImperiumTransactionService
{
	public enum ResourceKind
	{
		Silver,
		Gold,
		SplendidSteel,
		SunPlateSteel,
		SolarCrystal,
		SunLightFuel,
		FeatherThread,
		DarkMatter,
		ImperiumToken
	}

	public enum ExchangeKind
	{
		GargantuaCore,
		PsionicCore,
		HealerMechSerum,
		ResurrectorMechSerum
	}

	public readonly struct ResourceSnapshot(int capturedAtTick, int silver, int gold, int splendidSteel, int sunPlateSteel, int solarCrystal, int sunLightFuel, int featherThread, int darkMatter, int imperiumToken)
	{
		public int CapturedAtTick { get; } = capturedAtTick;

		public int Silver { get; } = silver;

		public int Gold { get; } = gold;

		public int SplendidSteel { get; } = splendidSteel;

		public int SunPlateSteel { get; } = sunPlateSteel;

		public int SolarCrystal { get; } = solarCrystal;

		public int SunLightFuel { get; } = sunLightFuel;

		public int FeatherThread { get; } = featherThread;

		public int DarkMatter { get; } = darkMatter;

		public int ImperiumToken { get; } = imperiumToken;

		public int GetAvailable(ResourceKind kind)
		{
			return kind switch
			{
				ResourceKind.Silver => Silver, 
				ResourceKind.Gold => Gold, 
				ResourceKind.SplendidSteel => SplendidSteel, 
				ResourceKind.SunPlateSteel => SunPlateSteel, 
				ResourceKind.SolarCrystal => SolarCrystal, 
				ResourceKind.SunLightFuel => SunLightFuel, 
				ResourceKind.FeatherThread => FeatherThread, 
				ResourceKind.DarkMatter => DarkMatter, 
				ResourceKind.ImperiumToken => ImperiumToken, 
				_ => 0, 
			};
		}
	}

	public readonly struct Availability
	{
		public bool Allowed { get; }

		public string Reason { get; }

		private Availability(bool allowed, string reason)
		{
			Allowed = allowed;
			Reason = reason;
		}

		public static Availability Available()
		{
			return new Availability(allowed: true, null);
		}

		public static Availability Unavailable(string reason)
		{
			return new Availability(allowed: false, reason);
		}
	}

	public readonly struct TransactionResult
	{
		public bool Succeeded { get; }

		public string Message { get; }

		public int RewardCount { get; }

		public Pawn RecruitedPawn { get; }

		private TransactionResult(bool succeeded, string message, int rewardCount, Pawn recruitedPawn)
		{
			Succeeded = succeeded;
			Message = message;
			RewardCount = rewardCount;
			RecruitedPawn = recruitedPawn;
		}

		public static TransactionResult Success(int rewardCount = 0, Pawn recruitedPawn = null)
		{
			return new TransactionResult(succeeded: true, null, rewardCount, recruitedPawn);
		}

		public static TransactionResult Failure(string message)
		{
			return new TransactionResult(succeeded: false, message, 0, null);
		}
	}

	public const int ExchangeTokenCost = 1;

	private const int ProceedCooldownTicks = 1800000;

	public const int RecruitTokenCost = 1;

	public static ResourceSnapshot CaptureResourceSnapshot(Map map)
	{
		if (map == null)
		{
			return default(ResourceSnapshot);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		foreach (Thing item in TradeUtility.AllLaunchableThingsForTrade(map, (ITrader)null))
		{
			if (item != null && item.stackCount > 0)
			{
				ThingDef def = item.def;
				int stackCount = item.stackCount;
				if (def == ThingDefOf.Silver)
				{
					num += stackCount;
				}
				else if (def == ThingDefOf.Gold)
				{
					num2 += stackCount;
				}
				else if (def == MiliraDefOf.Milira_SplendidSteel)
				{
					num3 += stackCount;
				}
				else if (def == MiliraDefOf.Milira_SunPlateSteel)
				{
					num4 += stackCount;
				}
				else if (def == MiliraImperiumDefOf.Milira_SolarCrystal)
				{
					num5 += stackCount;
				}
				else if (def == MiliraDefOf.Milira_SunLightFuel)
				{
					num6 += stackCount;
				}
				else if (def == MiliraDefOf.Milira_FeatherThread)
				{
					num7 += stackCount;
				}
				else if (def == MiliraImperiumDefOf.Milira_DarkMatter)
				{
					num8 += stackCount;
				}
				else if (def == MiliraImperiumDefOf.Milira_Feather_gold)
				{
					num9 += stackCount;
				}
			}
		}
		int capturedAtTick = ((Find.TickManager != null) ? Find.TickManager.TicksGame : (-1));
		return new ResourceSnapshot(capturedAtTick, num, num2, num3, num4, num5, num6, num7, num8, num9);
	}

	public static ThingDef GetResourceDef(ResourceKind kind)
	{
		return (ThingDef)(kind switch
		{
			ResourceKind.Silver => ThingDefOf.Silver, 
			ResourceKind.Gold => ThingDefOf.Gold, 
			ResourceKind.SplendidSteel => MiliraDefOf.Milira_SplendidSteel, 
			ResourceKind.SunPlateSteel => MiliraDefOf.Milira_SunPlateSteel, 
			ResourceKind.SolarCrystal => MiliraImperiumDefOf.Milira_SolarCrystal, 
			ResourceKind.SunLightFuel => MiliraDefOf.Milira_SunLightFuel, 
			ResourceKind.FeatherThread => MiliraDefOf.Milira_FeatherThread, 
			ResourceKind.DarkMatter => MiliraImperiumDefOf.Milira_DarkMatter, 
			ResourceKind.ImperiumToken => MiliraImperiumDefOf.Milira_Feather_gold, 
			_ => null, 
		});
	}

	public static string GetInsufficientResourceReason(ResourceKind kind, int requiredCount)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		return kind switch
		{
			ResourceKind.Silver => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedSilverLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.Gold => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedGoldLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.SplendidSteel => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedMilira_SplendidSteelLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.SunPlateSteel => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedMilira_SunPlateSteelLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.SolarCrystal => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedMilira_SolarCrystalLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.SunLightFuel => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedMilira_SunLightFuelLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.FeatherThread => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedMilira_FeatherThreadLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.DarkMatter => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedMilira_DarkMatterLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			ResourceKind.ImperiumToken => TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("NeedTokenLaunchable", NamedArgument.op_Implicit(requiredCount))), 
			_ => UnavailableText(), 
		};
	}

	public static void NotifyFailure(TransactionResult result)
	{
		if (!result.Succeeded)
		{
			Messages.Message(GenText.NullOrEmpty(result.Message) ? UnavailableText() : result.Message, MessageTypeDefOf.RejectInput, false);
		}
	}

	internal static Availability ValidateCommonContext(Map map, Faction faction, bool requireAlly)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if (map == null || faction == null || faction.temporary || faction.def != MiliraImperiumDefOf.Milira_Imperium)
		{
			return Availability.Unavailable(UnavailableText());
		}
		RelationUtil.EnsureEdgeWithPlayer(faction);
		if (requireAlly && (int)faction.RelationKindWith(Faction.OfPlayer) != 2)
		{
			return Availability.Unavailable(TaggedString.op_Implicit(Translator.Translate("Milira_RequireAllyForTribute")));
		}
		return Availability.Available();
	}

	internal static bool HasLaunchableResource(Map map, ThingDef resourceDef, int requiredCount)
	{
		if (map == null || resourceDef == null || requiredCount <= 0)
		{
			return requiredCount <= 0;
		}
		int num = 0;
		foreach (Thing item in TradeUtility.AllLaunchableThingsForTrade(map, (ITrader)null))
		{
			if (item != null && item.def == resourceDef)
			{
				num += item.stackCount;
				if (num >= requiredCount)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static TransactionResult TryConsumeResource(Map map, ResourceKind resourceKind, int count)
	{
		ThingDef resourceDef = GetResourceDef(resourceKind);
		if (map == null || resourceDef == null || count <= 0)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		if (!HasLaunchableResource(map, resourceDef, count))
		{
			return TransactionResult.Failure(GetInsufficientResourceReason(resourceKind, count));
		}
		try
		{
			TradeUtility.LaunchThingsOfType(resourceDef, count, map, (TradeShip)null);
			return TransactionResult.Success();
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Failed to consume transaction resource: " + ex);
			return TransactionResult.Failure(UnavailableText());
		}
	}

	internal static string UnavailableText()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("MI_Console_TransactionUnavailable"));
	}

	public static int GetDonationFavorReward(ResourceKind resourceKind, int resourceCount)
	{
		if (resourceCount <= 0)
		{
			return 0;
		}
		return resourceKind switch
		{
			ResourceKind.Silver => resourceCount / 1000, 
			ResourceKind.Gold => resourceCount / 100, 
			ResourceKind.SplendidSteel => resourceCount / 420, 
			ResourceKind.SunPlateSteel => resourceCount / 60, 
			ResourceKind.SolarCrystal => resourceCount / 40, 
			ResourceKind.SunLightFuel => resourceCount / 125, 
			ResourceKind.FeatherThread => resourceCount / 400, 
			ResourceKind.DarkMatter => resourceCount / 2, 
			_ => 0, 
		};
	}

	public static TransactionResult TryDonate(Map map, Pawn negotiator, Faction faction, ResourceKind resourceKind, int resourceCount)
	{
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		Availability availability = ValidateCommonContext(map, faction, requireAlly: true);
		if (!availability.Allowed)
		{
			return TransactionResult.Failure(availability.Reason);
		}
		if (negotiator == null || ((Thing)negotiator).Destroyed || negotiator.royalty == null)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		int donationFavorReward = GetDonationFavorReward(resourceKind, resourceCount);
		if (donationFavorReward <= 0)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		TransactionResult result = TryConsumeResource(map, resourceKind, resourceCount);
		if (!result.Succeeded)
		{
			return result;
		}
		try
		{
			negotiator.royalty.GainFavor(faction, donationFavorReward);
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Donation payment succeeded but favor grant failed: " + ex);
			return TransactionResult.Failure(UnavailableText());
		}
		Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("MiliraImperiumTributeRoyaltyFavorGain", NamedArgumentUtility.Named((object)negotiator, "NAME"), NamedArgument.op_Implicit(donationFavorReward))), MessageTypeDefOf.PositiveEvent, true);
		return TransactionResult.Success(donationFavorReward);
	}

	public static ThingDef GetExchangeRewardDef(ExchangeKind exchangeKind)
	{
		return (ThingDef)(exchangeKind switch
		{
			ExchangeKind.GargantuaCore => MiliraImperiumDefOf.Milira_GargantuaCore, 
			ExchangeKind.PsionicCore => MiliraImperiumDefOf.Milira_PsionicCore, 
			ExchangeKind.HealerMechSerum => MiliraImperiumDefOf.MechSerumHealer, 
			ExchangeKind.ResurrectorMechSerum => MiliraImperiumDefOf.MechSerumResurrector, 
			_ => null, 
		});
	}

	public static int GetExchangeRewardCount(ExchangeKind exchangeKind)
	{
		return (exchangeKind != ExchangeKind.HealerMechSerum) ? 1 : 2;
	}

	public static TransactionResult TryExchange(Map map, Pawn negotiator, Faction faction, ExchangeKind exchangeKind)
	{
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		Availability availability = ValidateCommonContext(map, faction, requireAlly: true);
		if (!availability.Allowed)
		{
			return TransactionResult.Failure(availability.Reason);
		}
		if (negotiator == null || ((Thing)negotiator).Destroyed)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		ThingDef exchangeRewardDef = GetExchangeRewardDef(exchangeKind);
		int exchangeRewardCount = GetExchangeRewardCount(exchangeKind);
		if (exchangeRewardDef == null || exchangeRewardCount <= 0)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		List<Thing> list;
		IntVec3 val;
		try
		{
			list = MakeRewardThings(exchangeRewardDef, exchangeRewardCount);
			val = DropCellFinder.TradeDropSpot(map);
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Failed to prepare an Imperium exchange: " + ex);
			return TransactionResult.Failure(UnavailableText());
		}
		TransactionResult result = TryConsumeResource(map, ResourceKind.ImperiumToken, 1);
		if (!result.Succeeded)
		{
			return result;
		}
		try
		{
			DropPodUtility.DropThingsNear(val, map, (IEnumerable<Thing>)list, 110, false, false, false, false, true, faction);
		}
		catch (Exception ex2)
		{
			Log.Error("[MiliraImperium] Exchange drop pod failed; attempting direct placement: " + ex2);
			if (!TryPlacePreparedRewards(list, val, map))
			{
				return TransactionResult.Failure(UnavailableText());
			}
		}
		return TransactionResult.Success(exchangeRewardCount);
	}

	private static List<Thing> MakeRewardThings(ThingDef rewardDef, int totalCount)
	{
		List<Thing> list = new List<Thing>();
		int num = totalCount;
		while (num > 0)
		{
			Thing val = ThingMaker.MakeThing(rewardDef, (ThingDef)null);
			int val2 = ((val.def.stackLimit > 0) ? val.def.stackLimit : num);
			num -= (val.stackCount = Math.Min(num, val2));
			list.Add(val);
		}
		return list;
	}

	private static bool TryPlacePreparedRewards(List<Thing> rewards, IntVec3 dropSpot, Map map)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		bool result = true;
		for (int i = 0; i < rewards.Count; i++)
		{
			Thing val = rewards[i];
			if (val == null || val.Destroyed || val.SpawnedOrAnyParentSpawned)
			{
				continue;
			}
			try
			{
				if (!GenPlace.TryPlaceThing(val, dropSpot, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
				{
					result = false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("[MiliraImperium] Failed to place a prepared exchange reward: " + ex);
				result = false;
			}
		}
		return result;
	}

	public static Availability GetProceedAvailability(Map map, Faction faction)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Invalid comparison between Unknown and I4
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		Availability result = ValidateCommonContext(map, faction, requireAlly: false);
		if (!result.Allowed)
		{
			return result;
		}
		MiliraImperiumCampaignState miliraImperiumCampaignState = MiliraImperium_GameComponent.Instance?.Campaign;
		if (miliraImperiumCampaignState == null)
		{
			return Availability.Unavailable(UnavailableText());
		}
		if (miliraImperiumCampaignState.IsProceedCoolingDown)
		{
			return Availability.Unavailable(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("MI_ReapplyInDays", NamedArgument.op_Implicit(((float)miliraImperiumCampaignState.ProceedTicksRemaining / 60000f).ToString("0.0")))));
		}
		if (!MapHasEligibleColonist(map, faction))
		{
			return Availability.Unavailable(TaggedString.op_Implicit(Translator.Translate("MI_NoEligibleColonist")));
		}
		if ((int)faction.RelationKindWith(Faction.OfPlayer) != 2)
		{
			return Availability.Unavailable(TaggedString.op_Implicit(Translator.Translate("MI_RequireImperiumAlly")));
		}
		return Availability.Available();
	}

	public static TransactionResult TryProceedToImperium(Map map, Faction faction)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		Availability proceedAvailability = GetProceedAvailability(map, faction);
		if (!proceedAvailability.Allowed)
		{
			return TransactionResult.Failure(proceedAvailability.Reason);
		}
		MiliraImperiumCampaignState miliraImperiumCampaignState = MiliraImperium_GameComponent.Instance?.Campaign;
		if (miliraImperiumCampaignState == null || Find.Storyteller == null || Find.TickManager == null)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		try
		{
			IncidentParms val = new IncidentParms
			{
				target = (IIncidentTarget)(object)map
			};
			Find.Storyteller.incidentQueue.Add(MiliraImperiumDefOf.Milira_ImperiumEndGame, Find.TickManager.TicksGame, val, 0);
			miliraImperiumCampaignState.StartProceedCooldown(1800000);
			return TransactionResult.Success();
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Failed to queue the Imperium end-game incident: " + ex);
			return TransactionResult.Failure(UnavailableText());
		}
	}

	private static bool MapHasEligibleColonist(Map map, Faction faction)
	{
		if (map == null || faction == null)
		{
			return false;
		}
		List<Pawn> freeColonistsSpawned = map.mapPawns.FreeColonistsSpawned;
		for (int i = 0; i < freeColonistsSpawned.Count; i++)
		{
			Pawn val = freeColonistsSpawned[i];
			if (val != null && ((Thing)val).Faction == Faction.OfPlayer && PawnHasEitherTitle(val, faction, MiliraImperiumDefOf.Milira_ImperiumTitleIX, MiliraImperiumDefOf.Milira_ImperiumTitleX))
			{
				return true;
			}
		}
		return false;
	}

	private static bool PawnHasEitherTitle(Pawn pawn, Faction faction, RoyalTitleDef first, RoyalTitleDef second)
	{
		if (pawn?.royalty == null)
		{
			return false;
		}
		List<RoyalTitle> allTitlesForReading = pawn.royalty.AllTitlesForReading;
		for (int i = 0; i < allTitlesForReading.Count; i++)
		{
			RoyalTitle val = allTitlesForReading[i];
			if (val != null && val.faction == faction && (val.def == first || val.def == second))
			{
				return true;
			}
		}
		return false;
	}

	public static TransactionResult TryRecruit(Map map, Pawn negotiator, Faction faction)
	{
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		Availability availability = ValidateCommonContext(map, faction, requireAlly: true);
		if (!availability.Allowed)
		{
			return TransactionResult.Failure(availability.Reason);
		}
		if (negotiator == null || ((Thing)negotiator).Destroyed)
		{
			return TransactionResult.Failure(UnavailableText());
		}
		ThingDef resourceDef = GetResourceDef(ResourceKind.ImperiumToken);
		if (!HasLaunchableResource(map, resourceDef, 1))
		{
			return TransactionResult.Failure(GetInsufficientResourceReason(ResourceKind.ImperiumToken, 1));
		}
		PawnKindDef val = PickRecruitKind();
		if (val == null)
		{
			Log.Error("[MiliraImperium] No valid PawnKindDef was available for Imperium recruitment.");
			return TransactionResult.Failure(UnavailableText());
		}
		IntVec3 val2;
		Pawn val3;
		try
		{
			val2 = DropCellFinder.TradeDropSpot(map);
			val3 = GenerateRecruit(val, faction);
			if (val3 == null)
			{
				return TransactionResult.Failure(UnavailableText());
			}
			Hediff val4 = HediffMaker.MakeHediff(MiliraImperiumDefOf.Hediff_ImperialJoinDelay, val3, (BodyPartRecord)null);
			val3.health.AddHediff(val4, (BodyPartRecord)null, (DamageInfo?)null, (DamageResult)null);
			HediffUtility.TryGetComp<HediffComp_JoinAfterDelay>(val4)?.Start(Faction.OfPlayer, 1);
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Failed to prepare an Imperium recruit: " + ex);
			return TransactionResult.Failure(UnavailableText());
		}
		TransactionResult result = TryConsumeResource(map, ResourceKind.ImperiumToken, 1);
		if (!result.Succeeded)
		{
			DiscardUnspawnedPawn(val3);
			return result;
		}
		try
		{
			DropPodUtility.DropThingsNear(val2, map, (IEnumerable<Thing>)new List<Thing>(1) { (Thing)(object)val3 }, 110, false, false, false, false, true, faction);
		}
		catch (Exception ex2)
		{
			Log.Error("[MiliraImperium] Recruit drop pod failed; attempting direct placement: " + ex2);
			if (!((Thing)val3).Destroyed && !((Thing)val3).SpawnedOrAnyParentSpawned)
			{
				try
				{
					GenSpawn.Spawn((Thing)(object)val3, val2, map, (WipeMode)0);
				}
				catch (Exception ex3)
				{
					Log.Error("[MiliraImperium] Failed to place the paid Imperium recruit: " + ex3);
					return TransactionResult.Failure(UnavailableText());
				}
			}
		}
		TryMarkRecruitConsumed(val);
		RegisterSpecialPawnIfNeeded(val, val3);
		Find.LetterStack.ReceiveLetter(Translator.Translate("MiliraImperium_RecruitPawn_Label"), TranslatorFormattedStringExtensions.Translate("MiliraImperium_RecruitPawn_Text", NamedArgument.op_Implicit(((Entity)val3).LabelShortCap), NamedArgumentUtility.Named((object)negotiator, "NAME")), LetterDefOf.PositiveEvent, LookTargets.op_Implicit(new TargetInfo(val2, map, false)), faction, (Quest)null, (List<ThingDef>)null, (string)null, 0, true);
		return TransactionResult.Success(1, val3);
	}

	private static Pawn GenerateRecruit(PawnKindDef pawnKind, Faction faction)
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		FloatRange value = default(FloatRange);
		((FloatRange)(ref value))..ctor(80f, 300f);
		string defName = ((Def)pawnKind).defName;
		if (defName == "Axolotl_YueJian_OC" || defName == "Axolotl_Luoyan" || defName == "Kiiro_Cecilia_OC")
		{
			((FloatRange)(ref value))..ctor(16f, 120f);
		}
		FloatRange? val = value;
		PawnGenerationRequest val2 = new PawnGenerationRequest(pawnKind, faction, (PawnGenerationContext)2, (PlanetTile?)null, true, false, false, false, false, 0f, false, true, false, true, true, false, false, false, false, 0f, 0f, (Pawn)null, 1f, (Predicate<Pawn>)null, (Predicate<Pawn>)null, (IEnumerable<TraitDef>)null, (IEnumerable<TraitDef>)null, (float?)null, (float?)null, (float?)null, (Gender?)null, (string)null, (string)null, (RoyalTitleDef)null, (Ideo)null, false, false, false, false, (List<GeneDef>)null, (List<GeneDef>)null, (XenotypeDef)null, (CustomXenotype)null, (List<XenotypeDef>)null, 0f, (DevelopmentalStage)8, (Func<XenotypeDef, PawnKindDef>)null, (FloatRange?)null, val, false, false, false, -1, 0, false);
		return AriandelLibrary_Utility_SpecialPawn.GeneratePawnWithAuthorization(val2, "Milira_RecruitPawnDoTokenMiliraImperium");
	}

	private static PawnKindDef PickRecruitKind()
	{
		List<(PawnKindDef, float)> list = new List<(PawnKindDef, float)>();
		MiliraRecruitUnique.AppendUniqueKinds(list);
		if (list.Count > 0)
		{
			float num = 0f;
			for (int i = 0; i < list.Count; i++)
			{
				num += list[i].Item2;
			}
			float num2 = Rand.Range(0f, num);
			float num3 = 0f;
			for (int j = 0; j < list.Count; j++)
			{
				num3 += list[j].Item2;
				if (num2 <= num3)
				{
					return list[j].Item1;
				}
			}
			return list[list.Count - 1].Item1;
		}
		float value = Rand.Value;
		if (value < 0.85f)
		{
			return MiliraImperiumDefOf.Milira_ImperialCitizenRE;
		}
		if (value < 0.9f)
		{
			return MiliraImperiumDefOf.Milira_SpaceMarineRE;
		}
		if (value < 0.95f)
		{
			return MiliraImperiumDefOf.Milira_OldHunterRE;
		}
		return MiliraImperiumDefOf.Milira_ShadowGuardRE;
	}

	private static void RegisterSpecialPawnIfNeeded(PawnKindDef pawnKind, Pawn pawn)
	{
		string staticID = SpecialPawnRegistry.GetStaticID(pawnKind);
		if (GenText.NullOrEmpty(staticID))
		{
			return;
		}
		AriandelLibrary_GameComponent instance = AriandelLibrary_GameComponent.Instance;
		SpecialPawnManager val = ((instance != null) ? instance.SpecialPawns : null);
		if (val == null)
		{
			Log.Warning("[MiliraImperium] AL SpecialPawns service was unavailable while registering " + staticID + ".");
			return;
		}
		try
		{
			val.RegisterSpecialPawn(staticID, pawn);
			if (Prefs.DevMode)
			{
				Log.Message("[MiliraImperium] Registered recruited special pawn " + ((object)pawn.Name)?.ToString() + " with StaticID " + staticID + ".");
			}
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Failed to register a delivered special recruit: " + ex);
		}
	}

	private static void TryMarkRecruitConsumed(PawnKindDef pawnKind)
	{
		try
		{
			MiliraRecruitUnique.TryMarkConsumed(pawnKind);
		}
		catch (Exception ex)
		{
			Log.Error("[MiliraImperium] Failed to persist the unique recruit consumption flag: " + ex);
		}
	}

	private static void DiscardUnspawnedPawn(Pawn pawn)
	{
		if (pawn == null || ((Thing)pawn).Destroyed || ((Thing)pawn).SpawnedOrAnyParentSpawned)
		{
			return;
		}
		try
		{
			((Thing)pawn).Destroy((DestroyMode)0);
		}
		catch (Exception ex)
		{
			Log.Warning("[MiliraImperium] Failed to discard an unpaid generated recruit: " + ex);
		}
	}
}
