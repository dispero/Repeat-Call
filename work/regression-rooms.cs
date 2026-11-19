using System;
using System.Reflection;
using UnityEngine;
using Golovomka;
var m=UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
var oldHistory=m.history;var oldPosition=m.position;var oldElapsed=m.elapsed;var oldRecording=m.recording;var oldSave=m.saveFileOverride;
m.saveFileOverride=System.IO.Path.Combine(Application.dataPath,"../work/regression-rooms-save.json");
object Call(string name,params object[] args)=>typeof(EchoMvp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,args);
void Check(bool ok,string label){if(!ok)throw new Exception(label);}
try {
 m.history=new EchoHistory{firstReturnSeen=true,remoteObtained=true};m.zone=2;m.position=new Vector2(-9,-3.5f);Call("ResetRoomRecording");m.BuildRoom();
 m.position=EchoMvp.PlateA;m.UpdateGhosts();Check(m.plateA&&!m.plateB,"Independent A");
 m.position=m.CurrentPlateB;m.UpdateGhosts();Check(!m.plateA&&m.plateB,"Independent B");
 m.position=new Vector2(0,3.7f);m.Interact("Пульт аварийного прохода");Call("Close");
 Check(m.zone==3&&m.history.copySlots==2&&m.recordingActive,"Third room unlocks second copy");
 for(int i=0;i<2;i++){
  m.position=i==0?EchoMvp.PlateA:m.CurrentPlateB;m.elapsed=20;m.Record();m.RestartCycle();
  Check(m.zone==3&&m.position==new Vector2(-9,-3.5f),"Third room local R");
 }
 Check(m.history.ForRoom(3).Count==2,"Third room two records");
 m.position=new Vector2(6,2);m.Interact("Пульт аварийного прохода");Call("Close");
 Check(m.zone==4&&m.history.manualRecording&&!m.recordingActive,"Fourth manual start");
 Check(m.history.ForRoom(3).Count==2,"Fourth entry preserves earlier room history");
 m.RestartCycle();Check(m.zone==4&&!m.recordingActive&&m.history.ForRoom(4).Count==0,"Empty manual return preserves history");
 m.Interact("Пульт записи");Call("Close");Check(m.recordingActive&&m.elapsed==0,"Manual common zero");
 for(int i=0;i<2;i++){
  m.position=i==0?EchoMvp.PlateA:m.CurrentPlateB;m.elapsed=20;m.Record();m.RestartCycle();
  Check(m.zone==4&&!m.recordingActive,"Fourth local return waits for command");m.StartRecording();
 }
 Check(m.history.ForRoom(4).Count==2,"Fourth room two records");
 return new{result="PASS: rooms 2–4, independent plates, slot unlock, local returns, history retention, manual start",frame=Time.frameCount};
} finally {
 m.history=oldHistory;m.zone=1;m.position=oldPosition;m.elapsed=oldElapsed;m.recording=oldRecording;m.recordingActive=true;m.mode="play";m.saveFileOverride=oldSave;Call("Close");m.BuildRoom();
}
