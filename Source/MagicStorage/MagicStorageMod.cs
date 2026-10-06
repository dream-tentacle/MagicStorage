using Verse;

namespace MagicStorage
{
    public sealed class MagicStorageMod : Mod
    {
        public MagicStorageMod(ModContentPack content) : base(content)
        {
            var assembly = typeof(MagicStorageMod).Assembly;
            string build = assembly.ManifestModule.ModuleVersionId.ToString("N");
            Log.Message("[MagicStorage] Storage receivers use native hauling. Build=" + build);
        }
    }
}
