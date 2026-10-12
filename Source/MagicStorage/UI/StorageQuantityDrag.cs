using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // Per-window quantity buttons: retain ordinary clicks and paint an adjustment across rows.
    // GUI events avoid a dependency on either DragSelect or Unity's legacy Input module.
    internal sealed class StorageQuantityDrag
    {
        private const float DragDistanceSquared = 20f;
        private readonly HashSet<int> visited = new HashSet<int>();
        private int controlID, capturedControl, startRow;
        private string startLabel;
        private Action startAction;
        private Vector2 startPosition;
        private Rect screenViewport;
        private bool pressed, dragging, hasViewport, inputEnabled;

        internal void BeginGUI(bool enabled)
        {
            controlID = GUIUtility.GetControlID(FocusType.Passive);
            hasViewport = false;
            inputEnabled = enabled && GUI.enabled && !Mouse.IsInputBlockedNow;
            Event current = Event.current;
            if (!inputEnabled || current.rawType == EventType.Ignore ||
                (pressed && GUIUtility.hotControl != capturedControl))
            { Reset(); return; }
            // Recover if the app lost focus and never delivered the previous mouse-up.
            if (pressed && current.rawType == EventType.MouseDown && current.button == 0)
            { Reset(); inputEnabled = true; }
            if (!pressed || current.rawType != EventType.MouseDrag || current.button != 0) return;
            if (!dragging && (GUIUtility.GUIToScreenPoint(current.mousePosition) - startPosition).sqrMagnitude > DragDistanceSquared)
            {
                dragging = true;
                visited.Add(startRow);
                // Include the starting row even if the first drag event has already left its button.
                startAction();
            }
            if (dragging) current.Use();
        }

        // The list is clipped and row coordinates are local; compare in screen coordinates.
        internal void SetViewport(Rect viewport)
        {
            Vector2 min = GUIUtility.GUIToScreenPoint(new Vector2(viewport.xMin, viewport.yMin));
            Vector2 max = GUIUtility.GUIToScreenPoint(new Vector2(viewport.xMax, viewport.yMax));
            screenViewport = new Rect(min, max - min);
            hasViewport = true;
        }

        internal void ButtonText(Rect rect, string label, int rowID, Action activate)
        {
            Widgets.ButtonText(rect, label, active: false);
            Event current = Event.current;
            if (!inputEnabled || !GUI.enabled || !hasViewport || !screenViewport.Contains(GUIUtility.GUIToScreenPoint(current.mousePosition)) ||
                !Mouse.IsOver(rect)) return;
            if (!pressed && current.type == EventType.MouseDown && current.button == 0 && GUIUtility.hotControl == 0)
            {
                pressed = true;
                dragging = false;
                startPosition = GUIUtility.GUIToScreenPoint(current.mousePosition);
                startRow = rowID;
                startLabel = label;
                startAction = activate;
                capturedControl = controlID;
                GUIUtility.hotControl = capturedControl;
                GUIUtility.keyboardControl = 0;
                visited.Clear();
                current.Use();
            }
            else if (pressed && dragging && current.rawType == EventType.MouseDrag && current.button == 0)
            {
                if (visited.Add(rowID)) activate();
                current.Use();
            }
            else if (pressed && !dragging && current.rawType == EventType.MouseUp && current.button == 0 &&
                rowID == startRow && label == startLabel)
            {
                activate();
                Reset();
                current.Use();
            }
        }

        internal void EndGUI()
        {
            Event current = Event.current;
            if (current.rawType == EventType.MouseUp && current.button == 0) Reset();
        }

        internal void Reset()
        {
            if (capturedControl != 0 && GUIUtility.hotControl == capturedControl) GUIUtility.hotControl = 0;
            pressed = dragging = hasViewport = inputEnabled = false;
            capturedControl = 0;
            startAction = null;
            visited.Clear();
        }
    }
}
