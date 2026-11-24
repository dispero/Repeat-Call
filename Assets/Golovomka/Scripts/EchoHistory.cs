using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Golovomka
{
    [Serializable] public struct EchoFrame
    {
        public float t, x, y;
        public int zone, facing;
        public Vector2 aim;
        public bool pistolDrawn;
        public EchoFrame(float time, Vector2 pos, int room, int direction, Vector2 aim = default, bool pistolDrawn = false)
        { t = time; x = pos.x; y = pos.y; zone = room; facing = direction; this.aim = aim; this.pistolDrawn = pistolDrawn; }
    }
    [Serializable] public struct EchoButtonPress
    {
        public float time;
        public int button;
        public bool startsSequence;

        public EchoButtonPress(float time, int button, bool startsSequence = false)
        {
            this.time = time;
            this.button = button;
            this.startsSequence = startsSequence;
        }
    }

    [Serializable] public struct EchoShot
    {
        public float time;
        public Vector2 origin, direction;
        public Vector2 muzzleOffset;
        // Старые записи вверх могли хранить смещение ствола вправо от корпуса.
        internal Vector2 Muzzle => origin + new Vector2(direction == Vector2.up ? 0f : muzzleOffset.x, muzzleOffset.y);

        public EchoShot(float time, Vector2 origin, Vector2 direction, Vector2 muzzleOffset = default)
        {
            this.time = time;
            this.origin = origin;
            this.direction = direction;
            this.muzzleOffset = muzzleOffset;
        }
    }

    [Serializable] public class EchoTape
    {
        public int room; // 0 — прежняя общая запись.
        public List<EchoFrame> frames = new List<EchoFrame>();
        public List<EchoButtonPress> buttonPresses = new List<EchoButtonPress>();
        public List<EchoShot> shots = new List<EchoShot>();
        public float duration;
        public bool Sample(float time, out EchoFrame value)
        {
            value = default;
            if (frames.Count == 0 || time < 0 || time >= duration) return false;
            int low = 0, high = frames.Count - 1;
            while (low < high) { int mid = (low + high + 1) / 2; if (frames[mid].t <= time) low = mid; else high = mid - 1; }
            value = frames[low];
            if (low + 1 < frames.Count && value.zone == frames[low + 1].zone)
            {
                var next = frames[low + 1];
                float k = Mathf.InverseLerp(value.t, next.t, time);
                value.x = Mathf.Lerp(value.x, next.x, k); value.y = Mathf.Lerp(value.y, next.y, k);
            }
            return true;
        }
    }
    [Serializable] public class EchoHistory
    {
        public const int MaxRoomNumber = 5;
        public const float TimedRoomInsertionWidth = 18f;
        public int version = 8, cycle = 1;
        public int returnRoom;
        [Serializable] sealed class LegacyProgress { public int clearedRooms; }
        public bool completed;
        public bool remoteObtained;
        public bool room2DoorPreviewSeen;
        public List<EchoTape> tapes = new List<EchoTape>();

        public EchoTape ForRoom(int room)
        {
            return tapes.FindLast(tape => tape.room == room ||
                (room != 4 && tape.room == 0 && tape.frames.Exists(frame => frame.zone == room)));
        }

        public void Push(EchoTape tape)
        {
            tapes.RemoveAll(previous => previous.room == tape.room ||
                (previous.room == 0 && previous.frames.Exists(frame => frame.zone == tape.room)));
            tapes.Add(tape);
            cycle++;
        }
        public static bool OnPlate(EchoFrame actor, Vector2 plate)
        {
            const float plateRadius = .65f;
            const float introductionPlateHalfWidth = 1.3f;

            var offset = new Vector2(actor.x, actor.y) - plate;
            if (actor.zone == 1)
                offset.x *= plateRadius / introductionPlateHalfWidth;
            return actor.zone > 0 && offset.magnitude < plateRadius;
        }
        public static string SavePath => Path.Combine(Application.persistentDataPath, "echoes-mvp-v3.json");
        public void Save(string path = null)
        {
            path = path ?? SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(this));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }
        public static EchoHistory Load(string path = null)
        {
            path = path ?? SavePath;
            foreach (string file in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<EchoHistory>(json);
                    if (data == null || data.version < 3 || data.version > 8 || data.cycle < 1 || data.tapes == null)
                        continue;
                    int maxRoom = data.version == 3 ? 4 : MaxRoomNumber;
                    // Форматы 3 и 4 могли хранить по две записи на комнату.
                    int maxTapes = data.version < 5 ? maxRoom * 2 : maxRoom + 1;
                    if (data.tapes.Count > maxTapes || data.returnRoom < 0 || data.returnRoom > maxRoom) continue;
                    bool valid = true;
                    foreach (var tape in data.tapes)
                    {
                        if (tape == null || tape.frames == null || tape.room < 0 || tape.room > maxRoom || tape.duration < 0 || float.IsNaN(tape.duration) || float.IsInfinity(tape.duration)) { valid = false; break; }
                        float previous = -1;
                        foreach (var f in tape.frames)
                        {
                            if (f.t < previous || float.IsNaN(f.t) || float.IsInfinity(f.t) || float.IsNaN(f.x) || float.IsNaN(f.y) || float.IsInfinity(f.x) || float.IsInfinity(f.y) || f.zone < 0 || f.zone > maxRoom || f.facing < 0 || f.facing > 3) { valid = false; break; }
                            previous = f.t;
                            if (!(f.aim == Vector2.zero || Mathf.Abs(f.aim.sqrMagnitude - 1f) < .001f)) valid = false;
                        }
                        tape.buttonPresses ??= new List<EchoButtonPress>();
                        previous = -1;
                        foreach (var press in tape.buttonPresses)
                        {
                            if (!(press.time >= 0 && press.time >= previous && press.time < tape.duration) ||
                                press.button < 1 || press.button > 3)
                                valid = false;
                            previous = press.time;
                        }
                        tape.shots ??= new List<EchoShot>();
                        bool finale = data.version >= 8 && tape.room == 5;
                        float interval = finale ? FinaleRoom.ShotInterval : TargetRangeRoom.ShotInterval;
                        previous = -interval;
                        foreach (var shot in tape.shots)
                        {
                            if ((tape.room != 4 && !finale) || !(shot.time >= 0 && shot.time < tape.duration &&
                                shot.time >= previous + interval) ||
                                !float.IsFinite(shot.origin.x) || !float.IsFinite(shot.origin.y) ||
                                !float.IsFinite(shot.Muzzle.x) || !float.IsFinite(shot.Muzzle.y) ||
                                (finale ? !TargetRangeRoom.IsCardinalShot(shot.direction)
                                    : !(Mathf.Abs(shot.direction.sqrMagnitude - 1f) < .001f)))
                                valid = false;
                            previous = shot.time;
                        }
                    }
                    if (!valid) continue;
                    if (data.returnRoom == 0)
                    {
                        int cleared = JsonUtility.FromJson<LegacyProgress>(json).clearedRooms;
                        if (cleared < 0 || cleared > maxRoom) continue;
                        if (cleared > 0)
                        {
                            data.remoteObtained = true;
                            data.returnRoom = Mathf.Min(cleared + 1, maxRoom);
                        }
                    }
                    if (data.version == 3) data.InsertButtonRoom();
                    if (data.returnRoom == 0 && (data.remoteObtained || data.tapes.Count > 0))
                        data.returnRoom = 1;
                    if (data.version < 6)
                        data.tapes.RemoveAll(tape => tape.room == 4);
                    if (data.version == 6)
                        foreach (var tape in data.tapes)
                            for (int i = 0; i < tape.frames.Count; i++)
                            {
                                var frame = tape.frames[i];
                                frame.pistolDrawn = frame.zone == 4;
                                tape.frames[i] = frame;
                            }
                    data.version = 8;
                    var seenRooms = new HashSet<int>();
                    for (int i = data.tapes.Count - 1; i >= 0; i--)
                        if (!seenRooms.Add(data.tapes[i].room)) data.tapes.RemoveAt(i);
                    return data;
                }
                catch (Exception) { /* Try the backup, retaining the invalid file. */ }
            }
            return null;
        }

        void InsertButtonRoom()
        {
            if (returnRoom >= 3) returnRoom++;
            foreach (var tape in tapes)
            {
                if (tape.room >= 3) tape.room++;
                for (int i = 0; i < tape.frames.Count; i++)
                {
                    var frame = tape.frames[i];
                    if (frame.zone < 3) continue;
                    frame.zone++;
                    frame.x += TimedRoomInsertionWidth;
                    tape.frames[i] = frame;
                }
            }
            version = 4;
        }
    }
}
