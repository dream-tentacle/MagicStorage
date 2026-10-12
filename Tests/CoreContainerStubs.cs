// UI boundaries only; the core building and its holder implementation are production code.
namespace UnityEngine { public class Texture2D { } }
namespace Verse
{
    public static class ContentFinder<T> where T : new()
    { public static T Get(string path, bool reportFailure = true) => new T(); }
    public interface IThingHolderTickable { bool ShouldTickContents { get; } }
    public class Gizmo
    {
        public bool disabled;
        public string disabledReason;
        public void Disable(string reason = null) { disabled = true; disabledReason = reason; }
    }
    public class Command_Action : Gizmo
    {
        public string defaultLabel, defaultDesc;
        public object icon;
        public System.Action action;
    }
    public partial class WindowStack
    {
        public readonly System.Collections.Generic.List<object> Windows = new System.Collections.Generic.List<object>();
        public void Add(object window) { Windows.Add(window); }
    }
    public partial class ThingDef { public object uiIcon; }
}
namespace MagicStorage
{
    public class Dialog_CosmicCrafting { public Dialog_CosmicCrafting(Building_StorageCore core) { } }
    public class Dialog_StorageInventory { public Dialog_StorageInventory(Building_StorageCore core) { } }
}
