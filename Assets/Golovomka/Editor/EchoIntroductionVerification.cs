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
        Check(m.position==spawn && m.history.firstReturnSeen && !m.history.remoteObtained && m.history.ForRoom(1) != null,"First return creates one recording at room entrance");
        Check(m.mode == "dialog" && m.elapsed == 0 && m.recordingActive,
            "The remote explanation opens at the entrance before playback advances");
        Call("Update");
        Check(m.elapsed == 0 && m.recording.frames.Count == 1, "The explanation pauses the attempt clock");
        Call("Close");
        Check(m.mode == "play" && m.elapsed == 0, "Closing the explanation resumes from the common zero");
        Check(m.history.ForRoom(1).Sample(onPlateTime,out var f) && Vector2.Distance(new Vector2(f.x,f.y),plate)<.1f,"First copy repeats panel approach");
        m.elapsed=onPlateTime; m.UpdateGhosts(); Check(m.plateA,"First copy opens door");
        m.elapsed=m.history.ForRoom(1).duration+.1f; m.UpdateGhosts(); Check(!m.plateA,"Copy ends and releases panel");
        m.position=remote;
        nearby = (Interaction)Call("Nearby");
        Check(nearby == Interaction.PickUpRemote,"Second interaction offers pickup");
        m.Interact(nearby); Check(m.history.remoteObtained && m.mode == "play", "Remote pickup does not reopen the explanation");
        m.RestartCycle(); Move(plate); float holdStart=m.elapsed;
        for(int i=0;i<600;i++){m.elapsed+=.02f;m.Record();}
        m.RestartCycle(); Check(m.zone==1 && m.position==spawn && m.elapsed==0 && m.recordingActive,"R resets current room and common clock");
        Check(m.history.ForRoom(1) != null,"One recording limit in first room");
        m.elapsed=holdStart+1; m.UpdateGhosts(); Check(m.plateA,"Prepared copy holds panel");
        Vector2 inside = intro.door.Passage.bounds.center;
        var doorApproach = intro.door.transform.Find("Door Approach").GetComponent<BoxCollider2D>();
        var approach = new Vector2(doorApproach.bounds.min.x - .27f, inside.y);
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
            Vector2 threshold = bounds.center + Vector3.right * passage.crossingOffset;
            if (room.roomNumber == 4)
                Check(threshold.x < bounds.min.x - .22f &&
                    !passage.Crossed(bounds.center - Vector3.right * .05f, bounds.center + Vector3.right * .05f, true),
                    "Finale entry starts in the range before Ivan's feet reach the doorway");
            Vector2 before = threshold - Vector2.right * .05f;
            Vector2 after = threshold + Vector2.right * .05f;
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
        Check(m.history.ForRoom(1) != null && m.recording.frames.Count == 1 && m.recording.frames[0].zone == 2,
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
        Check(m.position==(Vector2)room2.entrance.position && m.elapsed==0 && m.history.ForRoom(2) != null,
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
        Check(!m.recording.frames[0].pistolDrawn, "Room 3 first entry starts with the pistol holstered");
        m.RestartCycle();
        Check(m.position == (Vector2)room3.entrance.position && CanStand(m.position) &&
            m.position.x > sequence.second.door.Passage.bounds.max.x, "R spawns beyond the closed B door");
        bool SpawnPistolIs(bool drawn) => m.recording.frames[0].pistolDrawn == drawn &&
            ((SpriteRenderer)typeof(EchoMvp).GetField("hero", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(m)).sprite.name.StartsWith("IvanPistol_") == drawn;
        Check(SpawnPistolIs(false) && !(bool)typeof(EchoMvp)
            .GetProperty("CanDrawPistol", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m),
            "Room 3 retry starts without access to a pistol");
        Check(buttons.IsConfigured && !buttons.exitDoor.Passage.isTrigger,
            "Room 3 starts with one copy slot and a closed door");
        m.Interact(Interaction.UseRoomPassage);
        Check(m.zone == 2 && m.history.ForRoom(2) != null && m.history.returnRoom == 3,
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
        Check(m.history.ForRoom(3).buttonPresses.Count == 4 &&
            EchoHistory.Load(m.saveFileOverride).ForRoom(3).buttonPresses.Count == 4,
            "R saves four timed button events for the copy");
        m.elapsed = buttons.EndTime + .5f;
        m.UpdateGhosts();
        Check(!buttons.Solved, "Missing the three live presses leaves the door closed");
        m.RestartCycle();
        Check(m.history.ForRoom(3).buttonPresses.Count == 4, "An empty retry retains the useful recording");
        foreach (int step in new[] { 1, 3, 5 }) PressButton(step);
        m.elapsed = buttons.EndTime;
        m.UpdateGhosts();
        Check(buttons.Solved && buttons.exitDoor.Passage.isTrigger, "Three live presses and four copy presses open the exit");
        CrossDoor(room3, buttons.exitDoor, staysOpen: true);
        var room4 = rooms.Single(room => room.roomNumber == 4);
        var range = room4.GetComponent<TargetRangeRoom>();
        Check(m.zone == 4 && m.history.returnRoom == 4 && range.IsConfigured &&
            !range.exitDoor.Passage.isTrigger, "Room 4 starts with seven targets and a closed exit");
        var pairs = new[] { (1, 5), (0, 3), (2, 6), (1, 4), (0, 5) };
        bool ShootTarget(int index)
        {
            Vector2 target = range.TargetPosition(index);
            Vector2 origin = target - Vector2.up * 2f;
            Check(CanStand(origin - Vector2.up * TargetRangeRoom.MuzzleHeight), "The firing position below the target is walkable");
            return range.Shoot(origin, Vector2.up, m.elapsed, m.recording);
        }
        Check(!range.Shoot(range.TargetPosition(0) - Vector2.one, Vector2.one.normalized, m.elapsed, m.recording),
            "The firing range rejects diagonal shots");
        for (int wave = 0; wave < pairs.Length; wave++)
        {
            float start = TargetRangeRoom.WaveStart(wave);
            Check(TargetRangeRoom.ActiveWave(start + 1.19f) == wave && TargetRangeRoom.ActiveWave(start + 1.21f) == -1,
                $"Wave {wave + 1} is hittable for 1.2 seconds");
            m.elapsed = start + .05f;
            m.UpdateGhosts();
            Check(range.targets.Count(target => target.enabled) == 2, $"Two targets rise in wave {wave + 1}");
            Check(ShootTarget(pairs[wave].Item1), $"Upward shot in wave {wave + 1}");
            m.elapsed += .7f;
            Check(!ShootTarget(pairs[wave].Item2), $"The pistol cannot fire again within wave {wave + 1}");
        }
        m.elapsed = TargetRangeRoom.WaveStart(pairs.Length - 1) + 1.4f;
        m.UpdateGhosts();
        Check(range.targets.All(target => !target.enabled) && range.HasCompleteRecording &&
            !range.exitDoor.Passage.isTrigger, "Five single hits prepare a recording and leave the exit closed");
        m.RestartCycle();
        var savedRangeTape = EchoHistory.Load(m.saveFileOverride)?.ForRoom(4);
        Check(savedRangeTape?.shots.Count == 5 && savedRangeTape.shots.All(shot => shot.direction == Vector2.up),
            "Save retains all five upward shot vectors");
        Check(SpawnPistolIs(true), "Room 4 retry starts and records a drawn pistol");
        m.elapsed = TargetRangeRoom.WaveStart(4) + 1f;
        m.UpdateGhosts();
        Check(!range.Solved, "The recording alone cannot solve the range");
        m.RestartCycle();
        Check(m.history.ForRoom(4)?.shots.Count == 5, "An empty retry retains the recording");
        for (int wave = 0; wave < pairs.Length; wave++)
        {
            m.elapsed = TargetRangeRoom.WaveStart(wave) + 1.19f;
            m.UpdateGhosts();
            Check(range.targets.Count(target => target.enabled && target.transform.localScale.y == 1f) == 2 &&
                ShootTarget(pairs[wave].Item2), $"Both targets are fully raised for the late live shot in wave {wave + 1}");
            m.UpdateGhosts();
        }
        Check(range.Solved && range.exitDoor.Passage.isTrigger, "Five pairs open the exit");
        CrossDoor(room4, range.exitDoor, staysOpen: true);
        var tail = rooms.Single(room => room.roomNumber == 5);
        var finale = tail.GetComponent<FinaleRoom>();
        Check(finale && finale.IsConfigured && m.zone == 5 && m.recordingActive &&
            EchoHistory.Load(m.saveFileOverride)?.returnRoom == 5, "The exit saves the next return point");
        m.RestartCycle();
        Check(m.zone == 5 && m.recordingActive && m.elapsed == 0 && m.position == (Vector2)tail.entrance.position &&
            CanStand(m.position) && m.position.x < range.exitDoor.Passage.bounds.min.x - .22f && range.exitDoor.Passage.isTrigger,
            "R returns to the finale entrance and starts the recording clock");
        Check(SpawnPistolIs(true), "Room 5 retry starts and records a drawn pistol");

        VerifyLookoutAnimation(finale, m.position, Check);
        VerifyLookoutCover(finale, m.position, Check);

        Vector2 chaseStart = finale.pursuer.transform.position;
        Vector2 lookoutStart = finale.lookout.transform.position;
        Vector2 guardStart = finale.guard.transform.position;
        Vector2 upper = new Vector2(finale.upperApproach.bounds.center.x, -.44f);
        Vector2 lower = new Vector2(guardStart.x, -4.7f);
        Check(CanStand(upper) && CanStand(lower), "Both approaches have walkable positions");
        Check(tail.CameraBounds.size.x > 20f && CanStand(new Vector2(120f, -7.3f)),
            "The expanded room has a walkable lower lane and its own camera frame");
        finale.Tick(0, .1f, m.position, upper);
        Check(finale.Failure != null && !finale.fyodor.gameObject.activeSelf,
            "The hostage guard detects the copy on the upper approach");
        m.RestartCycle();
        Check(finale.Failure == null && finale.fyodor.gameObject.activeSelf, "A retry restores the hostages");
        finale.Tick(0, .1f, lower, null);
        Check(finale.Failure == null, "The lower approach does not alert the hostage guard");

        Vector2 copyInSight = chaseStart + Vector2.right * 2.3f;
        finale.Tick(0, .2f, lower, copyInSight);
        finale.Tick(0, .65f, lower, copyInSight);
        Check(finale.CopyHit && finale.Failure == null, "A gunman's shot can end only the copy's playback");
        m.RestartCycle();

        finale.Shoot(new Vector2(110.1f, -2.6f), Vector2.right, .2f, m.recording);
        Check(finale.guard.gameObject.activeSelf && finale.lookout.gameObject.activeSelf,
            "A distraction shot is stopped by the central equipment");
        for (int i = 0; i < 36; i++) finale.Tick(.025f, .225f + i * .025f, m.position, null);
        Check(Vector2.Distance(lookoutStart, finale.lookout.transform.position) > 1f,
            "The distraction makes the lower guard leave his position");
        m.elapsed = 1.2f;
        m.RestartCycle();
        var finaleTape = EchoHistory.Load(m.saveFileOverride)?.ForRoom(5);
        Check(finaleTape?.shots.Count == 1 && finaleTape.shots[0].direction == Vector2.right,
            "The finale shot survives a save and load");
        m.elapsed = .22f;
        m.UpdateGhosts();
        Call("UpdateFinale", .02f, false, false);
        Check(finale.Status(m.elapsed).Contains("ОТВЛЕЧЁН"), "The saved copy replays its distraction shot");
        m.elapsed = finaleTape.duration + .01f;
        m.UpdateGhosts();
        var ghost = (SpriteRenderer)typeof(EchoMvp).GetField("ghost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m);
        Check(!ghost.enabled, "The finale copy disappears at the end of playback");
        m.RestartCycle();

        void ShootFrom(Vector2 p, Vector2 direction, float time)
        {
            Check(CanStand(p), "Firing position is on the floor: " + p);
            finale.Shoot(p, direction, time, m.recording);
        }

        ShootFrom((Vector2)finale.fyodor.transform.position + Vector2.down, Vector2.up, .1f);
        Check(finale.Failure != null && !finale.fyodor.gameObject.activeSelf, "An accidental shot at a hostage fails the attempt");
        m.elapsed = .2f;
        m.RestartCycle();

        ShootFrom(chaseStart + Vector2.left * 1.15f, Vector2.right, .1f);
        ShootFrom(lookoutStart + Vector2.right * 1.1f, Vector2.left, .7f);
        Check(finale.Failure != null && finale.guard.gameObject.activeSelf,
            "Killing both gunmen before the guard fails the attempt");
        m.elapsed = 1f;
        m.RestartCycle();
        Check(finale.pursuer.gameObject.activeSelf && finale.lookout.gameObject.activeSelf &&
            finale.lookout.transform.position == (Vector3)lookoutStart, "All bandits return to their start positions");

        ShootFrom(chaseStart + Vector2.left * 1.15f, Vector2.right, .1f);
        ShootFrom(lower, Vector2.up, .7f);
        ShootFrom(lookoutStart + Vector2.right * 1.1f, Vector2.left, 1.3f);
        Check(finale.Cleared && finale.Failure == null, "The guard can be defeated from below before the last gunman");
        m.elapsed = 1.4f;
        m.position = finale.fyodor.transform.position;
        Call("UpdateFinale", 0f, false, true);
        Check(!finale.Complete && m.mode == "play", "Freeing Fyodor alone does not end the game");
        m.position = finale.alexei.transform.position;
        Call("UpdateFinale", 0f, false, true);
        Check(finale.Complete && m.mode == "done" && !m.recordingActive && EchoHistory.Load(m.saveFileOverride).completed,
            "Both releases finish the game and save completion");
        m.history = EchoHistory.Load(m.saveFileOverride);
        m.BeginCycle();
        Call("ActivateRoom", room1, null, false, false);
        m.RestartCycle();
        Check(finale.Complete && !m.recordingActive && m.mode == "done", "The reopened save restores the completed finale");

        var old = new EchoHistory { version = 3, returnRoom = 3, firstReturnSeen = true, remoteObtained = true };
        old.tapes.Add(new EchoTape { room = 3, duration = 3 });
        old.tapes.Add(new EchoTape { room = 3, duration = 4 });
        old.Save(m.saveFileOverride);
        var loaded = EchoHistory.Load(m.saveFileOverride);
        Check(loaded?.version == 8 && loaded.returnRoom == 4 && loaded.ForRoom(4) == null,
            "Legacy progress survives while old room 4 tapes are dropped");
        return JsonUtility.ToJson(new Result { passed = checks.ToArray(), frame = Time.frameCount });
    }

    static void VerifyLookoutAnimation(FinaleRoom finale, Vector2 entry, Action<bool, string> check)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var lookout = typeof(FinaleRoom).GetField("sentry", instance).GetValue(finale);
        var trace = typeof(FinaleRoom).GetField("lookoutTrace", instance).GetValue(finale);
        var line = (LineRenderer)trace.GetType().GetField("line").GetValue(trace);
        var facing = lookout.GetType().GetField("direction");
        var show = typeof(FinaleRoom).GetMethod("ShowBandit", BindingFlags.Static | BindingFlags.NonPublic);
        var fire = typeof(FinaleRoom).GetMethod("FireBandit", instance);
        var body = finale.lookout.GetComponent<SpriteRenderer>();
        var flash = finale.lookoutGun;

        void CheckFrame(string clip, string direction, string frame, bool moving, float time, bool charging = false)
        {
            show.Invoke(null, new object[] { lookout, moving, time, charging });
            string expected = $"DenimJacketPistol{clip}_{direction}_{frame}";
            check(body.sprite.name == expected, $"Lookout displays {expected}");
        }

        try
        {
            foreach (var (direction, name) in new[]
            {
                (Vector2.right, "Right"), (Vector2.left, "Left"), (Vector2.up, "Back"), (Vector2.down, "Front")
            })
            {
                finale.Begin(entry);
                facing.SetValue(lookout, direction);
                CheckFrame("Walk", name, "Ready", false, 0);
                CheckFrame("Walk", name, "StepA", true, .01f);
                CheckFrame("Walk", name, "Pass", true, .20f);
                CheckFrame("Walk", name, "StepB", true, .35f);
                CheckFrame("Shoot", name, "Aim", false, .50f, charging: true);
                check(!flash.enabled, "Lookout keeps the separate pistol hidden while aiming");

                fire.Invoke(finale, new object[] { lookout, entry + Vector2.up * 100f, null, direction, 1f, trace });
                facing.SetValue(lookout, -direction);
                CheckFrame("Shoot", name, "Flash", false, 1.02f);
                check(flash.enabled == (direction == Vector2.up), $"Lookout uses a separate flash only from behind ({name})");
                if (direction == Vector2.up)
                {
                    Vector2 start = line.GetPosition(0);
                    Vector2 end = line.GetPosition(1);
                    check(Mathf.Abs(start.x - body.transform.position.x) < .001f &&
                        Mathf.Abs(end.x - start.x) < .001f && Vector2.Distance(flash.transform.position, start) < .001f,
                        "Lookout's rear shot and flash share the center line");
                    check(flash.sortingOrder < body.sortingOrder && line.sortingOrder < body.sortingOrder,
                        "Lookout's rear shot is sorted behind the body");
                }
                CheckFrame("Shoot", name, "Recoil", false, 1.10f);
                check(!flash.enabled, $"Lookout flash ends before recoil ({name})");
                CheckFrame("Shoot", name, "Recover", false, 1.19f);
                facing.SetValue(lookout, direction);
                CheckFrame("Walk", name, "Ready", false, 1.26f);
            }
        }
        finally
        {
            finale.Begin(entry);
        }
        check(!flash.enabled && !line.enabled && body.sprite.name == "DenimJacketPistolWalk_Left_Ready",
            "Finale retry clears the lookout's shot and restores the idle pose");
    }

    static void VerifyLookoutCover(FinaleRoom finale, Vector2 entry, Action<bool, string> check)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            finale.pursuer.gameObject.SetActive(false);
            Vector2 start = finale.lookout.transform.position;
            Vector2 player = start + new Vector2(-4.15f, .74f);
            var lookout = typeof(FinaleRoom).GetField("sentry", instance).GetValue(finale);
            var firedAt = lookout.GetType().GetField("firedAt");
            for (int step = 0; step < 100 && (float)firedAt.GetValue(lookout) < 0f; step++)
                finale.Tick(.05f, step * .05f, player, null);
            check(Vector2.Distance(start, finale.lookout.transform.position) > .2f,
                "Lookout leaves the blocked firing position beside the central equipment");
            check((float)firedAt.GetValue(lookout) >= 0f && finale.Failure == "Иван погиб.",
                "Lookout fires only after reaching a position with a clear shot");
        }
        finally
        {
            finale.Begin(entry);
        }
    }
}
