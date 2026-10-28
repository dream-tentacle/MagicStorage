using Verse;

namespace MagicStorage
{
    // A recipe def from a removed or updated mod cannot be resolved on load, so the
    // order that used it has no valid work left and is dropped. Vanilla already logs
    // the missing def, but the player only sees the task vanish from the list; report
    // the count once per load instead of changing the production list silently.
    internal static class CraftingOrderLoadNotice
    {
        private static int removedOrders;
        private static bool scheduled;

        internal static void Report(int count)
        {
            if (count <= 0) return;
            removedOrders += count;
            if (scheduled) return;
            // Opening a window inside the save file's Scribe session is unsafe, and
            // every core of every map may report during the same load.
            scheduled = true;
            LongEventHandler.ExecuteWhenFinished(Show);
        }

        private static void Show()
        {
            scheduled = false;
            int count = removedOrders;
            removedOrders = 0;
            if (count <= 0) return;
            Log.Warning("[MagicStorage] Removed " + count +
                " production order(s) because their recipe def could not be found on load.");
            Find.WindowStack.Add(new Dialog_MessageBox(
                "MS_Craft_LoadFailure_Text".Translate(count),
                buttonAText: "MS_Craft_LoadFailure_Confirm".Translate(),
                title: "MS_Craft_LoadFailure_Title".Translate()));
        }
    }
}
