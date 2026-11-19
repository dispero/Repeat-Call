var g=UnityEngine.Object.FindAnyObjectByType<Golovomka.EchoMvp>();
void Check(bool v,string message){if(!v)throw new System.Exception(message);}
Golovomka.EchoTape Hold(int room,UnityEngine.Vector2 p){return new Golovomka.EchoTape{duration=100,frames=new System.Collections.Generic.List<Golovomka.EchoFrame>{new Golovomka.EchoFrame(0,p,room,3),new Golovomka.EchoFrame(100,p,room,3)}};}
bool Move(UnityEngine.Vector2 p){return (bool)typeof(Golovomka.EchoMvp).GetMethod("CanMove",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(g,new object[]{p});}
g.saveFileOverride=System.IO.Path.GetFullPath("work/four-room-save.json");g.history=new Golovomka.EchoHistory();g.BeginCycle();
Check(UnityEngine.Vector2.Distance(g.position,new UnityEngine.Vector2(-4,1))<1.6f,"Spawn near phone");
g.Interact("Телефон дежурного");g.Interact("Отправиться в лабораторию");Check(g.zone==1&&g.recordingActive&&g.RoomCopies==1,"Room one automatic and one copy");
g.history.tapes.Add(Hold(1,Golovomka.EchoMvp.PlateA));g.elapsed=1;g.position=new UnityEngine.Vector2(6,2);g.UpdateGhosts();g.Interact("Пульт аварийного прохода");
Check(g.zone==2&&g.history.copySlots==1,"Room two still one slot");
g.history.tapes.Clear();g.history.tapes.Add(new Golovomka.EchoTape{duration=100,frames=new System.Collections.Generic.List<Golovomka.EchoFrame>{new Golovomka.EchoFrame(0,Golovomka.EchoMvp.PlateA,2,3),new Golovomka.EchoFrame(2,Golovomka.EchoMvp.PlateA,2,3),new Golovomka.EchoFrame(3,g.CurrentPlateB,2,3),new Golovomka.EchoFrame(100,g.CurrentPlateB,2,3)}});
g.position=new UnityEngine.Vector2(0,1.3f);g.elapsed=1;g.UpdateGhosts();Check(g.plateA&&!g.plateB&&Move(new UnityEngine.Vector2(0,0))&&!Move(new UnityEngine.Vector2(0,2.5f)),"Room two first gate only");
g.elapsed=4;g.UpdateGhosts();Check(!g.plateA&&g.plateB&&Move(new UnityEngine.Vector2(0,2.5f)),"Room two second gate only");
g.position=new UnityEngine.Vector2(0,3.7f);g.Interact("Пульт аварийного прохода");Check(g.zone==3&&g.history.copySlots==2&&!g.history.manualRecording,"Second copy only in room three");
g.history.tapes.Clear();g.history.tapes.Add(Hold(3,Golovomka.EchoMvp.PlateA));g.history.tapes.Add(Hold(3,g.CurrentPlateB));g.elapsed=1;g.position=new UnityEngine.Vector2(6,2);g.UpdateGhosts();Check(g.plateA&&g.plateB,"Two-copy room three gate");
g.Interact("Пульт аварийного прохода");Check(g.zone==4&&g.history.manualRecording&&!g.recordingActive&&g.history.tapes.Count==0,"Room four isolated manual stage");
g.Interact("Пульт записи");Check(g.recordingActive&&g.recording.frames[0].zone==4,"Console starts room four recording");
g.elapsed=1;g.Record();g.RestartCycle();g.Interact("Телефон дежурного");g.Interact("Отправиться в лабораторию");Check(!g.recordingActive&&g.RoomCopies==1,"Manual-stage return and room one cap");
g.Interact("Пульт аварийного прохода");g.Interact("Пульт аварийного прохода");g.Interact("Пульт аварийного прохода");Check(g.zone==4&&!g.recordingActive&&g.history.tapes.Count==1,"Cleared-room bypass and manual history retained");
g.StartRecording();g.history.tapes.Clear();g.history.tapes.Add(Hold(4,Golovomka.EchoMvp.PlateA));g.history.tapes.Add(Hold(4,g.CurrentPlateB));g.elapsed=1;g.position=new UnityEngine.Vector2(6,2);g.UpdateGhosts();g.Interact("Пульт аварийного прохода");Check(g.history.completed,"Four-room demo completion");
Check(Golovomka.EchoHistory.Load(g.saveFileOverride).clearedRooms==3,"Progress saved");
return "PASS: phone spawn, four rooms, one clone rooms 1/2, sequential gates, two slots room 3, console room 4, recording history retained, completion and save";
