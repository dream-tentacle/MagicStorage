using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using WeaponStorage.UI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("0.0.0.0")]
[module: UnverifiableCode]
[module: RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace WeaponStorageUtil
{
	internal class WeaponStorageUtil
	{
		private static Assembly wsAssembly;

		private static bool initialized;

		public static bool Exists
		{
			get
			{
				if (initialized)
				{
					return wsAssembly != null;
				}
				foreach (ModContentPack runningMod in LoadedModManager.RunningMods)
				{
					foreach (Assembly loadedAssembly in runningMod.assemblies.loadedAssemblies)
					{
						if (loadedAssembly.GetName().Name.Equals("WeaponStorage") && !(loadedAssembly.GetType("WeaponStorage.WorldComp") == null))
						{
							initialized = true;
							wsAssembly = loadedAssembly;
							break;
						}
					}
					if (initialized)
					{
						break;
					}
				}
				initialized = true;
				return wsAssembly != null;
			}
		}

		public static bool TryEquipType(Pawn p, ThingDef def)
		{
			if (!Exists)
			{
				return false;
			}
			try
			{
				if (wsAssembly.GetType("WeaponStorage.WorldComp").GetField("AssignedWeapons", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is IDictionary dictionary)
				{
					object obj = dictionary[p];
					if (obj?.GetType().GetField("Weapons", BindingFlags.Instance | BindingFlags.Public)?.GetValue(obj) is List<ThingWithComps> list)
					{
						foreach (ThingWithComps item in list)
						{
							if (((Thing)item).def == def)
							{
								wsAssembly.GetType("WeaponStorage.HarmonyPatchUtil").GetMethod("EquipWeapon", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new object[3] { item, p, obj });
								return true;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning(ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			return false;
		}
	}
}
namespace WeaponStorage
{
	[HarmonyPatch(typeof(CaravanExitMapUtility), "ExitMapAndCreateCaravan", new Type[]
	{
		typeof(IEnumerable<Pawn>),
		typeof(Faction),
		typeof(PlanetTile),
		typeof(PlanetTile),
		typeof(PlanetTile),
		typeof(bool)
	})]
	internal static class CaravanExitMapUtility_ExitMapAndCreateCaravan
	{
		[HarmonyPriority(800)]
		private static void Prefix(IEnumerable<Pawn> pawns, Faction faction)
		{
			if (faction != Faction.OfPlayer || new List<Pawn>(pawns).Count <= 0)
			{
				return;
			}
			foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(null))
			{
				weaponStorage.ReclaimWeapons();
			}
		}
	}
	[HarmonyPatch(typeof(CaravanFormingUtility), "StopFormingCaravan")]
	internal static class CaravanFormingUtility_StopFormingCaravan
	{
		[HarmonyPriority(800)]
		private static void Postfix(Lord lord)
		{
			foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(lord.Map))
			{
				weaponStorage.ReclaimWeapons();
			}
		}
	}
	[HarmonyPatch(typeof(Dialog_FormCaravan), "PostOpen")]
	internal static class Dialog_FormCaravan_PostOpen
	{
		private static void Prefix(Window __instance, Map ___map)
		{
			if (!(((object)__instance).GetType() == typeof(Dialog_FormCaravan)))
			{
				return;
			}
			foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(___map))
			{
				weaponStorage.Empty();
			}
		}
	}
	[HarmonyPatch(typeof(Dialog_Trade), "Close")]
	internal static class Dialog_Trade_Close
	{
		private static void Postfix()
		{
			TradeUtil.ReclaimWeapons();
		}
	}
	[HarmonyPatch(typeof(MoveColonyUtility), "MoveColonyAndReset")]
	internal static class MoveColonyUtility_MoveColonyAndReset
	{
		[HarmonyPriority(800)]
		private static void Prefix()
		{
			WorldComp.ClearAll();
		}
	}
	[HarmonyPatch(/*Could not decode attribute arguments.*/)]
	internal static class Pawn_DraftController_Drafted
	{
		private static void Postfix(Pawn_DraftController __instance)
		{
			Pawn pawn = __instance.pawn;
			if (WorldComp.TryGetAssignedWeapons(pawn, out var aw) && aw.TryGetLastThingUsed(pawn, out var t))
			{
				HarmonyPatchUtil.EquipWeapon(t, pawn, aw);
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_DraftController), "GetGizmos")]
	public static class Pawn_DraftController_GetGizmos
	{
		[HarmonyPriority(600)]
		private static void Postfix(Pawn_DraftController __instance, ref IEnumerable<Gizmo> __result)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Expected O, but got Unknown
			try
			{
				Pawn pawn = __instance.pawn;
				if (((Thing)pawn).Faction != Faction.OfPlayer)
				{
					return;
				}
				List<Gizmo> list = null;
				if (Settings.ShowWeaponStorageButtonForPawns && WorldComp.HasStorages())
				{
					list = new List<Gizmo>(1) { (Gizmo)new Command_Action
					{
						icon = (Texture)(object)AssignUI.weaponStorageTexture,
						defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.UseWeaponStorage")),
						activateSound = SoundDef.Named("Click"),
						action = delegate
						{
							Find.WindowStack.Add((Window)(object)new AssignUI(null, pawn));
						}
					} };
					list.AddRange(__result);
				}
				if (WorldComp.TryGetAssignedWeapons(__instance.pawn, out var weapons))
				{
					if (list == null)
					{
						list = new List<Gizmo>();
						list.AddRange(__result);
					}
					foreach (ThingWithComps weapon in weapons.Weapons)
					{
						bool flag = Settings.IsTool((Thing)(object)weapon);
						bool flag2 = false;
						if (pawn.Drafted)
						{
							flag2 = true;
						}
						else if (flag || Settings.ShowWeaponsWhenNotDrafted)
						{
							flag2 = true;
						}
						if (flag2)
						{
							flag2 = pawn.equipment.Primary != weapon;
						}
						if (flag2)
						{
							list.Add((Gizmo)(object)CreateEquipWeaponGizmo(((Thing)weapon).def, delegate
							{
								HarmonyPatchUtil.EquipWeapon(weapon, pawn, weapons);
								weapons.SetLastThingUsed(pawn, weapon, isForceMelee: false);
							}));
						}
					}
				}
				foreach (SharedWeaponFilter f in WorldComp.SharedWeaponFilter)
				{
					if (list == null)
					{
						list = new List<Gizmo>();
						list.AddRange(__result);
					}
					f.UpdateFoundDefCache();
					if (!f.AssignedPawns.Contains(pawn))
					{
						continue;
					}
					foreach (ThingDef d in f.AllowedDefs)
					{
						if (d == ((Thing)(pawn.equipment.Primary?)).def || !f.FoundDefCacheContains(d))
						{
							continue;
						}
						list.Add((Gizmo)(object)CreateEquipWeaponGizmo(d, delegate
						{
							if (WorldComp.TryRemoveWeapon(d, f, includeBioencoded: false, out var weapon2))
							{
								HarmonyPatchUtil.EquipWeapon(weapon2, pawn);
								f.UpdateDefCache(d);
							}
						}, "WeaponStorage.EquipShared"));
					}
				}
				if (list != null)
				{
					__result = list;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("Exception while getting gizmos for pawn " + __instance.pawn.Name.ToStringShort + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace, (__instance.pawn.Name.ToStringFull + "WSGIZMO").GetHashCode());
			}
		}

		private static Command_Action CreateEquipWeaponGizmo(ThingDef def, Action equipWeaponAction, string label = "WeaponStorage.Equip")
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			Command_Action val = new Command_Action();
			if ((Object)(object)((BuildableDef)def).uiIcon != (Object)null)
			{
				((Command)val).icon = (Texture)(object)((BuildableDef)def).uiIcon;
			}
			else if (def.graphicData.texPath != null)
			{
				((Command)val).icon = (Texture)(object)ContentFinder<Texture2D>.Get(def.graphicData.texPath, true);
			}
			else
			{
				((Command)val).icon = null;
			}
			StringBuilder stringBuilder = new StringBuilder(TaggedString.op_Implicit(Translator.Translate(label)));
			stringBuilder.Append(" ");
			stringBuilder.Append(((Def)def).label);
			((Command)val).defaultLabel = stringBuilder.ToString();
			((Command)val).defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.EquipDesc"));
			((Command)val).activateSound = SoundDef.Named("Click");
			((Command)val).groupKey = (label + (object)def).GetHashCode();
			val.action = equipWeaponAction;
			return val;
		}
	}
	[HarmonyPatch(typeof(Pawn_EquipmentTracker), "AddEquipment")]
	internal static class Pawn_EquipmentTracker_AddEquipment
	{
		[HarmonyPriority(800)]
		private static void Prefix(Pawn_EquipmentTracker __instance)
		{
			if (__instance.Primary != null)
			{
				__instance.Remove(__instance.Primary);
			}
		}

		private static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps newEq)
		{
			if (((Thing)__instance.pawn).Faction == Faction.OfPlayerSilentFail && WorldComp.HasStorages() && WorldComp.CreateOrGetAssignedWeapons(__instance.pawn, out var aw))
			{
				aw.Add(newEq);
			}
		}
	}
	[HarmonyPatch(/*Could not decode attribute arguments.*/)]
	internal static class Pawn_EquipmentTracker_MakeRoomFor
	{
		[HarmonyPriority(800)]
		private static void Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			if ((int)((Thing)eq).def.equipmentType == 1 && __instance.Primary != null && WorldComp.TryGetAssignedWeapons(__instance.pawn, out var aw) && aw.Contains(eq))
			{
				__instance.Remove(eq);
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_EquipmentTracker), "TryDropEquipment")]
	internal static class Pawn_EquipmentTracker_TryDropEquipment
	{
		[HarmonyPriority(800)]
		private static void Prefix(ref Pawn __state, ThingWithComps eq)
		{
			if (!((Thing)eq).def.IsWeapon)
			{
				return;
			}
			ThingOwner holdingOwner = ((Thing)eq).holdingOwner;
			IThingHolder obj = ((holdingOwner != null) ? holdingOwner.Owner : null);
			Pawn_EquipmentTracker val = (Pawn_EquipmentTracker)(object)((obj is Pawn_EquipmentTracker) ? obj : null);
			if (val == null)
			{
				return;
			}
			Pawn pawn = val.pawn;
			if (pawn != null)
			{
				Faction faction = ((Thing)pawn).Faction;
				if (faction != null && faction.IsPlayer)
				{
					__state = val.pawn;
				}
			}
		}

		[HarmonyPriority(800)]
		private static void Postfix(ref Pawn __state, ThingWithComps eq)
		{
			if (__state == null)
			{
				return;
			}
			if (WorldComp.TryGetAssignedWeapons(__state, out var aw) && aw.Contains(eq))
			{
				if (!Settings.AllowPawnsToDropWeapon)
				{
					if (!WorldComp.Add(eq))
					{
						Log.Warning("unable to find weapon storage that can hold " + ((Thing)eq).ThingID + " so it will be dropped.");
						WorldComp.Drop(eq);
					}
					return;
				}
				if (aw.Remove(eq))
				{
					if (((Thing)eq).def.IsRangedWeapon)
					{
						if (!HarmonyPatchUtil.EquipRanged(aw))
						{
							HarmonyPatchUtil.EquipMelee(aw);
						}
					}
					else if (!HarmonyPatchUtil.EquipMelee(aw))
					{
						HarmonyPatchUtil.EquipRanged(aw);
					}
				}
				if (Settings.PlaceDroppedWeaponsInStorage && !WorldComp.Add(eq))
				{
					Log.Warning("unable to find weapon storage that can hold " + ((Thing)eq).ThingID + " so it will be dropped.");
					WorldComp.Drop(eq);
				}
				return;
			}
			foreach (SharedWeaponFilter item in WorldComp.SharedWeaponFilter)
			{
				if (item.Allows(eq) && !WorldComp.Add(eq))
				{
					Log.Warning("unable to find weapon storage that can hold " + ((Thing)eq).ThingID + " so it will be dropped.");
					WorldComp.Drop(eq);
					break;
				}
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
	internal static class Pawn_HealthTracker_MakeDowned
	{
		[HarmonyPriority(800)]
		private static void Prefix(Pawn_HealthTracker __instance, Pawn ___pawn)
		{
			if (Settings.EnableAssignWeapons && !__instance.Downed && ___pawn.IsColonist)
			{
				Pawn_EquipmentTracker equipment = ___pawn.equipment;
				if (((equipment != null) ? equipment.Primary : null) != null && WorldComp.TryGetAssignedWeapons(___pawn, out var aw))
				{
					HarmonyPatchUtil.UnequipPrimaryWeapon(___pawn, aw);
				}
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_HealthTracker), "MakeUndowned")]
	internal static class Pawn_HealthTracker_MakeUndowned
	{
		[HarmonyPriority(800)]
		private static void Prefix(Pawn ___pawn)
		{
			if (Settings.EnableAssignWeapons && ___pawn != null && ((Thing)___pawn).Faction == Faction.OfPlayer && ((Thing)___pawn).def.race.Humanlike && WorldComp.TryGetAssignedWeapons(___pawn, out var aw))
			{
				Pawn_EquipmentTracker equipment = ___pawn.equipment;
				if (((equipment != null) ? equipment.Primary : null) == null && aw.TryGetLastThingUsed(___pawn, out var t))
				{
					HarmonyPatchUtil.EquipWeapon(t, ___pawn, aw);
				}
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "Kill")]
	internal static class Pawn_Kill
	{
		private struct State
		{
			internal readonly Map Map;

			internal readonly ThingWithComps Weapon;

			internal State(Pawn pawn)
			{
				Map = ((Thing)pawn).Map;
				Pawn_EquipmentTracker equipment = pawn.equipment;
				Weapon = ((equipment != null) ? equipment.Primary : null);
			}
		}

		[HarmonyPriority(800)]
		private static void Prefix(Pawn __instance, ref State __state)
		{
			__state = new State(__instance);
		}

		[HarmonyPriority(800)]
		private static void Postfix(Pawn __instance, ref State __state)
		{
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			if (!__instance.Dead || !__instance.IsColonist)
			{
				return;
			}
			Pawn_ApparelTracker apparel = __instance.apparel;
			if (apparel == null)
			{
				return;
			}
			List<Apparel> lockedApparel = apparel.LockedApparel;
			if (lockedApparel == null || lockedApparel.Count != 0 || __state.Weapon == null)
			{
				return;
			}
			if (WorldComp.Add(__state.Weapon))
			{
				Pawn_EquipmentTracker equipment = __instance.equipment;
				if (equipment != null)
				{
					equipment.Remove(__state.Weapon);
				}
			}
			if (!WorldComp.TryGetAssignedWeapons(__instance, out var aw))
			{
				return;
			}
			WorldComp.RemoveAssignedWeapons(__instance);
			foreach (ThingWithComps weapon in aw.Weapons)
			{
				if (!WorldComp.Add(weapon))
				{
					BuildingUtil.DropSingleThing((Thing)(object)weapon, ((Thing)__instance).Position, __state.Map);
				}
			}
		}
	}
	[HarmonyPatch(typeof(Pawn_MeleeVerbs), "TryMeleeAttack")]
	internal static class Pawn_MeleeVerbs_TryMeleeAttack
	{
		private enum DamageType
		{
			Sharp,
			Blunt
		}

		private struct DT(DamageType dt, float p)
		{
			public DamageType DamageType = dt;

			public float Power = p;
		}

		private static readonly Dictionary<Def, DT> weaponDamageTypes = new Dictionary<Def, DT>();

		[HarmonyPriority(800)]
		private static void Postfix(Pawn_MeleeVerbs __instance, Thing target)
		{
			if (!Settings.AutoSwitchMelee)
			{
				return;
			}
			Pawn pawn = __instance.Pawn;
			if (pawn == null || !WorldComp.TryGetAssignedWeapons(pawn, out var aw))
			{
				return;
			}
			Verb val = pawn.TryGetAttackVerb(target, !pawn.IsColonist, false);
			if (val == null || !val.verbProps.IsMeleeAttack)
			{
				return;
			}
			float statValue = StatExtension.GetStatValue(target, StatDefOf.ArmorRating_Blunt, true, -1);
			float statValue2 = StatExtension.GetStatValue(target, StatDefOf.ArmorRating_Sharp, true, -1);
			DamageType dt = ((!(statValue > statValue2)) ? DamageType.Blunt : DamageType.Sharp);
			if (statValue == statValue2)
			{
				if (Settings.PreferredDamageType == PreferredDamageTypeEnum.WeaponStorage_None)
				{
					return;
				}
				dt = ((Settings.PreferredDamageType == PreferredDamageTypeEnum.ArmorBlunt) ? DamageType.Blunt : DamageType.Sharp);
			}
			if (TryGetBestWeapon(dt, (Thing)(object)pawn.equipment.Primary, aw, out var bestWeapon))
			{
				HarmonyPatchUtil.EquipWeapon(bestWeapon, pawn, aw);
			}
		}

		private static bool TryGetBestWeapon(DamageType dt, Thing equiped, AssignedWeaponContainer c, out ThingWithComps bestWeapon)
		{
			bestWeapon = null;
			DT dT = ((equiped == null) ? new DT(DamageType.Blunt, -1f) : GetWeaponDamage(equiped.def));
			if (dT.DamageType == dt && dT.Power != -1f)
			{
				return false;
			}
			dT.Power = -1f;
			foreach (ThingWithComps weapon in c.Weapons)
			{
				if (((Thing)weapon).def.IsMeleeWeapon)
				{
					DT weaponDamage = GetWeaponDamage(((Thing)weapon).def);
					if (weaponDamage.DamageType == dt && weaponDamage.Power > dT.Power)
					{
						dT.Power = weaponDamage.Power;
						bestWeapon = weapon;
					}
				}
			}
			return dT.Power > 0f;
		}

		private static DT GetWeaponDamage(ThingDef def)
		{
			if (weaponDamageTypes.TryGetValue((Def)(object)def, out var value))
			{
				return value;
			}
			value = new DT(DamageType.Blunt, -1f);
			foreach (Tool tool in def.tools)
			{
				if (!(tool.power > value.Power))
				{
					continue;
				}
				value.Power = tool.power;
				using IEnumerator<VerbProperties> enumerator2 = tool.VerbsProperties.GetEnumerator();
				if (enumerator2.MoveNext())
				{
					value.DamageType = ((enumerator2.Current?.meleeDamageDef == DamageDefOf.Blunt) ? DamageType.Blunt : DamageType.Sharp);
				}
			}
			weaponDamageTypes.Add((Def)(object)def, value);
			return value;
		}
	}
	[HarmonyPatch(typeof(RecipeWorkerCounter), "CountProducts")]
	internal static class RecipeWorkerCounter_CountProducts
	{
		private static void Postfix(ref int __result, RecipeWorkerCounter __instance, Bill_Production bill)
		{
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			List<ThingDefCountClass> products = __instance.recipe.products;
			if (!WorldComp.GetWeaponStorages((bill != null) ? ((Bill)bill).Map : null).Any() || products == null)
			{
				return;
			}
			foreach (ThingDefCountClass item in products)
			{
				ThingDef thingDef = item.thingDef;
				if (!thingDef.IsWeapon || bill == null)
				{
					continue;
				}
				foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(((Bill)bill).Map))
				{
					__result += weaponStorage.GetWeaponCount(thingDef, bill.qualityRange, bill.hpRange, bill.limitToAllowedStuff ? ((Bill)bill).ingredientFilter : null);
				}
			}
		}
	}
	[HarmonyPatch(typeof(ReservationManager), "CanReserve")]
	internal static class ReservationManager_CanReserve
	{
		private static void Postfix(ref bool __result, LocalTargetInfo target, Map ___map)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			if (__result || (((LocalTargetInfo)(ref target)).Thing != null && !(((object)target).GetType() == typeof(Building_WeaponStorage))))
			{
				return;
			}
			IEnumerable<Thing> enumerable = ((___map != null) ? ___map.thingGrid.ThingsAt(((LocalTargetInfo)(ref target)).Cell) : null);
			if (enumerable == null)
			{
				return;
			}
			foreach (Thing item in enumerable)
			{
				if (((object)item).GetType() == typeof(Building_WeaponStorage))
				{
					__result = true;
				}
			}
		}
	}
	[HarmonyPatch(typeof(ScribeSaver), "InitSaving")]
	internal static class ScribeSaver_InitSaving
	{
		private static void Prefix()
		{
			try
			{
				foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(null))
				{
					try
					{
						weaponStorage.ReclaimWeapons(force: true);
					}
					catch (Exception ex)
					{
						Log.Warning("Error while reclaiming weapon for storage\n" + ex.Message);
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Warning("Error while reclaiming weapons\n" + ex2.Message);
			}
		}
	}
	[HarmonyPatch(typeof(SettlementAbandonUtility), "Abandon")]
	internal static class SettlementAbandonUtility_Abandon
	{
		[HarmonyPriority(800)]
		private static void Prefix(MapParent settlement)
		{
			WorldComp.Remove(settlement.Map);
		}
	}
	[HarmonyPatch(typeof(TradeDeal), "Reset")]
	internal static class TradeDeal_Reset
	{
		private static void Prefix()
		{
			TradeUtil.ReclaimWeapons();
		}
	}
	[HarmonyPatch(typeof(TradeShip), "ColonyThingsWillingToBuy")]
	internal static class TradeShip_ColonyThingsWillingToBuy
	{
		private static void Postfix(ref IEnumerable<Thing> __result, Pawn playerNegotiator)
		{
			if (((playerNegotiator != null) ? ((Thing)playerNegotiator).Map : null) != null)
			{
				List<Thing> list = new List<Thing>(__result);
				list.AddRange(TradeUtil.EmptyWeaponStorages(((Thing)playerNegotiator).Map));
				__result = list;
			}
		}
	}
	[HarmonyPatch(typeof(Verb_ShootOneUse), "SelfConsume")]
	internal static class Verb_ShootOneUse_SelfConsume
	{
		private static void Prefix(Verb_ShootOneUse __instance)
		{
			Thing caster = ((Verb)__instance).caster;
			Pawn val = (Pawn)(object)((caster is Pawn) ? caster : null);
			if (val != null && WorldComp.TryGetAssignedWeapons(val, out var aw))
			{
				aw.Remove(((Verb)__instance).EquipmentSource);
			}
		}
	}
	[HarmonyPatch(typeof(WealthWatcher), "ForceRecount")]
	public static class WealthWatcher_ForceRecount
	{
		private static void Postfix(WealthWatcher __instance)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected O, but got Unknown
			Map map = (Map)(((object)__instance).GetType().GetField("map", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(__instance));
			FieldInfo field = ((object)__instance).GetType().GetField("wealthItems", BindingFlags.Instance | BindingFlags.NonPublic);
			if (!(field == null))
			{
				float wealthItems = (float)field.GetValue(__instance);
				float num = TallyWealth(WorldComp.GetWeaponStorages(map), wealthItems);
				field.SetValue(__instance, num);
			}
		}

		private static float TallyWealth(IEnumerable<Building_WeaponStorage> storages, float wealthItems)
		{
			foreach (Building_WeaponStorage storage in storages)
			{
				foreach (ThingWithComps weapon in storage.GetWeapons(includeBioencoded: true))
				{
					wealthItems += (float)((Thing)weapon).stackCount + ((Thing)weapon).MarketValue;
				}
			}
			return wealthItems;
		}
	}
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
	public static class WorkGiver_DoBill_TryFindBestBillIngredients
	{
		private static void Postfix(ref bool __result, Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen)
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			//IL_018d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0192: Unknown result type (might be due to invalid IL or missing references)
			//IL_0197: Unknown result type (might be due to invalid IL or missing references)
			//IL_0305: Unknown result type (might be due to invalid IL or missing references)
			if (bill.Map == null)
			{
				Log.Error("Bill's map is null");
			}
			else
			{
				if (__result || !WorldComp.HasStorages(bill.Map) || bill.Map != ((Thing)pawn).Map)
				{
					return;
				}
				Dictionary<ThingDef, int> dictionary = new Dictionary<ThingDef, int>();
				foreach (ThingCount item in chosen)
				{
					ThingCount current = item;
					int value = ((!dictionary.TryGetValue(((ThingCount)(ref current)).Thing.def, out value)) ? ((ThingCount)(ref current)).Count : (value + ((ThingCount)(ref current)).Count));
					dictionary[((ThingCount)(ref current)).Thing.def] = value;
				}
				LinkedList<HarmonyPatches.NeededIngrediants> linkedList = new LinkedList<HarmonyPatches.NeededIngrediants>();
				foreach (IngredientCount ingredient in bill.recipe.ingredients)
				{
					bool flag = false;
					foreach (KeyValuePair<ThingDef, int> item2 in dictionary)
					{
						if ((int)ingredient.GetBaseCount() == item2.Value && ingredient.filter.Allows(item2.Key))
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						linkedList.AddLast(new HarmonyPatches.NeededIngrediants(ingredient.filter, (int)ingredient.GetBaseCount()));
					}
				}
				List<HarmonyPatches.WeaponsToUse> list = new List<HarmonyPatches.WeaponsToUse>();
				foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(bill.Map))
				{
					IntVec3 val = ((Thing)weaponStorage).Position - billGiver.Position;
					if (!((double)(float)((IntVec3)(ref val)).LengthHorizontalSquared < Math.Pow(bill.ingredientSearchRadius, 2.0)))
					{
						continue;
					}
					LinkedListNode<HarmonyPatches.NeededIngrediants> linkedListNode = linkedList.First;
					while (linkedListNode != null)
					{
						LinkedListNode<HarmonyPatches.NeededIngrediants> next = linkedListNode.Next;
						HarmonyPatches.NeededIngrediants value2 = linkedListNode.Value;
						if (weaponStorage.TryGetFilteredWeapons(bill, value2.Filter, out var gotten))
						{
							foreach (ThingWithComps item3 in gotten)
							{
								value2.Add(new HarmonyPatches.StoredWeapons(weaponStorage, item3));
							}
							if (value2.CountReached())
							{
								list.Add(new HarmonyPatches.WeaponsToUse(value2.GetFoundThings(), value2.Count));
								value2.Clear();
								linkedList.Remove(linkedListNode);
							}
						}
						linkedListNode = next;
					}
				}
				if (linkedList.Count == 0)
				{
					__result = true;
					foreach (HarmonyPatches.WeaponsToUse item4 in list)
					{
						int num = item4.Count;
						foreach (HarmonyPatches.StoredWeapons weapon in item4.Weapons)
						{
							if (num <= 0)
							{
								break;
							}
							if (weapon.Storage.Remove(weapon.Weapon))
							{
								num -= ((Thing)weapon.Weapon).stackCount;
								chosen.Add(new ThingCount((Thing)(object)weapon.Weapon, ((Thing)weapon.Weapon).stackCount, false));
							}
						}
					}
				}
				list.Clear();
				foreach (HarmonyPatches.NeededIngrediants item5 in linkedList)
				{
					item5.Clear();
				}
				linkedList.Clear();
				dictionary.Clear();
			}
		}
	}
	public class AssignedWeaponContainer : IExposable
	{
		private class AssignedWeapon : IExposable
		{
			public bool IsEquipped;

			public ThingWithComps Weapon;

			public void ExposeData()
			{
				try
				{
					Scribe_Values.Look<bool>(ref IsEquipped, "isEquipped", false, false);
					if (IsEquipped)
					{
						Scribe_References.Look<ThingWithComps>(ref Weapon, "weapon", false);
					}
					else
					{
						Scribe_Deep.Look<ThingWithComps>(ref Weapon, "weapon", (object[])null);
					}
				}
				catch
				{
					Weapon = null;
					IsEquipped = false;
				}
			}
		}

		public readonly List<ThingWithComps> weapons = new List<ThingWithComps>();

		private ThingWithComps LastToolUsed;

		private ThingWithComps LastWeaponUsed;

		public Pawn Pawn;

		private List<AssignedWeapon> tmp;

		public HashSet<int> weaponIds = new HashSet<int>();

		public IEnumerable<ThingWithComps> Weapons => weapons;

		public int Count => weaponIds.Count;

		public void ExposeData()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Invalid comparison between Unknown and I4
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Invalid comparison between Unknown and I4
			//IL_020c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0212: Invalid comparison between Unknown and I4
			//IL_0214: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Invalid comparison between Unknown and I4
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				tmp = new List<AssignedWeapon>(weapons.Count);
				ThingWithComps primary = Pawn.equipment.Primary;
				foreach (ThingWithComps weapon in Weapons)
				{
					AssignedWeapon item = new AssignedWeapon
					{
						IsEquipped = (primary == weapon),
						Weapon = weapon
					};
					tmp.Add(item);
				}
			}
			Scribe_References.Look<Pawn>(ref Pawn, "pawn", false);
			Scribe_Collections.Look<AssignedWeapon>(ref tmp, "weapons", (LookMode)2, Array.Empty<object>());
			Scribe_References.Look<ThingWithComps>(ref LastWeaponUsed, "lastWeaponUsed", false);
			Scribe_References.Look<ThingWithComps>(ref LastToolUsed, "lastToolUsed", false);
			if ((int)Scribe.mode == 4)
			{
				if (weaponIds == null)
				{
					weaponIds = new HashSet<int>();
				}
				weapons.Clear();
				weaponIds.Clear();
				foreach (AssignedWeapon item2 in tmp)
				{
					if (item2.Weapon == null)
					{
						Log.Error("failed to load weapon assigned to " + Pawn.Name.ToStringShort);
						continue;
					}
					weapons.Add(item2.Weapon);
					weaponIds.Add(((Thing)item2.Weapon).thingIDNumber);
				}
				if ((int)Scribe.mode == 4 && Pawn != null)
				{
					foreach (ThingWithComps weapon2 in weapons)
					{
						CompEquippable comp = weapon2.GetComp<CompEquippable>();
						List<Verb> list = ((comp != null) ? comp.AllVerbs : null);
						if (list == null)
						{
							continue;
						}
						foreach (Verb item3 in list)
						{
							item3.caster = (Thing)(object)Pawn;
						}
					}
				}
			}
			if (((int)Scribe.mode == 1 || (int)Scribe.mode == 4) && tmp != null)
			{
				tmp.Clear();
				tmp = null;
			}
		}

		public void Add(ThingWithComps weapon)
		{
			if (!Contains(weapon))
			{
				weapons.Add(weapon);
				weaponIds.Add(((Thing)weapon).thingIDNumber);
			}
		}

		public bool Contains(ThingWithComps weapon)
		{
			if (weapon != null)
			{
				return weaponIds.Contains(((Thing)weapon).thingIDNumber);
			}
			return false;
		}

		public void Clear()
		{
			weaponIds.Clear();
			weapons.Clear();
		}

		public bool TryGetLastThingUsed(Pawn pawn, out ThingWithComps t)
		{
			bool flag = false;
			t = (pawn.Drafted ? LastWeaponUsed : LastToolUsed);
			if (t != null)
			{
				if (weaponIds.Contains(((Thing)t).thingIDNumber))
				{
					flag = true;
				}
				else
				{
					SetLastThingUsed(pawn, null, isForceMelee: false);
				}
			}
			if (!flag)
			{
				t = null;
			}
			return flag;
		}

		public void SetLastThingUsed(Pawn pawn, ThingWithComps t, bool isForceMelee)
		{
			if (pawn.Drafted || !Settings.IsTool((Thing)(object)t))
			{
				LastWeaponUsed = t;
			}
			if (!pawn.Drafted)
			{
				LastToolUsed = t;
			}
		}

		public bool Remove(ThingWithComps weapon)
		{
			if (LastToolUsed == weapon)
			{
				LastToolUsed = null;
			}
			if (LastWeaponUsed == weapon)
			{
				LastWeaponUsed = null;
			}
			weaponIds.Remove(((Thing)weapon).thingIDNumber);
			return weapons.Remove(weapon);
		}
	}
	internal class BuildingUtil
	{
		public static List<T> FindThingsOfTypeNextTo<T>(Map map, IntVec3 position, int distance) where T : Thing
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			int num = Math.Max(0, position.x - distance);
			int num2 = Math.Min(map.info.Size.x, position.x + distance);
			int num3 = Math.Max(0, position.z - distance);
			int num4 = Math.Min(map.info.Size.z, position.z + distance);
			ReservationManager reservationManager = map.reservationManager;
			List<T> list = new List<T>();
			for (int i = num - 1; i <= num2; i++)
			{
				for (int j = num3 - 1; j <= num4; j++)
				{
					foreach (Thing item in map.thingGrid.ThingsAt(new IntVec3(i, position.y, j)))
					{
						T val = (T)(object)((item is T) ? item : null);
						if (val != null && (reservationManager == null || !reservationManager.IsReservedByAnyoneOf(new LocalTargetInfo((Thing)(object)val), Faction.OfPlayer)))
						{
							list.Add(val);
						}
					}
				}
			}
			return list;
		}

		public static bool DropThing(Thing toDrop, Building_WeaponStorage from, Map map, bool makeForbidden = false)
		{
			if (toDrop == null)
			{
				return false;
			}
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			try
			{
				from.AllowAdds = false;
				return (toDrop.stackCount <= toDrop.def.stackLimit) ? DropSingleThing(toDrop, from, map, makeForbidden) : DropThing(toDrop, toDrop.stackCount, from, map, null, makeForbidden);
			}
			catch (Exception ex)
			{
				Log.Warning("failed to drop " + ((Def)(toDrop.def?)).defName + "\n" + ex.Message);
				return true;
			}
			finally
			{
				from.AllowAdds = true;
			}
		}

		public static bool DropThing(Thing toDrop, IntVec3 from, Map map, bool makeForbidden = false)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			if (toDrop.stackCount != 0)
			{
				if (toDrop.stackCount > toDrop.def.stackLimit)
				{
					return DropThing(toDrop, toDrop.stackCount, from, map, null, makeForbidden);
				}
				return DropSingleThing(toDrop, from, map, makeForbidden);
			}
			Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
			return false;
		}

		public static bool DropThing(Thing toDrop, int amountToDrop, Building_WeaponStorage from, Map map, List<Thing> droppedThings = null, bool makeForbidden = false)
		{
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			bool result = false;
			try
			{
				from.AllowAdds = false;
				bool flag = false;
				while (!flag)
				{
					int num = toDrop.def.stackLimit;
					if (num > amountToDrop)
					{
						num = amountToDrop;
						flag = true;
					}
					if (num >= toDrop.stackCount)
					{
						if (amountToDrop > num)
						{
							Log.Error($"        ThingStorage: Unable to drop {amountToDrop - num} of {((Def)toDrop.def).label}");
						}
						num = toDrop.stackCount;
						flag = true;
					}
					if (num > 0)
					{
						amountToDrop -= num;
						Thing val = toDrop.SplitOff(num);
						droppedThings?.Add(val);
						if (DropSingleThing(val, from, map, makeForbidden))
						{
							result = true;
						}
					}
				}
				return result;
			}
			finally
			{
				from.AllowAdds = true;
			}
		}

		public static bool DropThing(Thing toDrop, int amountToDrop, IntVec3 from, Map map, List<Thing> droppedThings = null, bool makeForbidden = false)
		{
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			bool result = false;
			bool flag = false;
			while (!flag)
			{
				int num = toDrop.def.stackLimit;
				if (num > amountToDrop)
				{
					num = amountToDrop;
					flag = true;
				}
				if (num >= toDrop.stackCount)
				{
					if (amountToDrop > num)
					{
						Log.Error($"        ThingStorage: Unable to drop {amountToDrop - num} of {((Def)toDrop.def).label}");
					}
					num = toDrop.stackCount;
					flag = true;
				}
				if (num > 0)
				{
					amountToDrop -= num;
					Thing val = toDrop.SplitOff(num);
					droppedThings?.Add(val);
					if (DropSingleThing(val, from, map, makeForbidden))
					{
						result = true;
					}
				}
			}
			return result;
		}

		public static bool DropSingleThing(Thing toDrop, Building_WeaponStorage from, Map map, bool makeForbidden = false)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			try
			{
				from.AllowAdds = false;
				return DropSingleThing(toDrop, ((Thing)from).InteractionCell, map, makeForbidden);
			}
			finally
			{
				from.AllowAdds = true;
			}
		}

		public static bool DropSingleThing(Thing toDrop, IntVec3 from, Map map, bool makeForbidden = false)
		{
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			if (toDrop == null)
			{
				return false;
			}
			if (toDrop.stackCount == 0)
			{
				Log.Warning("To Drop Thing " + ((Entity)toDrop).Label + " had stack count of 0");
				return false;
			}
			try
			{
				if (!toDrop.Spawned)
				{
					Thing val = default(Thing);
					GenThing.TryDropAndSetForbidden(toDrop, from, map, (ThingPlaceMode)1, ref val, makeForbidden);
					if (!toDrop.Spawned && !GenPlace.TryPlaceThing(toDrop, from, map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1))
					{
						Log.Error($"Failed to spawn {((Entity)toDrop).Label} x{toDrop.stackCount}");
						return false;
					}
				}
				toDrop.Position = from;
			}
			catch (Exception ex)
			{
				Log.Error("BuildingUtil.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			return toDrop.Spawned;
		}
	}
	internal class Building_RepairWeaponStorage : Building
	{
		private const int LOW_POWER_COST = 10;

		private const long THIRTY_SECONDS = 300000000L;

		private static readonly LinkedList<ThingWithComps> AllWeaponsBeingRepaired = new LinkedList<ThingWithComps>();

		private LinkedList<Building_WeaponStorage> AttachedWeaponStorages = new LinkedList<Building_WeaponStorage>();

		private ThingWithComps beingRepaird;

		public CompPowerTrader compPowerTrader;

		private AssignedWeaponContainer container;

		private long lastSearch = DateTime.Now.Ticks;

		private long lastTick = DateTime.Now.Ticks;

		public override string GetInspectString()
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			StringBuilder stringBuilder = new StringBuilder(((ThingWithComps)this).GetInspectString());
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(Environment.NewLine);
			}
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("WeaponStorage.AttachedWeaponStorages")));
			stringBuilder.Append(": ");
			stringBuilder.Append(AttachedWeaponStorages.Count);
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("WeaponStorage.IsRepairing")));
			stringBuilder.Append(": ");
			if (beingRepaird != null)
			{
				stringBuilder.Append(((Entity)beingRepaird).Label);
				if (container != null)
				{
					stringBuilder.Append(" (");
					stringBuilder.Append(container.Pawn.Name.ToStringShort);
					stringBuilder.Append(")");
				}
				stringBuilder.Append(Environment.NewLine);
				stringBuilder.Append("    ");
				stringBuilder.Append(((Thing)beingRepaird).HitPoints.ToString());
				stringBuilder.Append("/");
				stringBuilder.Append(((Thing)beingRepaird).MaxHitPoints);
			}
			else
			{
				stringBuilder.Append(bool.FalseString);
			}
			return stringBuilder.ToString();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).SpawnSetup(map, respawningAfterLoad);
			compPowerTrader = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			compPowerTrader.PowerOutput = -10f;
			foreach (Building_WeaponStorage item in BuildingUtil.FindThingsOfTypeNextTo<Building_WeaponStorage>(((Thing)this).Map, ((Thing)this).Position, Settings.RepairAttachmentDistance))
			{
				Add(item);
			}
			compPowerTrader.powerStartedAction = delegate
			{
				compPowerTrader.PowerOutput = 10f;
			};
			compPowerTrader.powerStoppedAction = delegate
			{
				StopRepairing();
				compPowerTrader.PowerOutput = 0f;
			};
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).Destroy(mode);
			StopRepairing();
			AttachedWeaponStorages.Clear();
		}

		public override void Discard(bool silentlyRemoveReferences = false)
		{
			((Thing)this).Discard(silentlyRemoveReferences);
			StopRepairing();
			AttachedWeaponStorages.Clear();
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			((Building)this).DeSpawn(mode);
			StopRepairing();
			AttachedWeaponStorages.Clear();
		}

		public override void Tick()
		{
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			((Entity)this).TickRare();
			long ticks = DateTime.Now.Ticks;
			if (ticks - lastTick <= Settings.RepairAttachmentUpdateIntervalTicks)
			{
				return;
			}
			lastTick = DateTime.Now.Ticks;
			if (!compPowerTrader.PowerOn)
			{
				if (beingRepaird != null)
				{
					StopRepairing();
				}
			}
			else if (beingRepaird == null)
			{
				if (ticks - lastSearch > 300000000)
				{
					lastSearch = ticks;
					StartRepairing();
				}
			}
			else if (beingRepaird != null && ((Thing)beingRepaird).HitPoints >= ((Thing)beingRepaird).MaxHitPoints)
			{
				((Thing)beingRepaird).HitPoints = ((Thing)beingRepaird).MaxHitPoints;
				StopRepairing();
				lastSearch = ticks;
				StartRepairing();
			}
			if (beingRepaird != null)
			{
				ThingWithComps obj = beingRepaird;
				((Thing)obj).HitPoints = ((Thing)obj).HitPoints + Settings.RepairAttachmentMendingSpeed;
				if (((Thing)beingRepaird).HitPoints > ((Thing)beingRepaird).MaxHitPoints)
				{
					((Thing)beingRepaird).HitPoints = ((Thing)beingRepaird).MaxHitPoints;
				}
				float num = GenTemperature.ControlTemperatureTempChange(((Thing)this).Position, ((Thing)this).Map, 10f, float.MaxValue);
				Room room = RegionAndRoomQuery.GetRoom((Thing)(object)this, (RegionType)15);
				room.Temperature += num;
				compPowerTrader.PowerOutput = 0f - ((CompPower)compPowerTrader).Props.basePowerConsumption;
			}
			else
			{
				compPowerTrader.PowerOutput = 10f;
			}
		}

		private void OrderAttachedWeaponStorages()
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
			bool flag = true;
			LinkedListNode<Building_WeaponStorage> linkedListNode = AttachedWeaponStorages.First;
			while (linkedListNode != null)
			{
				LinkedListNode<Building_WeaponStorage> next = linkedListNode.Next;
				if (!((Thing)linkedListNode.Value).Spawned)
				{
					AttachedWeaponStorages.Remove(linkedListNode);
				}
				else if (linkedListNode.Next != null && ((Building_Storage)linkedListNode.Value).settings.Priority < ((Building_Storage)linkedListNode.Next.Value).settings.Priority)
				{
					flag = false;
				}
				linkedListNode = next;
			}
			if (flag)
			{
				return;
			}
			LinkedList<Building_WeaponStorage> linkedList = new LinkedList<Building_WeaponStorage>();
			for (linkedListNode = AttachedWeaponStorages.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
			{
				Building_WeaponStorage value = linkedListNode.Value;
				bool flag2 = false;
				for (LinkedListNode<Building_WeaponStorage> linkedListNode2 = linkedList.First; linkedListNode2 != null; linkedListNode2 = linkedListNode2.Next)
				{
					if (((Building_Storage)value).settings.Priority > ((Building_Storage)linkedListNode2.Value).settings.Priority)
					{
						linkedList.AddBefore(linkedListNode2, value);
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					linkedList.AddLast(value);
				}
			}
			AttachedWeaponStorages.Clear();
			AttachedWeaponStorages = linkedList;
		}

		private void StartRepairing()
		{
			OrderAttachedWeaponStorages();
			foreach (AssignedWeaponContainer assignedWeaponContainer in WorldComp.AssignedWeaponContainers)
			{
				foreach (ThingWithComps weapon in assignedWeaponContainer.Weapons)
				{
					if (((Thing)weapon).HitPoints < ((Thing)weapon).MaxHitPoints && !AllWeaponsBeingRepaired.Contains(weapon))
					{
						beingRepaird = weapon;
						container = assignedWeaponContainer;
						AllWeaponsBeingRepaired.AddLast(weapon);
						return;
					}
				}
			}
			for (LinkedListNode<Building_WeaponStorage> linkedListNode = AttachedWeaponStorages.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
			{
				foreach (ThingWithComps weapon2 in linkedListNode.Value.GetWeapons(includeBioencoded: true))
				{
					if (((Thing)weapon2).HitPoints < ((Thing)weapon2).MaxHitPoints && !AllWeaponsBeingRepaired.Contains(weapon2))
					{
						beingRepaird = weapon2;
						container = null;
						AllWeaponsBeingRepaired.AddLast(weapon2);
						return;
					}
				}
			}
		}

		private void StopRepairing()
		{
			if (beingRepaird != null)
			{
				AllWeaponsBeingRepaired.Remove(beingRepaird);
				beingRepaird = null;
				container = null;
			}
		}

		public void Add(Building_WeaponStorage s)
		{
			if (AttachedWeaponStorages != null && !AttachedWeaponStorages.Contains(s))
			{
				AttachedWeaponStorages.AddLast(s);
			}
		}

		public void Remove(Building_WeaponStorage s)
		{
			if (AttachedWeaponStorages != null && AttachedWeaponStorages.Any() && AttachedWeaponStorages.Contains(s))
			{
				AttachedWeaponStorages.Remove(s);
			}
		}
	}
	public class Building_WeaponStorage : Building_Storage
	{
		private readonly FieldInfo AllowedDefsFI = typeof(ThingFilter).GetField("allowedDefs", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly ThingFilter previousStorageFilters = new ThingFilter();

		public readonly Dictionary<ThingDef, LinkedList<ThingWithComps>> StoredBioEncodedWeapons = new Dictionary<ThingDef, LinkedList<ThingWithComps>>();

		public readonly Dictionary<ThingDef, LinkedList<ThingWithComps>> StoredWeapons = new Dictionary<ThingDef, LinkedList<ThingWithComps>>();

		public bool AllowAdds;

		private List<Thing> forceAddedWeapons;

		public bool IncludeInSharedWeapons = true;

		private bool includeInTradeDeals = true;

		public string Name = "";

		public List<ThingWithComps> temp;

		private Map CurrentMap { get; set; }

		public bool IncludeInTradeDeals => includeInTradeDeals;

		public override string Label
		{
			get
			{
				if (!(Name != ""))
				{
					return ((Thing)this).Label;
				}
				return Name;
			}
		}

		public int Count => StoredWeapons.Count + StoredBioEncodedWeapons.Count;

		public Building_WeaponStorage()
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Expected O, but got Unknown
			AllowAdds = true;
		}

		public bool HasWeapon(SharedWeaponFilter filter, ThingDef thingDef)
		{
			if (!StoredWeapons.TryGetValue(thingDef, out var value))
			{
				return false;
			}
			if (value == null)
			{
				StoredWeapons.Remove(thingDef);
			}
			else
			{
				foreach (ThingWithComps item in value)
				{
					if (filter.Allows(item))
					{
						return true;
					}
				}
			}
			return false;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			CurrentMap = map;
			if (base.settings == null)
			{
				base.settings = new StorageSettings((IStoreSettingsParent)(object)this);
				base.settings.CopyFrom(((Thing)this).def.building.defaultStorageSettings);
				base.settings.filter.SetDisallowAll((IEnumerable<ThingDef>)null, (IEnumerable<SpecialThingFilterDef>)null);
			}
			UpdatePreviousStorageFilter();
			WorldComp.Add(this);
			foreach (Building_RepairWeaponStorage item in BuildingUtil.FindThingsOfTypeNextTo<Building_RepairWeaponStorage>(((Thing)this).Map, ((Thing)this).Position, Settings.RepairAttachmentDistance))
			{
				item.Add(this);
			}
			WorldComp.InitializeAssignedWeapons();
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Dispose();
				((Building_Storage)this).Destroy(mode);
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + ".Destroy\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Dispose();
				((Building_Storage)this).DeSpawn(mode);
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + ".DeSpawn\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool CanAdd(ThingWithComps t)
		{
			if (t != null)
			{
				return base.settings.AllowedToAccept((Thing)(object)t);
			}
			return false;
		}

		private void Dispose()
		{
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (StoredWeapons != null)
				{
					foreach (LinkedList<ThingWithComps> value in StoredWeapons.Values)
					{
						foreach (ThingWithComps item in value)
						{
							DropThing((Thing)(object)item);
						}
					}
					StoredWeapons.Clear();
				}
				if (StoredBioEncodedWeapons != null)
				{
					foreach (LinkedList<ThingWithComps> value2 in StoredBioEncodedWeapons.Values)
					{
						foreach (ThingWithComps item2 in value2)
						{
							DropThing((Thing)(object)item2);
						}
					}
					StoredBioEncodedWeapons.Clear();
				}
			}
			catch (Exception ex)
			{
				Log.Error(((object)this).GetType().Name + ".Dispose\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
			WorldComp.Remove(this);
			foreach (Building_RepairWeaponStorage item3 in BuildingUtil.FindThingsOfTypeNextTo<Building_RepairWeaponStorage>(((Thing)this).Map, ((Thing)this).Position, Settings.RepairAttachmentDistance))
			{
				item3?.Remove(this);
			}
		}

		public bool TryRemoveWeapon(ThingDef thingDef, SharedWeaponFilter filter, bool includeBioencoded, out ThingWithComps weapon)
		{
			Log.Message($"Trying to remove {thingDef}");
			if (StoredWeapons.TryGetValue(thingDef, out var value))
			{
				for (LinkedListNode<ThingWithComps> linkedListNode = value.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
				{
					if (filter.Allows(linkedListNode.Value))
					{
						weapon = linkedListNode.Value;
						value.Remove(linkedListNode);
						return true;
					}
				}
			}
			if (includeBioencoded && StoredBioEncodedWeapons.TryGetValue(thingDef, out value))
			{
				for (LinkedListNode<ThingWithComps> linkedListNode2 = value.First; linkedListNode2 != null; linkedListNode2 = linkedListNode2.Next)
				{
					if (filter.Allows(linkedListNode2.Value))
					{
						weapon = linkedListNode2.Value;
						value.Remove(linkedListNode2);
						return true;
					}
				}
			}
			weapon = null;
			return false;
		}

		private bool DropThing(Thing t)
		{
			return BuildingUtil.DropThing(t, this, CurrentMap);
		}

		private void DropWeapons<T>(IEnumerable<T> things) where T : Thing
		{
			try
			{
				if (things == null)
				{
					return;
				}
				foreach (T thing in things)
				{
					DropThing((Thing)(object)thing);
				}
			}
			catch (Exception ex)
			{
				Log.Error("ChangeDresser:Building_Dresser.DropApparel\n" + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
			}
		}

		public void Empty()
		{
			try
			{
				AllowAdds = false;
				foreach (LinkedList<ThingWithComps> value in StoredWeapons.Values)
				{
					DropWeapons<ThingWithComps>((IEnumerable<ThingWithComps>)value);
				}
				foreach (LinkedList<ThingWithComps> value2 in StoredBioEncodedWeapons.Values)
				{
					DropWeapons<ThingWithComps>((IEnumerable<ThingWithComps>)value2);
				}
				CombatExtendedUtil.EmptyAmmo(this);
				StoredWeapons.Clear();
				StoredBioEncodedWeapons.Clear();
			}
			finally
			{
				AllowAdds = true;
			}
		}

		public override void Notify_ReceivedThing(Thing newItem)
		{
			if (!AllowAdds)
			{
				if (!newItem.Spawned)
				{
					DropThing(newItem);
				}
				return;
			}
			ThingWithComps val = (ThingWithComps)(object)((newItem is ThingWithComps) ? newItem : null);
			if ((val == null || !((Thing)val).def.IsWeapon) && !CombatExtendedUtil.IsAmmo(newItem))
			{
				if (!newItem.Spawned)
				{
					DropThing(newItem);
				}
				return;
			}
			((Building_Storage)this).Notify_ReceivedThing(newItem);
			if (CombatExtendedUtil.AddAmmo(newItem))
			{
				return;
			}
			ThingWithComps val2 = (ThingWithComps)(object)((newItem is ThingWithComps) ? newItem : null);
			if (val2 != null && !Contains(val2))
			{
				if (((Thing)val2).Spawned)
				{
					((Entity)val2).DeSpawn((DestroyMode)0);
				}
				if (!AddWeapon(val2) && !WorldComp.Add(val2))
				{
					BuildingUtil.DropThing((Thing)(object)val2, this, CurrentMap, makeForbidden: true);
				}
			}
		}

		private bool Contains(ThingWithComps t)
		{
			return Contains(t, CompBiocodable.IsBiocoded((Thing)(object)t) ? StoredBioEncodedWeapons : StoredWeapons);
		}

		private bool Contains(ThingWithComps t, Dictionary<ThingDef, LinkedList<ThingWithComps>> storage)
		{
			if (t != null && storage.TryGetValue(((Thing)t).def, out var value))
			{
				return value.Contains(t);
			}
			return false;
		}

		internal bool AddWeapon(ThingWithComps weapon)
		{
			if (!CanAdd(weapon))
			{
				return false;
			}
			if (((Thing)weapon).Spawned)
			{
				((Entity)weapon).DeSpawn((DestroyMode)0);
			}
			if (CombatExtendedUtil.AddAmmo((Thing)(object)weapon))
			{
				return true;
			}
			AddToSortedList(weapon, CompBiocodable.IsBiocoded((Thing)(object)weapon) ? StoredBioEncodedWeapons : StoredWeapons);
			return true;
		}

		private void AddToSortedList(ThingWithComps weapon, Dictionary<ThingDef, LinkedList<ThingWithComps>> storage)
		{
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			_ = ((Def)((Thing)weapon).def).label;
			if (!storage.TryGetValue(((Thing)weapon).def, out var value))
			{
				value = new LinkedList<ThingWithComps>();
				value.AddFirst(weapon);
				storage[((Thing)weapon).def] = value;
				return;
			}
			QualityCategory val = default(QualityCategory);
			if (QualityUtility.TryGetQuality((Thing)(object)weapon, ref val))
			{
				QualityCategory val2 = default(QualityCategory);
				for (LinkedListNode<ThingWithComps> linkedListNode = value.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
				{
					if (QualityUtility.TryGetQuality((Thing)(object)linkedListNode.Value, ref val2) && (val > val2 || (val == val2 && ((Thing)weapon).HitPoints >= ((Thing)linkedListNode.Value).HitPoints)))
					{
						value.AddBefore(linkedListNode, weapon);
						return;
					}
				}
			}
			value.AddLast(weapon);
		}

		internal int GetWeaponCount(ThingDef expectedDef, QualityRange qualityRange, FloatRange hpRange, ThingFilter ingredientFilter)
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			int num = 0;
			foreach (LinkedList<ThingWithComps> value in StoredWeapons.Values)
			{
				foreach (ThingWithComps item in (IEnumerable<ThingWithComps>)value)
				{
					if (Allows((Thing)(object)item, expectedDef, qualityRange, hpRange, ingredientFilter))
					{
						num++;
					}
				}
			}
			foreach (LinkedList<ThingWithComps> value2 in StoredBioEncodedWeapons.Values)
			{
				foreach (ThingWithComps item2 in (IEnumerable<ThingWithComps>)value2)
				{
					if (Allows((Thing)(object)item2, expectedDef, qualityRange, hpRange, ingredientFilter))
					{
						num++;
					}
				}
			}
			return num;
		}

		private bool Allows(Thing t, ThingDef expectedDef, QualityRange qualityRange, FloatRange hpRange, ThingFilter filter)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			if (t.def != expectedDef)
			{
				return false;
			}
			if (expectedDef.useHitPoints && hpRange.min != 0f && hpRange.max != 100f)
			{
				float num = (float)t.HitPoints / (float)t.MaxHitPoints;
				num = GenMath.RoundedHundredth(num);
				if (!((FloatRange)(ref hpRange)).IncludesEpsilon(Mathf.Clamp01(num)))
				{
					return false;
				}
			}
			if (qualityRange == QualityRange.All || !QualityUtility.FollowQualityThingFilter(t.def))
			{
				if (filter != null)
				{
					return filter.Allows(t.Stuff);
				}
				return true;
			}
			QualityCategory val = default(QualityCategory);
			if (!QualityUtility.TryGetQuality(t, ref val))
			{
				val = (QualityCategory)2;
			}
			if (!((QualityRange)(ref qualityRange)).Includes(val))
			{
				return false;
			}
			if (filter != null)
			{
				return filter.Allows(t.Stuff);
			}
			return true;
		}

		internal bool TryGetFilteredWeapons(Bill bill, ThingFilter filter, out List<ThingWithComps> gotten)
		{
			List<ThingWithComps> list = new List<ThingWithComps>();
			foreach (KeyValuePair<ThingDef, LinkedList<ThingWithComps>> storedWeapon in StoredWeapons)
			{
				GetFilteredWeaponsFromStorage(bill, filter, list, storedWeapon);
			}
			foreach (KeyValuePair<ThingDef, LinkedList<ThingWithComps>> storedBioEncodedWeapon in StoredBioEncodedWeapons)
			{
				GetFilteredWeaponsFromStorage(bill, filter, list, storedBioEncodedWeapon);
			}
			gotten = ((list.Count > 0) ? list : null);
			return gotten != null;
		}

		private void GetFilteredWeaponsFromStorage(Bill bill, ThingFilter filter, List<ThingWithComps> gotten, KeyValuePair<ThingDef, LinkedList<ThingWithComps>> kv)
		{
			if (!filter.Allows(kv.Key))
			{
				return;
			}
			foreach (ThingWithComps item in kv.Value)
			{
				if (bill.IsFixedOrAllowedIngredient((Thing)(object)item) && filter.Allows((Thing)(object)item))
				{
					gotten.Add(item);
				}
			}
		}

		internal void ReclaimWeapons(bool force = false)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if (((Thing)this).Map == null)
			{
				return;
			}
			List<ThingWithComps> list = BuildingUtil.FindThingsOfTypeNextTo<ThingWithComps>(((Thing)this).Map, ((Thing)this).Position, 1);
			if (list.Count <= 0)
			{
				return;
			}
			foreach (ThingWithComps item in list)
			{
				if (!AddWeapon(item) && force && ((Thing)item).Spawned && (((Thing)item).def.IsWeapon || CombatExtendedUtil.IsAmmo((Thing)(object)item)) && !((Thing)item).def.IsStuff)
				{
					((Entity)item).DeSpawn((DestroyMode)0);
					if (forceAddedWeapons == null)
					{
						forceAddedWeapons = new List<Thing>(list.Count);
					}
					forceAddedWeapons.Add((Thing)(object)item);
				}
			}
			list.Clear();
		}

		private void HandleThingsOnTop()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			if (!((Thing)this).Spawned)
			{
				return;
			}
			foreach (Thing item in ((Thing)this).Map.thingGrid.ThingsAt(((Thing)this).Position))
			{
				if (item != null && (object)item != this && !(item is Blueprint) && !(item is Building))
				{
					ThingWithComps val = (ThingWithComps)(object)((item is ThingWithComps) ? item : null);
					if ((val == null || !AddWeapon(val)) && item.Spawned)
					{
						IntVec3 position = item.Position;
						position.x++;
						item.Position = position;
						Log.Warning("Moving " + ((Entity)item).Label);
					}
				}
			}
		}

		public override void ExposeData()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Invalid comparison between Unknown and I4
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0192: Invalid comparison between Unknown and I4
			//IL_0194: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Invalid comparison between Unknown and I4
			((Building_Storage)this).ExposeData();
			if ((int)Scribe.mode == 1)
			{
				temp = new List<ThingWithComps>();
				foreach (LinkedList<ThingWithComps> value in StoredWeapons.Values)
				{
					temp.AddRange(value);
				}
				foreach (LinkedList<ThingWithComps> value2 in StoredBioEncodedWeapons.Values)
				{
					temp.AddRange(value2);
				}
				if (forceAddedWeapons == null)
				{
					forceAddedWeapons = new List<Thing>();
				}
			}
			Scribe_Collections.Look<ThingWithComps>(ref temp, false, "storedWeapons", (LookMode)2, Array.Empty<object>());
			Scribe_Values.Look<bool>(ref includeInTradeDeals, "includeInTradeDeals", true, false);
			Scribe_Values.Look<bool>(ref IncludeInSharedWeapons, "includeInSharedWeapons", true, false);
			Scribe_Collections.Look<Thing>(ref forceAddedWeapons, false, "forceAddedWeapons", (LookMode)2, Array.Empty<object>());
			Scribe_Values.Look<string>(ref Name, "name", "", false);
			if ((int)Scribe.mode == 3)
			{
				StoredWeapons.Clear();
				StoredBioEncodedWeapons.Clear();
				if (temp != null)
				{
					foreach (ThingWithComps item in temp)
					{
						AddToSortedList(item, CompBiocodable.IsBiocoded((Thing)(object)item) ? StoredBioEncodedWeapons : StoredWeapons);
					}
				}
			}
			if ((int)Scribe.mode == 1 || (int)Scribe.mode == 3)
			{
				if (temp != null)
				{
					temp.Clear();
					temp = null;
				}
				List<Thing> list = forceAddedWeapons;
				if (list != null && list.Count == 0)
				{
					forceAddedWeapons = null;
				}
			}
		}

		public override string GetInspectString()
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			((Entity)this).Tick();
			StringBuilder stringBuilder = new StringBuilder(((Building_Storage)this).GetInspectString());
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(Environment.NewLine);
			}
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("WeaponStorage.StoragePriority")));
			stringBuilder.Append(": ");
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("StoragePriority" + ((object)base.settings.Priority/*cast due to constrained. prefix*/).ToString())));
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("WeaponStorage.Count")));
			stringBuilder.Append(": ");
			stringBuilder.Append(Count);
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(TaggedString.op_Implicit(Translator.Translate("WeaponStorage.IncludeInTradeDeals")));
			stringBuilder.Append(": ");
			stringBuilder.Append(includeInTradeDeals.ToString());
			return stringBuilder.ToString();
		}

		public IEnumerable<ThingWithComps> GetWeapons(bool includeBioencoded)
		{
			foreach (LinkedList<ThingWithComps> value in StoredWeapons.Values)
			{
				foreach (ThingWithComps item in value)
				{
					yield return item;
				}
			}
			if (!includeBioencoded)
			{
				yield break;
			}
			foreach (LinkedList<ThingWithComps> value2 in StoredBioEncodedWeapons.Values)
			{
				foreach (ThingWithComps item2 in value2)
				{
					yield return item2;
				}
			}
		}

		public IEnumerable<ThingWithComps> GetBioEncodedWeapons()
		{
			foreach (LinkedList<ThingWithComps> value in StoredBioEncodedWeapons.Values)
			{
				foreach (ThingWithComps item in value)
				{
					yield return item;
				}
			}
		}

		public bool Remove(ThingWithComps weapon)
		{
			if (weapon != null)
			{
				return RemoveFrom(weapon, (!CompBiocodable.IsBiocoded((Thing)(object)weapon)) ? StoredWeapons : StoredBioEncodedWeapons);
			}
			return true;
		}

		private bool RemoveFrom(ThingWithComps weapon, Dictionary<ThingDef, LinkedList<ThingWithComps>> storage)
		{
			if (weapon == null)
			{
				return true;
			}
			if (!storage.TryGetValue(((Thing)weapon).def, out var value) || !value.Remove(weapon) || ((Thing)weapon).Spawned || DropThing((Thing)(object)weapon))
			{
				return ((Thing)weapon).Spawned;
			}
			Log.Warning("failed to drop " + ((Entity)weapon).Label + " from storage " + ((Entity)this).Label);
			return false;
		}

		public bool RemoveNoDrop(ThingWithComps thing)
		{
			if (!StoredWeapons.TryGetValue(((Thing)thing).def, out var value) && !StoredBioEncodedWeapons.TryGetValue(((Thing)thing).def, out value))
			{
				return false;
			}
			return value.Remove(thing);
		}

		public override void TickLong()
		{
			if (((Thing)this).Spawned && ((Thing)this).Map != null)
			{
				HandleThingsOnTop();
			}
			while (WorldComp.WeaponsToDrop.Count > 0)
			{
				DropThing((Thing)(object)WorldComp.WeaponsToDrop.Pop());
			}
			List<Thing> list = forceAddedWeapons;
			if (list == null || list.Count <= 0)
			{
				return;
			}
			foreach (Thing forceAddedWeapon in forceAddedWeapons)
			{
				try
				{
					DropThing(forceAddedWeapon);
				}
				catch
				{
				}
			}
			forceAddedWeapons.Clear();
			forceAddedWeapons = null;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Expected O, but got Unknown
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Expected O, but got Unknown
			//IL_0202: Unknown result type (might be due to invalid IL or missing references)
			//IL_0207: Unknown result type (might be due to invalid IL or missing references)
			//IL_0212: Unknown result type (might be due to invalid IL or missing references)
			//IL_0218: Unknown result type (might be due to invalid IL or missing references)
			//IL_0227: Unknown result type (might be due to invalid IL or missing references)
			//IL_022d: Unknown result type (might be due to invalid IL or missing references)
			//IL_023c: Unknown result type (might be due to invalid IL or missing references)
			//IL_024c: Unknown result type (might be due to invalid IL or missing references)
			//IL_025e: Unknown result type (might be due to invalid IL or missing references)
			//IL_026a: Expected O, but got Unknown
			//IL_026f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0274: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0285: Unknown result type (might be due to invalid IL or missing references)
			//IL_0294: Unknown result type (might be due to invalid IL or missing references)
			//IL_029a: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d7: Expected O, but got Unknown
			//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_010e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0139: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			//IL_0158: Unknown result type (might be due to invalid IL or missing references)
			//IL_017d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0189: Expected O, but got Unknown
			//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0301: Unknown result type (might be due to invalid IL or missing references)
			//IL_0310: Unknown result type (might be due to invalid IL or missing references)
			//IL_0316: Unknown result type (might be due to invalid IL or missing references)
			//IL_0325: Unknown result type (might be due to invalid IL or missing references)
			//IL_0335: Unknown result type (might be due to invalid IL or missing references)
			//IL_0347: Unknown result type (might be due to invalid IL or missing references)
			//IL_0353: Expected O, but got Unknown
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_01df: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fd: Expected O, but got Unknown
			IEnumerable<Gizmo> gizmos = ((Building_Storage)this).GetGizmos();
			List<Gizmo> list = ((gizmos == null) ? new List<Gizmo>(1) : gizmos.ToList());
			int hashCode = "WeaponStorage".GetHashCode();
			if (Settings.EnableAssignWeapons)
			{
				list.Add((Gizmo)new Command_Action
				{
					icon = (Texture)(object)AssignUI.assignweaponsTexture,
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.AssignWeaponsDesc")),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.AssignWeapons")),
					activateSound = SoundDef.Named("Click"),
					action = delegate
					{
						Find.WindowStack.Add((Window)(object)new AssignUI(this));
					},
					groupKey = hashCode
				});
			}
			else
			{
				list.Add((Gizmo)new Command_Action
				{
					icon = (Texture)(object)AssignUI.assignweaponsTexture,
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage")),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage")),
					activateSound = SoundDef.Named("Click"),
					action = delegate
					{
						Find.WindowStack.Add((Window)(object)new AssignUI(this));
					},
					groupKey = hashCode
				});
			}
			hashCode++;
			if (Settings.EnableAssignWeapons)
			{
				list.Add((Gizmo)new Command_Action
				{
					icon = (Texture)(object)AssignUI.assignweaponsTexture,
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.SharedWeaponsDesc")),
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.SharedWeapons")),
					activateSound = SoundDef.Named("Click"),
					action = delegate
					{
						Find.WindowStack.Add((Window)(object)new SharedWeaponsUI());
					},
					groupKey = hashCode
				});
				hashCode++;
				if (CombatExtendedUtil.HasCombatExtended)
				{
					list.Add((Gizmo)new Command_Action
					{
						icon = (Texture)(object)AssignUI.ammoTexture,
						defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.ManageAmmoDesc")),
						defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.ManageAmmo")),
						activateSound = SoundDef.Named("Click"),
						action = delegate
						{
							Find.WindowStack.Add((Window)(object)new AmmoUI(this));
						},
						groupKey = hashCode
					});
					hashCode++;
				}
			}
			list.Add((Gizmo)new Command_Action
			{
				icon = (Texture)(object)AssignUI.emptyTexture,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.EmptyDesc")),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.Empty")),
				activateSound = SoundDef.Named("Click"),
				action = Empty,
				groupKey = hashCode
			});
			hashCode++;
			list.Add((Gizmo)new Command_Action
			{
				icon = (Texture)(object)AssignUI.collectTexture,
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.CollectDesc")),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.Collect")),
				activateSound = SoundDef.Named("Click"),
				action = delegate
				{
					ReclaimWeapons();
				},
				groupKey = hashCode
			});
			hashCode++;
			list.Add((Gizmo)new Command_Action
			{
				icon = (Texture)(object)(includeInTradeDeals ? AssignUI.yesSellTexture : AssignUI.noSellTexture),
				defaultDesc = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.IncludeInTradeDealsDesc")),
				defaultLabel = TaggedString.op_Implicit(Translator.Translate("WeaponStorage.IncludeInTradeDeals")),
				activateSound = SoundDef.Named("Click"),
				action = delegate
				{
					includeInTradeDeals = !includeInTradeDeals;
				},
				groupKey = hashCode
			});
			return list;
		}

		protected bool AreStorageSettingsEqual()
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			ThingFilter filter = base.settings.filter;
			if (filter.AllowedDefCount != previousStorageFilters.AllowedDefCount || filter.AllowedQualityLevels != previousStorageFilters.AllowedQualityLevels || filter.AllowedHitPointsPercents != previousStorageFilters.AllowedHitPointsPercents)
			{
				return false;
			}
			HashSet<ThingDef> hashSet = AllowedDefsFI.GetValue(filter) as HashSet<ThingDef>;
			foreach (ThingDef item in (HashSet<ThingDef>)AllowedDefsFI.GetValue(previousStorageFilters))
			{
				if (hashSet != null && !hashSet.Contains(item))
				{
					return false;
				}
			}
			return true;
		}

		private void UpdatePreviousStorageFilter()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			ThingFilter filter = base.settings.filter;
			previousStorageFilters.AllowedHitPointsPercents = filter.AllowedHitPointsPercents;
			previousStorageFilters.AllowedQualityLevels = filter.AllowedQualityLevels;
			HashSet<ThingDef> obj = AllowedDefsFI.GetValue(previousStorageFilters) as HashSet<ThingDef>;
			obj?.Clear();
			GenCollection.AddRange<ThingDef>(obj, AllowedDefsFI.GetValue(filter) as HashSet<ThingDef>);
		}
	}
	public class CombatExtendedUtil : WorldComponent
	{
		public static Dictionary<ThingDef, int> Ammo;

		private List<ThingDefCount> ac;

		public static bool HasCombatExtended { get; private set; }

		static CombatExtendedUtil()
		{
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			Ammo = new Dictionary<ThingDef, int>();
			HasCombatExtended = false;
			bool flag = false;
			foreach (ModMetaData item in ModsConfig.ActiveModsInLoadOrder)
			{
				if (item.Name.Equals("Combat Extended"))
				{
					HasCombatExtended = true;
				}
				if (item.Name.StartsWith("[KV] Weapon Storage Combat Extended Patch"))
				{
					flag = true;
				}
			}
			if (HasCombatExtended && !flag)
			{
				Log.Error(TaggedString.op_Implicit(Translator.Translate("WeaponStorage.UseWSCEPatch")));
			}
		}

		public CombatExtendedUtil(World world)
			: base(world)
		{
			Ammo?.Clear();
			if (Ammo == null)
			{
				Ammo = new Dictionary<ThingDef, int>();
			}
		}

		public static void SetHasCombatExtended(bool enabled)
		{
			HasCombatExtended = enabled;
		}

		public static bool AddAmmo(ThingDef def, int count)
		{
			int value = ((!Ammo.TryGetValue(def, out value)) ? count : (value + count));
			Ammo[def] = value;
			return true;
		}

		public static bool AddAmmo(Thing ammo)
		{
			if (!IsAmmo(ammo))
			{
				return false;
			}
			if (ammo.Spawned)
			{
				((Entity)ammo).DeSpawn((DestroyMode)0);
			}
			int value = ((!Ammo.TryGetValue(ammo.def, out value)) ? ammo.stackCount : (value + ammo.stackCount));
			Ammo[ammo.def] = value;
			return true;
		}

		internal static void DropAmmo(ThingDef def, Building_WeaponStorage ws)
		{
			DropAllNoUpate(def, GetAmmoCount(def), ws);
			Ammo[def] = 0;
		}

		public static bool TryDropAmmo(ThingDef def, int count, IntVec3 position, Map map)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			Thing droppedAmmo;
			return TryDropAmmo(def, count, position, map, out droppedAmmo);
		}

		public static bool TryDropAmmo(ThingDef def, int count, IntVec3 position, Map map, out Thing droppedAmmo)
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			if (count > 0 && def != null && Ammo.TryGetValue(def, out var value) && value > 0)
			{
				if (value < count)
				{
					count = value;
				}
				value -= count;
				droppedAmmo = MakeAmmo(def, count);
				if (BuildingUtil.DropThing(droppedAmmo, position, map))
				{
					Ammo[def] = value;
					return true;
				}
				Log.Error("Failed to drop " + ((Def)def).defName + " x" + count);
			}
			droppedAmmo = null;
			return false;
		}

		public static bool TryRemoveAmmo(ThingDef def, int count)
		{
			Thing ammo;
			return TryRemoveAmmo(def, count, out ammo, spawn: false);
		}

		public static bool TryRemoveAmmo(ThingDef def, int count, out Thing ammo, bool spawn = true)
		{
			if (count > 0 && def != null && Ammo.TryGetValue(def, out var value) && value > 0)
			{
				if (value < count)
				{
					count = value;
				}
				value -= count;
				Ammo[def] = value;
				ammo = (spawn ? MakeAmmo(def, count) : null);
				return true;
			}
			ammo = null;
			return false;
		}

		public static bool HasAmmo(ThingDef def)
		{
			return GetAmmoCount(def) > 0;
		}

		public static bool IsAmmo(Thing ammo)
		{
			if (HasCombatExtended)
			{
				return ((object)ammo)?.GetType().GetProperty("AmmoDef", BindingFlags.Instance | BindingFlags.NonPublic) != null;
			}
			return false;
		}

		public static int GetAmmoCount(Thing ammo)
		{
			return GetAmmoCount(ammo.def);
		}

		public static int GetAmmoCount(ThingDef def)
		{
			if (HasCombatExtended && def != null && Ammo.TryGetValue(def, out var value))
			{
				return value;
			}
			return 0;
		}

		public static void EmptyAmmo(Building_WeaponStorage ws)
		{
			foreach (KeyValuePair<ThingDef, int> item in Ammo)
			{
				DropAllNoUpate(item.Key, item.Value, ws);
			}
			Ammo.Clear();
		}

		private static void DropAllNoUpate(ThingDef def, int count, Building_WeaponStorage ws)
		{
			while (count > 0)
			{
				int num = Math.Max(def.stackLimit, 1);
				if (num > count)
				{
					num = count;
				}
				BuildingUtil.DropThing(MakeAmmo(def, num), ws, ((Thing)ws).Map);
				count -= num;
			}
		}

		private static Thing MakeAmmo(ThingDef def, int count)
		{
			Thing val = ThingMaker.MakeThing(def, (ThingDef)null);
			val.stackCount = count;
			if (val.stackCount == 0)
			{
				Log.Error(((Entity)val).Label + " has stack count of 0");
			}
			return val;
		}

		internal static List<ThingDefCount> GetThingCounts()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			List<ThingDefCount> list = new List<ThingDefCount>(Ammo.Count);
			foreach (KeyValuePair<ThingDef, int> item in Ammo)
			{
				list.Add(new ThingDefCount(item.Key, item.Value));
			}
			return list;
		}

		public override void ExposeData()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Invalid comparison between Unknown and I4
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Invalid comparison between Unknown and I4
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Invalid comparison between Unknown and I4
			//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Invalid comparison between Unknown and I4
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			((WorldComponent)this).ExposeData();
			if ((int)Scribe.mode == 1)
			{
				ac = GetThingCounts();
			}
			else if ((int)Scribe.mode == 2)
			{
				Ammo.Clear();
				ac?.Clear();
				ac = new List<ThingDefCount>();
			}
			Scribe_Collections.Look<ThingDefCount>(ref ac, "ammo", (LookMode)2, Array.Empty<object>());
			if ((int)Scribe.mode == 4)
			{
				Ammo.Clear();
				foreach (ThingDefCount item in ac)
				{
					ThingDefCount current = item;
					if (((ThingDefCount)(ref current)).Count > 0)
					{
						Ammo.Add(((ThingDefCount)(ref current)).ThingDef, ((ThingDefCount)(ref current)).Count);
					}
				}
			}
			if ((int)Scribe.mode == 1 || (int)Scribe.mode == 4)
			{
				ac?.Clear();
				ac = null;
			}
		}
	}
	[StaticConstructorOnStartup]
	internal class HarmonyPatches
	{
		public struct StoredWeapons(Building_WeaponStorage storage, ThingWithComps weapon)
		{
			public readonly Building_WeaponStorage Storage = storage;

			public readonly ThingWithComps Weapon = weapon;
		}

		public struct WeaponsToUse(List<StoredWeapons> weapons, int count)
		{
			public readonly List<StoredWeapons> Weapons = weapons;

			public readonly int Count = count;
		}

		public class NeededIngrediants(ThingFilter filter, int count)
		{
			public readonly int Count = count;

			public readonly ThingFilter Filter = filter;

			public readonly Dictionary<Def, List<StoredWeapons>> FoundThings = new Dictionary<Def, List<StoredWeapons>>();

			public void Add(StoredWeapons things)
			{
				if (!FoundThings.TryGetValue((Def)(object)((Thing)things.Weapon).def, out var value))
				{
					value = new List<StoredWeapons>();
					FoundThings.Add((Def)(object)((Thing)things.Weapon).def, value);
				}
				value.Add(things);
			}

			public void Clear()
			{
				FoundThings.Clear();
			}

			public bool CountReached()
			{
				foreach (List<StoredWeapons> value in FoundThings.Values)
				{
					if (CountReached(value))
					{
						return true;
					}
				}
				return false;
			}

			private bool CountReached(List<StoredWeapons> l)
			{
				int num = Count;
				foreach (StoredWeapons item in l)
				{
					num -= ((Thing)item.Weapon).stackCount;
				}
				return num <= 0;
			}

			public List<StoredWeapons> GetFoundThings()
			{
				foreach (List<StoredWeapons> value in FoundThings.Values)
				{
					if (CountReached(value))
					{
						return value;
					}
				}
				return null;
			}
		}

		static HarmonyPatches()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			new Harmony("com.weaponstorage.rimworld.mod").PatchAll(Assembly.GetExecutingAssembly());
		}
	}
	public static class HarmonyPatchUtil
	{
		public static void UnequipPrimaryWeapon(Pawn pawn, AssignedWeaponContainer c)
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			object obj;
			if (pawn == null)
			{
				obj = null;
			}
			else
			{
				Pawn_EquipmentTracker equipment = pawn.equipment;
				obj = ((equipment != null) ? equipment.Primary : null);
			}
			ThingWithComps val = (ThingWithComps)obj;
			if (val != null)
			{
				pawn.equipment.Remove(val);
				if (c != null && c.Contains(val))
				{
					c.Add(val);
				}
				else if (!WorldComp.Add(val) && !BuildingUtil.DropSingleThing((Thing)(object)val, ((Thing)pawn).Position, ((Thing)pawn).Map))
				{
					Log.Warning("Failed to drop " + pawn.Name.ToStringShort + "'s primary weapon [" + ((Entity)pawn.equipment.Primary).Label + "].");
				}
			}
		}

		public static void EquipWeapon(ThingWithComps weapon, Pawn pawn)
		{
			if (WorldComp.TryGetAssignedWeapons(pawn, out var aw))
			{
				EquipWeapon(weapon, pawn, aw);
			}
		}

		public static void EquipWeapon(ThingWithComps weapon, Pawn pawn, AssignedWeaponContainer c)
		{
			Pawn_EquipmentTracker equipment = pawn.equipment;
			if (((equipment != null) ? equipment.Primary : null) != weapon)
			{
				UnequipPrimaryWeapon(pawn, c);
				Pawn_EquipmentTracker equipment2 = pawn.equipment;
				if (equipment2 != null)
				{
					equipment2.AddEquipment(weapon);
				}
			}
		}

		internal static bool EquipRanged(AssignedWeaponContainer c)
		{
			foreach (ThingWithComps weapon in c.Weapons)
			{
				if (((Thing)weapon).def.IsRangedWeapon)
				{
					EquipWeapon(weapon, c.Pawn, c);
					return true;
				}
			}
			return false;
		}

		internal static bool EquipMelee(AssignedWeaponContainer c)
		{
			foreach (ThingWithComps weapon in c.Weapons)
			{
				if (((Thing)weapon).def.IsMeleeWeapon)
				{
					EquipWeapon(weapon, c.Pawn, c);
					return true;
				}
			}
			return false;
		}
	}
	public enum PreferredDamageTypeEnum
	{
		WeaponStorage_None,
		ArmorBlunt,
		ArmorSharp
	}
	public class Settings : ModSettings
	{
		private const float FIRST_COLUMN_WIDTH = 250f;

		private const float SECOND_COLUMN_X = 260f;

		private const int DEFAULT_REPAIR_SPEED = 1;

		private const float DEFAULT_REPAIR_UPDATE_INTERVAL = 5f;

		public static bool ShowWeaponsWhenNotDrafted;

		public static bool AutoSwitchMelee = true;

		public static readonly int RepairAttachmentDistance = 6;

		public static PreferredDamageTypeEnum PreferredDamageType = PreferredDamageTypeEnum.ArmorSharp;

		public static int RepairAttachmentMendingSpeed = 1;

		private static string RepairAttachmentMendingSpeedBuffer = 1.ToString();

		public static float RepairAttachmentUpdateInterval = 5f;

		private static string repairAttachmentUpdateIntervalBuffer = 5f.ToString();

		public static bool AllowPawnsToDropWeapon = true;

		public static bool PlaceDroppedWeaponsInStorage = true;

		public static bool ShowWeaponStorageButtonForPawns = true;

		public static bool EnableAssignWeapons = true;

		public static long RepairAttachmentUpdateIntervalTicks => (long)(RepairAttachmentUpdateInterval * 10000000f);

		public override void ExposeData()
		{
			((ModSettings)this).ExposeData();
			Scribe_Values.Look<bool>(ref ShowWeaponsWhenNotDrafted, "WeaponStorage.ShowWeaponsWhenNotDrafted", false, false);
			Scribe_Values.Look<int>(ref RepairAttachmentMendingSpeed, "WeaponStorage.RepairAttachmentHpPerTick", 1, false);
			RepairAttachmentMendingSpeedBuffer = RepairAttachmentMendingSpeed.ToString();
			Scribe_Values.Look<float>(ref RepairAttachmentUpdateInterval, "WeaponStorage.RepairAttachmentUpdateInterval", 5f, false);
			repairAttachmentUpdateIntervalBuffer = $"{RepairAttachmentUpdateInterval:0.0###}";
			Scribe_Values.Look<bool>(ref AutoSwitchMelee, "WeaponStorage.AutoSwitchMelee", true, false);
			Scribe_Values.Look<PreferredDamageTypeEnum>(ref PreferredDamageType, "WeaponStorage.PreferredDamageType", PreferredDamageTypeEnum.ArmorSharp, false);
			Scribe_Values.Look<bool>(ref AllowPawnsToDropWeapon, "WeaponStorage.AllowPawnsToDropWeapon", true, false);
			Scribe_Values.Look<bool>(ref PlaceDroppedWeaponsInStorage, "WeaponStorage.PlaceDroppedWeaponsInStorage", true, false);
			Scribe_Values.Look<bool>(ref ShowWeaponStorageButtonForPawns, "WeaponStorage.ShowButtonForPawns", true, false);
			Scribe_Values.Look<bool>(ref EnableAssignWeapons, "EnableAssignWeapons", true, false);
		}

		public static void DoSettingsWindowContents(Rect rect)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0131: Unknown result type (might be due to invalid IL or missing references)
			//IL_013b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0219: Unknown result type (might be due to invalid IL or missing references)
			//IL_0223: Unknown result type (might be due to invalid IL or missing references)
			//IL_0245: Unknown result type (might be due to invalid IL or missing references)
			//IL_024f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0265: Unknown result type (might be due to invalid IL or missing references)
			//IL_029d: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0311: Unknown result type (might be due to invalid IL or missing references)
			//IL_0368: Unknown result type (might be due to invalid IL or missing references)
			//IL_0346: Unknown result type (might be due to invalid IL or missing references)
			//IL_0350: Expected O, but got Unknown
			//IL_0404: Unknown result type (might be due to invalid IL or missing references)
			//IL_040e: Expected O, but got Unknown
			//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_039d: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a7: Expected O, but got Unknown
			//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_03fe: Expected O, but got Unknown
			float num = 40f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.ShowButtonForPawns"));
			Widgets.Checkbox(new Vector2(260f, num + 4f), ref ShowWeaponStorageButtonForPawns, 24f, false, false, (Texture2D)null, (Texture2D)null);
			num += 32f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.ShowWeaponsWhenNotDrafted"));
			Widgets.Checkbox(new Vector2(260f, num + 4f), ref ShowWeaponsWhenNotDrafted, 24f, false, false, (Texture2D)null, (Texture2D)null);
			num += 32f;
			num += 20f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.RepairAttachmentSettings"));
			num += 32f;
			NumberInput(ref num, "WeaponStorage.SecondsBetweenTicks", ref RepairAttachmentUpdateInterval, ref repairAttachmentUpdateIntervalBuffer, 5f, 0.25f, 120f);
			NumberInput(ref num, "WeaponStorage.HPPerTick", ref RepairAttachmentMendingSpeed, ref RepairAttachmentMendingSpeedBuffer, 1, 1, 60);
			num += 20f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.AllowPawnsToDropWeapon"));
			Widgets.Checkbox(new Vector2(260f, num + 4f), ref AllowPawnsToDropWeapon, 24f, false, false, (Texture2D)null, (Texture2D)null);
			num += 32f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.PlaceDroppedWeaponsInStorage"));
			Widgets.Checkbox(new Vector2(260f, num + 4f), ref PlaceDroppedWeaponsInStorage, 24f, false, false, (Texture2D)null, (Texture2D)null);
			num += 32f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.EnableAssignWeapons"));
			Widgets.Checkbox(new Vector2(260f, num + 4f), ref EnableAssignWeapons, 24f, false, false, (Texture2D)null, (Texture2D)null);
			Widgets.Label(new Rect(310f, num, 200f, 30f), Translator.Translate("WeaponStorage.UnassignAllWeaponsFirst"));
			num += 32f;
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.AutoSwitchMeleeForTarget"));
			Widgets.Checkbox(new Vector2(260f, num + 4f), ref AutoSwitchMelee, 24f, false, false, (Texture2D)null, (Texture2D)null);
			num += 32f;
			if (!AutoSwitchMelee)
			{
				return;
			}
			Widgets.Label(new Rect(0f, num, 250f, 30f), Translator.Translate("WeaponStorage.PreferredDamageType"));
			if (!Widgets.ButtonText(new Rect(260f, num, 100f, 30f), TaggedString.op_Implicit(Translator.Translate(PreferredDamageType.ToString())), true, true, true, (TextAnchor?)null))
			{
				return;
			}
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			if (PreferredDamageType != PreferredDamageTypeEnum.WeaponStorage_None)
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate(PreferredDamageTypeEnum.WeaponStorage_None.ToString())), (Action)delegate
				{
					PreferredDamageType = PreferredDamageTypeEnum.WeaponStorage_None;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			if (PreferredDamageType != PreferredDamageTypeEnum.ArmorBlunt)
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate(PreferredDamageTypeEnum.ArmorBlunt.ToString())), (Action)delegate
				{
					PreferredDamageType = PreferredDamageTypeEnum.ArmorBlunt;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			if (PreferredDamageType != PreferredDamageTypeEnum.ArmorSharp)
			{
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate(PreferredDamageTypeEnum.ArmorSharp.ToString())), (Action)delegate
				{
					PreferredDamageType = PreferredDamageTypeEnum.ArmorSharp;
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}

		public static bool IsTool(Thing t)
		{
			if (t != null)
			{
				return IsTool(t.def);
			}
			return false;
		}

		public static bool IsTool(ThingDef def)
		{
			return ((Def)(def?)).defName.StartsWith("RTFTJ") ?? false;
		}

		private static void NumberInput(ref float y, string label, ref float val, ref string buffer, float defaultVal, float min, float max)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Widgets.Label(new Rect(20f, y, 250f, 30f), Translator.Translate(label));
				Widgets.TextFieldNumeric<float>(new Rect(280f, y, 50f, 30f), ref val, ref buffer, min, max);
				if (Widgets.ButtonText(new Rect(340f, y, 100f, 30f), TaggedString.op_Implicit(Translator.Translate("ResetButton")), true, true, true, (TextAnchor?)null))
				{
					val = defaultVal;
					buffer = $"{defaultVal:0.0###}";
				}
			}
			catch
			{
				val = min;
				buffer = $"{min:0.0###}";
			}
			y += 32f;
		}

		private static void NumberInput(ref float y, string label, ref int val, ref string buffer, int defaultVal, int min, int max)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Widgets.Label(new Rect(20f, y, 250f, 30f), Translator.Translate(label));
				Widgets.TextFieldNumeric<int>(new Rect(280f, y, 50f, 30f), ref val, ref buffer, (float)min, (float)max);
				if (Widgets.ButtonText(new Rect(340f, y, 100f, 30f), TaggedString.op_Implicit(Translator.Translate("ResetButton")), true, true, true, (TextAnchor?)null))
				{
					val = defaultVal;
					buffer = defaultVal.ToString();
				}
			}
			catch
			{
				val = min;
				buffer = min.ToString();
			}
			y += 32f;
		}
	}
	public class SettingsController : Mod
	{
		public SettingsController(ModContentPack content)
			: base(content)
		{
			((Mod)this).GetSettings<Settings>();
		}

		public override string SettingsCategory()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("WeaponStorage"));
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			Settings.DoSettingsWindowContents(inRect);
		}
	}
	public class SharedWeaponFilter : IExposable
	{
		private readonly HashSet<ThingDef> foundDefCache = new HashSet<ThingDef>();

		public HashSet<ThingDef> AllowedDefs = new HashSet<ThingDef>();

		public HashSet<Pawn> AssignedPawns = new HashSet<Pawn>();

		public FloatRange HpRange = new FloatRange(0f, 1f);

		public string Label;

		private long lastCacheUpdate;

		public QualityRange QualityRange = QualityRange.All;

		public void ExposeData()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Invalid comparison between Unknown and I4
			Scribe_Values.Look<string>(ref Label, "label", "", false);
			Scribe_Collections.Look<ThingDef>(ref AllowedDefs, "allowedDefs", (LookMode)4);
			Scribe_Values.Look<FloatRange>(ref HpRange, "hpRange", default(FloatRange), false);
			Scribe_Values.Look<QualityRange>(ref QualityRange, "qalityRange", default(QualityRange), false);
			Scribe_Collections.Look<Pawn>(ref AssignedPawns, false, "assignedPawns", (LookMode)3);
			if ((int)Scribe.mode == 4 && AssignedPawns == null)
			{
				AssignedPawns = new HashSet<Pawn>();
			}
		}

		public bool Allows(ThingWithComps t)
		{
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			QualityCategory val = default(QualityCategory);
			if (!QualityUtility.TryGetQuality((Thing)(object)t, ref val))
			{
				return (float)((Thing)t).HitPoints >= HpRange.min * (float)((Thing)t).def.BaseMaxHitPoints;
			}
			if (val >= QualityRange.min && val <= QualityRange.max)
			{
				return (float)((Thing)t).HitPoints >= HpRange.min * (float)((Thing)t).def.BaseMaxHitPoints;
			}
			return false;
		}

		public bool FoundDefCacheContains(ThingDef d)
		{
			return foundDefCache.Contains(d);
		}

		public void UpdateFoundDefCache()
		{
			long ticks = DateTime.Now.Ticks;
			if (ticks - lastCacheUpdate <= 10000000)
			{
				return;
			}
			lastCacheUpdate = ticks;
			foundDefCache.Clear();
			foreach (ThingDef allowedDef in AllowedDefs)
			{
				if (CombatExtendedUtil.GetAmmoCount(allowedDef) > 0)
				{
					foundDefCache.Add(allowedDef);
					continue;
				}
				foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(null))
				{
					if (weaponStorage.HasWeapon(this, allowedDef))
					{
						foundDefCache.Add(allowedDef);
						break;
					}
				}
			}
		}

		public void UpdateDefCache(ThingDef def)
		{
			if (AllowedDefs.Contains(def))
			{
				if (CombatExtendedUtil.GetAmmoCount(def) > 0)
				{
					foundDefCache.Add(def);
					return;
				}
				foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(null))
				{
					if (weaponStorage.HasWeapon(this, def))
					{
						foundDefCache.Add(def);
						return;
					}
				}
			}
			foundDefCache.Remove(def);
		}
	}
	internal static class TradeUtil
	{
		public static IEnumerable<Thing> EmptyWeaponStorages(Map map)
		{
			List<Thing> list = new List<Thing>();
			foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(map))
			{
				if (((Thing)weaponStorage).Map != map || !((Thing)weaponStorage).Spawned || !weaponStorage.IncludeInTradeDeals)
				{
					continue;
				}
				foreach (ThingWithComps weapon in weaponStorage.GetWeapons(includeBioencoded: true))
				{
					list.Add((Thing)(object)weapon);
				}
				weaponStorage.Empty();
			}
			return list;
		}

		public static void ReclaimWeapons()
		{
			foreach (Building_WeaponStorage weaponStorage in WorldComp.GetWeaponStorages(null))
			{
				if (((Thing)weaponStorage).Map != null && ((Thing)weaponStorage).Spawned)
				{
					weaponStorage.ReclaimWeapons();
				}
			}
		}
	}
	public class WorldComp : WorldComponent
	{
		public static List<Building_WeaponStorage> WeaponStoragesToUse;

		private static readonly Dictionary<Pawn, AssignedWeaponContainer> AssignedWeapons;

		public static List<SharedWeaponFilter> SharedWeaponFilter;

		public static readonly Stack<ThingWithComps> WeaponsToDrop;

		private List<AssignedWeaponContainer> tmp;

		private static ThingDef WeaponStorageDef { get; set; }

		public static IEnumerable<AssignedWeaponContainer> AssignedWeaponContainers
		{
			get
			{
				if (!Settings.EnableAssignWeapons)
				{
					return new List<AssignedWeaponContainer>(0);
				}
				return AssignedWeapons.Values;
			}
		}

		static WorldComp()
		{
			WeaponStoragesToUse = new List<Building_WeaponStorage>();
			AssignedWeapons = new Dictionary<Pawn, AssignedWeaponContainer>();
			SharedWeaponFilter = new List<SharedWeaponFilter>();
			WeaponsToDrop = new Stack<ThingWithComps>();
			WeaponStorageDef = null;
		}

		public WorldComp(World world)
			: base(world)
		{
			if (WeaponStorageDef == null)
			{
				ThingDef val = null;
				List<ThingDef> list = new List<ThingDef>();
				foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
				{
					if (((Def)allDef).defName.Equals("WeaponStorage"))
					{
						val = allDef;
						WeaponStorageDef = allDef;
					}
					else if (allDef.IsWeapon)
					{
						list.Add(allDef);
					}
				}
				foreach (ThingDef item in list)
				{
					if (val != null)
					{
						val.building.fixedStorageSettings.filter.SetAllow(item, true);
					}
					bool flag = !((Def)item).defName.Equals("Beer") && !item.IsStuff;
					if (val != null)
					{
						val.building.defaultStorageSettings.filter.SetAllow(item, flag);
					}
				}
				if (val != null)
				{
					val.building.fixedStorageSettings.filter.RecalculateDisplayRootCategory();
				}
				if (val != null)
				{
					val.building.defaultStorageSettings.filter.RecalculateDisplayRootCategory();
				}
				if (WeaponStorageDef == null)
				{
					Log.Error("Unabled to find WeaponStorageDef");
				}
			}
			foreach (AssignedWeaponContainer value in AssignedWeapons.Values)
			{
				value.Clear();
			}
			AssignedWeapons.Clear();
			SharedWeaponFilter.Clear();
			if (WeaponStoragesToUse == null)
			{
				WeaponStoragesToUse = new List<Building_WeaponStorage>();
			}
			WeaponStoragesToUse.Clear();
		}

		public static void Add(Building_WeaponStorage ws)
		{
			if (((ws != null) ? ((Thing)ws).Map : null) == null)
			{
				Log.Error("Cannot add WeaponStorage that is either null or has a null map.");
			}
			else if (!WeaponStoragesToUse.Contains(ws))
			{
				WeaponStoragesToUse.Add(ws);
			}
		}

		public static bool Add(ThingWithComps t)
		{
			if (t == null)
			{
				return false;
			}
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (item.AddWeapon(t))
				{
					return true;
				}
			}
			return false;
		}

		public static bool CanAdd(ThingWithComps t)
		{
			if (t == null)
			{
				return false;
			}
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (item.CanAdd(t))
				{
					return true;
				}
			}
			return false;
		}

		public static bool TryRemoveWeapon(ThingDef def, SharedWeaponFilter filter, bool includeBioencoded, out ThingWithComps weapon)
		{
			if (def != null)
			{
				if (CombatExtendedUtil.TryRemoveAmmo(def, 1, out var ammo))
				{
					weapon = (ThingWithComps)(object)((ammo is ThingWithComps) ? ammo : null);
					if (weapon != null)
					{
						return true;
					}
				}
				foreach (Building_WeaponStorage item in WeaponStoragesToUse)
				{
					if (item.TryRemoveWeapon(def, filter, includeBioencoded, out weapon))
					{
						return true;
					}
				}
			}
			weapon = null;
			return false;
		}

		public static void Drop(ThingWithComps w)
		{
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (BuildingUtil.DropThing((Thing)(object)w, item, ((Thing)item).Map))
				{
					break;
				}
			}
		}

		public static List<Building_WeaponStorage> GetWeaponStorages()
		{
			List<Building_WeaponStorage> list = new List<Building_WeaponStorage>(WeaponStoragesToUse.Count);
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (((Thing)item).Spawned)
				{
					list.Add(item);
				}
			}
			return list;
		}

		public static IEnumerable<Building_WeaponStorage> GetWeaponStorages(Map map)
		{
			if (WeaponStoragesToUse == null)
			{
				yield break;
			}
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (map == null || (((Thing)item).Spawned && ((Thing)item).Map == map))
				{
					yield return item;
				}
			}
		}

		public static bool HasStorages()
		{
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (((Thing)item).Spawned)
				{
					return true;
				}
			}
			return false;
		}

		public static bool HasStorages(Map map)
		{
			foreach (Building_WeaponStorage item in WeaponStoragesToUse)
			{
				if (((Thing)item).Spawned && ((Thing)item).Map == map)
				{
					return true;
				}
			}
			return false;
		}

		public static void Remove(Building_WeaponStorage ws)
		{
			WeaponStoragesToUse.Remove(ws);
		}

		public static void Remove(Map map)
		{
			for (int num = WeaponStoragesToUse.Count - 1; num >= 0; num--)
			{
				if (((Thing)WeaponStoragesToUse[num]).Map == map)
				{
					WeaponStoragesToUse.RemoveAt(num);
				}
			}
		}

		public override void ExposeData()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Invalid comparison between Unknown and I4
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Invalid comparison between Unknown and I4
			//IL_0169: Unknown result type (might be due to invalid IL or missing references)
			//IL_016f: Invalid comparison between Unknown and I4
			((WorldComponent)this).ExposeData();
			if ((int)Scribe.mode == 1)
			{
				tmp = AssignedWeapons.Values.ToList();
			}
			Scribe_Collections.Look<AssignedWeaponContainer>(ref tmp, "assignedWeapons", (LookMode)2, Array.Empty<object>());
			Scribe_Collections.Look<SharedWeaponFilter>(ref SharedWeaponFilter, "sharedWeaponFilter", (LookMode)2, Array.Empty<object>());
			if ((int)Scribe.mode == 4)
			{
				foreach (AssignedWeaponContainer item in tmp)
				{
					if (!Settings.EnableAssignWeapons)
					{
						if (item.Weapons == null)
						{
							continue;
						}
						foreach (ThingWithComps weapon in item.Weapons)
						{
							if (!Add(weapon))
							{
								WeaponsToDrop.Push(weapon);
							}
						}
					}
					else if (item.Pawn == null || item.Pawn.Dead)
					{
						Log.Warning($"Unable to load pawn [{item.Pawn}]. Re-storing assigned weapons");
						if (item.Weapons == null)
						{
							continue;
						}
						foreach (ThingWithComps weapon2 in item.Weapons)
						{
							if (!Add(weapon2))
							{
								WeaponsToDrop.Push(weapon2);
							}
						}
					}
					else
					{
						AssignedWeapons.Add(item.Pawn, item);
					}
				}
			}
			if ((int)Scribe.mode == 1 || (int)Scribe.mode == 4)
			{
				if (tmp != null)
				{
					tmp.Clear();
					tmp = null;
				}
				if (SharedWeaponFilter == null)
				{
					SharedWeaponFilter = new List<SharedWeaponFilter>();
				}
			}
		}

		public static void SortWeaponStoragesToUse()
		{
			WeaponStoragesToUse.Sort((Building_WeaponStorage l, Building_WeaponStorage r) => ((Enum)((Building_Storage)l).settings.Priority/*cast due to constrained. prefix*/).CompareTo((object)((Building_Storage)r).settings.Priority));
		}

		public static bool CreateOrGetAssignedWeapons(Pawn pawn, out AssignedWeaponContainer aw)
		{
			if (!Settings.EnableAssignWeapons)
			{
				aw = null;
				return false;
			}
			if (AssignedWeapons.TryGetValue(pawn, out aw))
			{
				return true;
			}
			aw = new AssignedWeaponContainer();
			AssignedWeapons.Add(pawn, aw);
			return true;
		}

		public static void AddAssignedWeapons(Pawn pawn, AssignedWeaponContainer aw)
		{
			if (Settings.EnableAssignWeapons)
			{
				AssignedWeapons[pawn] = aw;
			}
		}

		public static bool TryGetAssignedWeapons(Pawn pawn, out AssignedWeaponContainer aw)
		{
			if (Settings.EnableAssignWeapons)
			{
				return AssignedWeapons.TryGetValue(pawn, out aw);
			}
			aw = null;
			return false;
		}

		public static void RemoveAssignedWeapons(Pawn pawn)
		{
			AssignedWeapons.Remove(pawn);
		}

		public static void InitializeAssignedWeapons()
		{
			if (!Settings.EnableAssignWeapons || AssignedWeapons.Count != 0)
			{
				return;
			}
			foreach (SelectablePawns pawn in Util.GetPawns(excludeNonViolent: true))
			{
				AssignedWeaponContainer assignedWeaponContainer = new AssignedWeaponContainer
				{
					Pawn = pawn.Pawn
				};
				if (pawn.Pawn.equipment.Primary != null)
				{
					assignedWeaponContainer.Add(pawn.Pawn.equipment.Primary);
				}
				AssignedWeapons.Add(pawn.Pawn, assignedWeaponContainer);
			}
		}

		public static void ClearAll()
		{
			WeaponStoragesToUse.Clear();
			AssignedWeapons.Clear();
			SharedWeaponFilter.Clear();
			WeaponsToDrop.Clear();
		}
	}
}
namespace WeaponStorage.UI
{
	[StaticConstructorOnStartup]
	public class AmmoUI : Window
	{
		private readonly List<ThingDefCount> ammo = new List<ThingDefCount>();

