using System;
using UnityEngine;

namespace MagicStorage
{
    internal static class StorageShelfLayout
    {
        internal const float ItemSize = 0.4f;

        // Approved fourth draft: texture centers (104,80), (184,80), (144,167.36)
        // in a 288px image drawn at 1.125 cells, or 256px per cell.
        internal static Vector3 Offset(int slot)
        {
            switch (slot)
            {
                case 0: return new Vector3(-0.15625f, 0f, 0.25f);
                case 1: return new Vector3(0.15625f, 0f, 0.25f);
                default: return new Vector3(0f, 0f, -0.09125f);
            }
        }

        internal static Vector2 FitSize(Vector2 size, float angle)
        {
            float width = Math.Abs(size.x), height = Math.Abs(size.y);
            double radians = angle * Math.PI / 180.0;
            float cos = (float)Math.Abs(Math.Cos(radians)), sin = (float)Math.Abs(Math.Sin(radians));
            float extent = Math.Max(width * cos + height * sin, width * sin + height * cos);
            if (extent <= 0f || float.IsNaN(extent) || float.IsInfinity(extent))
                return new Vector2(ItemSize, ItemSize);
            float scale = ItemSize / extent;
            return new Vector2(width * scale, height * scale);
        }
    }
}
