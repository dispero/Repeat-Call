using System;
using System.Collections.Generic;
using UnityEngine;

namespace Golovomka
{
    // Области в мировых координатах. Transform владельца намеренно не влияет на них:
    // комнаты MVP используют общую систему координат.
    [DisallowMultipleComponent]
    public sealed class FurnitureLayout : MonoBehaviour
    {
        [Serializable]
        public class Area
        {
            public string label;
            [Range(0, 4)] public int room;
            public Rect bounds;
        }

        public List<Area> areas = new List<Area>();

        public void CopyRoom(int room, List<Rect> destination)
        {
            destination.Clear();
            foreach (var area in areas)
                if (area.room == room && area.bounds.width > 0 && area.bounds.height > 0)
                    destination.Add(area.bounds);
        }

        public static Rect FromPixels(int room, float x, float y, float width, float height)
        {
            float scale = room == 0 ? 13.2f / 992f : .0145f;
            float centerX = room == 0 ? 793 : 799.5f;
            float centerY = room == 0 ? 496 : 492;
            return new Rect((x - centerX) * scale, (centerY - y - height) * scale, width * scale, height * scale);
        }

        public void InitializeDefaults()
        {
            areas.Clear();
            Add(0, "Центральный стол", 535, 660, 545, 215);
            Add(0, "Стул у центрального стола", 730, 860, 145, 70);
            Add(0, "Мебель слева сверху", 135, 395, 395, 120);
            Add(0, "Стул слева", 275, 500, 120, 70);
            Add(0, "Мебель у дальней стены", 910, 402, 150, 57);
            Add(0, "Мебель справа", 1415, 411, 85, 40);
            Add(0, "Мебель у левой стены", 72, 535, 45, 185);
            Add(0, "Мебель слева снизу", 72, 790, 120, 155);
            Add(1, "Мебель слева", 145, 495, 120, 89);
            Add(1, "Диван", 278, 500, 305, 95);
            Add(1, "Тумба", 592, 502, 104, 91);
            Add(1, "Журнальный стол", 706, 490, 143, 104);
            Add(1, "Рабочий стол", 878, 502, 434, 88);
            Add(1, "Стул", 1045, 548, 101, 70);
            for (int room = 2; room <= 4; room++)
            {
                areas.Add(new Area { room = room, label = "Стол с записью / пульт", bounds = new Rect(-1.3f, -2.8f, 2.6f, 1.5f) });
                areas.Add(new Area { room = room, label = "Стол аварийного прохода", bounds = new Rect(4.7f, 1.8f, 2.6f, 1.5f) });
            }
        }

        void Add(int room, string label, float x, float y, float width, float height)
        {
            areas.Add(new Area { room = room, label = label, bounds = FromPixels(room, x, y, width, height) });
        }
    }
}
