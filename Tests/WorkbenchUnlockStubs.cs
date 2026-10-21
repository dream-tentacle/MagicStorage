// Engine cost calculation and serialization are boundaries; the unlock, inventory,
// shelf availability and material transaction services themselves are production code.
using System.Collections.Generic;
using System.Linq;

namespace Verse
{
    public class Game
    {
        public readonly MagicStorage.GameComponent_WorkbenchUnlocks Unlocks;
        public Game() { Unlocks = new MagicStorage.GameComponent_WorkbenchUnlocks(this); }
        public T GetComponent<T>() where T : GameComponent => Unlocks as T;
    }
    public class GameComponent
    {
        public virtual void ExposeData() { }
    }
    public static class Current { public static Game Game; }
    public static partial class Scribe_Collections
    {
        public static Dictionary<string, bool> SavedUnlocks;
        public static bool LoadingUnlocks;
        public static void Look(ref Dictionary<string, bool> value, string key, LookMode keys, LookMode values)
        {
            if (keys != LookMode.Value || values != LookMode.Value) throw new System.Exception("Unlocks must use value serialization.");
            if (LoadingUnlocks) value = SavedUnlocks == null ? null : new Dictionary<string, bool>(SavedUnlocks);
            else SavedUnlocks = new Dictionary<string, bool>(value);
        }
    }
    public partial class ThingDef
    {
        public bool IsResearchFinished = true;
        public int CostStuffCount;
        public List<ThingDefCountClass> CostList => costList;
        public readonly List<ThingDef> AllowedStuffs = new List<ThingDef>();
        public List<ThingDefCountClass> AdjustedCosts;
    }
}
namespace RimWorld
{
    public static class GenStuff
    {
        public static IEnumerable<Verse.ThingDef> AllowedStuffsFor(Verse.ThingDef table) => table.AllowedStuffs;
    }
    public static class CostListCalculator
    {
        public static List<Verse.ThingDefCountClass> CostListAdjusted(this Verse.ThingDef table, Verse.ThingDef stuff) =>
            table.AdjustedCosts ?? table.costList ?? new List<Verse.ThingDefCountClass>();
    }
}
