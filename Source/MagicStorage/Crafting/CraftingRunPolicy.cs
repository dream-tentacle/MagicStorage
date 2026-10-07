namespace MagicStorage
{
    internal static class CraftingRunPolicy
    {
        // Same hysteresis as Bill_Production.ShouldDoNow, independent of its runtime.
        internal static bool ShouldRun(CraftingRepeatMode mode, bool suspended, int repeats,
            int target, bool pauseWhenSatisfied, int unpauseAt, long count, ref bool paused)
        {
            if (mode != CraftingRepeatMode.TargetCount) paused = false;
            if (suspended) return false;
            if (mode == CraftingRepeatMode.Forever) return true;
            if (mode == CraftingRepeatMode.RepeatCount) return repeats > 0;
            if (pauseWhenSatisfied && count >= target) paused = true;
            if (count <= unpauseAt || !pauseWhenSatisfied) paused = false;
            return !paused && count < target;
        }
    }
}
