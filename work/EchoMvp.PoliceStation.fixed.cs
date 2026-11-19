using UnityEngine;
using System.Collections.Generic;

namespace Golovomka
{
    public partial class EchoMvp
    {
        // Композиция автора 1586x992. Временный размер вписывает комнату между HUD
        // и подсказкой. Существующие камера и PPU сохраняются.
        const float StationScale = 13.2f / 992f;
        static Vector2 StationPoint(float x, float y) => new Vector2((x - 793) * StationScale, (496 - y) * StationScale);
        static readonly Vector2 StationExit = StationPoint(1240, 450);
        static readonly Vector2 StationSpawn = StationPoint(850, 500);
        static readonly Vector2 StationPhone = StationPoint(850, 535);
        Sprite stationLayout;
        readonly Dictionary<string, Sprite> stationForeground = new Dictionary<string, Sprite>();

        Rect StationRect(float x, float y, float width, float height)
        {
            return new Rect(StationPoint(x, y + height), new Vector2(width, height) * StationScale);
        }

        // Только выпуклые контуры самой мебели, без прямоугольных кусков пола.
        // Общие rect/pivot/transform с фоном исключают сдвиг слоёв при PixelSnapping.
        void StationContour(string name, int footY, params Vector2[] outline)
        {
            if (!stationForeground.TryGetValue(name, out var sprite))
            {
                var texture = stationLayout.texture;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect);
                var vertices = new Vector2[outline.Length];
                for (int i = 0; i < outline.Length; i++)
                    vertices[i] = new Vector2((outline[i].x - texture.width * .5f) / 16,
                        (texture.height * .5f - outline[i].y) / 16);
                var triangles = new ushort[(outline.Length - 2) * 3];
                for (int i = 0; i < outline.Length - 2; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = (ushort)(i + 1);
                    triangles[i * 3 + 2] = (ushort)(i + 2);
                }
                sprite.OverrideGeometry(vertices, triangles);
                sprite.name = name;
                createdSprites.Add(sprite); stationForeground.Add(name, sprite);
            }
            var sr = SpriteObject(name, sprite, Vector2.zero,
                100 - Mathf.RoundToInt(StationPoint(0, footY).y * 10), Color.white);
            sr.transform.localScale = Vector3.one * (16 * StationScale);
        }

        void StationOccluder(string name, int x, int top, int width, int height, int footY)
        {
            StationContour(name, footY, new Vector2(x, top), new Vector2(x + width, top),
                new Vector2(x + width, top + height), new Vector2(x, top + height));
        }

        bool CanMoveInStation(Vector2 p)
        {
            if (!StationRect(65, 410, 1440, 525).Contains(p)) return false;
            foreach (var rect in furniture)
                if (rect.Overlaps(new Rect(p.x - .22f, p.y - .1f, .44f, .28f))) return false;
            return true;
        }

        void BuildPoliceStation()
        {
            if (!stationLayout)
            {
                var texture = Resources.Load<Texture2D>("Art/PoliceStationLayout");
                stationLayout = Crop(texture, 0, 0, texture.width, texture.height);
            }
            var room = SpriteObject("PoliceStationLayout", stationLayout, Vector2.zero, -100, Color.white);
            room.transform.localScale = Vector3.one * (16 * StationScale);

            furniture.Add(StationRect(535, 600, 545, 275));
            furniture.Add(StationRect(730, 860, 145, 70));
            furniture.Add(StationRect(135, 395, 395, 120));
            furniture.Add(StationRect(275, 500, 120, 70));
            furniture.Add(StationRect(910, 402, 150, 57));
            furniture.Add(StationRect(1415, 411, 85, 40));
            furniture.Add(StationRect(72, 535, 45, 185));
            furniture.Add(StationRect(72, 790, 120, 155));

            StationOccluder("DutyDeskTop", 537, 600, 540, 165, 875);
            StationOccluder("DutyDeskLeft", 537, 765, 166, 110, 875);
            StationOccluder("DutyDeskRight", 913, 765, 164, 110, 875);
            StationContour("DutyComputer", 875, new Vector2(605, 540), new Vector2(625, 516),
                new Vector2(720, 516), new Vector2(746, 542), new Vector2(746, 600), new Vector2(605, 600));
            // Промежуток между абажуром и стойкой остаётся свободным.
            StationContour("LampShade", 875, new Vector2(934, 518), new Vector2(968, 518),
                new Vector2(987, 541), new Vector2(987, 558), new Vector2(978, 568),
                new Vector2(920, 568), new Vector2(912, 557), new Vector2(912, 543));
            StationContour("LampArm", 875, new Vector2(980, 538), new Vector2(993, 543),
                new Vector2(1028, 563), new Vector2(1021, 575), new Vector2(985, 553));
            StationContour("LampStem", 875, new Vector2(1022, 568), new Vector2(1036, 579),
                new Vector2(1037, 600), new Vector2(1023, 600));
            StationContour("DutyChairBack", 931, new Vector2(734, 794), new Vector2(751, 776),
                new Vector2(849, 776), new Vector2(868, 795), new Vector2(868, 883), new Vector2(734, 883));
            StationOccluder("DutyChairSeat", 738, 879, 131, 17, 931);
            StationOccluder("DutyChairLeftLeg", 738, 896, 15, 35, 931);
            StationOccluder("DutyChairRightLeg", 852, 896, 15, 35, 931);
            StationOccluder("OfficeDesk", 137, 300, 391, 215, 515);
            StationContour("OfficeChairBack", 572, new Vector2(276, 435), new Vector2(293, 416),
                new Vector2(375, 416), new Vector2(393, 435), new Vector2(393, 527), new Vector2(276, 527));
            StationOccluder("OfficeChairSeat", 278, 520, 115, 16, 572);
            StationOccluder("OfficeChairLeftLeg", 277, 536, 14, 36, 572);
            StationOccluder("OfficeChairRightLeg", 379, 536, 14, 36, 572);
            StationOccluder("FilingCabinet", 910, 161, 150, 299, 460);
            StationOccluder("PlantStand", 74, 800, 116, 150, 950);
            StationContour("PlantLeaves", 950, new Vector2(90, 738), new Vector2(151, 738),
                new Vector2(178, 763), new Vector2(174, 802), new Vector2(89, 802), new Vector2(78, 769));
            StationOccluder("Radiator", 72, 517, 46, 205, 722);
            StationOccluder("FlagBase", 1416, 409, 82, 42, 451);
            WorldLabel("В ЛАБОРАТОРИЮ / E", StationPoint(1240, 480), amber);
        }
    }
}
