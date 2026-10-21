using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: AssemblyTitle("ItemTeleporter")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ItemTeleporter")]
[assembly: AssemblyCopyright("Copyright ©  2022")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("6c1fe1cf-351f-48e2-b8cb-f4f05d79cea4")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
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
namespace ItemTeleporter
{
	[StaticConstructorOnStartup]
	public static class ItemTeleporterStartup
	{
		static ItemTeleporterStartup()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			new Harmony("ItemTeleporter.Mod").PatchAll();
		}
	}
	[HarmonyPatch(typeof(Thing), "DrawGUIOverlay")]
	internal static class Thing_DrawGUIOverlay_Patch
	{
		private static bool Prefix(Thing __instance)
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			if (!(__instance is Building_ItemTeleporter) && __instance.Map != null)
			{
				Building_ItemTeleporter firstThing = GridsUtility.GetFirstThing<Building_ItemTeleporter>(__instance.Position, __instance.Map);
				if (firstThing != null)
				{
					return false;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Thing), "Print")]
	public static class Thing_Print_Patch
	{
		public static bool Prefix(Thing __instance)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			if (!(__instance is Building_ItemTeleporter))
			{
				Building_ItemTeleporter firstThing = GridsUtility.GetFirstThing<Building_ItemTeleporter>(__instance.Position, __instance.Map);
				if (firstThing != null)
				{
					return false;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
	public static class Pawn_JobTracker_StartJob_Patch
	{
		public static bool Prefix(Pawn ___pawn, Job newJob, JobCondition lastJobEndCondition = (JobCondition)0, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = null, bool fromQueue = false, bool canReturnCurJobToPool = false)
		{
			//IL_0418: Unknown result type (might be due to invalid IL or missing references)
			//IL_0446: Unknown result type (might be due to invalid IL or missing references)
			//IL_044b: Unknown result type (might be due to invalid IL or missing references)
			//IL_044d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0498: Unknown result type (might be due to invalid IL or missing references)
			//IL_0180: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0245: Unknown result type (might be due to invalid IL or missing references)
			//IL_0261: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0324: Unknown result type (might be due to invalid IL or missing references)
			//IL_0331: Unknown result type (might be due to invalid IL or missing references)
			//IL_05ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_060b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0671: Unknown result type (might be due to invalid IL or missing references)
			//IL_0688: Unknown result type (might be due to invalid IL or missing references)
			if (newJob != null)
			{
				HashSet<Building_ItemTeleporter> value2;
				if (newJob.def == JobDefOf.DoBill && newJob.countQueue != null && newJob.targetQueueB != null && Building_ItemTeleporter.buildings.TryGetValue(((Thing)___pawn).Map, out var value))
				{
					List<Building_ItemTeleporter> list = (from x in value
						where x.compPower.PowerOn && x.billInterceptionEnabled && IntVec3Utility.DistanceTo(((Thing)x).Position, ((LocalTargetInfo)(ref newJob.targetA)).Thing.Position) <= 10f
						orderby IntVec3Utility.DistanceTo(((Thing)x).Position, ((LocalTargetInfo)(ref newJob.targetA)).Thing.Position)
						select x).ToList();
					if (GenCollection.Any<Building_ItemTeleporter>(list))
					{
						int i;
						IntVec3 val2 = default(IntVec3);
						for (i = 0; i < newJob.countQueue.Count; i++)
						{
							LocalTargetInfo val = newJob.targetQueueB[i];
							if (GenCollection.Any<Thing>(GridsUtility.GetThingList(((LocalTargetInfo)(ref val)).Thing.Position, ((LocalTargetInfo)(ref newJob.targetA)).Thing.Map), (Predicate<Thing>)((Thing x) => x is Building_ItemTeleporter)))
							{
								continue;
							}
							foreach (Building_ItemTeleporter item in list)
							{
								StorageSettings storeSettings = ((Building_Storage)item).GetStoreSettings();
								val = newJob.targetQueueB[i];
								if (!storeSettings.AllowedToAccept(((LocalTargetInfo)(ref val)).Thing))
								{
									continue;
								}
								IEnumerable<IntVec3> enumerable = ((Building_Storage)item).AllSlotCells().Where(delegate(IntVec3 x)
								{
									//IL_0000: Unknown result type (might be due to invalid IL or missing references)
									//IL_0031: Unknown result type (might be due to invalid IL or missing references)
									//IL_0036: Unknown result type (might be due to invalid IL or missing references)
									Map map = ((LocalTargetInfo)(ref newJob.targetA)).Thing.Map;
									LocalTargetInfo val6 = newJob.targetQueueB[i];
									return StoreUtility.IsGoodStoreCell(x, map, ((LocalTargetInfo)(ref val6)).Thing, ___pawn, ((Thing)___pawn).Faction);
								});
								if (GenCollection.TryRandomElement<IntVec3>(enumerable, ref val2))
								{
									val = newJob.targetQueueB[i];
									Thing thing = ((LocalTargetInfo)(ref val)).Thing;
									if (thing.stackCount <= newJob.countQueue[i])
									{
										((Entity)thing).DeSpawn((DestroyMode)0);
										GenSpawn.Spawn(thing, val2, ((Thing)___pawn).Map, (WipeMode)0);
										FleckMaker.ThrowLightningGlow(thing.DrawPos, thing.Map, 0.5f);
									}
									else if (thing.stackCount > newJob.countQueue[i])
									{
										val = newJob.targetQueueB[i];
										Thing val3 = ((LocalTargetInfo)(ref val)).Thing.SplitOff(newJob.countQueue[i]);
										GenSpawn.Spawn(val3, val2, thing.Map, (WipeMode)0);
										newJob.targetQueueB[i] = LocalTargetInfo.op_Implicit(val3);
										FleckMaker.ThrowLightningGlow(val3.DrawPos, val3.Map, 0.5f);
									}
									break;
								}
							}
						}
					}
				}
				else if (newJob.def == JobDefOf.HaulToCell && Building_ItemTeleporter.buildings.TryGetValue(((Thing)___pawn).Map, out value2))
				{
					Thing thing2 = ((LocalTargetInfo)(ref newJob.targetA)).Thing;
					SlotGroup slotGroup = StoreUtility.GetSlotGroup(thing2.Position, ((Thing)___pawn).Map);
					IntVec3 cell = ((LocalTargetInfo)(ref newJob.targetB)).Cell;
					SlotGroup slotGroup2 = StoreUtility.GetSlotGroup(cell, ((Thing)___pawn).Map);
					if (slotGroup != null && slotGroup2 != null && slotGroup2.parent == slotGroup.parent)
					{
						return true;
					}
					List<Thing> thingList = GridsUtility.GetThingList(thing2.Position, thing2.Map);
					if (!GenCollection.Any<Thing>(thingList, (Predicate<Thing>)((Thing x) => x is Building_ItemTeleporter)))
					{
						List<Building_ItemTeleporter> list2 = (from x in value2
							where x.compPower.PowerOn && x.haulingInterceptionEnabled
							orderby IntVec3Utility.DistanceTo(((Thing)x).Position, thing2.Position)
							select x).ToList();
						IntVec3 val4 = default(IntVec3);
						foreach (Building_ItemTeleporter item2 in list2)
						{
							if (!((Building_Storage)item2).GetStoreSettings().AllowedToAccept(((LocalTargetInfo)(ref newJob.targetA)).Thing))
							{
								continue;
							}
							IEnumerable<IntVec3> enumerable2 = from x in ((Building_Storage)item2).AllSlotCells()
								where StoreUtility.IsGoodStoreCell(x, thing2.Map, thing2, ___pawn, ((Thing)___pawn).Faction)
								select x;
							if (GenCollection.TryRandomElement<IntVec3>(enumerable2, ref val4))
							{
								if (thing2.stackCount <= newJob.count)
								{
									((Entity)thing2).DeSpawn((DestroyMode)0);
									GenSpawn.Spawn(thing2, val4, ((Thing)___pawn).Map, (WipeMode)0);
									FleckMaker.ThrowLightningGlow(thing2.DrawPos, thing2.Map, 0.5f);
								}
								else if (thing2.stackCount > newJob.count)
								{
									Thing val5 = thing2.SplitOff(newJob.count);
									GenSpawn.Spawn(val5, val4, thing2.Map, (WipeMode)0);
									FleckMaker.ThrowLightningGlow(val5.DrawPos, val5.Map, 0.5f);
								}
								___pawn.ClearReservationsForJob(newJob);
								return false;
							}
						}
					}
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
	public static class PawnRenderer_RenderPawnInternal_Patch
	{
		[HarmonyBefore(new string[] { "rimworld.Nals.FacialAnimation" })]
		private static bool Prefix(PawnRenderer __instance)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			Pawn pawn = __instance.pawn;
			PawnRenderFlags flags = __instance.results.parms.flags;
			if (!PawnRenderFlagsExtension.FlagSet(flags, (PawnRenderFlags)1) && pawn.Dead)
			{
				Corpse corpse = pawn.Corpse;
				if (corpse != null && ((Thing)corpse).Spawned && GridsUtility.GetFirstThing<Building_ItemTeleporter>(((Thing)pawn.Corpse).Position, ((Thing)pawn.Corpse).Map) != null)
				{
					return false;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(SteadyEnvironmentEffects), "TryDoDeteriorate")]
	public static class TryDoDeteriorate_Patch
	{
		public static bool Prefix(Thing t)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (((t != null) ? t.Map : null) != null)
			{
				Building_ItemTeleporter firstThing = GridsUtility.GetFirstThing<Building_ItemTeleporter>(t.Position, t.Map);
				if (firstThing != null && firstThing.compPower.PowerOn)
				{
					return false;
				}
			}
			return true;
		}
	}
	[HarmonyPatch(/*Could not decode attribute arguments.*/)]
	public static class Active_Patch
	{
		public static bool Prefix(CompRottable __instance, ref bool __result)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			ThingWithComps parent = ((ThingComp)__instance).parent;
			if (((parent != null) ? ((Thing)parent).Map : null) != null)
			{
				Building_ItemTeleporter firstThing = GridsUtility.GetFirstThing<Building_ItemTeleporter>(((Thing)((ThingComp)__instance).parent).Position, ((Thing)((ThingComp)__instance).parent).Map);
				if (firstThing != null && firstThing.compPower.PowerOn)
				{
					__result = false;
					return false;
				}
			}
			return true;
		}
	}
	public class Building_ItemTeleporter : Building_Storage
	{
		public static Dictionary<Map, HashSet<Building_ItemTeleporter>> buildings = new Dictionary<Map, HashSet<Building_ItemTeleporter>>();

		public CompPowerTrader compPower;

		public bool haulingInterceptionEnabled = true;

		public bool billInterceptionEnabled = true;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			((Building_Storage)this).SpawnSetup(map, respawningAfterLoad);
			compPower = ((ThingWithComps)this).GetComp<CompPowerTrader>();
			if (!buildings.TryGetValue(map, out var value))
			{
				value = (buildings[map] = new HashSet<Building_ItemTeleporter>());
			}
			value.Add(this);
		}

		public override void DeSpawn(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			if (buildings.TryGetValue(((Thing)this).Map, out var value))
			{
				value.Remove(this);
			}
			((Building_Storage)this).DeSpawn(mode);
		}

		public override void Destroy(DestroyMode mode = (DestroyMode)0)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			if (buildings.TryGetValue(((Thing)this).Map, out var value))
			{
				value.Remove(this);
			}
			((Building_Storage)this).Destroy(mode);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo item in <>n__0())
			{
				yield return item;
			}
			if (((Thing)this).Faction == Faction.OfPlayer)
			{
				yield return (Gizmo)new Command_Toggle
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("IT.EnableBillInterception")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("IT.EnableBillInterceptionDesc")),
					icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Icons/EnableBillInterception", true),
					isActive = () => billInterceptionEnabled,
					toggleAction = delegate
					{
						billInterceptionEnabled = !billInterceptionEnabled;
					}
				};
				yield return (Gizmo)new Command_Toggle
				{
					defaultLabel = TaggedString.op_Implicit(Translator.Translate("IT.EnableHaulingInterception")),
					defaultDesc = TaggedString.op_Implicit(Translator.Translate("IT.EnableHaulingInterceptionDesc")),
					icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Icons/EnableHaulingInterception", true),
					isActive = () => haulingInterceptionEnabled,
					toggleAction = delegate
					{
						haulingInterceptionEnabled = !haulingInterceptionEnabled;
					}
				};
			}
		}

		public override void ExposeData()
		{
			((Building_Storage)this).ExposeData();
			Scribe_Values.Look<bool>(ref haulingInterceptionEnabled, "haulingInterceptionEnabled", true, false);
			Scribe_Values.Look<bool>(ref billInterceptionEnabled, "billInterceptionEnabled", true, false);
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private IEnumerable<Gizmo> <>n__0()
		{
			return ((Building_Storage)this).GetGizmos();
		}
	}
}
