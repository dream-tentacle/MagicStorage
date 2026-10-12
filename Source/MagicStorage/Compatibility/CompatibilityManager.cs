using System;
using HarmonyLib;
using Verse;

namespace MagicStorage
{
    [StaticConstructorOnStartup]
    internal static class CompatibilityManager
    {
        private static readonly IStorageCompatibility[] modules = { new MonolynCompatibility(), new NivarianCannonCompatibility() };

        static CompatibilityManager()
        {
            var harmony = new Harmony("mjcg.magicstorage.compatibility");
            foreach (var module in modules)
            {
                if (!ModsConfig.IsActive(module.PackageId)) continue;
                try { module.Apply(harmony); }
                catch (Exception exception)
                { Log.Error("[MagicStorage] Failed to apply compatibility for " + module.PackageId + ": " + exception); }
            }
        }
    }
}
