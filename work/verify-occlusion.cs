using System;
using System.Reflection;
using System.Collections.Generic;
using Golovomka;
using UnityEngine;
var game = UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
game.BeginCycle();
var flags = BindingFlags.Instance | BindingFlags.NonPublic;
var meshes = (Dictionary<string, Sprite>)typeof(EchoMvp).GetField("stationForeground", flags).GetValue(game);
bool Contains(Sprite sprite, Vector2 pixel)
{
    var p = new Vector2((pixel.x - sprite.texture.width * .5f) / 16, (sprite.texture.height * .5f - pixel.y) / 16);
    var vertices = sprite.vertices; var triangles = sprite.triangles;
    float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    for (int i = 0; i < triangles.Length; i += 3)
    {
        var a = vertices[triangles[i]]; var b = vertices[triangles[i+1]]; var c = vertices[triangles[i+2]];
        float ab = Cross(b-a, p-a), bc = Cross(c-b, p-b), ca = Cross(a-c, p-c);
        if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
    }
    return false;
}
foreach (var point in new[] { new Vector2(990, 580), new Vector2(800, 917), new Vector2(335, 554) })
    foreach (var mesh in meshes)
        if (Contains(mesh.Value, point)) throw new Exception("Floor obscures character at " + point + ": " + mesh.Key);
if (!Contains(meshes["LampPixels"], new Vector2(950, 545))) throw new Exception("Shade must occlude");
if (!Contains(meshes["LampPixels"], new Vector2(1003.5f, 580.5f))) throw new Exception("Stem must not have a one-pixel hole");
if (!Contains(meshes["LampPixels"], new Vector2(995, 538))) throw new Exception("Arm edge must occlude");
if (Contains(meshes["LampPixels"], new Vector2(1032, 580))) throw new Exception("Right floor must not occlude");
if (!Contains(meshes["DutyDeskTop"], new Vector2(850, 700))) throw new Exception("Desk must occlude");
foreach (var sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>())
    if (sr.sprite && meshes.ContainsKey(sr.sprite.name) && sr.transform.position != Vector3.zero)
        throw new Exception("Foreground alignment differs from background");
game.position = new Vector2((1080-793)*13.2f/992, (496-580)*13.2f/992); game.facing = 0;
return "PASS: floor between lamp parts and chair legs is not rendered in foreground; actual shade/desk occlude; layers share origin";


