using System;
using System.Reflection;
using MagicStorage;
using UnityEngine;
using Verse;

internal static class QuantityDragTests
{
    private static int assertions;
    private static readonly Rect First = new Rect(20, 20, 30, 30);
    private static readonly Rect Second = new Rect(20, 50, 30, 30);
    private static readonly Rect Third = new Rect(20, 80, 30, 30);
    private static void Equal<T>(T expected, T actual, string message)
    { assertions++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
    private static void Frame(int frame, float x, float y, bool held, bool down, bool up, Action draw,
        EventType type = EventType.Repaint)
    {
        Time.frameCount = frame;
        Input.mousePosition = new Vector3(x, 1000 - y);
        Input.Held = held; Input.Down = down; Input.Up = up;
        Event.current = new Event { type = type, mousePosition = new Vector2(x, y) };
        draw();
    }
    private static void ResetEngine()
    {
        // DragSelect has no lifecycle reset; isolate tests without adding one to production.
        foreach (FieldInfo field in typeof(StorageQuantityDrag).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
            field.SetValue(null, Activator.CreateInstance(field.FieldType));
        GUIUtility.hotControl = GUIUtility.keyboardControl = 0;
        Mouse.Blocked = false; Widgets.Draws = 0; Widgets.NativeClick = false;
    }
    public static int Main()
    {
        try
        {
            ClicksAndThreshold(); AcrossRowsAndRevisits(); SameRowButtons(); ClickFrameInterval();
            SharedStateAndInputPolling(); MouseHitTesting(); NativeClickAndLargeQuantities(); ReleaseOutside();
            Console.WriteLine("PASS: " + assertions + " assertions (DragSelect-style production input logic; engine boundary stubbed).");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void ClicksAndThreshold()
    {
        ResetEngine(); int count = 0;
        Action draw = () => StorageQuantityDrag.ButtonText(First, ">", 1, () => count++);
        Frame(100, 30, 30, true, true, false, draw); Equal(0, count, "press waits for click or drag");
        Frame(101, 34, 32, true, false, false, draw); Equal(0, count, "distance squared exactly 20 is not a drag");
        Frame(102, 35, 30, true, false, false, draw); Equal(1, count, "distance squared above 20 begins drag");
        Frame(103, 36, 30, true, false, false, draw); Equal(1, count, "same row does not repeat while held");
        Frame(104, 36, 30, false, false, true, draw); Equal(1, count, "drag release is not a click");
        Frame(105, 30, 30, false, false, false, draw);
        Frame(110, 30, 30, true, true, false, draw);
        Frame(111, 30, 30, false, false, true, draw); Equal(2, count, "ordinary release clicks once");
        Frame(111, 30, 30, false, false, true, draw, EventType.Layout);
        Equal(2, count, "another GUI pass in release frame does not click again");
    }
    private static void AcrossRowsAndRevisits()
    {
        ResetEngine(); int a = 0, b = 0, c = 0;
        Action draw = () =>
        {
            StorageQuantityDrag.ButtonText(First, ">>", 1, () => a++);
            StorageQuantityDrag.ButtonText(Second, ">>", 2, () => b++);
            StorageQuantityDrag.ButtonText(Third, ">>", 3, () => c++);
        };
        Frame(100, 30, 30, true, true, false, draw);
        Frame(101, 30, 65, true, false, false, draw);
        Equal(0, a, "starting row is not filled in after pointer has left"); Equal(1, b, "next row activated");
        Frame(102, 30, 66, true, false, false, draw); Equal(1, b, "same row is suppressed");
        Frame(103, 30, 95, true, false, false, draw); Equal(1, c, "third row activated");
        Frame(104, 30, 65, true, false, false, draw); Equal(2, b, "returning to a previous row activates it again");
        Frame(105, 30, 30, true, false, false, draw); Equal(1, a, "returning to starting row activates it");
        Frame(106, 30, 65, true, false, false, draw); Equal(3, b, "A to B to A remains repeatable");
        Frame(107, 30, 65, false, false, true, draw); Equal(3, b, "release does not add a drag click");
    }
    private static void SameRowButtons()
    {
        ResetEngine(); int add = 0, remove = 0, other = 0;
        Action draw = () =>
        {
            StorageQuantityDrag.ButtonText(First, ">", 1, () => add++);
            StorageQuantityDrag.ButtonText(new Rect(60, 20, 30, 30), "<", 1, () => remove++);
            StorageQuantityDrag.ButtonText(Second, ">", 2, () => other++);
        };
        Frame(100, 30, 30, true, true, false, draw);
        Frame(101, 35, 30, true, false, false, draw); Equal(1, add, "first button activates");
        Frame(102, 70, 30, true, false, false, draw); Equal(0, remove, "different button of same thing ID remains suppressed");
        Frame(103, 30, 65, true, false, false, draw); Equal(1, other, "another row changes last row");
        Frame(104, 70, 30, true, false, false, draw); Equal(1, remove, "button under pointer determines action on return");
    }
    private static void ClickFrameInterval()
    {
        ResetEngine(); int count = 0;
        Action draw = () => StorageQuantityDrag.ButtonText(First, ">", 1, () => count++);
        Frame(99, 30, 30, true, true, false, draw);
        Frame(100, 30, 30, false, false, true, draw); Equal(1, count, "first click accepted");
        Frame(104, 30, 30, true, true, false, draw);
        Frame(105, 30, 30, false, false, true, draw); Equal(1, count, "exactly five frames is suppressed");
        Frame(105, 30, 30, true, true, false, draw);
        Frame(106, 30, 30, false, false, true, draw); Equal(2, count, "six frames after last accepted release is accepted");
    }
    private static void SharedStateAndInputPolling()
    {
        ResetEngine(); int a = 0, b = 0;
        Action firstWindow = () => StorageQuantityDrag.ButtonText(First, ">", 1, () => a++);
        Action secondWindow = () => StorageQuantityDrag.ButtonText(First, ">>", 2, () => b++);
        GUIUtility.hotControl = 77; GUIUtility.keyboardControl = 88;
        Frame(100, 30, 30, true, true, false, firstWindow, EventType.Used);
        Frame(101, 35, 30, true, false, false, firstWindow, EventType.Layout);
        Equal(1, a, "input polling operates independently of GUI event type");
        Frame(102, 36, 30, true, false, false, secondWindow);
        Equal(1, b, "another caller shares the active drag");
        Frame(103, 36, 30, true, false, false, firstWindow); Equal(2, a, "last row is shared between callers");
        Equal(77, GUIUtility.hotControl, "drag does not capture mouse control");
        Equal(88, GUIUtility.keyboardControl, "drag does not clear keyboard control");
    }
    private static void MouseHitTesting()
    {
        ResetEngine(); int count = 0;
        Action draw = () => StorageQuantityDrag.ButtonText(First, ">", 1, () => count++);
        Frame(100, 150, 150, true, true, false, draw);
        Frame(101, 30, 30, true, false, false, draw);
        Equal(1, count, "DragSelect does not require the press to begin on the button");
        Mouse.Blocked = true;
        Frame(102, 35, 30, true, false, false, draw); Equal(1, count, "blocked Mouse.IsOver does not activate");
        Mouse.Blocked = false;
        Frame(103, 30, 30, false, false, false, draw);
        Frame(104, 35, 30, false, false, false, draw); Equal(1, count, "no left input does not activate");
    }
    private static void NativeClickAndLargeQuantities()
    {
        ResetEngine(); long selected = 0;
        Action draw = () => StorageQuantityDrag.ButtonText(First, ">M", 1, () => selected = 5000000000L);
        Widgets.NativeClick = true;
        Frame(100, 30, 30, false, false, false, draw);
        Equal(0L, selected, "native ButtonText return is ignored as in DragSelect");
        Equal(1, Widgets.Draws, "native button still draws");
        Frame(101, 30, 30, true, true, false, draw);
        Frame(102, 30, 30, false, false, true, draw); Equal(5000000000L, selected, "bulk quantity remains a long");
        Action reverse = () => StorageQuantityDrag.ButtonText(First, "<<", 1, () => selected = -7);
        Frame(110, 30, 30, true, true, false, reverse);
        Frame(111, 30, 30, false, false, true, reverse); Equal(-7L, selected, "reverse action comes from hovered button");
    }
    private static void ReleaseOutside()
    {
        ResetEngine(); int count = 0;
        Action draw = () => StorageQuantityDrag.ButtonText(First, ">", 1, () => count++);
        Frame(100, 30, 30, true, true, false, draw);
        Frame(101, 150, 150, false, false, true, draw); Equal(0, count, "release outside button does not click");
        Frame(102, 30, 30, false, false, false, draw);
        Frame(110, 30, 30, true, true, false, draw);
        Frame(111, 30, 30, false, false, true, draw); Equal(1, count, "next ordinary click works after idle over button");
    }
}