		private readonly List<ThingDefCount> searchResults = new List<ThingDefCount>();

		private readonly Building_WeaponStorage weaponStorage;

		private bool performSearch;

		private float PreviousY;

		private Vector2 scrollPosition = new Vector2(0f, 0f);

		private string searchText = "";

		public override Vector2 InitialSize => new Vector2(650f, 600f);

		public AmmoUI(Building_WeaponStorage weaponStorage)
			: base((IWindowDrawing)null)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			this.weaponStorage = weaponStorage;
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			RebuildItems();
		}

		private void RebuildItems()
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			ammo.Clear();
			foreach (ThingDefCount thingCount in CombatExtendedUtil.GetThingCounts())
			{
				ammo.Add(thingCount);
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0322: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0124: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0223: Unknown result type (might be due to invalid IL or missing references)
			//IL_025a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01de: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e8: Expected O, but got Unknown
			GUI.color = Color.white;
			Text.Font = (GameFont)1;
			try
			{
				float num = 0f;
				Widgets.Label(new Rect(0f, num, 100f, 30f), Translator.Translate("WeaponStorage.ManageAmmo"));
				num += 32f;
				Widgets.Label(new Rect(0f, num + 4f, 70f, 30f), Translator.Translate("WeaponStorage.Search"));
				string value = searchText;
				searchText = Widgets.TextField(new Rect(75f, num, 150f, 30f), searchText);
				if (!searchText.Equals(value))
				{
					performSearch = true;
				}
				num += 44f;
				List<ThingDefCount> thingsToShow = GetThingsToShow();
				if (thingsToShow == null || thingsToShow.Count == 0)
				{
					string text = "No ammo in storage";
					if (searchText.Length > 0)
					{
						text = "No matches found";
					}
					Widgets.Label(new Rect(40f, num, 200f, 30f), text);
					return;
				}
				Widgets.BeginScrollView(new Rect(40f, num, ((Rect)(ref inRect)).width - 80f, ((Rect)(ref inRect)).height - num - 50f), ref scrollPosition, new Rect(0f, 0f, ((Rect)(ref inRect)).width - 96f, PreviousY), true);
				PreviousY = 0f;
				foreach (ThingDefCount item in thingsToShow)
				{
					ThingDefCount current = item;
					ThingDef thingDef = ((ThingDefCount)(ref current)).ThingDef;
					Widgets.ThingIcon(new Rect(0f, PreviousY, 30f, 30f), thingDef, (ThingDef)null, (ThingStyleDef)null, 1f, (Color?)null, (int?)null, 1f);
					if (Widgets.InfoCardButton(40f, PreviousY, (Def)(object)thingDef))
					{
						Find.WindowStack.Add((Window)new Dialog_InfoCard((Def)(object)thingDef, (Precept_ThingStyle)null));
					}
					Widgets.Label(new Rect(70f, PreviousY, 250f, 30f), ((Def)thingDef).label);
					Widgets.Label(new Rect(340f, PreviousY, 40f, 30f), ((ThingDefCount)(ref current)).Count.ToString());
					if (Widgets.ButtonImage(new Rect(((Rect)(ref inRect)).width - 100f, PreviousY, 20f, 20f), AssignUI.DropTexture, true, (string)null))
					{
						CombatExtendedUtil.DropAmmo(thingDef, weaponStorage);
						RebuildItems();
						performSearch = true;
						break;
					}
					PreviousY += 32f;
				}
				Widgets.EndScrollView();
			}
			catch (Exception ex)
			{
				string text2 = ((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message;
				Log.Error(text2);
				Messages.Message(text2, MessageTypeDefOf.NegativeEvent, true);
				((Window)this).Close(true);
			}
			finally
			{
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
			}
		}

		private List<ThingDefCount> GetThingsToShow()
		{
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			if (performSearch)
			{
				performSearch = false;
				searchResults.Clear();
				if (!GenText.NullOrEmpty(searchText.Trim()))
				{
					string value = searchText.ToLower().Trim();
					foreach (ThingDefCount item in ammo)
					{
						ThingDefCount current = item;
						if (((Def)((ThingDefCount)(ref current)).ThingDef).label.ToLower().Contains(value) || ((Def)((ThingDefCount)(ref current)).ThingDef).defName.ToLower().Contains(value))
						{
							searchResults.Add(current);
						}
					}
				}
			}
			if (searchResults.Count > 0)
			{
				return searchResults;
			}
			if (!GenText.NullOrEmpty(searchText.Trim()))
			{
				return null;
			}
			return ammo;
		}
	}
	[StaticConstructorOnStartup]
	public class AssignUI : Window
	{
		public static readonly Texture2D DropTexture;

		public static Texture2D UnknownWeaponIcon;

		public static readonly Texture2D assignweaponsTexture;

		public static readonly Texture2D emptyTexture;

		public static readonly Texture2D collectTexture;

		public static readonly Texture2D yesSellTexture;

		public static readonly Texture2D noSellTexture;

		public static readonly Texture2D meleeTexture;

		public static readonly Texture2D rangedTexture;

		public static readonly Texture2D ammoTexture;

		public static readonly Texture2D nextTexture;

		public static readonly Texture2D previousTexture;

		public static readonly Texture2D weaponStorageTexture;

		private readonly List<SelectablePawns> selectablePawns;

		private AssignedWeaponContainer assignedWeapons;

		private int pawnIndex = -1;

		private List<ThingWithComps> PossibleWeapons;

		private float PreviousY;

		private Vector2 scrollPosition = new Vector2(0f, 0f);

		private string textBuffer = "";

		private Building_WeaponStorage weaponStorage;

		public override Vector2 InitialSize => new Vector2(650f, 600f);

		static AssignUI()
		{
			DropTexture = ContentFinder<Texture2D>.Get("UI/drop", true);
			UnknownWeaponIcon = ContentFinder<Texture2D>.Get("UI/UnknownWeapon", true);
			assignweaponsTexture = ContentFinder<Texture2D>.Get("UI/assignweapons", true);
			emptyTexture = ContentFinder<Texture2D>.Get("UI/empty", true);
			collectTexture = ContentFinder<Texture2D>.Get("UI/collect", true);
			yesSellTexture = ContentFinder<Texture2D>.Get("UI/yessell", true);
			noSellTexture = ContentFinder<Texture2D>.Get("UI/nosell", true);
			meleeTexture = ContentFinder<Texture2D>.Get("UI/melee", true);
			rangedTexture = ContentFinder<Texture2D>.Get("UI/ranged", true);
			ammoTexture = ContentFinder<Texture2D>.Get("UI/ammo", true);
			nextTexture = ContentFinder<Texture2D>.Get("UI/next", true);
			previousTexture = ContentFinder<Texture2D>.Get("UI/previous", true);
			weaponStorageTexture = ContentFinder<Texture2D>.Get("UI/weaponstorage", true);
		}

		public AssignUI(Building_WeaponStorage weaponStorage, Pawn pawn = null)
			: base((IWindowDrawing)null)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			this.weaponStorage = weaponStorage;
			PossibleWeapons = null;
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			selectablePawns = Util.GetPawns(excludeNonViolent: true);
			UpdatePawnIndex(pawn);
		}

		private void RebuildPossibleWeapons()
		{
			if (PossibleWeapons != null)
			{
				PossibleWeapons.Clear();
				PossibleWeapons = null;
			}
			int num = weaponStorage?.Count ?? 0;
			if (assignedWeapons != null)
			{
				num += assignedWeapons.Count;
			}
			PossibleWeapons = new List<ThingWithComps>(num);
			if (assignedWeapons != null)
			{
				foreach (ThingWithComps weapon in assignedWeapons.Weapons)
				{
					PossibleWeapons.Add(weapon);
				}
			}
			if (weaponStorage == null)
			{
				return;
			}
			foreach (ThingWithComps weapon2 in weaponStorage.GetWeapons(includeBioencoded: false))
			{
				PossibleWeapons.Add(weapon2);
			}
			foreach (ThingWithComps bioEncodedWeapon in weaponStorage.GetBioEncodedWeapons())
			{
				PossibleWeapons.Add(bioEncodedWeapon);
			}
		}

		private bool IsAssignedWeapon(int i)
		{
			if (assignedWeapons != null)
			{
				return i < assignedWeapons.Count;
			}
			return false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a59: Unknown result type (might be due to invalid IL or missing references)
			//IL_021f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0295: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0390: Unknown result type (might be due to invalid IL or missing references)
			//IL_039a: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0419: Unknown result type (might be due to invalid IL or missing references)
			//IL_041e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0423: Unknown result type (might be due to invalid IL or missing references)
			//IL_035a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_030b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0315: Expected O, but got Unknown
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			//IL_016c: Expected O, but got Unknown
			//IL_0335: Unknown result type (might be due to invalid IL or missing references)
			//IL_033f: Expected O, but got Unknown
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0196: Expected O, but got Unknown
			//IL_05f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_07cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_064d: Unknown result type (might be due to invalid IL or missing references)
			//IL_062f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0639: Expected O, but got Unknown
			//IL_0828: Unknown result type (might be due to invalid IL or missing references)
			//IL_0853: Unknown result type (might be due to invalid IL or missing references)
			//IL_080a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0814: Expected O, but got Unknown
			//IL_0689: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_0925: Unknown result type (might be due to invalid IL or missing references)
			//IL_0950: Unknown result type (might be due to invalid IL or missing references)
			//IL_0907: Unknown result type (might be due to invalid IL or missing references)
			//IL_0911: Expected O, but got Unknown
			//IL_0735: Unknown result type (might be due to invalid IL or missing references)
			//IL_0562: Unknown result type (might be due to invalid IL or missing references)
			//IL_09ac: Unknown result type (might be due to invalid IL or missing references)
			GUI.color = Color.white;
			Text.Font = (GameFont)1;
			try
			{
				float num = 0f;
				float num2 = 0f;
				string text;
				if (Settings.EnableAssignWeapons)
				{
					Widgets.Label(new Rect(num, num2 + 4f, 100f, 30f), Translator.Translate("WeaponStorage.AssignTo"));
					num += 80f;
					if (selectablePawns.Count > 0 && GUI.Button(new Rect(num, num2, 30f, 30f), (Texture)(object)previousTexture))
					{
						pawnIndex--;
						if (pawnIndex < 0 || assignedWeapons == null)
						{
							pawnIndex = selectablePawns.Count - 1;
						}
						LoadAssignedWeapons();
					}
					num += 30f;
					text = ((assignedWeapons != null) ? SelectablePawns.GetLabelAndStatsFor(assignedWeapons.Pawn) : "");
					if (Widgets.ButtonText(new Rect(num, num2, 400f, 30f), text, true, true, true, (TextAnchor?)null))
					{
						List<FloatMenuOption> list = new List<FloatMenuOption>();
						foreach (SelectablePawns p in selectablePawns)
						{
							list.Add(new FloatMenuOption(p.LabelAndStats, (Action)delegate
							{
								UpdatePawnIndex(p.Pawn);
							}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
						}
						Find.WindowStack.Add((Window)new FloatMenu(list));
					}
					num += 400f;
					if (selectablePawns.Count > 0 && GUI.Button(new Rect(num, num2, 30f, 30f), (Texture)(object)nextTexture))
					{
						pawnIndex++;
						if (pawnIndex >= selectablePawns.Count || assignedWeapons == null)
						{
							pawnIndex = 0;
						}
						LoadAssignedWeapons();
					}
					num2 += 40f;
				}
				num = 0f;
				Widgets.Label(new Rect(num, num2 - 4f, 100f, 60f), Translator.Translate("WeaponStorage"));
				num += 80f;
				if (WorldComp.HasStorages() && GUI.Button(new Rect(num, num2, 30f, 30f), (Texture)(object)previousTexture))
				{
					NextWeaponStorage(-1);
				}
				num += 30f;
				text = ((weaponStorage != null) ? ((Entity)weaponStorage).Label : "");
				if (Widgets.ButtonText(new Rect(num, num2, 400f, 30f), text, true, true, true, (TextAnchor?)null))
				{
					List<FloatMenuOption> list2 = new List<FloatMenuOption>();
					foreach (Building_WeaponStorage ws in WorldComp.GetWeaponStorages())
					{
						list2.Add(new FloatMenuOption(((Entity)ws).Label, (Action)delegate
						{
							weaponStorage = ws;
							RebuildPossibleWeapons();
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
					}
					Find.WindowStack.Add((Window)new FloatMenu(list2));
				}
				num += 400f;
				if (WorldComp.HasStorages() && GUI.Button(new Rect(num, num2, 30f, 30f), (Texture)(object)nextTexture))
				{
					NextWeaponStorage(1);
				}
				num2 += 40f;
				Widgets.Label(new Rect(0f, num2 + 4f, 70f, 30f), Translator.Translate("WeaponStorage.Search"));
				textBuffer = Widgets.TextField(new Rect(80f, num2, 200f, 30f), textBuffer);
				num2 += 40f;
				float num3 = ((Rect)(ref inRect)).width - 100f;
				scrollPosition = GUI.BeginScrollView(new Rect(40f, num2, num3, ((Rect)(ref inRect)).height - num2 - 121f), scrollPosition, new Rect(0f, 0f, num3 - 16f, PreviousY));
				num2 = 0f;
				if (PossibleWeapons != null)
				{
					for (int num4 = 0; num4 < PossibleWeapons.Count; num4++)
					{
						num = 0f;
						ThingWithComps val = PossibleWeapons[num4];
						bool flag = false;
						if (assignedWeapons != null && weaponStorage != null)
						{
							flag = IsAssignedWeapon(num4);
							if (!flag && !IncludeWeapon(val))
							{
								continue;
							}
							bool flag2 = flag;
							Widgets.Checkbox(num, num2, ref flag, 20f, false, false, (Texture2D)null, (Texture2D)null);
							num += 22f;
							if (flag != flag2)
							{
								if (IsAssignedWeapon(num4))
								{
									if (assignedWeapons.Pawn.equipment.Primary == val)
									{
										assignedWeapons.Pawn.equipment.Remove(val);
										if (assignedWeapons.Pawn.jobs.curJob.def == JobDefOf.Hunt)
										{
											assignedWeapons.Pawn.jobs.StopAll(false, true);
										}
									}
									if (assignedWeapons.Remove(val))
									{
										if (weaponStorage == null || (!weaponStorage.AddWeapon(val) && !WorldComp.Add(val)))
										{
											BuildingUtil.DropSingleThing((Thing)(object)val, ((Thing)assignedWeapons.Pawn).Position, ((Thing)assignedWeapons.Pawn).Map);
										}
									}
									else
									{
										Log.Error($"Unable to remove weapon {val}");
									}
								}
								else if (weaponStorage != null && weaponStorage.RemoveNoDrop(val))
								{
									assignedWeapons.Add(val);
								}
								else
								{
									Log.Error($"Unable to remove weapon {val}");
								}
								RebuildPossibleWeapons();
								break;
							}
						}
						if (!flag && !IncludeWeapon(val))
						{
							continue;
						}
						Widgets.ThingIcon(new Rect(num, num2, 30f, 30f), (Thing)(object)val, 1f, (Rot4?)null, false, 1f, false);
						num += 32f;
						if (Widgets.InfoCardButton(num, num2, (Thing)(object)val))
						{
							Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)val, (Precept_ThingStyle)null));
						}
						num += 32f;
						Widgets.Label(new Rect(num, num2, 250f, 30f), ((Entity)val).Label);
						num += 252f;
						if (weaponStorage != null && Widgets.ButtonImage(new Rect(num3 - 16f - 30f, num2, 20f, 20f), DropTexture, true, (string)null))
						{
							if (IsAssignedWeapon(num4))
							{
								if (assignedWeapons != null && !assignedWeapons.Remove(val))
								{
									Log.Error("Unable to drop assigned weapon");
								}
							}
							else if (weaponStorage == null || !weaponStorage.Remove(val))
							{
								Log.Error($"Unable to remove weapon {val}");
							}
							RebuildPossibleWeapons();
							break;
						}
						CompBiocodable comp = val.GetComp<CompBiocodable>();
						if (((comp != null) ? comp.CodedPawn : null) != null)
						{
							num2 += 26f;
							Widgets.Label(new Rect(num - 250f - 2f, num2, 250f, 20f), ((ThingComp)comp).CompInspectStringExtra());
							num2 += 4f;
						}
						PossibleWeapons[num4] = val;
						num2 += 32f;
					}
				}
				else if (weaponStorage != null)
				{
					foreach (ThingWithComps weapon in weaponStorage.GetWeapons(includeBioencoded: false))
					{
						if (IncludeWeapon(weapon))
						{
							num = 34f;
							Widgets.ThingIcon(new Rect(num, num2, 30f, 30f), (Thing)(object)weapon, 1f, (Rot4?)null, false, 1f, false);
							num += 32f;
							if (Widgets.InfoCardButton(num, num2, (Thing)(object)weapon))
							{
								Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)weapon, (Precept_ThingStyle)null));
							}
							num += 32f;
							Widgets.Label(new Rect(num, num2, 250f, 30f), ((Entity)weapon).Label);
							num += 252f;
							if (Widgets.ButtonImage(new Rect(num + 100f, num2, 20f, 20f), DropTexture, true, (string)null))
							{
								weaponStorage.Remove(weapon);
								break;
							}
							num2 += 32f;
						}
					}
					foreach (ThingWithComps bioEncodedWeapon in weaponStorage.GetBioEncodedWeapons())
					{
						num = 34f;
						Widgets.ThingIcon(new Rect(num, num2, 30f, 30f), (Thing)(object)bioEncodedWeapon, 1f, (Rot4?)null, false, 1f, false);
						num += 32f;
						if (Widgets.InfoCardButton(num, num2, (Thing)(object)bioEncodedWeapon))
						{
							Find.WindowStack.Add((Window)new Dialog_InfoCard((Thing)(object)bioEncodedWeapon, (Precept_ThingStyle)null));
						}
						num += 32f;
						Widgets.Label(new Rect(num, num2, 250f, 30f), ((Entity)bioEncodedWeapon).Label);
						num += 252f;
						if (Widgets.ButtonImage(new Rect(num + 100f, num2, 20f, 20f), DropTexture, true, (string)null))
						{
							weaponStorage.Remove(bioEncodedWeapon);
							break;
						}
						CompBiocodable comp2 = bioEncodedWeapon.GetComp<CompBiocodable>();
						if (((comp2 != null) ? comp2.CodedPawn : null) != null)
						{
							num2 += 26f;
							Widgets.Label(new Rect(num - 250f - 2f, num2, 250f, 20f), ((ThingComp)comp2).CompInspectStringExtra());
							num2 += 4f;
						}
						num2 += 32f;
					}
				}
				GUI.EndScrollView();
				PreviousY = num2;
			}
			catch (Exception ex)
			{
				string text2 = ((object)this).GetType().Name + " closed due to: " + ex.GetType().Name + " " + ex.Message;
				Log.Error(text2);
				Messages.Message(text2, MessageTypeDefOf.NegativeEvent, true);
				((Window)this).Close(true);
			}
			finally
			{
				Text.Anchor = (TextAnchor)0;
				GUI.color = Color.white;
			}
		}

