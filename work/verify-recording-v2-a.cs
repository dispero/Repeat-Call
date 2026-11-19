var g=UnityEngine.Object.FindAnyObjectByType<Golovomka.EchoMvp>();
void Check(bool v,string m){if(!v)throw new System.Exception(m);}
g.saveFileOverride=System.IO.Path.GetFullPath("work/recording-v2-test.json");g.history=new Golovomka.EchoHistory();g.BeginCycle();
Check(!g.recordingActive&&g.recording.frames.Count==0&&g.elapsed==0,"Station must not record");
g.StartRecording();Check(!g.recordingActive,"Cannot start in station");
g.Interact("Телефон дежурного");g.Interact("Отправиться в лабораторию");
Check(g.recordingActive&&g.elapsed==0&&g.recording.frames.Count==1&&g.recording.frames[0].zone==1,"Automatic basement start");
g.elapsed=2;g.position=Golovomka.EchoMvp.PlateA;g.Record();g.RestartCycle();
Check(g.history.tapes.Count==1&&g.history.copySlots==1&&!g.recordingActive,"First copy cap");
g.Interact("Телефон дежурного");g.Interact("Отправиться в лабораторию");
g.position=new UnityEngine.Vector2(0,1.5f);g.mode="play";
return "PASS: station excluded, automatic basement start at zero, first copy cap. Waiting for crossing trigger.";
