// Controlled IMGUI boundary. Production gesture handling is linked unchanged.
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Rect(Vector2 position, Vector2 size) : this(position.x, position.y, size.x, size.y) { }
        public float xMin => x;
        public float yMin => y;
        public float xMax => x + width;
        public float yMax => y + height;
        public bool Contains(Vector2 point) => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
    }
    public enum EventType { MouseDown, MouseUp, MouseDrag, Repaint, Layout, Ignore, Used }
    public enum FocusType { Passive }
    public sealed class Event
    {
        public static Event current;
        public EventType type, rawType;
        public Vector2 mousePosition;
        public int button;
        public void Use() { type = EventType.Used; }
    }
    public static class GUI { public static bool enabled = true; }
    public static class GUIUtility
    {
        public static int hotControl, keyboardControl, ControlID = 1;
        public static Vector2 GroupOffset;
        public static int GetControlID(FocusType type) => ControlID;
        public static Vector2 GUIToScreenPoint(Vector2 point) => point + GroupOffset;
    }
}
namespace Verse
{
    public static class Mouse
    {
        public static bool Blocked;
        public static bool IsInputBlockedNow => Blocked;
        public static bool IsOver(UnityEngine.Rect rect) => !Blocked && rect.Contains(UnityEngine.Event.current.mousePosition);
    }
    public static class Widgets
    {
        public static int Draws;
        public static bool ButtonText(UnityEngine.Rect rect, string label, bool active = true)
        { Draws++; if (active) throw new System.Exception("Quantity button must not also use native click handling."); return false; }
    }
}
