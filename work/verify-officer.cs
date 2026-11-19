using System;
using System.Reflection;
using System.Collections.Generic;
using Golovomka;
using UnityEngine;
var game = UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
var flags = BindingFlags.Instance | BindingFlags.NonPublic;
void Check(bool value, string label) { if (!value) throw new Exception(label); }
var frames = (Sprite[])typeof(EchoMvp).GetField("frames", flags).GetValue(game);
var select = typeof(EchoMvp).GetMethod("OfficerFrame", flags);
Check(frames.Length == 16, "16 frames loaded");
float minHeight = 100, maxHeight = 0;
for (int direction = 0; direction < 4; direction++)
{
    var idle = (Sprite)select.Invoke(game, new object[] { direction, false, 0f });
    Check(idle == frames[direction * 4], "Idle direction " + direction);
    var first = (Sprite)select.Invoke(game, new object[] { direction, true, 0f });
    var second = (Sprite)select.Invoke(game, new object[] { direction, true, .25f });
    Check(first != idle && second != idle && first != second, "Walk phases " + direction);
}
foreach (var frame in frames)
{
    Check(frame.texture.name == "IvanOfficerWalk", "New texture");
    Check(Mathf.Abs(frame.pivot.y) < .001f, "Foot pivot");
    minHeight = Mathf.Min(minHeight, frame.bounds.size.y);
    maxHeight = Mathf.Max(maxHeight, frame.bounds.size.y);
}
Check(minHeight > 2.5f && maxHeight < 3.6f, "MVP size");
game.history = new EchoHistory { copySlots = 2 };
game.saveFileOverride = System.IO.Path.GetFullPath("work/officer-test-save.json");
game.BeginCycle(); Check(!game.recordingActive, "Station not recording");
game.zone = 3; game.position = new Vector2(4, -3.5f); game.BuildRoom();
foreach (var pair in new[] { (EchoMvp.PlateA, 0), (game.CurrentPlateB, 1) })
    game.history.tapes.Add(new EchoTape { duration = 2, frames = new List<EchoFrame> {
        new EchoFrame(0, pair.Item1, 3, pair.Item2), new EchoFrame(2, pair.Item1, 3, pair.Item2) } });
game.recordingActive = true; game.elapsed = 1; game.UpdateGhosts();
var ghosts = (SpriteRenderer[])typeof(EchoMvp).GetField("ghosts", flags).GetValue(game);
Check(ghosts[0].enabled && ghosts[1].enabled && game.plateA && game.plateB, "Both copies and plates");
Check(ghosts[0].sprite == frames[0] && ghosts[1].sprite == frames[4], "Waiting copies use idle");
game.history.tapes[0].frames[0] = new EchoFrame(0, EchoMvp.PlateA + Vector2.left, 3, 0);
game.UpdateGhosts(); Check(ghosts[0].sprite != frames[0], "Moving copy uses walk frame");
game.elapsed = 2; game.UpdateGhosts();
Check(!ghosts[0].enabled && !ghosts[1].enabled && !game.plateA && !game.plateB, "Copies disappear/release");
game.history = new EchoHistory(); game.BeginCycle();
game.position = new Vector2(5, -2.3f);
var hero = (SpriteRenderer)typeof(EchoMvp).GetField("hero", flags).GetValue(game);
typeof(EchoMvp).GetMethod("Place", flags).Invoke(game, new object[] { hero, game.position });
return new { result = "PASS: 16 frames, 4 facings and walk phases, foot anchors, dimensions, stationary/moving copies, plates, disappearance", minHeight, maxHeight };
