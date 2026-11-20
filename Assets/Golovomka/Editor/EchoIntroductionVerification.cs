using System;
using System.Collections.Generic;
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
        var labRoots = SceneManager.GetSceneByPath(Room01Layout.ScenePath).GetRootGameObjects();
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
        var intro = UnityEngine.Object.FindAnyObjectByType<Room01Layout>();
        Check(intro && Array.TrueForAll(stationRoots, root => !root.activeSelf), "Laboratory scene active and station roots hidden");
        var preview = UnityEngine.Object.FindAnyObjectByType<Room02Layout>();
        Check(preview && preview.gameObject.scene == intro.gameObject.scene &&
            preview.transform.parent == intro.transform.parent, "Room 2 preview shares the laboratory scene");
        Check(preview.entrance.position.x > intro.doorway.bounds.max.x, "Room 2 preview lies beyond the demo exit");
        Vector2 spawn = intro.entrance.position;
        Check(m.zone==1 && m.position==spawn && m.recordingActive && m.recording.frames[0].zone==1, "Basement starts recording at its scene marker");
        var wallColliders = intro.transform.parent.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>();
        Check(Array.Exists(wallColliders, wall => wall.name == "Стены" && wall.shapeCount > 0), "Laboratory wall tiles generate solid shapes");
        m.Interact(Interaction.ReturnToStation);
        Check(station.gameObject.activeInHierarchy && Array.TrueForAll(labRoots, root => !root.activeSelf) &&
            m.position == (Vector2)station.entrance.position && !m.recordingActive && m.history.tapes.Count == 0,
            "Returning to the station discards the unfinished attempt and restores its scene");
        m.Interact(Interaction.AnswerPhone); Call("Close"); m.Interact(Interaction.EnterLaboratory);
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
        Vector2 inside = intro.doorway.bounds.center;
        var approach = new Vector2(intro.doorway.bounds.min.x - .27f, inside.y);
        m.position=approach; Check(Can(inside),"Open mechanical passage allows entry");
        m.elapsed=1000; m.UpdateGhosts(); Check(intro.doorBlocker.enabled && !Can(inside),"Closed door blocks entry");
        m.elapsed=holdStart+1; m.UpdateGhosts();
        m.position=inside; m.elapsed=1000; m.UpdateGhosts();
        Check(!intro.doorBlocker.enabled && Can(approach),"Closing door allows escape from opening");
        var sofa = intro.furniture.Find("Диван").GetComponent<BoxCollider2D>();
        var desk = intro.remoteSprite.GetComponentInParent<BoxCollider2D>();
        Check(!CanStand(sofa.bounds.center),"Sofa collision");
        Check(!CanStand(desk.bounds.center),"Desk collision");
        m.position=approach; m.elapsed=holdStart+1; m.UpdateGhosts();
        Vector2 exit = intro.exit.position;
        var beyondExit = new Vector2(exit.x + .03f, Mathf.Max(exit.y + .03f, inside.y));
        Check(Can(beyondExit),"Prepared copy allows reaching the demo exit");
        m.position=beyondExit;
        Check((bool)Call("AtIntroductionExit"),"Crossing the right exit triggers completion");
        Call("Update");
        Check(m.zone==1 && m.mode=="done" && m.history.clearedRooms==1 && !m.recordingActive,"Demo ends in room 1");
        Check(m.history.ForRoom(1).Count==1 && m.recording.frames.Count==0,"Completion preserves the last recording and discards the current attempt");
        var finished=EchoHistory.Load(m.saveFileOverride);
        Check(finished!=null && finished.clearedRooms==1 && finished.remoteObtained,"Room 1 completion survives save/load");
        var h=new EchoHistory {copySlots=2,firstReturnSeen=true,remoteObtained=true};
        EchoTape T(int z,float duration)=>new EchoTape {room=z,duration=duration,frames=new List<EchoFrame>{new EchoFrame(0,Vector2.zero,z,0)}};
        h.Push(T(1,1));h.Push(T(1,2));h.Push(T(3,3));h.Push(T(3,4));h.Push(T(3,5));
        Check(h.ForRoom(1).Count==1 && h.ForRoom(3).Count==2 && h.ForRoom(3)[0].duration==4,"Per-room limits and oldest replacement");
        h.Save(m.saveFileOverride);var loaded=EchoHistory.Load(m.saveFileOverride);
        Check(loaded!=null && loaded.remoteObtained && loaded.firstReturnSeen && loaded.ForRoom(3).Count==2,"Pickup, first return and room recordings survive save/load");
        return JsonUtility.ToJson(new Result { passed = checks.ToArray(), frame = Time.frameCount });
    }
}
