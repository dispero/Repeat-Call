var g=UnityEngine.Object.FindAnyObjectByType<Golovomka.EchoMvp>();
void Check(bool value,string name) { if(!value) throw new System.Exception(name); }
Golovomka.EchoTape Tape(UnityEngine.Vector2 plate,float duration) {
 return new Golovomka.EchoTape { duration=duration, frames=new System.Collections.Generic.List<Golovomka.EchoFrame>{
 new Golovomka.EchoFrame(0,new UnityEngine.Vector2(0,-2.5f),0,3),
 new Golovomka.EchoFrame(2,new UnityEngine.Vector2(-9,-3.5f),1,0),
 new Golovomka.EchoFrame(10,plate,1,3), new Golovomka.EchoFrame(duration,plate,1,3)} };
}
g.saveFileOverride=System.IO.Path.GetFullPath("work/smoke-save.json");
g.history=new Golovomka.EchoHistory(); g.BeginCycle();
g.Interact("Телефон дежурного");
g.mode="play";
g.Interact("Отправиться в лабораторию");
Check(g.zone==1,"Travel interaction");
Check(g.recording.frames.Exists(f=>f.zone==0)&&g.recording.frames.Exists(f=>f.zone==1),"Full route recorded");
g.history.tapes.Add(Tape(Golovomka.EchoMvp.PlateA,30));
g.history.tapes.Add(Tape(Golovomka.EchoMvp.PlateB,25));g.history.cycle=3;
g.elapsed=12;g.zone=1;g.position=new UnityEngine.Vector2(6,2);g.BuildRoom();g.UpdateGhosts();
Check(g.plateA&&g.plateB,"Both plates from two copies");
g.Interact("Пульт аварийного прохода");Check(g.mode=="done"&&g.history.completed,"Demo completion");
Check(Golovomka.EchoHistory.Load(g.saveFileOverride).completed,"Completion persisted");
g.history.completed=false;g.elapsed=26;g.UpdateGhosts();Check(g.plateA&&!g.plateB,"Second copy expired");
g.elapsed=31;g.UpdateGhosts();Check(!g.plateA&&!g.plateB,"Both copies expired");
g.history=new Golovomka.EchoHistory();g.BeginCycle();g.zone=1;g.position=new UnityEngine.Vector2(6,2);g.BuildRoom();g.Interact("Пульт аварийного прохода");Check(!g.history.completed&&g.mode=="dialog","No solo bypass");
return "PASS: travel, full-route recording, two-copy plates, completion and save, independent expiry, no solo bypass";
