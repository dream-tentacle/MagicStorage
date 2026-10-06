// Minimal job engine boundary. The production driver is linked into the test assembly.
// Navigation and native item reservations are deliberately not simulated here.
using System;
using System.Collections.Generic;
using Verse;

namespace Verse
{
    public static class Scribe_Values
    {
        public static void Look(ref bool value, string key, bool fallback) { }
        public static void Look(ref IntVec3 value, string key) { }
    }
    public class MinifiedThing : Thing { public Thing InnerThing; }
    public enum PathEndMode { ClosestTouch, Touch, OnCell }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        private IntVec3 cell;
        private bool hasCell;
        public LocalTargetInfo(IntVec3 value) { cell = value; hasCell = true; Thing = null; }
        public Pawn Pawn => Thing as Pawn;
        public bool HasThing => Thing != null;
        public bool IsValid => HasThing || hasCell;
        public IntVec3 Cell => Thing?.Position ?? cell;
        public static implicit operator LocalTargetInfo(Thing thing) => new LocalTargetInfo { Thing = thing };
        public static implicit operator LocalTargetInfo(IntVec3 value) => new LocalTargetInfo(value);
    }
    public partial class Pawn : Thing
    {
        public Verse.AI.Pawn_JobTracker jobs = new Verse.AI.Pawn_JobTracker();
        public Pawn_CarryTracker carryTracker = new Pawn_CarryTracker();
        public bool Reserve(LocalTargetInfo target, Verse.AI.Job job, int maxPawns, int stackCount, object layer, bool error) => true;
    }
    public class Pawn_CarryTracker
    {
        public int MaxStackSpaceEver(ThingDef def) => def.stackLimit;
        public int AvailableStackSpace(ThingDef def) => Math.Max(0, MaxStackSpaceEver(def) - (CarriedThing?.stackCount ?? 0));
        public ThingOwner innerContainer = new ThingOwner(null);
        public Thing CarriedThing => innerContainer.Count > 0 ? innerContainer[0] : null;
    }
    public partial class ReservationManager
    {
        public void Release(LocalTargetInfo target, Pawn pawn, Verse.AI.Job job) { }
    }
}
namespace Verse.AI
{
    public enum TargetIndex { A, B, C }
    public enum JobCondition { Succeeded, Incompletable, InterruptForced }
    public class Job
    {
        public JobDef def;
        public RimWorld.HaulMode haulMode;
        public Thing targetA, targetB;
        public LocalTargetInfo targetC, targetBCell;
        public bool playerForced, ignoreForbidden, overeat;
        public int count;
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.A ? targetA : index == TargetIndex.C ? targetC : targetB != null ? (LocalTargetInfo)targetB : targetBCell;
        public void SetTarget(TargetIndex index, Thing thing) { if (index == TargetIndex.A) targetA = thing; else targetB = thing; }
    }
    public partial class Pawn_JobTracker { public JobDriver curDriver; public Job curJob; }
    public class Toil { public Action initAction; public Pawn actor; }
    public abstract class JobDriver
    {
        public Pawn pawn;
        public Job job;
        public bool ended;
        public JobCondition? EndCondition;
        public readonly List<Toil> TestToils = new List<Toil>();
        private int jumpIndex = -1;
        private readonly List<Action<JobCondition>> finish = new List<Action<JobCondition>>();
        private Func<JobCondition, Job> finalizer;
        protected void SetFinalizerJob(Func<JobCondition, Job> factory) { finalizer = factory; }
        public Job GetFinalizerJob(JobCondition condition) => finalizer?.Invoke(condition);
        public virtual bool TryMakePreToilReservations(bool error) => true;
        protected abstract IEnumerable<Toil> MakeNewToils();
        public virtual void ExposeData() { }
        public void AddFinishAction(Action<JobCondition> action) { finish.Add(action); }
        public void SetupToils() { TestToils.Clear(); foreach (var toil in MakeNewToils()) { toil.actor = pawn; TestToils.Add(toil); } }
        public void JumpToToil(Toil toil) { jumpIndex = TestToils.IndexOf(toil); }
        // Execute only init actions and explicit jumps. Pathfinding/ticks/serialization remain stubbed.
        public void RunTestToils(int startIndex = 0)
        {
            pawn.jobs.curDriver = this; pawn.jobs.curJob = job;
            int remaining = 20;
            for (int index = startIndex; index < TestToils.Count && !ended;)
            {
                if (--remaining < 0) throw new Exception("Test toil loop");
                jumpIndex = -1;
                TestToils[index].initAction?.Invoke();
                index = jumpIndex >= 0 ? jumpIndex : index + 1;
            }
        }
        public void Cleanup(JobCondition condition) { foreach (var action in finish) action(condition); }
        public void EndJobWith(JobCondition condition) { EndCondition = condition; ended = true; Cleanup(condition); }
    }
    public static class ToilExtensions
    {
        public static T FailOnDestroyedOrNull<T>(this T value, TargetIndex index) => value;
        public static T FailOnForbidden<T>(this T value, TargetIndex index) => value;
        public static T FailOnSelfAndParentsDespawnedOrNull<T>(this T value, TargetIndex index) => value;
        public static T FailOn<T>(this T value, Func<bool> predicate) => value;
    }
    public static class Toils_Goto
    {
        public static int SourceVisits, DestinationVisits;
        public static Toil GotoThing(TargetIndex index, PathEndMode end, bool canGotoSpawnedParent = false) =>
            new Toil { initAction = () => { if (index == TargetIndex.A) SourceVisits++; else DestinationVisits++; } };
    }
    public static class Toils_Haul
    {
        public static int PickupCalls;
        public static Toil StartCarryThing(TargetIndex index, bool canTakeFromInventory = false)
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                PickupCalls++;
                var pawn = toil.actor; var job = pawn.jobs.curJob; var item = job.GetTarget(index).Thing;
                int space = pawn.carryTracker.AvailableStackSpace(item.def);
                if (space <= 0) throw new Exception("StartCarryThing got availableStackSpace 0");
                var taken = item.SplitOff(Math.Min(job.count, Math.Min(space, item.stackCount)));
                if (!pawn.carryTracker.innerContainer.TryAdd(taken)) throw new Exception("Test pickup rejected");
                job.targetA = pawn.carryTracker.CarriedThing;
            };
            return toil;
        }
    }
    public static class Toils_General
    { public static Toil Do(Action action) => new Toil { initAction = action }; }
}
