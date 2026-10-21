using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using RimWorld;
using Verse;
using WeaponStorage;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyVersion("0.0.0.0")]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
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
namespace MendingWeaponStoragePatch
{
	[StaticConstructorOnStartup]
	internal class HarmonyPatches_Mending
	{
		static HarmonyPatches_Mending()
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			if (ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => "MendAndRecycle".Equals(m.Name)))
			{
				try
				{
					new Harmony("com.mendingweaponstoragepatch.rimworld.mod").PatchAll(Assembly.GetExecutingAssembly());
					Log.Message("MendingWeaponStoragePatch Harmony Patches:" + Environment.NewLine + "  Postfix:" + Environment.NewLine + "    WorkGiver_DoBill.TryFindBestBillIngredients - Priority Last");
					return;
				}
				catch (Exception ex)
				{
					Log.Error("Failed to patch Mending & Recycling." + Environment.NewLine + ex.Message);
					return;
				}
			}
			Log.Message("MendingWeaponStoragePatch did not find MendAndRecycle. Will not load patch.");
		}
	}
	[HarmonyPriority(0)]
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
	internal static class WorkGiver_DoBill_TryFindBestBillIngredients
	{
		private static void Postfix(ref bool __result, Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen)
		{
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			if (__result || pawn == null || bill?.recipe == null || bill.Map != ((Thing)pawn).Map || ((Def)bill.recipe).defName.Contains("Weapon"))
			{
				return;
			}
			IEnumerable<Building_WeaponStorage> weaponStorages = WorldComp.GetWeaponStorages(bill.Map);
			if (weaponStorages == null)
			{
				Log.Message("MendingWeaponStoragePatch failed to retrieve WeaponStorages");
				return;
			}
			foreach (Building_WeaponStorage item in weaponStorages)
			{
				IntVec3 val = ((Thing)item).Position - billGiver.Position;
				if (!((float)((IntVec3)(ref val)).LengthHorizontalSquared < bill.ingredientSearchRadius * bill.ingredientSearchRadius))
				{
					continue;
				}
				foreach (KeyValuePair<ThingDef, LinkedList<ThingWithComps>> storedWeapon in item.StoredWeapons)
				{
					FindMatches(ref __result, bill, chosen, item, storedWeapon);
				}
				foreach (KeyValuePair<ThingDef, LinkedList<ThingWithComps>> storedBioEncodedWeapon in item.StoredBioEncodedWeapons)
				{
					FindMatches(ref __result, bill, chosen, item, storedBioEncodedWeapon);
				}
			}
		}

		private static void FindMatches(ref bool __result, Bill bill, List<ThingCount> chosen, Building_WeaponStorage ws, KeyValuePair<ThingDef, LinkedList<ThingWithComps>> kv)
		{
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			if (!bill.ingredientFilter.Allows(kv.Key))
			{
				return;
			}
			foreach (ThingWithComps item in kv.Value)
			{
				if (!bill.ingredientFilter.Allows((Thing)(object)item) || ((Thing)item).HitPoints == ((Thing)item).MaxHitPoints)
				{
					continue;
				}
				if (ws.Remove(item))
				{
					if (!((Thing)item).Spawned)
					{
						Log.Error("Failed to spawn weapon-to-mend [" + ((Entity)item).Label + "] from weapon storage [" + ((Entity)ws).Label + "].");
						__result = false;
					}
					else
					{
						__result = true;
						chosen.Add(new ThingCount((Thing)(object)item, 1, false));
					}
				}
				else
				{
					__result = false;
				}
				break;
			}
		}
	}
}
