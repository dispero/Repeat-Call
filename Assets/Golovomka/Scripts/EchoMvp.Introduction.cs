using System.Collections.Generic;
using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        // Временный масштаб по одобренному эскизу. Финальные PPU не задаёт.
        const float IntroScale = .0145f;
        static Vector2 IntroPoint(float x, float y) => new Vector2((x - 799.5f) * IntroScale, (492 - y) * IntroScale);
        static Rect IntroRect(float x, float y, float w, float h) => new Rect(IntroPoint(x, y + h), new Vector2(w, h) * IntroScale);
        public static readonly Vector2 IntroSpawn = IntroPoint(205, 755);
        public static readonly Vector2 IntroPlate = IntroPoint(800, 768);
        static readonly Vector2 IntroRemote = IntroPoint(1190, 558);
        static readonly Vector2 IntroDoor = IntroPoint(1410, 675);
        public Vector2 CurrentPlateA
        {
            get
            {
                if (zone == 1) return room01 ? (Vector2)room01.plateOverlay.transform.position : IntroPlate;
                return zone == 2 && room02 ? (Vector2)room02.plateA.transform.position : PlateA;
            }
        }
        List<EchoTape> ActiveTapes => history.ForRoom(zone);
        Texture2D introTexture;
        readonly Dictionary<string, Sprite> introSprites = new Dictionary<string, Sprite>();
        readonly List<(SpriteRenderer renderer, float left, float right)> introDepth = new List<(SpriteRenderer, float, float)>();
        SpriteRenderer remoteCover;
        Sprite introDoorSprite;

        Sprite IntroCrop(string key, int x, int y, int w, int h)
        {
            if (!introSprites.TryGetValue(key, out var sprite))
            {
                sprite = Crop(introTexture, x, y, w, h);
                introSprites.Add(key, sprite);
            }
            return sprite;
        }

        void IntroLayer(string key, int x, int y, int w, int h, int footY)
        {
            var sr = SpriteObject(key, IntroCrop(key, x, y, w, h), IntroPoint(x + w * .5f, y + h * .5f),
                100 - Mathf.RoundToInt(IntroPoint(0, footY).y * 10), Color.white);
            sr.transform.localScale = Vector3.one * (16 * IntroScale);
            introDepth.Add((sr, IntroPoint(x, 0).x, IntroPoint(x + w, 0).x));
        }

        void BuildIntroduction()
        {
            introDepth.Clear();
            if (room01)
            {
                gate = room01.door;
                padA = room01.plateOverlay;
                remoteCover = null;
                foreach (var box in room01.furniture.GetComponentsInChildren<BoxCollider2D>())
                {
                    var bounds = box.bounds;
                    if (box.enabled) furniture.Add(Room02Layout.ToRect(bounds));
                    var sprite = box.GetComponentInChildren<SpriteRenderer>();
                    if (sprite)
                        sprite.sortingOrder = 100 - Mathf.RoundToInt(bounds.min.y * 10);
                }
                var remoteDesk = room01.remoteSprite.GetComponentInParent<BoxCollider2D>();
                if (remoteDesk && remoteDesk.TryGetComponent<SpriteRenderer>(out var deskSprite))
                    room01.remoteSprite.sortingOrder = deskSprite.sortingOrder + 1;
                return;
            }
            if (!introTexture) introTexture = Resources.Load<Texture2D>("Art/LaboratoryIntroduction");
            var bg = SpriteObject("LaboratoryIntroduction", IntroCrop("Background", 0, 0, 1599, 984), Vector2.zero, -100, Color.white);
            bg.transform.localScale = Vector3.one * (16 * IntroScale);

            // Опорные области мебели; верхняя часть предметов может перекрывать голову.
            furniture.Add(IntroRect(145, 495, 120, 89));
            furniture.Add(IntroRect(278, 500, 305, 95));
            furniture.Add(IntroRect(592, 502, 104, 91));
            furniture.Add(IntroRect(706, 490, 143, 104));
            furniture.Add(IntroRect(878, 502, 434, 88));
            furniture.Add(IntroRect(1045, 548, 101, 70));
            // Точные нижние края мебели задают порядок перекрытия при подходе.
            IntroLayer("IntroSofa", 275, 390, 311, 205, 595);
            IntroLayer("IntroSideTable", 592, 451, 104, 142, 593);
            IntroLayer("IntroTeaTable", 703, 486, 147, 108, 594);
            IntroLayer("IntroDesk", 875, 405, 442, 185, 590);
            IntroLayer("IntroComputer", 958, 328, 107, 80, 590);
            IntroLayer("IntroScope", 1167, 343, 140, 68, 590);
            IntroLayer("IntroChair", 1045, 486, 101, 132, 618);

            // Маленькая вставка закрывает только снятый переносной пульт,
            // используя пустой участок того же металлического лотка.
            remoteCover = SpriteObject("EmptyRemoteTray", IntroCrop("EmptyTray", 1153, 535, 18, 18), IntroPoint(1190, 534), 100 - Mathf.RoundToInt(IntroPoint(0, 590).y * 10) + 1, Color.white);
            remoteCover.transform.localScale = new Vector3(16 * IntroScale * 2.1f, 16 * IntroScale * 1.5f, 1);
            remoteCover.enabled = history.remoteObtained;

            // Панель не светится: только небольшое затемнение при утапливании.
            padA = Block("PressurePanelDepression", IntroPlate, new Vector2(174, 48) * IntroScale, Color.clear, -80);
            padB = Block("UnusedPanel", Vector2.zero, Vector2.zero, Color.clear); padB.enabled = false;
            if (!introDoorSprite)
            {
                var doorTexture = Resources.Load<Texture2D>("Art/LaboratoryIntroductionDoor");
                introDoorSprite = Crop(doorTexture, 1350, 400, 122, 310);
            }
            gate = SpriteObject("MechanicalDoorLeaf", introDoorSprite, IntroPoint(1411, 555), 260, Color.white);
            gate.transform.localScale = Vector3.one * (16 * IntroScale);
        }

        void UpdateIntroductionVisuals()
        {
            gate.gameObject.SetActive(!GateOpen);
            padA.color = plateA ? new Color(0, 0, 0, .15f) : Color.clear;
            if (room01) room01.remoteSprite.enabled = !history.remoteObtained;
            else if (remoteCover) remoteCover.enabled = history.remoteObtained;
        }

        bool CanMoveInIntroduction(Vector2 p)
        {
            var walkable = room01 ? Room02Layout.ToRect(room01.walkArea.bounds) : IntroRect(145, 595, 1315, 305);
            if (!walkable.Contains(p)) return false;
            var feet = new Rect(p.x - .22f, p.y - .1f, .44f, .28f);
            foreach (var rect in furniture) if (rect.Overlaps(feet)) return false;
            var doorway = room01 ? Room02Layout.ToRect(room01.doorway.bounds) : IntroRect(1375, 590, 85, 135);
            if (p.x > doorway.xMin)
            {
                if (!doorway.Contains(p)) return false;
                // Не пускаем к закрытой створке, но позволяем покинуть проём.
                if (!GateOpen && !doorway.Contains(position)) return false;
            }
            return true;
        }

        string IntroNearby()
        {
            var remote = room01 ? (Vector2)room01.remote.position : IntroRemote;
            if (!history.remoteObtained && Vector2.Distance(position, remote) < 1.65f)
                return history.firstReturnSeen ? "Взять пульт" : "Осмотреть пульт";
            if (Vector2.Distance(position, RoomSpawn(1)) < .95f) return "Вернуться в участок";
            return "";
        }

        void UseIntroRemote()
        {
            if (!history.firstReturnSeen)
            {
                history.firstReturnSeen = true;
                ReturnToRoom();
            }
            else if (!history.remoteObtained)
            {
                history.remoteObtained = true;
                Save(); UpdateIntroductionVisuals();
            }
        }

        bool AtIntroductionExit()
        {
            var exit = room01 ? (Vector2)room01.exit.position : IntroPoint(1440, 725);
            return position.x > exit.x && position.y > exit.y;
        }

        Vector2 RoomSpawn(int room)
        {
            if (room == 1) return room01 ? (Vector2)room01.entrance.position : IntroSpawn;
            return room == 2 && room02 ? (Vector2)room02.entrance.position : new Vector2(-9, -3.5f);
        }

        Vector2 ReturnFromNextRoom()
        {
            if (zone == 1) return room01 ? (Vector2)room01.returnFromNext.position : IntroPoint(1340, 670);
            if (zone == 2) return room02 ? (Vector2)room02.returnFromNext.position : new Vector2(0, 3.6f);
            return new Vector2(6, 1.5f);
        }

        void ResetRoomRecording()
        {
            recordingActive = false; elapsed = 0; nextSample = 0; recording = new EchoTape { room = zone };
            if (zone > 0 && zone != 4) StartRecording();
        }

        void ReturnToRoom()
        {
            if (recordingActive)
            {
                Record(); recording.duration = elapsed;
                if (elapsed > 0) history.Push(recording); else history.cycle++;
            }
            else history.cycle++;
            Save(); position = RoomSpawn(zone); facing = 0; mode = "play";
            panel.gameObject.SetActive(false);
            ResetRoomRecording(); BuildRoom();
        }

        void EnterSecondRoom()
        {
            history.clearedRooms = Mathf.Max(history.clearedRooms, 1);
            zone = 2; position = RoomSpawn(zone); ResetRoomRecording();
            Save(); BuildRoom();
            Dialog("КОМНАТА 2", "Дальше — две двери и одна копия. Запись здесь начинается заново.\nR возвращает к входу этой комнаты.");
        }
    }
}