		private void NextWeaponStorage(int increment)
		{
			List<Building_WeaponStorage> weaponStorages = WorldComp.GetWeaponStorages();
			if (weaponStorage == null)
			{
				weaponStorage = weaponStorages[(increment < 0) ? (weaponStorages.Count - 1) : 0];
			}
			else
			{
				for (int i = 0; i < weaponStorages.Count; i++)
				{
					if (weaponStorage == weaponStorages[i])
					{
						i += increment;
						if (i < 0)
						{
							weaponStorage = weaponStorages[weaponStorages.Count - 1];
						}
						else if (i >= weaponStorages.Count)
						{
							weaponStorage = weaponStorages[0];
						}
						else
						{
							weaponStorage = weaponStorages[i];
						}
						break;
					}
				}
			}
			RebuildPossibleWeapons();
		}

		private void UpdatePawnIndex(Pawn p)
		{
			pawnIndex = -1;
			if (p == null)
			{
				return;
			}
			for (pawnIndex = 0; pawnIndex < selectablePawns.Count; pawnIndex++)
			{
				if (selectablePawns[pawnIndex].Pawn.Equals(p))
				{
					LoadAssignedWeapons();
					break;
				}
			}
		}

		private void LoadAssignedWeapons()
		{
			SelectablePawns selectablePawns = this.selectablePawns[pawnIndex];
			if (!WorldComp.TryGetAssignedWeapons(selectablePawns.Pawn, out assignedWeapons))
			{
				assignedWeapons = new AssignedWeaponContainer
				{
					Pawn = selectablePawns.Pawn
				};
				if (selectablePawns.Pawn.equipment.Primary != null)
				{
					assignedWeapons.Add(selectablePawns.Pawn.equipment.Primary);
				}
				WorldComp.AddAssignedWeapons(selectablePawns.Pawn, assignedWeapons);
			}
			RebuildPossibleWeapons();
		}

