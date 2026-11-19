using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Golovomka;
var m = UnityEngine.Object.FindFirstObjectByType<EchoMvp>();
if (!m) throw new Exception("Missing game");
var checks = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); }
object Call(string name, params object[] args) => typeof(EchoMvp).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m, args);
Vector2 Pt(float x,float y) => new Vector2((x-799.5f)*.0145f,(492-y)*.0145f);
bool Can(Vector2 p) => (bool)Call("CanMove", p);
void Walk(Vector2 target) {
    while(Vector2.Distance(m.position,target)>.001f) {
        var p = Vector2.MoveTowards(m.position,target,.07f);
        if(!Can(p)) throw new Exception("Blocked route at "+p+" toward "+target);
        m.position=p; m.elapsed+=.02f; m.Record(); m.UpdateGhosts();
    }
}
m.saveFileOverride=System.IO.Path.GetFullPath("work/intro-verification-save.json");
m.history=new EchoHistory(); m.BeginCycle();
m.RestartCycle(); Check(m.zone==0 && !m.recordingActive && m.history.tapes.Count==0,"No recording or R in police station");
m.Interact("Отправиться в лабораторию"); Check(m.zone==0,"Phone required");
Call("Close"); m.Interact("Телефон дежурного"); Call("Close"); m.Interact("Отправиться в лабораторию");
Check(m.zone==1 && m.position==EchoMvp.IntroSpawn && m.recordingActive,"Basement start");
m.RestartCycle(); Check(m.history.cycle==1 && m.position==EchoMvp.IntroSpawn,"R locked before obtaining remote");
Walk(EchoMvp.IntroPlate); Check(m.plateA,"First approach presses panel");
float onPlateTime=m.elapsed;
Walk(Pt(1190,650)); Check(!m.plateA,"Leaving panel removes signal");
Check((string)Call("Nearby")=="Осмотреть пульт","Remote reachable from floor");
m.Interact("Осмотреть пульт");
Check(m.position==EchoMvp.IntroSpawn && m.history.firstReturnSeen && !m.history.remoteObtained && m.history.ForRoom(1).Count==1,"First return creates one recording at room entrance");
Check(m.history.ForRoom(1)[0].Sample(onPlateTime,out var f) && Vector2.Distance(new Vector2(f.x,f.y),EchoMvp.IntroPlate)<.1f,"First copy repeats panel approach");
m.elapsed=onPlateTime; m.UpdateGhosts(); Check(m.plateA,"First copy opens door");
m.elapsed=m.history.ForRoom(1)[0].duration+.1f; m.UpdateGhosts(); Check(!m.plateA,"Copy ends and releases panel");
m.position=Pt(1190,650); Check((string)Call("Nearby")=="Взять пульт","Second interaction offers pickup");
m.Interact("Взять пульт"); Check(m.history.remoteObtained,"Remote pickup");
// Новая попытка: реальный маршрут через CanMove, затем ожидание на панели.
m.RestartCycle(); Walk(EchoMvp.IntroPlate); float holdStart=m.elapsed;
for(int i=0;i<600;i++){m.elapsed+=.02f;m.Record();}
m.RestartCycle(); Check(m.zone==1 && m.position==EchoMvp.IntroSpawn && m.elapsed==0 && m.recordingActive,"R resets current room and common clock");
Check(m.history.ForRoom(1).Count==1,"One recording limit in first room");
m.elapsed=holdStart+1; m.UpdateGhosts(); Check(m.plateA,"Prepared copy holds panel");
m.position=Pt(1350,670); Check(Can(Pt(1410,670)),"Open mechanical passage allows entry");
m.elapsed=1000; m.UpdateGhosts(); Check(!Can(Pt(1410,670)),"Closed door blocks entry");
m.position=Pt(1410,670); Check(Can(Pt(1350,670)),"Closing door allows escape from opening");
Check(!Can(Pt(500,530)),"Sofa collision"); Check(!Can(Pt(1240,550)),"Desk collision");
m.position=Pt(1350,670); m.elapsed=holdStart+1; m.UpdateGhosts();
Call("EnterSecondRoom"); Call("Close");
Check(m.zone==2 && m.history.clearedRooms==1 && m.elapsed==0 && m.recording.room==2,"Transition and fresh room recording");
Check(m.history.ForRoom(1).Count==1 && m.history.ForRoom(2).Count==0,"Recordings isolated between rooms");
m.elapsed=2; m.position=EchoMvp.PlateA;m.Record();m.RestartCycle();Check(m.zone==2 && m.position==new Vector2(-9,-3.5f),"R returns to second room entrance");
m.Interact("Предыдущая комната");Check(m.zone==1 && m.history.remoteObtained,"Backtracking retains remote");
var h=new EchoHistory {copySlots=2,firstReturnSeen=true,remoteObtained=true};
EchoTape T(int z,float duration)=>new EchoTape {room=z,duration=duration,frames=new List<EchoFrame>{new EchoFrame(0,Vector2.zero,z,0)}};
h.Push(T(1,1));h.Push(T(1,2));h.Push(T(3,3));h.Push(T(3,4));h.Push(T(3,5));
Check(h.ForRoom(1).Count==1 && h.ForRoom(3).Count==2 && h.ForRoom(3)[0].duration==4,"Per-room limits and oldest replacement");
h.Save(m.saveFileOverride);var loaded=EchoHistory.Load(m.saveFileOverride);
Check(loaded!=null && loaded.remoteObtained && loaded.firstReturnSeen && loaded.ForRoom(3).Count==2,"Pickup, first return and room recordings survive save/load");
// Снимок рабочего состояния с удерживающей панель копией.
m.history=new EchoHistory{firstReturnSeen=true,remoteObtained=true};
m.history.Push(new EchoTape {room=1,duration=300,frames=new List<EchoFrame>{new EchoFrame(0,EchoMvp.IntroPlate,1,0),new EchoFrame(300,EchoMvp.IntroPlate,1,0)}});
m.zone=1;m.position=Pt(1170,690);m.mode="play";m.elapsed=0;m.recording=new EchoTape{room=1};m.recordingActive=true;m.BuildRoom();
return new {passed=checks,frame=Time.frameCount};
