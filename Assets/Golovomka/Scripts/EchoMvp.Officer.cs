using System;
using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        // Лист: строки вправо/влево/назад/вперёд, 4 кадра в строке.
        // Временный размер MVP: полная ячейка 4 мировые единицы;
        // непрозрачный силуэт около 3,1 единицы. Камера и её PPU не меняются.
        Sprite[] LoadOfficerFrames()
        {
            if (!walk || !walk.isReadable || walk.width < 64 || walk.height < 64)
                throw new InvalidOperationException("IvanOfficerWalk: нужен читаемый лист 4×4.");
            var pixels = walk.GetPixels32();
            float cellWidth = walk.width / 4f, cellHeight = walk.height / 4f;
            var result = new Sprite[16];
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                int left = Mathf.RoundToInt(column * cellWidth), right = Mathf.RoundToInt((column + 1) * cellWidth);
                int bottom = walk.height - Mathf.RoundToInt((row + 1) * cellHeight), top = walk.height - Mathf.RoundToInt(row * cellHeight);
                int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1;
                for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                {
                    if (pixels[y * walk.width + x].a < 128) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                if (maxX < minX) throw new InvalidOperationException("Пустой кадр Ивана: " + row + ", " + column);
                // Выравниваем каждый кадр по подошвам, сохраняя центр ячейки:
                // прозрачные поля генерации не вызывают вертикального дрожания.
                float width = maxX - minX + 1, height = maxY - minY + 1;
                var pivot = new Vector2((left + cellWidth * .5f - minX) / width, 0);
                var sprite = Sprite.Create(walk, new Rect(minX, minY, width, height), pivot, cellHeight / 4f);
                sprite.name = "Ivan_" + row + "_" + column;
                result[row * 4 + column] = sprite; createdSprites.Add(sprite);
            }
            return result;
        }

        Sprite OfficerFrame(int direction, bool moving, float time)
        {
            // Стояние отдельно; цикл шага: левая — проход — правая — проход.
            int step = (int)(time * 8) % 4;
            int phase = moving ? (step == 3 ? 2 : step + 1) : 0;
            return frames[Mathf.Clamp(direction, 0, 3) * 4 + phase];
        }
    }
}