		private bool IncludeWeapon(ThingWithComps weapon)
		{
			if (textBuffer.Length <= 0)
			{
				return true;
			}
			string text = textBuffer.ToLower();
			if ((text.StartsWith("mel") || text.StartsWith("mee")) && ((Thing)weapon).def.IsMeleeWeapon)
			{
				return true;
			}
			if (text.StartsWith("ran") && ((Thing)weapon).def.IsRangedWeapon)
			{
				return true;
			}
			return ((Entity)weapon).Label.ToLower().Contains(text);
		}
	}
	public struct SelectablePawns(Pawn pawn)
	{
		public readonly Pawn Pawn = pawn;

		private string labelAndStats = null;

		public string LabelAndStats
		{
			get
			{
				if (labelAndStats == null)
				{
					labelAndStats = GetLabelAndStatsFor(Pawn);
				}
				return labelAndStats;
			}
		}

		public string Melee
		{
			get
			{
				if (Pawn.WorkTagIsDisabled((WorkTags)8))
				{
					return "-";
				}
				return Pawn.skills.GetSkill(SkillDefOf.Melee).levelInt.ToString();
			}
		}

		public string Ranged
		{
			get
			{
				if (Pawn.WorkTagIsDisabled((WorkTags)8))
				{
					return "-";
				}
				return Pawn.skills.GetSkill(SkillDefOf.Shooting).levelInt.ToString();
			}
		}

		public static string GetLabelAndStatsFor(Pawn pawn)
		{
			string text = (pawn.WorkTagIsDisabled((WorkTags)8) ? "-" : pawn.skills.GetSkill(SkillDefOf.Melee).levelInt.ToString());
			string text2 = (pawn.WorkTagIsDisabled((WorkTags)8) ? "-" : pawn.skills.GetSkill(SkillDefOf.Shooting).levelInt.ToString());
			return pawn.Name.ToStringShort + " -- " + ((Def)SkillDefOf.Melee).label + ": " + text + " -- " + ((Def)SkillDefOf.Shooting).label + ": " + text2;
		}

		public override bool Equals(object o)
		{
			if (o is SelectablePawns selectablePawns)
			{
				return ((Thing)(Pawn?)).thingIDNumber == ((Thing)(selectablePawns.Pawn?)).thingIDNumber;
			}
			Pawn val = (Pawn)((o is Pawn) ? o : null);
			if (val != null)
			{
				return ((Thing)(Pawn?)).thingIDNumber == ((Thing)val).thingIDNumber;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return ((object)Pawn).GetHashCode() * labelAndStats.GetHashCode();
		}
	}
	internal class SharedWeaponFilterUI : Window
	{
		private readonly SharedWeaponFilter filter;

		private readonly SortedDictionary<string, ThingDef> meleeWeapons = new SortedDictionary<string, ThingDef>();

		private readonly SortedDictionary<string, ThingDef> rangedWeapons = new SortedDictionary<string, ThingDef>();

		private Vector2 scrollPosition = new Vector2(0f, 0f);

		private float y;

		public override Vector2 InitialSize => new Vector2(300f, 600f);

		public SharedWeaponFilterUI(SharedWeaponFilter filter)
			: base((IWindowDrawing)null)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			this.filter = filter;
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
			{
				if (allDef.IsRangedWeapon)
				{
					rangedWeapons[((Def)allDef).label] = allDef;
				}
				else if (allDef.IsMeleeWeapon)
				{
					meleeWeapons[((Def)allDef).label] = allDef;
				}
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			float num = 3f;
			Widgets.Label(new Rect(0f, num, 70f, 32f), Translator.Translate("WeaponStorage.Name"));
			filter.Label = Widgets.TextArea(new Rect(80f, num - 3f, ((Rect)(ref inRect)).width - 100f, 32f), filter.Label, false);
			num += 36f;
			DrawHitPointsFilterConfig(0f, ref num, ((Rect)(ref inRect)).width, filter);
			DrawQualityFilterConfig(0f, ref num, ((Rect)(ref inRect)).width, filter);
			Widgets.Label(new Rect(0f, num, 200f, 30f), Translator.Translate("WeaponStorage.AllowedWeapons"));
			num += 32f;
			Widgets.BeginScrollView(new Rect(10f, num, ((Rect)(ref inRect)).width - 10f, ((Rect)(ref inRect)).height - 40f - num), ref scrollPosition, new Rect(0f, 0f, ((Rect)(ref inRect)).width - 26f, y), true);
			y = 0f;
			foreach (ThingDef value in meleeWeapons.Values)
			{
				DrawThingDefCheckbox(value);
			}
			foreach (ThingDef value2 in rangedWeapons.Values)
			{
				DrawThingDefCheckbox(value2);
			}
			Widgets.EndScrollView();
		}

		private void DrawThingDefCheckbox(ThingDef d)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			Widgets.Label(new Rect(0f, y, 190f, 30f), ((Def)d).label);
			bool flag;
			bool num = (flag = filter.AllowedDefs.Contains(d));
			Widgets.Checkbox(new Vector2(200f, y), ref flag, 28f, false, false, (Texture2D)null, (Texture2D)null);
			if (num != flag)
			{
				if (flag)
				{
					filter.AllowedDefs.Add(d);
				}
				else
				{
					filter.AllowedDefs.Remove(d);
				}
			}
			y += 32f;
		}

		private void DrawHitPointsFilterConfig(float x, ref float internalY, float width, SharedWeaponFilter internalFilter)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			Rect val = new Rect(x + 20f, internalY, width - 40f, 28f);
			FloatRange hpRange = internalFilter.HpRange;
			Widgets.FloatRange(val, 1, ref hpRange, 0f, 1f, "HitPoints", (ToStringStyle)8, 0f, (GameFont)1, (Color?)null, 0f);
			internalFilter.HpRange = hpRange;
			internalY += 28f;
			internalY += 5f;
			Text.Font = (GameFont)1;
		}

		private void DrawQualityFilterConfig(float x, ref float internalY, float width, SharedWeaponFilter internalFilter)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			Rect val = new Rect(x + 20f, internalY, width - 40f, 28f);
			QualityRange qualityRange = internalFilter.QualityRange;
			Widgets.QualityRange(val, "WeaponStorageQualityRange".GetHashCode(), ref qualityRange);
			internalFilter.QualityRange = qualityRange;
			internalY += 28f;
			internalY += 5f;
			Text.Font = (GameFont)1;
		}
	}
	internal class SharedWeaponsUI : Window
	{
		private readonly IEnumerable<SelectablePawns> pawns;

		private float lastX;

		private float lastY;

		private Vector2 scrollPosition = new Vector2(0f, 0f);

		private SharedWeaponFilter selectedFilter;

		public override Vector2 InitialSize => new Vector2(800f, 600f);

		public SharedWeaponsUI()
			: base((IWindowDrawing)null)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			base.closeOnClickedOutside = true;
			base.doCloseButton = true;
			base.doCloseX = true;
			base.absorbInputAroundWindow = true;
			base.forcePause = true;
			pawns = Util.GetPawns(excludeNonViolent: true);
		}

		public override void DoWindowContents(Rect inRect)
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0199: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
			//IL_024b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0255: Unknown result type (might be due to invalid IL or missing references)
			//IL_0273: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_030c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Expected O, but got Unknown
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Expected O, but got Unknown
			//IL_0344: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_041a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0450: Unknown result type (might be due to invalid IL or missing references)
			//IL_047c: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_0510: Unknown result type (might be due to invalid IL or missing references)
			//IL_057c: Unknown result type (might be due to invalid IL or missing references)
			if (!Find.WindowStack.CurrentWindowGetsInput)
			{
				return;
			}
			Text.Font = (GameFont)1;
			float num = 0f;
			if (Widgets.ButtonText(new Rect(0f, num, 250f, 32f), (selectedFilter == null) ? ((object)Translator.Translate("WeaponStorage.SharedWeaponsFilter")/*cast due to constrained. prefix*/).ToString() : selectedFilter.Label, true, true, true, (TextAnchor?)null) && WorldComp.SharedWeaponFilter.Count > 0)
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				foreach (SharedWeaponFilter f in WorldComp.SharedWeaponFilter)
				{
					list.Add(new FloatMenuOption(f.Label, (Action)delegate
					{
						selectedFilter = f;
					}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
				}
				Find.WindowStack.Add((Window)new FloatMenu(list));
			}
			if (Widgets.ButtonText(new Rect(275f, num, 100f, 32f), TaggedString.op_Implicit(Translator.Translate("WeaponStorage.New")), true, true, true, (TextAnchor?)null))
			{
				SharedWeaponFilter sharedWeaponFilter = new SharedWeaponFilter
				{
					Label = ""
				};
				WorldComp.SharedWeaponFilter.Add(sharedWeaponFilter);
				Find.WindowStack.Add((Window)(object)new SharedWeaponFilterUI(sharedWeaponFilter));
			}
			if (selectedFilter != null && Widgets.ButtonText(new Rect(400f, num, 100f, 32f), TaggedString.op_Implicit(Translator.Translate("WeaponStorage.Edit")), true, true, true, (TextAnchor?)null))
			{
				Find.WindowStack.Add((Window)(object)new SharedWeaponFilterUI(selectedFilter));
			}
			if (selectedFilter != null && Widgets.ButtonText(new Rect(525f, num, 100f, 32f), TaggedString.op_Implicit(Translator.Translate("Delete")), true, true, true, (TextAnchor?)null))
			{
				WorldComp.SharedWeaponFilter.Remove(selectedFilter);
				selectedFilter = null;
			}
			num += 60f;
			if (WorldComp.SharedWeaponFilter.Count == 0)
			{
				return;
			}
			float num2 = 0f;
			Widgets.Label(new Rect(num2, num, 100f, 30f), Translator.Translate("MedGroupColonists"));
			num2 += 120f;
			Widgets.DrawTextureFitted(new Rect(num2, num, 30f, 30f), (Texture)(object)AssignUI.meleeTexture, 1f, 1f);
			num2 += 40f;
			Widgets.DrawTextureFitted(new Rect(num2, num, 30f, 30f), (Texture)(object)AssignUI.rangedTexture, 1f, 1f);
			num2 += 50f;
			float num3 = num2;
			Vector2 val = default(Vector2);
			((Vector2)(ref val))..ctor(scrollPosition.x, 0f);
			Widgets.BeginScrollView(new Rect(num2, num, ((Rect)(ref inRect)).width - num3 - 36f, 32f), ref val, new Rect(0f, 0f, lastX, 32f), false);
			num2 = 0f;
			foreach (SharedWeaponFilter item in WorldComp.SharedWeaponFilter)
			{
				Widgets.Label(new Rect(num2, 0f, 200f, 30f), item.Label);
				num2 += 150f;
			}
			Widgets.EndScrollView();
			num += 32f;
			((Vector2)(ref val))..ctor(0f, scrollPosition.y);
			Widgets.BeginScrollView(new Rect(20f, num, 200f, ((Rect)(ref inRect)).height - num - 76f), ref val, new Rect(0f, 0f, 100f, lastY), false);
			float num4 = 0f;
			foreach (SelectablePawns pawn in pawns)
			{
				num2 = 0f;
				Widgets.Label(new Rect(0f, num4 + 2f, 100f, 30f), pawn.Pawn.Name.ToStringShort);
				num2 += 110f;
				Widgets.Label(new Rect(num2, num4 + 2f, 30f, 30f), pawn.Melee);
				num2 += 40f;
				Widgets.Label(new Rect(num2, num4 + 2f, 30f, 30f), pawn.Ranged);
				num2 += 50f;
				num4 += 32f;
				Widgets.DrawLineHorizontal(0f, num4, 200f);
			}
			Widgets.EndScrollView();
			Widgets.BeginScrollView(new Rect(num2, num, ((Rect)(ref inRect)).width - num3 - 20f, ((Rect)(ref inRect)).height - num - 60f), ref scrollPosition, new Rect(0f, 0f, lastX, lastY), true);
			num4 = 0f;
			foreach (SelectablePawns pawn2 in pawns)
			{
				num2 = 0f;
				foreach (SharedWeaponFilter item2 in WorldComp.SharedWeaponFilter)
				{
					bool flag = item2.AssignedPawns.Contains(pawn2.Pawn);
					bool flag2 = flag;
					Widgets.Checkbox(new Vector2(num2, num4 + 2f), ref flag2, 26f, false, false, (Texture2D)null, (Texture2D)null);
					if (flag2 != flag)
					{
						if (flag2)
						{
							item2.AssignedPawns.Add(pawn2.Pawn);
						}
						else
						{
							item2.AssignedPawns.Remove(pawn2.Pawn);
						}
					}
					num2 += 150f;
				}
				num4 += 32f;
				Widgets.DrawLineHorizontal(0f, num4, lastX);
			}
			Widgets.EndScrollView();
			lastX = num2;
			lastY = num4;
		}
	}
	public static class Util
	{
		public static List<SelectablePawns> GetPawns(bool excludeNonViolent)
		{
			if (!Settings.EnableAssignWeapons)
			{
				return new List<SelectablePawns>();
			}
			SortedDictionary<string, List<Pawn>> sortedDictionary = new SortedDictionary<string, List<Pawn>>();
			foreach (Pawn allMapsCaravansAndTravellingTransporters_Alive_Colonist in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
			{
				if (allMapsCaravansAndTravellingTransporters_Alive_Colonist == null || ((Thing)allMapsCaravansAndTravellingTransporters_Alive_Colonist).Faction != Faction.OfPlayer || !((Thing)allMapsCaravansAndTravellingTransporters_Alive_Colonist).def.race.Humanlike || allMapsCaravansAndTravellingTransporters_Alive_Colonist.Dead)
				{
					continue;
				}
				Pawn_ApparelTracker apparel = allMapsCaravansAndTravellingTransporters_Alive_Colonist.apparel;
				if (apparel == null)
				{
					continue;
				}
				List<Apparel> lockedApparel = apparel.LockedApparel;
				if (lockedApparel != null && lockedApparel.Count == 0 && (!excludeNonViolent || !allMapsCaravansAndTravellingTransporters_Alive_Colonist.WorkTagIsDisabled((WorkTags)8)))
				{
					string toStringShort = allMapsCaravansAndTravellingTransporters_Alive_Colonist.Name.ToStringShort;
					if (!sortedDictionary.TryGetValue(toStringShort, out var value))
					{
						List<Pawn> list = (sortedDictionary[toStringShort] = new List<Pawn>());
						value = list;
					}
					value.Add(allMapsCaravansAndTravellingTransporters_Alive_Colonist);
				}
			}
			List<SelectablePawns> list3 = new List<SelectablePawns>(sortedDictionary.Count);
			foreach (List<Pawn> value2 in sortedDictionary.Values)
			{
				foreach (Pawn item in value2)
				{
					list3.Add(new SelectablePawns(item));
				}
			}
			return list3;
		}
	}
	internal struct WeaponSelected(ThingWithComps thing, bool isChecked)
	{
		public ThingWithComps thing = thing;

		public bool isChecked = isChecked;
	}
}
namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	internal sealed class IgnoresAccessChecksToAttribute : Attribute
	{
		internal IgnoresAccessChecksToAttribute(string assemblyName)
		{
		}
	}
}
