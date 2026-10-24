using System;
using UnityEngine;

namespace MagicStorage
{
    internal static class StorageShelfLayout
    {
        internal const float ItemSize = 0.4f;
        private const float GridSize = 0.78f;

        internal static float ItemSizeFor(int slotCount)
        {
            if (slotCount == 3) return ItemSize;
            int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(slotCount)));
            return Math.Min(ItemSize, GridSize / columns * 0.88f);
        }

        // Approved fourth draft: texture centers (104,80), (184,80), (144,167.36)
        // in a 288px image drawn at 1.125 cells, or 256px per cell.
        internal static Vector3 Offset(int slot, int slotCount = 3)
        {
            if (slotCount != 3)
            {
                slotCount = Math.Max(1, slotCount);
                int columns = (int)Math.Ceiling(Math.Sqrt(slotCount));
                int rows = (int)Math.Ceiling(slotCount / (double)columns);
                int row = slot / columns, column = slot % columns;
                int rowSlots = Math.Min(columns, slotCount - row * columns);
                return new Vector3((column + 0.5f - rowSlots / 2f) * GridSize / columns, 0f,
                    0.08f + (rows / 2f - row - 0.5f) * GridSize / rows);
            }
            switch (slot)
            {
                case 0: return new Vector3(-0.15625f, 0f, 0.25f);
                case 1: return new Vector3(0.15625f, 0f, 0.25f);
                default: return new Vector3(0f, 0f, -0.09125f);
            }
        }

        internal static Vector2 FitSize(Vector2 size, float angle, int slotCount = 3)
        {
            float itemSize = ItemSizeFor(slotCount);
            float width = Math.Abs(size.x), height = Math.Abs(size.y);
            double radians = angle * Math.PI / 180.0;
            float cos = (float)Math.Abs(Math.Cos(radians)), sin = (float)Math.Abs(Math.Sin(radians));
            float extent = Math.Max(width * cos + height * sin, width * sin + height * cos);
            if (extent <= 0f || float.IsNaN(extent) || float.IsInfinity(extent))
                return new Vector2(itemSize, itemSize);
            float scale = itemSize / extent;
            return new Vector2(width * scale, height * scale);
        }
    }
}
