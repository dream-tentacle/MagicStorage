using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    public sealed class GameComponent_WorkbenchUnlocks : GameComponent
    {
        // String keys never resolve Def references: removed mods leave harmless entries.
        private Dictionary<string, bool> unlocked = new Dictionary<string, bool>();

        public GameComponent_WorkbenchUnlocks(Game game) { }

        internal bool IsUnlocked(string defName) => !string.IsNullOrEmpty(defName) &&
            unlocked.TryGetValue(defName, out bool value) && value;

        internal void Unlock(string defName)
        {
            if (ValidateDefName(defName)) unlocked[defName] = true;
        }

        internal static bool ValidateDefName(string defName)
        {
            if (!string.IsNullOrWhiteSpace(defName)) return true;
            Log.Error("[MagicStorage] Cannot unlock workbench: defName is null, empty or whitespace.");
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref unlocked, "unlockedWorkbenches", LookMode.Value, LookMode.Value);
            if (unlocked == null) unlocked = new Dictionary<string, bool>();
        }
    }
}
