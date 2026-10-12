using HarmonyLib;

namespace MagicStorage
{
    internal interface IStorageCompatibility
    {
        string PackageId { get; }
        void Apply(Harmony harmony);
    }
}
