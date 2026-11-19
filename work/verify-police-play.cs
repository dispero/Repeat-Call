using System;
using System.Collections.Generic;
using System.Reflection;
using Golovomka;
using UnityEngine;
var game = UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
var canMove = typeof(EchoMvp).GetMethod("CanMove", BindingFlags.Instance | BindingFlags.NonPublic);
var nearby = typeof(EchoMvp).GetMethod("Nearby", BindingFlags.Instance | BindingFlags.NonPublic);
bool Walkable(Vector2 p) => (bool)canMove.Invoke(game, new object[] { p });
game.history = new EchoHistory();
game.saveFileOverride = System.IO.Path.GetFullPath("work/police-art-test-save.json");
game.BeginCycle();
Check(!game.recordingActive && game.zone == 0, "No recording at station");
game.StartRecording();
Check(!game.recordingActive, "Cannot start recording at station");
Check((string)nearby.Invoke(game, null) == "Телефон дежурного", "Phone reachable from spawn");
Check(!Walkable(new Vector2(0, -2.2f)), "Desk blocks movement");
// Поиск пути по той же CanMove, которую использует движение. Это проверка
// доступности, а не ручное прохождение клавиатурой.
var queue = new Queue<Vector2Int>(); var visited = new HashSet<Vector2Int>();
var start = Vector2Int.zero; queue.Enqueue(start); visited.Add(start);
Vector2 origin = game.position; bool found = false;
var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
while (queue.Count > 0)
{
    var cell = queue.Dequeue(); Vector2 p = origin + (Vector2)cell * .25f;
    game.position = p;
    if ((string)nearby.Invoke(game, null) == "Отправиться в лабораторию") { found = true; break; }
    foreach (var direction in directions)
    {
        var next = cell + direction;
        if (visited.Contains(next)) continue;
        if (!Walkable(origin + (Vector2)next * .25f)) continue;
        visited.Add(next); queue.Enqueue(next);
    }
}
Check(found, "Exit reachable around furniture");
game.Interact("Отправиться в лабораторию");
Check(game.zone == 0, "Phone required before departure");
game.Interact("Телефон дежурного");
game.Interact("Отправиться в лабораторию");
Check(game.zone == 1 && game.recordingActive && game.RoomCopies == 1, "Basement starts recording");
for (int room = 1; room <= 3; room++)
{
    game.position = room == 2 ? new Vector2(0, 3.6f) : new Vector2(6, 2);
    game.Interact("Пульт аварийного прохода");
    Check(game.zone == room + 1, "Room transition " + room);
    Check(game.RoomCopies == (game.zone < 3 ? 1 : 2), "Room copy limit");
}
Check(!game.recordingActive && game.history.manualRecording, "Manual room waits for command");
game.StartRecording(); Check(game.recordingActive, "Manual start");
game.elapsed = 1; game.Record(); game.RestartCycle();
Check(game.zone == 0 && !game.recordingActive && game.history.tapes.Count == 1, "Retry at station");
var loaded = EchoHistory.Load(game.saveFileOverride);
Check(loaded != null && loaded.manualRecording && loaded.clearedRooms == 3, "Save/load after retry");
game.Interact("Телефон дежурного"); game.Interact("Отправиться в лабораторию");
Check(!game.recordingActive, "Manual stage does not auto-record on revisit");
game.history = new EchoHistory { copySlots = 2 };
foreach (int room in new[] { 1, 2, 3, 4 })
{
    game.zone = room; game.position = new Vector2(-9, -3.5f); game.BuildRoom();
    Check(game.RoomCopies == (room < 3 ? 1 : 2), "Copy limit after unlock");
}
game.zone = 1; game.position = new Vector2(-9, -3.5f);
game.history.tapes.Add(new EchoTape { duration = 1, frames = new List<EchoFrame> {
    new EchoFrame(0, EchoMvp.PlateA, 1, 3), new EchoFrame(1, EchoMvp.PlateA, 1, 3) } });
game.BuildRoom(); game.recordingActive = true; game.elapsed = .5f; game.UpdateGhosts();
Check(game.plateA, "Copy holds plate");
game.elapsed = 1; game.UpdateGhosts(); Check(!game.plateA, "Copy ends and releases plate");
game.history = new EchoHistory(); game.BeginCycle();
return "PASS: station recording guard; phone and exit accessibility; desk collision; departure gate; room transitions/copy limits; manual start; retry; test save/load; copy disappears and releases plate. Test states, not manual walkthrough.";

