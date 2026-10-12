using System;
using MagicStorage;
using UnityEngine;
using Verse;

internal static class QuantityDragTests
{
    private static int assertions;
    private static readonly Rect Viewport = new Rect(0, 0, 200, 180);
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
    private static void SetEvent(EventType type, float x, float y, int button = 0)
    {
        GUIUtility.GroupOffset = new Vector2(0, 0);
        Event.current = new Event { type = type, rawType = type, mousePosition = new Vector2(x, y), button = button };
    }
    private static void Frame(StorageQuantityDrag drag, EventType type, float x, float y, Action draw, bool enabled = true, int button = 0)
    { SetEvent(type, x, y, button); drag.BeginGUI(enabled); drag.SetViewport(Viewport); draw(); drag.EndGUI(); }
    private static void ResetEngine()
    { GUI.enabled = true; GUIUtility.hotControl = GUIUtility.keyboardControl = 0; GUIUtility.ControlID = 1; Mouse.Blocked = false; }
    public static int Main()
    {
        try
        {
            ClicksAndThreshold(); AcrossRowsAndRelease(); OnlyStartOnButton(); DisabledAndReset();
            PerWindowState(); ClippedRowsAndLocalCoordinates(); BothDirectionsAndBulk();
            Console.WriteLine("PASS: " + assertions + " assertions (production quantity drag logic; IMGUI boundaries stubbed).");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void ClicksAndThreshold()
    {
        ResetEngine(); var drag = new StorageQuantityDrag(); int count = 0;
        Action draw = () => drag.ButtonText(new Rect(20, 20, 30, 30), ">", 1, () => count++);
        Frame(drag, EventType.MouseDown, 30, 30, draw); Equal(0, count, "press waits for click or drag");
        Frame(drag, EventType.Repaint, 30, 30, draw); Equal(0, count, "repaint never clicks");
        Frame(drag, EventType.MouseDrag, 33, 31, draw); Equal(0, count, "small pointer jitter preserves click");
        Frame(drag, EventType.MouseUp, 33, 31, draw); Equal(1, count, "ordinary click fires once"); Equal(0, GUIUtility.hotControl, "click releases capture");
        Frame(drag, EventType.MouseDown, 30, 30, draw);
        Frame(drag, EventType.MouseDrag, 35, 30, draw); Equal(2, count, "movement above threshold begins drag");
        Frame(drag, EventType.MouseUp, 35, 30, draw); Equal(2, count, "drag release does not add a click");
        Frame(drag, EventType.MouseDown, 30, 30, draw);
        Frame(drag, EventType.MouseUp, 190, 170, draw); Equal(2, count, "release outside cancels click"); Equal(0, GUIUtility.hotControl, "outside release clears capture");
    }
    private static void AcrossRowsAndRelease()
    {
        ResetEngine(); var drag = new StorageQuantityDrag(); var counts = new int[3];
        Action draw = () => { for (int i = 0; i < 3; i++) { int row = i; drag.ButtonText(new Rect(20, 20 + i * 30, 30, 30), ">>", 10 + i, () => counts[row]++); } };
        Frame(drag, EventType.MouseDown, 30, 30, draw);
        Frame(drag, EventType.MouseDrag, 30, 65, draw);
        Equal(1, counts[0], "starting row included when pointer already moved away"); Equal(1, counts[1], "next row activated");
        Frame(drag, EventType.MouseDrag, 30, 66, draw); Frame(drag, EventType.Repaint, 30, 66, draw);
        Equal(1, counts[1], "hovering same row never repeats");
        Frame(drag, EventType.MouseDrag, 30, 95, draw); Equal(1, counts[2], "third row activated");
        Frame(drag, EventType.MouseDrag, 30, 30, draw); Equal(1, counts[0], "returning to visited row does not repeat");
        Frame(drag, EventType.MouseUp, 190, 170, draw);
        Frame(drag, EventType.MouseDrag, 30, 65, draw); Equal(1, counts[1], "no adjustment after release");
        Frame(drag, EventType.MouseDown, 30, 65, draw); Frame(drag, EventType.MouseUp, 30, 65, draw);
        Equal(2, counts[1], "later gesture may select same row again");
    }
    private static void OnlyStartOnButton()
    {
        ResetEngine(); var drag = new StorageQuantityDrag(); int count = 0;
        Action draw = () => drag.ButtonText(new Rect(20, 20, 30, 30), ">", 1, () => count++);
        Frame(drag, EventType.MouseDown, 150, 150, draw); Frame(drag, EventType.MouseDrag, 30, 30, draw);
        Equal(0, count, "drag beginning on text or elsewhere does not paint quantities");
        Frame(drag, EventType.MouseDown, 30, 30, draw, button: 1); Frame(drag, EventType.MouseDrag, 30, 40, draw, button: 1);
        Frame(drag, EventType.MouseUp, 30, 40, draw, button: 1); Equal(0, count, "right mouse button ignored");
        Mouse.Blocked = true; Frame(drag, EventType.MouseDown, 30, 30, draw); Frame(drag, EventType.MouseUp, 30, 30, draw);
        Equal(0, count, "blocked window cannot begin selection");
    }
    private static void DisabledAndReset()
    {
        ResetEngine(); var drag = new StorageQuantityDrag(); int count = 0;
        Action draw = () => drag.ButtonText(new Rect(20, 20, 30, 30), ">", 1, () => count++);
        GUI.enabled = false; Frame(drag, EventType.MouseDown, 30, 30, draw); Frame(drag, EventType.MouseUp, 30, 30, draw);
        Equal(0, count, "disabled controls do not fire"); GUI.enabled = true;
        Frame(drag, EventType.MouseDown, 30, 30, draw); Frame(drag, EventType.MouseDrag, 30, 40, draw, enabled: false);
        Equal(0, count, "network becoming unusable cancels pending start"); Equal(0, GUIUtility.hotControl, "disabled network releases mouse");
        Frame(drag, EventType.MouseDown, 30, 30, draw); drag.Reset(); Frame(drag, EventType.MouseDrag, 30, 40, draw);
        Equal(0, count, "refresh or close cancels pending gesture");
        Frame(drag, EventType.MouseUp, 30, 40, draw); Frame(drag, EventType.MouseDown, 30, 30, draw);
        Frame(drag, EventType.Ignore, 30, 40, draw); Equal(0, GUIUtility.hotControl, "ignored input clears capture");
        Frame(drag, EventType.MouseDown, 30, 30, draw, enabled: false);
        Frame(drag, EventType.MouseUp, 30, 30, draw, enabled: false);
        Equal(0, count, "unusable network cannot arm a click");
        Frame(drag, EventType.MouseDown, 30, 30, draw);
        Mouse.Blocked = true; Frame(drag, EventType.MouseDrag, 30, 40, draw);
        Equal(0, count, "obscuring window cancels pending start"); Mouse.Blocked = false;
        Frame(drag, EventType.MouseDown, 30, 30, draw);
        Frame(drag, EventType.MouseDown, 30, 30, draw); Frame(drag, EventType.MouseUp, 30, 30, draw);
        Equal(1, count, "next click recovers after a lost mouse-up");
    }
    private static void PerWindowState()
    {
        ResetEngine(); var first = new StorageQuantityDrag(); var second = new StorageQuantityDrag(); int a = 0, b = 0;
        Action drawA = () => first.ButtonText(new Rect(20, 20, 30, 30), ">", 1, () => a++);
        Action drawB = () => second.ButtonText(new Rect(20, 20, 30, 30), ">", 1, () => b++);
        Frame(first, EventType.MouseDown, 30, 30, drawA);
        GUIUtility.ControlID = 2; Frame(second, EventType.MouseDrag, 30, 40, drawB);
        Equal(0, b, "other window does not inherit gesture");
        GUIUtility.hotControl = 99; GUIUtility.ControlID = 1; Frame(first, EventType.MouseDrag, 30, 40, drawA);
        Equal(0, a, "stolen capture cancels previous window gesture"); Equal(99, GUIUtility.hotControl, "reset preserves other control's capture");
    }
    private static void ClippedRowsAndLocalCoordinates()
    {
        ResetEngine(); var drag = new StorageQuantityDrag(); int start = 0, hidden = 0, local = 0;
        Action draw = () =>
        {
            drag.ButtonText(new Rect(20, 20, 30, 30), ">", 1, () => start++);
            drag.ButtonText(new Rect(20, 180, 30, 30), ">", 2, () => hidden++);
            Vector2 screen = Event.current.mousePosition;
            GUIUtility.GroupOffset = new Vector2(0, 100);
            Event.current.mousePosition = screen - GUIUtility.GroupOffset;
            drag.ButtonText(new Rect(20, 0, 30, 30), ">", 3, () => local++);
            GUIUtility.GroupOffset = new Vector2(0, 0); Event.current.mousePosition = screen;
        };
        Frame(drag, EventType.MouseDown, 30, 30, draw); Frame(drag, EventType.MouseDrag, 30, 190, draw);
        Equal(1, start, "start still included when pointer leaves viewport"); Equal(0, hidden, "clipped row outside viewport cannot receive gesture");
        Frame(drag, EventType.MouseDrag, 30, 110, draw); Equal(1, local, "local row coordinates match screen viewport");
        Frame(drag, EventType.MouseUp, 30, 110, draw);
    }
    private static void BothDirectionsAndBulk()
    {
        ResetEngine(); var drag = new StorageQuantityDrag(); var changes = new long[2];
        Action draw = () =>
        {
            for (int i = 0; i < 2; i++)
            {
                int row = i;
                drag.ButtonText(new Rect(10, 20 + i * 30, 25, 30), "<<", i + 1, () => changes[row] = -7);
                drag.ButtonText(new Rect(70, 20 + i * 30, 25, 30), ">>", i + 1, () => changes[row] = 5000000000L);
            }
        };
        Frame(drag, EventType.MouseDown, 80, 30, draw); Frame(drag, EventType.MouseDrag, 80, 65, draw); Frame(drag, EventType.MouseUp, 80, 65, draw);
        Equal(5000000000L, changes[0], "bulk load keeps large quantity exact"); Equal(5000000000L, changes[1], "bulk load paints next row");
        Frame(drag, EventType.MouseDown, 20, 30, draw); Frame(drag, EventType.MouseDrag, 20, 65, draw); Frame(drag, EventType.MouseUp, 20, 65, draw);
        Equal(-7L, changes[0], "return direction applies to starting row"); Equal(-7L, changes[1], "return direction paints next row");
    }
}
