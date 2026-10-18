using System;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // Matches DragSelect 1.6.0.2's quantity-button wrapper and left-button state machine.
    // Its quantity buttons pass listenRightButton: false and use the thing ID as rectHash.
    internal static class StorageQuantityDrag
    {
        private static int leftDragControl;
        private static Vector3 draggingStartPos;
        private static bool leftDragging;
        private static int lastPressFrame;

        internal static void ButtonText(Rect rect, string label, int rowID, Action activate)
        {
            Widgets.ButtonText(rect, label);
            if (ButtonInvisibleDraggable(rect, rowID)) activate();
        }

        private static bool ButtonInvisibleDraggable(Rect rect, int rowID)
        {
            bool clicked = false;
            if (!Mouse.IsOver(rect)) return false;
            if (Input.GetMouseButtonDown(0))
            {
                draggingStartPos = Input.mousePosition;
                leftDragging = false;
            }
            if (Input.GetMouseButton(0))
            {
                if (!leftDragging && (draggingStartPos - Input.mousePosition).sqrMagnitude > 20f)
                    leftDragging = true;
            }
            else if (!Input.GetMouseButtonUp(0))
            {
                leftDragging = false;
                leftDragControl = 0;
            }
            if (Input.GetMouseButtonUp(0) && Time.frameCount - lastPressFrame > 5)
            {
                if (!leftDragging) clicked = true;
                lastPressFrame = Time.frameCount;
                leftDragging = false;
            }
            if ((leftDragging && leftDragControl != rowID) ^ clicked)
            {
                leftDragControl = rowID;
                return true;
            }
            return false;
        }
    }
}
