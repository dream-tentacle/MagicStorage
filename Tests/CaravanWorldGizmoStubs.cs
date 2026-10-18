// Controlled world selection and drawing boundaries for the production UI insertion.
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace Verse
{
    public partial class Command_Action { public bool groupable = true; }
    public partial class MapPawns { public int ColonistCount = 1; }
    public static partial class Find
    {
        public static readonly WorldSelector WorldSelector = new WorldSelector();
        public static readonly WorldObjectsHolder WorldObjects = new WorldObjectsHolder();
    }
}
namespace RimWorld.Planet
{
    public class WorldObject { public PlanetTile Tile; public Faction Faction = Faction.OfPlayer; }
    public class MapParent : WorldObject
    {
        public Map Map; public FormCaravanComp Forming = new FormCaravanComp();
        public bool HasMap => Map != null;
        public T GetComponent<T>() where T : class => Forming as T;
    }
    public class Settlement : MapParent { }
    public class FormCaravanComp
    {
        public static readonly UnityEngine.Texture2D FormCaravanCommand = new UnityEngine.Texture2D();
        public bool Reform;
    }
    public class WorldSelector
    {
        public PlanetTile SelectedTile = PlanetTile.Invalid;
        public List<WorldObject> SelectedObjects = new List<WorldObject>();
    }
    public class WorldObjectsHolder
    {
        public Dictionary<int, MapParent> Parents = new Dictionary<int, MapParent>();
        public Settlement SettlementAt(PlanetTile tile) => MapParentAt(tile) as Settlement;
        public MapParent MapParentAt(PlanetTile tile) => Parents.TryGetValue(tile.tile, out var parent) ? parent : null;
    }
    public static class WorldGizmoUtility
    {
        private static readonly List<object> tmpObjectsList = new List<object>();
        private static readonly List<MapParent> settlements = new List<MapParent>();
        public static Command_Action NativeCommand;
        public static bool Available, TilePickerActive;
        public static List<object> Drawn;
        public static void Configure(params MapParent[] bases)
        {
            settlements.Clear(); settlements.AddRange(bases);
            Find.WorldObjects.Parents.Clear(); Find.WorldSelector.SelectedObjects.Clear(); Find.WorldSelector.SelectedTile = PlanetTile.Invalid;
            foreach (MapParent parent in bases) Find.WorldObjects.Parents[parent.Tile.tile] = parent;
            Available = true; TilePickerActive = false;
            NativeCommand = new Command_Action { defaultLabel = "CommandFormCaravan".Translate() };
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryGetCaravanGizmo(out Gizmo gizmo) { gizmo = NativeCommand; return Available; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WorldUIOnGUI()
        {
            tmpObjectsList.Clear();
            if (!TilePickerActive)
            {
                if (TryGetCaravanGizmo(out var gizmo)) tmpObjectsList.Add(gizmo);
            }
            Drawn = tmpObjectsList.ToList();
            tmpObjectsList.Clear();
        }
    }
}
