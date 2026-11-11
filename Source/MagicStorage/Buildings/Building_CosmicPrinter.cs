using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    public sealed class Building_CosmicPrinter : Building, ICraftingExecutor
    {
        public CosmicPrinterSettings Settings = new CosmicPrinterSettings();
        public Pawn BoundPawn;
        private bool ignoreNonPrinterOrders;
        private CraftingOrder order;
        private Building_StorageCore assignedCore;
        private MapComponent_CosmicCrafting dispatcher;
        private int revision, nextSearchTick;
        private StorageRefreshTimer policyTimer;
        private bool policyAllowed = true;
        public StorageNetwork Network => GetComp<CompStorageNode>()?.Network;
        public bool CanWork => Spawned && Faction == Faction.OfPlayer && Network?.CanWork == true &&
            !this.IsBurning() && (BoundPawn != null ? PrinterPawnBinding.IsAvailable(BoundPawn, Map)
                : Settings.WorkSpeed > 0 && Settings.Efficiency > 0);

        Thing ICraftingExecutor.WorkerThing => this;
        Building_StorageCore ICraftingExecutor.Core => Network?.Core;
        CraftingOrder ICraftingExecutor.Order => order;
        bool ICraftingExecutor.AssignmentActive => Spawned && order != null;
        bool ICraftingExecutor.Allows(CraftingOrder candidate) => Allows(candidate);
        bool ICraftingExecutor.CanContinue() => CanContinue();
        void ICraftingExecutor.CancelAssignment() => ReleaseAssignment();
        void ICraftingExecutor.ReleaseAssignment() => ReleaseAssignment();
        float ICraftingExecutor.WorkSpeed(RecipeDef recipe) => PrinterPawnBinding.WorkSpeed(BoundPawn, recipe, Settings.WorkSpeed);
        void ICraftingExecutor.Learn(RecipeDef recipe, int ticks) { }
        IEnumerable<Thing> ICraftingExecutor.MakeProducts(RecipeDef recipe, List<Thing> input, Thing dominant) =>
            PrinterCraftingProduction.MakeProducts(this, recipe, input, dominant, order.Precept);
        void ICraftingExecutor.NotifyCompleted(List<Thing> products) { }

        private bool Allows(CraftingOrder candidate)
            => CanWork && candidate != null && !candidate.RestrictWorkbench && PrinterCraftingWorkerPolicy.Allows(candidate, Settings.SkillLevel, BoundPawn, ignoreNonPrinterOrders) &&
                PrinterPawnBinding.WorkSpeed(BoundPawn, candidate.Recipe, Settings.WorkSpeed) > 0 &&
                PrinterPawnBinding.Efficiency(BoundPawn, candidate.Recipe, Settings.Efficiency) > 0;

        internal bool TryApplyConfiguration(CosmicPrinterSettings proposed, Pawn boundPawn, bool allowChanges)
        {
            if (!Spawned || Faction != Faction.OfPlayer || proposed == null) return false;
            Settings.TryApply(proposed, allowChanges);
            BoundPawn = boundPawn;
            return true;
        }

        private bool CanContinue()
        {
            if (!CanWork || order == null || Network.Core != assignedCore || assignedCore.Crafting.Find(order.Id) != order ||
                order.Revision != revision || order.Suspended || dispatcher?.IsAssigned(order, this) != true || !Allows(order)) return false;
            if (policyTimer.IsDue(StorageRefreshTask.CraftingPolicy, Find.TickManager.TicksGame))
                policyAllowed = dispatcher.CanRun(assignedCore, order);
            return policyAllowed;
        }

        protected override void Tick()
        {
            base.Tick();
            // Minified containers still tick their unspawned contents.
            if (!Spawned) return;
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (order != null && !CanContinue()) ReleaseAssignment();
            if (!CanWork) return;
            dispatcher = Map.GetComponent<MapComponent_CosmicCrafting>();
            if (order == null)
            {
                int tick = Find.TickManager.TicksGame;
                if (tick < nextSearchTick) return;
                nextSearchTick = tick + Math.Max(1, MagicStorageMod.Settings.GetRefreshTicks(StorageRefreshTask.CraftingPolicy));
                order = dispatcher.FindAutomaticOrder(this);
                if (order == null) return;
                assignedCore = Network.Core;
                revision = order.Revision;
                if (!dispatcher.TryClaim((ICraftingExecutor)this)) { ReleaseAssignment(); return; }
                policyTimer.Start(tick); policyAllowed = true;
                nextSearchTick = tick;
            }
            try
            {
                order.Batch.WorkWith(order, this, 1);
                if (!order.Batch.Finished) return;
                if (order.Batch.CompleteWith(order, this)) order.Batch = null;
                ReleaseAssignment();
            }
            catch (Exception exception)
            {
                if (order != null) order.Suspended = true;
                ReleaseAssignment();
                Log.Error("[MagicStorage] Cosmic printer stopped: " + exception);
            }
        }

        private void ReleaseAssignment()
        {
            dispatcher?.Release(order, this);
            order = null; assignedCore = null;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (Settings == null) Settings = new CosmicPrinterSettings();
            Settings.Normalize();
            dispatcher = map.GetComponent<MapComponent_CosmicCrafting>();
            dispatcher.RegisterAutomatic(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        { dispatcher?.UnregisterAutomatic(this); base.DeSpawn(mode); }

        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            ReleaseAssignment();
            base.SetFaction(newFaction, recruiter);
            if (Spawned) Map.GetComponent<MapComponent_StorageNetworks>().NotifyFactionChanged(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref Settings, "printerSettings");
            Scribe_References.Look(ref BoundPawn, "boundPawn");
            Scribe_Values.Look(ref ignoreNonPrinterOrders, "ignoreNonPrinterOrders", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Settings == null) Settings = new CosmicPrinterSettings();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = "MS_Printer_Settings".Translate(),
                defaultDesc = "MS_Printer_SettingsDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/PrinterSettings"),
                action = () => Find.WindowStack.Add(new Dialog_CosmicPrinter(this))
            };
            yield return new Command_Toggle
            {
                defaultLabel = "MS_Printer_IgnoreNonPrinterOrders".Translate(),
                defaultDesc = "MS_Printer_IgnoreNonPrinterOrdersDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                isActive = () => ignoreNonPrinterOrders,
                toggleAction = () =>
                {
                    if (!Spawned || Faction != Faction.OfPlayer) return;
                    ignoreNonPrinterOrders = !ignoreNonPrinterOrders;
                    if (ignoreNonPrinterOrders && order != null && order.WorkerKind != CraftingWorkerKind.Printers)
                        ReleaseAssignment();
                    nextSearchTick = 0;
                }
            };
            Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
            if (Network?.CanWork != true) yield break;
            yield return new Command_Action
            {
                defaultLabel = "MS_Craft_Open".Translate(),
                hotKey = StorageKeyBindingDefOf.MS_ProductionOrders,
                defaultDesc = "MS_Craft_OpenDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/MagicStorage/CraftingList"),
                action = () =>
                {
                    if (!Spawned || Faction != Faction.OfPlayer) return;
                    Map.GetComponent<MapComponent_StorageNetworks>().EnsureCurrent();
                    if (Network?.CanWork == true) Find.WindowStack.Add(new Dialog_CosmicCrafting(Network.Core));
                }
            };
        }

        public override string GetInspectString()
        {
            string pauseReason = GetPauseReason();
            string status = pauseReason != null ? "MS_Printer_Paused".Translate(pauseReason).ToString()
                : order != null
                    ? "MS_Printer_Working".Translate(CraftingRecipeCatalog.Label(order.Recipe, order.Precept), order.Progress.ToString("P0")).ToString()
                    : "MS_Printer_Idle".Translate().ToString();
            string binding = BoundPawn != null ? "\n" + "MS_Printer_BoundTo".Translate(BoundPawn.LabelShortCap).ToString() : "";
            return (base.GetInspectString() + binding + "\n" + status).Trim();
        }

        private string GetPauseReason()
        {
            if (BoundPawn != null)
            {
                if (BoundPawn.Dead) return "MS_Printer_PauseBoundDead".Translate();
                if (BoundPawn.Destroyed) return "MS_Printer_PauseBoundUnavailable".Translate();
                if (!BoundPawn.Spawned || BoundPawn.Map != Map) return "MS_Printer_PauseBoundAway".Translate();
                if (BoundPawn.Faction != Faction.OfPlayer || !BoundPawn.IsColonist)
                    return "MS_Printer_PauseBoundNotColonist".Translate();
                if (BoundPawn.skills == null) return "MS_Printer_PauseBoundNoSkills".Translate();
            }
            if (!Spawned || Faction != Faction.OfPlayer) return "MS_Printer_PauseUnavailable".Translate();
            if (this.IsBurning()) return "MS_Printer_PauseBurning".Translate();
            if (Network?.CanWork != true) return "MS_Printer_PauseDisconnected".Translate();
            if (BoundPawn == null)
            {
                if (Settings.WorkSpeed <= 0) return "MS_Printer_PauseZeroSpeed".Translate();
                if (Settings.Efficiency <= 0) return "MS_Printer_PauseZeroEfficiency".Translate();
            }
            return null;
        }
    }
}
