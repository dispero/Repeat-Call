using Golovomka;
using UnityEngine;
using System.Collections.Generic;
var game = UnityEngine.Object.FindAnyObjectByType<EchoMvp>();
game.history = new EchoHistory { copySlots = 2 };
game.zone = 3; game.position = new Vector2(0, -3.6f); game.BuildRoom();
foreach (var point in new[] { EchoMvp.PlateA, game.CurrentPlateB })
    game.history.tapes.Add(new EchoTape { duration = 1000, frames = new List<EchoFrame> {
        new EchoFrame(0, point, 3, 3), new EchoFrame(1000, point, 3, 3) } });
game.recording = new EchoTape(); game.recordingActive = true; game.elapsed = 1;
game.UpdateGhosts(); game.mode = "play";
return "Preview: hero and two waiting copies";
