using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golovomka;
using Interaction = Golovomka.EchoMvp.Interaction;

// Сценарная проверка на тестовых состояниях. Запускать только в отдельной
// Play Mode-сессии: оставляет тестовую комнату для снимка и пишет лишь в work.
public static class EchoIntroductionVerification
{
    [Serializable] class Result { public string[] passed; public int frame; }
    public static string VerifyPlay()
    {
        if (!Application.isPlaying) throw new Exception("Run in a dedicated Play Mode session");
        var m = UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
        if (!m) throw new Exception("Missing game");
        var checks = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); }
        object Call(string name, params object[] args) => typeof(EchoMvp).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m, args);
        bool Can(Vector2 p) => (bool)Call("CanMove", p);
        bool CanStand(Vector2 p) => (bool)Call("CanStandAt", p);
        void Move(Vector2 point)
        {
            if (!CanStand(point)) throw new Exception("Blocked test position " + point);
            m.position = point; m.elapsed += .5f; m.Record(); m.UpdateGhosts();
        }
        Vector2 ReachRemote(Vector2 point)
        {
            for (float radius = .25f; radius < 1.65f; radius += .25f)
            for (int step = 0; step < 16; step++)
            {
                float angle = Mathf.PI * step / 8;
                var candidate = point + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (CanStand(candidate)) return candidate;
            }
            throw new Exception("No walkable position near the remote");
        }
        m.saveFileOverride=System.IO.Path.Combine(Application.dataPath, "../work/intro-verification-save.json");
        m.history=new EchoHistory(); m.BeginCycle();
        var station = UnityEngine.Object.FindAnyObjectByType<PoliceStationLayout>();
        var stationRoots = SceneManager.GetSceneByPath(PoliceStationLayout.ScenePath).GetRootGameObjects();
        var labRoots = SceneManager.GetSceneByPath(LaboratoryRoom.ScenePath).GetRootGameObjects();
        Check(station && m.position == (Vector2)station.entrance.position, "Station spawn comes from the scene");
        var chair = station.furniture.Find("Стул дежурного").GetComponent<BoxCollider2D>();
        Check(!chair.isTrigger && !CanStand(chair.bounds.center), "Station furniture has solid collision");
        var stationSpawn = m.position;
        m.position = new Vector2(100, 100);
        var openSpace = m.position + Vector2.right;
        Check(Can(openSpace), "Open space outside the station has no movement boundary");
        var temporaryWall = new GameObject("Temporary collision check", typeof(BoxCollider2D));
        SceneManager.MoveGameObjectToScene(temporaryWall, station.gameObject.scene);
        try
        {
            temporaryWall.transform.position = m.position + Vector2.right * .5f;
            var collider = temporaryWall.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(.01f, 1);
            Physics2D.SyncTransforms();
            Check(CanStand(openSpace) && !Can(openSpace), "A thin collider stops movement even with a clear destination");
            collider.isTrigger = true;
            Check(Can(openSpace), "A trigger does not restrict movement");
        }
        finally
        {
            temporaryWall.SetActive(false);
            UnityEngine.Object.Destroy(temporaryWall);
            m.position = stationSpawn;
        }
        Check(Array.TrueForAll(labRoots, root => !root.activeSelf), "All laboratory roots are hidden in the station");
        Check(SceneUtility.GetBuildIndexByScenePath(Room02Layout.ScenePath)<0 && !SceneManager.GetSceneByPath(Room02Layout.ScenePath).isLoaded,"Standalone room 2 draft is not loaded");
        m.RestartCycle(); Check(m.zone==0 && !m.recordingActive && m.history.tapes.Count==0,"No recording or R in police station");
        m.Interact(Interaction.EnterLaboratory); Check(m.zone==0,"Phone required");
        Call("Close"); m.Interact(Interaction.AnswerPhone); Call("Close"); m.Interact(Interaction.EnterLaboratory);
        var fade = GameObject.Find("StationFade").GetComponent<UnityEngine.UI.Image>();
        Call("AdvanceStationFade", .125f);
        Check(m.zone == 0 && !m.recordingActive && Mathf.Approximately(fade.color.a, .5f),
            "Station darkens before the scene switches");
        Call("AdvanceStationFade", .125f);
        Check(m.zone == 1 && m.elapsed == 0 && fade.color.a == 1f,
            "Laboratory activates at full black with a fresh recording");
        Call("AdvanceStationFade", .125f);
        Check(m.elapsed == 0 && Mathf.Approximately(fade.color.a, .5f), "Laboratory fades in");
        Call("AdvanceStationFade", .125f);
        Check(!fade.gameObject.activeSelf && fade.color.a == 0f, "Fade releases gameplay");
        var intro = UnityEngine.Object.FindAnyObjectByType<Room01Layout>();
        Check(intro && Array.TrueForAll(stationRoots, root => !root.activeSelf), "Laboratory scene active and station roots hidden");
        var rooms = UnityEngine.Object.FindObjectsByType<LaboratoryRoom>(FindObjectsSortMode.None);
        var sequence = rooms.Single(room => room.roomNumber == 2).GetComponent<SequenceRoomLayout>();
        Check(sequence && sequence.gameObject.scene == intro.gameObject.scene &&
            sequence.IsConfigured, "Both authored rooms share the laboratory scene");
        var room1 = intro.GetComponent<LaboratoryRoom>();
        var room2 = sequence.GetComponent<LaboratoryRoom>();
        Check(room2.entrance.position.x > intro.door.Passage.bounds.max.x, "Room 2 entry is beyond the first doorway");
        Vector2 spawn = room1.entrance.position;
        Check(m.zone==1 && m.position==spawn && m.recordingActive && m.recording.frames[0].zone==1, "Basement starts recording at its scene marker");
        var wallColliders = intro.transform.parent.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>();
        Check(Array.Exists(wallColliders, wall => wall.name == "Стены" && wall.shapeCount > 0), "Laboratory wall tiles generate solid shapes");
        m.Interact(Interaction.UseRoomPassage);
        Check(station.gameObject.activeInHierarchy && Array.TrueForAll(labRoots, root => !root.activeSelf) &&
            m.position == (Vector2)station.entrance.position && !m.recordingActive && m.history.tapes.Count == 0,
            "Returning to the station discards the unfinished attempt and restores its scene");
        m.Interact(Interaction.AnswerPhone); Call("Close"); m.Interact(Interaction.EnterLaboratory);
        Call("AdvanceStationFade", .25f);
        Call("AdvanceStationFade", .25f);
        Vector2 plate = m.CurrentPlateA;
        var remote = ReachRemote(intro.remote.position);
        Check(intro.gameObject.activeInHierarchy && m.position==spawn && m.recordingActive,"Laboratory scene can be reactivated");
        m.RestartCycle(); Check(m.history.cycle==1 && m.position==spawn,"R locked before obtaining remote");
        Move(plate); Check(m.plateA,"First approach presses panel");
        float onPlateTime=m.elapsed;
        Move(remote); Check(!m.plateA,"Leaving panel removes signal");
        var nearby = (Interaction)Call("Nearby");
        Check(nearby == Interaction.InspectRemote,"Remote reachable from floor");
        m.Interact(nearby);
        Check(m.position==spawn && m.history.firstReturnSeen && !m.history.remoteObtained && m.history.ForRoom(1).Count==1,"First return creates one recording at room entrance");
        Check(m.history.ForRoom(1)[0].Sample(onPlateTime,out var f) && Vector2.Distance(new Vector2(f.x,f.y),plate)<.1f,"First copy repeats panel approach");
        m.elapsed=onPlateTime; m.UpdateGhosts(); Check(m.plateA,"First copy opens door");
        m.elapsed=m.history.ForRoom(1)[0].duration+.1f; m.UpdateGhosts(); Check(!m.plateA,"Copy ends and releases panel");
        m.position=remote;
        nearby = (Interaction)Call("Nearby");
        Check(nearby == Interaction.PickUpRemote,"Second interaction offers pickup");
        m.Interact(nearby); Check(m.history.remoteObtained,"Remote pickup");
        m.RestartCycle(); Move(plate); float holdStart=m.elapsed;
        for(int i=0;i<600;i++){m.elapsed+=.02f;m.Record();}
        m.RestartCycle(); Check(m.zone==1 && m.position==spawn && m.elapsed==0 && m.recordingActive,"R resets current room and common clock");
        Check(m.history.ForRoom(1).Count==1,"One recording limit in first room");
        m.elapsed=holdStart+1; m.UpdateGhosts(); Check(m.plateA,"Prepared copy holds panel");
        Vector2 inside = intro.door.Passage.bounds.center;
        var approach = new Vector2(intro.door.Passage.bounds.min.x - .27f, inside.y);
        m.position=approach; Check(Can(inside),"Open mechanical passage allows entry");
        m.elapsed=1000; m.UpdateGhosts(); Check(!intro.door.Passage.isTrigger && !Can(inside),"Closed door blocks entry");
        m.elapsed=holdStart+1; m.UpdateGhosts();
        m.position=inside; m.elapsed=1000; m.UpdateGhosts();
        Check(intro.door.Passage.isTrigger && Can(approach),"Closing door allows escape from opening");
        var sofa = intro.furniture.Find("Диван").GetComponent<BoxCollider2D>();
        var desk = intro.remoteSprite.GetComponentInParent<BoxCollider2D>();
        Check(!CanStand(sofa.bounds.center),"Sofa collision");
        Check(!CanStand(desk.bounds.center),"Desk collision");
        m.position=approach; m.elapsed=holdStart+1; m.UpdateGhosts();
        void CrossDoor(LaboratoryRoom room, SlidingDoor door, bool staysOpen = false)
        {
            var passage = Array.Find(room.passages, item => item.IsAutomatic);
            var bounds = door.Passage.bounds;
            Vector2 before = bounds.center - Vector3.right * .05f;
            Vector2 after = bounds.center + Vector3.right * .05f;
            Check(passage.crossingDoor == door && door.Passage.isTrigger &&
                !passage.Crossed(before, after, false) && !passage.Crossed(after, after, true) &&
                !passage.Crossed(after, before, true), "Crossing requires movement in the correct direction and the remote");
            m.position = before;
            Check(Can(after), "The open doorway can be crossed");
            m.position = after;
            var crossing = Call("FindCrossing", before);
            Check(ReferenceEquals(crossing, passage), "Crossing picks the automatic exit for the active room");
            Call("UsePassage", crossing);
            Check(m.position == after, "The transition preserves the player's position");
            while (m.position.x < bounds.max.x + .35f)
            {
                var step = m.position + Vector2.right * .1f;
                if (!Can(step)) throw new Exception("Blocked after crossing " + door.name + " at " + step);
                m.position = step;
            }
            m.UpdateGhosts();
            Check(door.Passage.isTrigger == staysOpen, "The old door has the expected state after crossing");
        }

        CrossDoor(room1, intro.door);
        Check(m.zone == 2 && m.history.returnRoom == 2 && m.recordingActive,
            "First exit saves the remote return point and starts recording in room 2");
        Check(m.history.ForRoom(1).Count == 1 && m.recording.frames.Count == 1 && m.recording.frames[0].zone == 2,
            "Changing rooms preserves the last tape and starts a fresh local attempt");
        var finished = EchoHistory.Load(m.saveFileOverride);
        Check(finished != null && finished.returnRoom == 2 && finished.remoteObtained,
            "The new return point survives save/load");
        m.history = finished;
        m.RestartCycle();
        Check(m.position == (Vector2)room2.entrance.position && CanStand(m.position),
            "The loaded return point is walkable");
        Vector2 a = sequence.first.plate.transform.position;
        Vector2 b = sequence.second.plate.transform.position;
        Vector2 doorA = sequence.first.door.Passage.bounds.center;
        Vector2 doorB = sequence.second.door.Passage.bounds.center;
        var between = Vector2.Lerp(doorA, doorB, .5f);
        Check(!Can(doorA),"First sequence door is initially closed");
        Check(Can(a),"Ivan can reach A before the first door");
        Move(a);
        float timeA = m.elapsed;
        m.elapsed += 6;
        m.Record();
        Check(Can(b),"Ivan can record A then B without crossing the first door");
        Move(b);
        float timeB = m.elapsed;
        var camera = Camera.main;
        float cameraX = camera.transform.position.x;
        Check(!m.history.room2DoorPreviewSeen && !sequence.first.door.Passage.isTrigger && sequence.second.door.Passage.isTrigger,
            "Ivan starts the first preview at the exit controlled by the left plate");
        Call("AdvanceRoom2DoorPreview", .55f);
        Call("FitRoomCamera", false);
        Check(Mathf.Abs(camera.transform.position.x - doorB.x) < Mathf.Abs(cameraX - doorB.x) &&
            m.position == b && m.elapsed == timeB, "Preview moves the camera to B while Ivan and recording stay put");
        Call("AdvanceRoom2DoorPreview", 1.2f);
        Check(Mathf.Approximately(camera.transform.position.x, cameraX) &&
            EchoHistory.Load(m.saveFileOverride)?.room2DoorPreviewSeen == true,
            "Preview returns the camera and remembers completion in the save");
        m.elapsed += 6;
        m.RestartCycle();
        Check(m.position==(Vector2)room2.entrance.position && m.elapsed==0 && m.history.ForRoom(2).Count==1,
            "Room 2 has a local reset and keeps one tape");
        m.elapsed=timeA; m.UpdateGhosts();
        Check(m.plateA && !m.plateB && Can(doorA) && Can(between) && !Can(doorB),
            "Copy opens A only and Ivan can reach the waiting area");
        Move(between);
        m.elapsed=timeB; m.UpdateGhosts();
        Check(!m.plateA && m.plateB && Can(doorB), "Copy opens B only");
        Check(typeof(EchoMvp).GetField("room2PreviewDoor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m) == null,
            "Playback does not repeat the door preview");
        CrossDoor(room2, sequence.second.door);
        var room3 = rooms.Single(room => room.roomNumber == 3);
        var buttons = room3.GetComponent<TimedButtonsRoom>();
        Check(m.zone == 3 && m.history.returnRoom == 3 &&
            !sequence.first.door.Passage.isTrigger && !sequence.second.door.Passage.isTrigger &&
            EchoHistory.Load(m.saveFileOverride)?.returnRoom == 3,
            "Room 2 exit saves the next return point and releases both doors");
        m.RestartCycle();
        Check(m.position == (Vector2)room3.entrance.position && CanStand(m.position) &&
            m.position.x > sequence.second.door.Passage.bounds.max.x, "R spawns beyond the closed B door");
        Check(room3.copyLimit == 1 && buttons.IsConfigured && !buttons.exitDoor.Passage.isTrigger,
            "Room 3 starts with one copy slot and a closed door");
        m.Interact(Interaction.UseRoomPassage);
        Check(m.zone == 2 && m.history.ForRoom(2).Count == 1 && m.history.returnRoom == 3,
            "Returning to room 2 preserves its history and the saved point");
        m.RestartCycle();
        Check(m.zone == 3 && m.position == (Vector2)room3.entrance.position, "R returns to the saved room");
        var points = buttons.buttons.Select(b => (Vector2)b.interactionPoint.position).ToArray();
        m.position = points[0];
        Check(Can(points[1]) && Can(points[2]) && CanStand(points[0]), "The button lane is walkable");
        float MoveSpeed() => (float)typeof(EchoMvp).GetField("moveSpeed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m);
        Check((Vector2.Distance(points[0], points[2]) - 2 * buttons.interactionRadius) / MoveSpeed() >
            buttons.stepInterval + buttons.pressWindow, "A single player cannot reach the first two signals in time");
        void PressButton(int step)
        {
            m.elapsed = buttons.startDelay + step * buttons.stepInterval + buttons.pressWindow * .5f;
            m.position = points[buttons.order[step] - 1];
            m.Record();
            m.UpdateGhosts();
            Check((Interaction)Call("Nearby") == Interaction.PressTimedButton, "E selects the nearby timed button");
            m.Interact(Interaction.PressTimedButton);
        }
        m.Interact(Interaction.PressTimedButton);
        Check(m.recording.buttonPresses.Count == 0, "Pressing before the first window does not record an event");
        foreach (int step in new[] { 0, 2, 4, 6 }) PressButton(step);
        Check(!buttons.Solved, "One recorded role alone leaves the door closed");
        m.RestartCycle();
        Check(m.history.ForRoom(3).Single().buttonPresses.Count == 4 &&
            EchoHistory.Load(m.saveFileOverride).ForRoom(3).Single().buttonPresses.Count == 4,
            "R saves four timed button events for the copy");
        m.elapsed = buttons.EndTime + .5f;
        m.UpdateGhosts();
        Check(!buttons.Solved, "Missing the three live presses leaves the door closed");
        m.RestartCycle();
        Check(m.history.ForRoom(3).Single().buttonPresses.Count == 4, "An empty retry retains the useful recording");
        foreach (int step in new[] { 1, 3, 5 }) PressButton(step);
        m.elapsed = buttons.EndTime;
        m.UpdateGhosts();
        Check(buttons.Solved && buttons.exitDoor.Passage.isTrigger, "Three live presses and four copy presses open the exit");
        CrossDoor(room3, buttons.exitDoor, staysOpen: true);
        var room4 = rooms.Single(room => room.roomNumber == 4);
        var shared = room4.GetComponent<SequenceRoomLayout>();
        Check(m.zone == 4 && m.history.returnRoom == 4 && room4.copyLimit == 2 &&
            shared.requireBothPlates && shared.IsConfigured, "The next room unlocks two copies");
        Vector2 sharedA = shared.first.plate.transform.position;
        Vector2 sharedB = shared.second.plate.transform.position;
        var sharedDoor = shared.first.door.Passage;
        Vector2 sharedCenter = sharedDoor.bounds.center;
        Check(Can(sharedA) && Can(sharedB) && !sharedDoor.isTrigger,
            "Both plates are reachable before the closed door");
        m.RestartCycle();
        Check(m.zone == 4 && m.position == (Vector2)room4.entrance.position, "R returns to the saved room");
        Move(sharedA);
        Check(m.plateA && !m.plateB && !sharedDoor.isTrigger, "Ivan activates A without opening the shared door");
        m.elapsed = 6;
        m.RestartCycle();
        Move(sharedB);
        Check(m.plateA && m.plateB && sharedDoor.isTrigger,
            "One copy and Ivan can open the shared door together");
        m.elapsed = 8;
        m.RestartCycle();
        Check(m.elapsed == 0 && m.position == (Vector2)room4.entrance.position &&
            m.history.ForRoom(4).Count == 2, "R restarts both local recordings from a common zero");
        m.elapsed = 1;
        m.UpdateGhosts();
        Check(m.plateA && m.plateB && sharedDoor.isTrigger, "Two simultaneous copies open the shared door");
        m.elapsed = 7;
        m.UpdateGhosts();
        Check(!m.plateA && m.plateB && !sharedDoor.isTrigger, "The door closes when the first copy ends");
        m.elapsed = 1;
        m.UpdateGhosts();
        CrossDoor(room4, shared.first.door);
        var tail = rooms.Single(room => room.disableRecording && room.roomNumber == 5);
        Check(m.zone == 5 && !m.recordingActive && !sharedDoor.isTrigger &&
            EchoHistory.Load(m.saveFileOverride)?.returnRoom == 5,
            "The last door saves the empty room as a return point");
        m.RestartCycle();
        Check(m.zone == 5 && !m.recordingActive && m.position == (Vector2)tail.entrance.position &&
            CanStand(m.position) && m.position.x > sharedDoor.bounds.max.x &&
            m.history.ForRoom(4).Count == 2 && m.history.ForRoom(2).Count == 1,
            "R returns to a walkable position without starting recording in the empty room");
        var h=new EchoHistory {copySlots=2,firstReturnSeen=true,remoteObtained=true};
        EchoTape T(int z,float duration)=>new EchoTape {room=z,duration=duration,frames=new List<EchoFrame>{new EchoFrame(0,Vector2.zero,z,0)}};
        h.Push(T(1,1),1);h.Push(T(1,2),1);h.Push(T(4,3),2);h.Push(T(4,4),2);h.Push(T(4,5),2);
        Check(h.ForRoom(1).Count==1 && h.ForRoom(4).Count==2 && h.ForRoom(4)[0].duration==4,"Per-room limits and oldest replacement");
        h.Save(m.saveFileOverride);var loaded=EchoHistory.Load(m.saveFileOverride);
        Check(loaded!=null && loaded.remoteObtained && loaded.firstReturnSeen && loaded.ForRoom(4).Count==2,"Pickup, first return and room recordings survive save/load");
        h = new EchoHistory { version = 3, returnRoom = 3, firstReturnSeen = true, remoteObtained = true };
        h.Push(T(3, 3), 2);
        h.Save(m.saveFileOverride);
        loaded = EchoHistory.Load(m.saveFileOverride);
        var frame = loaded.ForRoom(4).Single().frames[0];
        Check(loaded.version == 4 && loaded.returnRoom == 4 && frame.zone == 4 &&
            frame.x == EchoHistory.TimedRoomInsertionWidth, "Old save moves the shared-room progress and recording to room 4");
        return JsonUtility.ToJson(new Result { passed = checks.ToArray(), frame = Time.frameCount });
    }
}
