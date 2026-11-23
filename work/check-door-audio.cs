var doors = UnityEngine.Object.FindObjectsByType<Golovomka.SlidingDoor>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var results = new System.Collections.Generic.List<string>();
foreach (var door in doors)
{
    for (var t = door.transform; t; t = t.parent) t.gameObject.SetActive(true);
    var audio = door.GetComponent<UnityEngine.AudioSource>();
    var type = typeof(Golovomka.SlidingDoor);
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    var step = type.GetMethod("Apply", flags);
    var open = (UnityEngine.AudioClip)type.GetField("openClip", flags).GetValue(door);
    var close = (UnityEngine.AudioClip)type.GetField("closeClip", flags).GetValue(door);
    door.SetOpen(false, true);
    audio.Stop();
    door.SetOpen(true);
    door.SetOpen(false);
    var cancelled = !audio.isPlaying;
    door.SetOpen(true);
    step.Invoke(door, new object[] { .25f });
    var started = audio.isPlaying;
    audio.Stop();
    step.Invoke(door, new object[] { .5f });
    var once = !audio.isPlaying;
    door.SetOpen(false);
    step.Invoke(door, new object[] { .25f });
    var reversed = audio.isPlaying;
    audio.Stop();
    step.Invoke(door, new object[] { 0f });
    var ended = !audio.isPlaying;
    door.SetOpen(true, true);
    var snappedOpen = audio.isPlaying;
    audio.Stop();
    door.SetOpen(false, true);
    var snappedClose = audio.isPlaying;
    audio.Stop();
    door.SetOpen(false, true);
    var unchanged = !audio.isPlaying;
    results.Add(door.name + " " + (open && open.name == "DoorOpen" && close && close.name == "DoorClose" && cancelled && started && once && reversed && ended && snappedOpen && snappedClose && unchanged));
}
return new { Playing = UnityEngine.Application.isPlaying, Count = doors.Length, Results = results };
