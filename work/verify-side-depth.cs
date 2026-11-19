using System;
using System.Reflection;
using Golovomka;
using UnityEngine;
var game=UnityEngine.Object.FindAnyObjectByType<EchoMvp>(); game.BeginCycle();
var flags=BindingFlags.NonPublic|BindingFlags.Instance;
var canMove=typeof(EchoMvp).GetMethod("CanMove",flags);
var place=typeof(EchoMvp).GetMethod("Place",flags);
var hero=(SpriteRenderer)typeof(EchoMvp).GetField("hero",flags).GetValue(game);
var top=GameObject.Find("DutyDeskTop").GetComponent<SpriteRenderer>();
var lamp=GameObject.Find("LampPixels").GetComponent<SpriteRenderer>();
Vector2 P(float x,float y)=>new Vector2((x-793)*13.2f/992,(496-y)*13.2f/992);
void At(float x,float y)
{
    game.position=P(x,y); place.Invoke(game,new object[]{hero,game.position});
}
foreach(float x in new[]{515f,1100f})
{
    var p=P(x,820);
    if(!(bool)canMove.Invoke(game,new object[]{p}))throw new Exception("Original close side contact changed");
    At(x,820);
    if(top.enabled||lamp.enabled)throw new Exception("Side layers cover hero");
}
At(850,625);
if(!top.enabled||!lamp.enabled||top.sortingOrder<=hero.sortingOrder)throw new Exception("Rear overlap lost");
At(950,910);
if(top.sortingOrder>=hero.sortingOrder)throw new Exception("Front overlap wrong");
At(515,820); game.facing=0;
return "PASS: original close contact on both sides; hero in front at sides/front; desk and lamp in front at rear";
