// Controlled input/rendering boundary. The production DragSelect-style state machine is linked unchanged.
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public bool Contains(Vector2 point) => point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;
    }
    public enum EventType { MouseDown, MouseUp, MouseDrag, Repaint, Layout, Ignore, Used }
    public sealed class Event
    {
        public static Event current;
        public EventType type;
        public Vector2 mousePosition;
    }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool Held, Down, Up;
        public static bool GetMouseButton(int button) => button == 0 && Held;
        public static bool GetMouseButtonDown(int button) => button == 0 && Down;
        public static bool GetMouseButtonUp(int button) => button == 0 && Up;
    }
    public static class Time { public static int frameCount; }
    public static class GUIUtility { public static int hotControl, keyboardControl; }
}
namespace Verse
{
    public static class Mouse
    {
        public static bool Blocked;
        public static bool IsOver(UnityEngine.Rect rect) => !Blocked && rect.Contains(UnityEngine.Event.current.mousePosition);
    }
    public static class Widgets
    {
        public static int Draws;
        public static bool NativeClick;
        public static bool ButtonText(UnityEngine.Rect rect, string label)
        { Draws++; return NativeClick; }
    }
}
