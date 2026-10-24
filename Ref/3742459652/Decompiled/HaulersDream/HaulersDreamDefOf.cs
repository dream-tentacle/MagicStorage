using RimWorld;
using Verse;

namespace HaulersDream;

[DefOf]
public static class HaulersDreamDefOf
{
	public static JobDef HaulersDream_UnloadInventory;

	public static JobDef HaulersDream_SelfPickup;

	public static JobDef HaulersDream_OverloadConstructDeliver;

	public static JobDef HaulersDream_ConstructDeliverBuild;

	public static JobDef HaulersDream_ClaimFromHauler;

	public static JobDef HaulersDream_BatchCraft;

	public static JobDef HaulersDream_InventoryDoBill;

	public static JobDef HaulersDream_BillPrepGather;

	public static JobDef HaulersDream_BulkHaul;

	public static JobDef HaulersDream_KeepInInventory;

	public static JobDef HaulersDream_LoadPackAnimal;

	public static JobDef HaulersDream_UnloadCarrierInBulk;

	public static JobDef HaulersDream_LoadTransportersInBulk;

	public static JobDef HaulersDream_LoadPortalInBulk;

	public static JobDef HaulersDream_LoadVehicleInBulk;

	public static JobDef HaulersDream_BulkRefuel;

	static HaulersDreamDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(HaulersDreamDefOf));
	}
}
