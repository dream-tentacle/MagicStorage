// Shared engine boundaries for storage, crafting and construction tests.
// These do not emulate pathfinding or the game's serialization.
using System;
using System.Collections.Generic;
using Verse.AI;

namespace Verse
{
    public class DefModExtension { }
    public static class DefDatabase<T> where T : class
    {
        public static T GetNamedSilentFail(string name) => name == "MS_Mote_ConstructionSupplyBeam" ? SupplyBeam as T : null;
        public static ThingDef SupplyBeam = new ThingDef();
        private static readonly Dictionary<string, T> values = new Dictionary<string, T>();
        public static T GetNamed(string name)
        {
            if (!values.TryGetValue(name, out var value))
            {
                if (name != "MS_CosmicCrafting") throw new Exception("Unknown fixture def: " + name);
                value = new JobDef() as T; values[name] = value;
            }
            return value;
        }
    }
    public class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public partial struct IntVec3
    {
        public bool InHorDistOf(IntVec3 other, float distance) => DistanceToSquared(other) <= distance * distance;
        public bool IsForbidden(Pawn pawn) => false;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Equals(b);
        public static bool operator !=(IntVec3 a, IntVec3 b) => !a.Equals(b);
    }
    public partial class Thing
    {
        public bool forbidden, burning, stale;
        public bool IsForbidden(Pawn pawn) => forbidden;
        public bool IsBurning() => burning;
    }
    public partial class Pawn
    {
        public bool IsColonist = true, Downed, IsColonyMechPlayerControlled;
        public PawnInventory inventory = new PawnInventory();
        public PawnHealth health = new PawnHealth();
        public RaceProperties RaceProps = new RaceProperties();
        public readonly HashSet<Thing> Unreachable = new HashSet<Thing>();
    }
    public class PawnInventory { public ThingOwner innerContainer = new ThingOwner(null); }
    public class PawnHealth { public PawnCapacities capacities = new PawnCapacities(); }
    public class PawnCapacities { public bool manipulation = true; public bool CapableOf(object def) => manipulation; }
    public class RaceProperties { public bool IsMechanoid; public int mechFixedSkillLevel; }
}
