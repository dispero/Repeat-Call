var g=UnityEngine.Object.FindAnyObjectByType<Golovomka.EchoMvp>();
void Check(bool v,string m){if(!v)throw new System.Exception(m);}
Check(g.history.copySlots==2,"Second slot unlocked after crossing");
g.Interact("Пульт аварийного прохода");Check(g.history.manualRecording&&g.recordingActive,"Manual unlock does not interrupt current recording");
g.elapsed=10;g.position=Golovomka.EchoMvp.PlateB;g.Record();g.RestartCycle();
Check(g.history.tapes.Count==2,"Two recordings retained");
g.Interact("Телефон дежурного");g.Interact("Отправиться в лабораторию");
Check(!g.recordingActive&&g.elapsed==0&&g.recording.frames.Count==0,"Manual mode waits on basement entry");
g.UpdateGhosts();
foreach(var s in UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>())if(s.name.StartsWith("Echo"))Check(!s.enabled,"Ghosts wait for manual start");
g.StartRecording();Check(g.recordingActive&&g.recording.frames[0].t==0&&g.recording.frames[0].zone==1,"Manual start");
g.elapsed=1;g.StartRecording();Check(g.elapsed==1,"Repeated start does not reset");
var loaded=Golovomka.EchoHistory.Load(g.saveFileOverride);Check(loaded.copySlots==2&&loaded.manualRecording,"Unlocks persisted");
g.BeginCycle();int count=g.history.tapes.Count;g.RestartCycle();Check(g.history.tapes.Count==count,"Empty cycle preserves recordings");
g.mode="pause";
return "PASS: second copy unlock, manual unlock, no early ghosts, start from zero, duplicate start ignored, unlock persistence, empty cycle";
