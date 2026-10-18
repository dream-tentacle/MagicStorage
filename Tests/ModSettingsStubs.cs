// Settings persistence and GUI remain native engine boundaries.
namespace MagicStorage
{
    public sealed class StorageInventoryRule : Verse.IExposable { public void ExposeData() { } }
}
#if !TRADING_STUB_RECT
namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
}
#endif
namespace UnityEngine
{
    public struct Vector2 { public static Vector2 zero => default(Vector2); }
    public static class Mathf { public static float Max(float a, float b) => System.Math.Max(a, b); }
}
namespace Verse
{
    public static partial class Widgets
    {
        public static void BeginScrollView(UnityEngine.Rect rect, ref UnityEngine.Vector2 scroll, UnityEngine.Rect view) { }
        public static void EndScrollView() { }
        public static void DrawMenuSection(UnityEngine.Rect rect) { }
    }
    public class TabRecord
    {
        public readonly string label;
        public readonly System.Action clickedAction;
        public readonly bool Selected;
        public TabRecord(string label, System.Action clickedAction, bool selected)
        { this.label = label; this.clickedAction = clickedAction; Selected = selected; }
    }
    public static class TabDrawer
    {
        public static readonly System.Collections.Generic.List<TabRecord> Tabs = new System.Collections.Generic.List<TabRecord>();
        public static T DrawTabs<T>(UnityEngine.Rect rect, System.Collections.Generic.List<T> tabs, float maxTabWidth = 200f) where T : TabRecord
        { Tabs.Clear(); foreach (var tab in tabs) Tabs.Add(tab); return null; }
    }
    public abstract class ModSettings : IExposable { public virtual void ExposeData() { } }
    public class Listing_Standard
    {
        public static readonly System.Collections.Generic.List<string> NumericLabels = new System.Collections.Generic.List<string>();
        public float CurHeight => 500;
        public void Begin(UnityEngine.Rect rect) { }
        public void End() { }
        public void TextFieldNumericLabeled<T>(string label, ref T value, ref string buffer, float min, float max) where T : struct
        { NumericLabels.Add(label); }
        public void Gap() { }
        public void CheckboxLabeled(string label, ref bool value) { }
        public void Label(string label) { }
        public float Slider(float value, float min, float max) => value;
        public bool ButtonText(string label) => false;
    }
}
