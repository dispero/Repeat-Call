using System;
using System.Reflection;
using Golovomka;
using UnityEngine;
var game=UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
var flags=BindingFlags.NonPublic|BindingFlags.Instance;
var fit=typeof(EchoMvp).GetMethod("FitRoomCamera",flags);
var cam=Camera.main; int cases=0;
try
{
    foreach(int zone in new[]{0,1,2,3,4})
    foreach(float aspect in new[]{4f/3,16f/9,21f/9,9f/16})
    {
        game.zone=zone; cam.aspect=aspect; fit.Invoke(game,null);
        Vector2 size=zone==0?new Vector2(1586*13.2f/992,13.2f):new Vector2(24,12.7f);
        float y=zone==0?0:.85f;
        var low=cam.WorldToViewportPoint(new Vector3(-size.x/2,y-size.y/2,0));
        var high=cam.WorldToViewportPoint(new Vector3(size.x/2,y+size.y/2,0));
        if(low.x<0||low.y<0||high.x>1||high.y>1)throw new Exception("Clipped room "+zone+" "+aspect);
        if(Mathf.Max(high.x-low.x,high.y-low.y)<.99f)throw new Exception("Room does not fill available axis");
        if(cam.rect!=new Rect(0,0,1,1))throw new Exception("Letterboxed camera viewport");
        cases++;
    }
}
finally {cam.ResetAspect();game.BeginCycle();}
Canvas.ForceUpdateCanvases();
foreach(string name in new[]{"heading","objective","status","prompt","caution"})
{
    var text=(UnityEngine.UI.Text)typeof(EchoMvp).GetField(name,flags).GetValue(game);
    var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
    foreach(var corner in corners)
        if(corner.x<-.1f||corner.x>Screen.width+.1f||corner.y<-.1f||corner.y>Screen.height+.1f)
            throw new Exception("HUD outside current window: "+name);
}
return "PASS: 20 room/aspect combinations (4:3,16:9,21:9,9:16), whole room, maximum uniform fit, full viewport, HUD within current screen";
