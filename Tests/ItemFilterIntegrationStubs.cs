// Rendering adapters are tested separately by NativeItemFilterTests. Existing trade
// and caravan integration suites keep their focus on inventory, reservations and execution.
using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace Verse
{
    public static class UI { public static int screenWidth = 1340, screenHeight = 900; }
    public class QuickSearchWidget { public void Reset() { } }
    public static partial class Widgets
    {
        public static void BeginGroup(Rect rect) { }
        public static void EndGroup() { }
    }
    public static class FilterRectExtensions { public static Rect AtZero(this Rect rect) => new Rect(0, 0, rect.width, rect.height); }
#if CARAVAN_FILTER_INTEGRATION_STUBS
    public class Window
    {
        public virtual Vector2 InitialSize => new Vector2(1024, 900);
        public virtual void DoWindowContents(Rect rect) { }
    }
#endif
}
namespace MagicStorage
{
    internal sealed class StorageItemFilterPanel
    {
        internal StorageItemFilterPanel(Map map, QuickSearchWidget search, Action changed, Action rulesChanged, Action reset = null) { }
        internal static float Width(float width) => Math.Min(260, width * 0.25f);
        internal void Draw(Rect rect, IEnumerable<Thing> sourceItems) { }
    }
    internal static class StorageInventoryFilters { internal static bool CanGroup(Thing a, Thing b, object rules) => true; }
    internal static class StorageNativeItemFilters
    {
        internal static void InvalidateTrade(object dialog) { }
        internal static void RebuildTrade(object dialog) { }
        internal static void InvalidateCaravan(object dialog, bool resetSearch = false) { }
        internal static void RebuildCaravan(object dialog) { }
    }
}
