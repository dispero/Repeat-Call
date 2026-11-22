using UnityEngine;

namespace Golovomka
{
    [DisallowMultipleComponent]
    public sealed class TargetRangeRoom : MonoBehaviour
    {
        internal event System.Action ShotFired;

        [System.Serializable]
        public sealed class PistolPose
        {
            public Sprite ready;
            public Sprite[] walk = new Sprite[CharacterWalkCycle.WalkingFrameCount];
            public Sprite[] shoot = new Sprite[RangePistol.ShotFrameCount];
            public Vector2 muzzle;

            internal Sprite GetBody(bool moving, float time) =>
                moving ? walk[CharacterWalkCycle.WalkingFrame(time)] : ready;
        }

        public const int Waves = 5;
        public const float StartDelay = 4f;
        public const float WaveInterval = 2.2f;
        public const float Window = 1f;
        public const float ShotInterval = 1f;
        public const float MuzzleHeight = .85f;
        const float Range = 24f;
        const float HatchTravelTime = .14f;

        static readonly (int first, int second)[] Pairs = { (1, 5), (0, 3), (2, 6), (1, 4), (0, 5) };

        public SlidingDoor exitDoor;
        public SpriteRenderer[] targets = new SpriteRenderer[7];
        public SpriteRenderer[] indicators = new SpriteRenderer[Waves];
        public Sprite openHatch;
        public Collider2D targetWall;
        public Sprite pistolFlash;
        [Tooltip("Порядок ракурсов: вправо, вверх, влево, вниз.")]
        public PistolPose[] pistolPoses = new PistolPose[RangePistol.DirectionCount];

        readonly int[] hits = new int[Waves];
        readonly RaycastHit2D[] obstacles = new RaycastHit2D[16];
        Vector3[] raisedPositions;
        SpriteRenderer[] hatches;
        Sprite[] closedHatches;
        static readonly ContactFilter2D Solid = new ContactFilter2D { useTriggers = false };
        int nextReplayShot;
        int recordedWaves;
        float nextShot;
        bool unlocked;
        LineRenderer playerTrace, echoTrace;
        RangePistol playerPistol, echoPistol;
        float playerTraceUntil, echoTraceUntil;

        public bool IsConfigured => exitDoor && exitDoor.Passage && openHatch && targetWall &&
            pistolFlash && pistolPoses.Length == RangePistol.DirectionCount &&
            System.Array.TrueForAll(pistolPoses, pose => pose != null && pose.ready &&
                pose.walk.Length == CharacterWalkCycle.WalkingFrameCount &&
                pose.shoot.Length == RangePistol.ShotFrameCount &&
                System.Array.TrueForAll(pose.shoot, frame => frame) &&
                System.Array.TrueForAll(pose.walk, frame => frame)) &&
            targets.Length == 7 &&
            indicators.Length == Waves && System.Array.TrueForAll(targets, item => item &&
                item.transform.parent && item.transform.parent.TryGetComponent<SpriteRenderer>(out _)) &&
            System.Array.TrueForAll(indicators, item => item);

        public bool Solved => unlocked || CompletedWaves == Waves;
        public bool HasCompleteRecording => recordedWaves == (1 << Waves) - 1;

        public int CompletedWaves
        {
            get
            {
                int count = 0;
                foreach (int pair in hits)
                    if (pair == 3) count++;
                return count;
            }
        }

        void Awake()
        {
            raisedPositions = System.Array.ConvertAll(targets, target => target ? target.transform.localPosition : Vector3.zero);
            hatches = System.Array.ConvertAll(targets, target => target && target.transform.parent
                ? target.transform.parent.GetComponent<SpriteRenderer>() : null);
            closedHatches = System.Array.ConvertAll(hatches, hatch => hatch ? hatch.sprite : null);
        }

        public Vector2 TargetPosition(int target) => targets[target].transform.parent.TransformPoint(raisedPositions[target]);
        public static float WaveStart(int wave) => StartDelay + wave * WaveInterval;

        public static int ActiveWave(float time)
        {
            if (!(time >= StartDelay && time < WaveStart(Waves))) return -1;
            int wave = Mathf.FloorToInt((time - StartDelay) / WaveInterval);
            return time < WaveStart(wave) + Window ? wave : -1;
        }

        public void ResetAttempt(bool alreadyPassed)
        {
            System.Array.Clear(hits, 0, hits.Length);
            recordedWaves = nextReplayShot = 0;
            nextShot = 0;
            unlocked = alreadyPassed;
            Hide();
        }

        public void Hide()
        {
            CloseHatches();
            foreach (var indicator in indicators) indicator.color = new Color(.25f, .24f, .21f);
            if (playerTrace) playerTrace.enabled = false;
            if (echoTrace) echoTrace.enabled = false;
            HidePistols();
            playerTraceUntil = echoTraceUntil = 0;
        }

        internal void HidePistols()
        {
            playerPistol?.Hide();
            echoPistol?.Hide();
        }

        internal static bool IsCardinalShot(Vector2 direction) =>
            (direction.x == 0f && Mathf.Abs(direction.y) == 1f) ||
            (direction.y == 0f && Mathf.Abs(direction.x) == 1f);

        public bool Shoot(Vector2 origin, Vector2 direction, float time, EchoTape recording, Vector2 muzzleOffset = default)
        {
            if (time < nextShot || Solved || !IsCardinalShot(direction)) return false;

            nextShot = time + ShotInterval;
            var shot = new EchoShot(time, origin, direction, muzzleOffset);
            recording.shots.Add(shot);
            Resolve(shot, false);
            return true;
        }

        public void Replay(EchoTape tape, float time)
        {
            if (tape == null) return;
            while (nextReplayShot < tape.shots.Count && tape.shots[nextReplayShot].time <= time)
            {
                var shot = tape.shots[nextReplayShot++];
                if (IsCardinalShot(shot.direction)) Resolve(shot, true);
            }
        }

        void Resolve(EchoShot shot, bool echo)
        {
            ShotFired?.Invoke();
            Vector2 origin = shot.Muzzle;
            Vector2 direction = shot.direction;
            float distance = Range;
            float targetDistance = Range;
            int obstacleCount = Physics2D.Raycast(origin, direction, Solid, obstacles, Range);
            for (int i = 0; i < obstacleCount; i++)
            {
                var obstacle = obstacles[i];
                if (obstacle.collider.gameObject.scene != gameObject.scene) continue;
                distance = Mathf.Min(distance, obstacle.distance);
                // Плитка стены занимает всю её высоту на экране. Мишень закреплена на лицевой стороне.
                if (obstacle.collider != targetWall)
                    targetDistance = Mathf.Min(targetDistance, obstacle.distance);
            }

            int wave = ActiveWave(shot.time);
            if (wave >= 0)
            {
                var pair = Pairs[wave];
                int hit = 0;
                CheckTarget(pair.first, 1);
                CheckTarget(pair.second, 2);
                if (hit != 0)
                {
                    distance = targetDistance;
                    hits[wave] |= hit;
                    if (!echo) recordedWaves |= 1 << wave;
                }

                void CheckTarget(int target, int bit)
                {
                    const float radius = .32f;
                    Vector2 offset = TargetPosition(target) - origin;
                    float along = Vector2.Dot(offset, direction);
                    float perpendicular = offset.sqrMagnitude - along * along;
                    if (along < 0 || perpendicular > radius * radius) return;
                    float entry = Mathf.Max(0, along - Mathf.Sqrt(radius * radius - perpendicular));
                    if (entry >= targetDistance) return;
                    targetDistance = entry;
                    hit = bit;
                }
            }

            var trace = echo ? echoTrace : playerTrace;
            if (!trace)
            {
                trace = CreateLine(echo ? "След выстрела копии" : "След выстрела Ивана",
                    echo ? new Color(.3f, .9f, 1f) : new Color(1f, .8f, .42f), .045f);
                if (echo) echoTrace = trace;
                else playerTrace = trace;
            }
            trace.sortingOrder = RangePistol.TraceSortingOrder(direction, shot.origin.y - MuzzleHeight);
            trace.SetPosition(0, origin);
            trace.SetPosition(1, origin + direction * distance);
            AnimateShot(shot, echo);
            if (echo) echoTraceUntil = shot.time + .08f;
            else playerTraceUntil = shot.time + .08f;
        }

        RangePistol GetPistol(bool echo)
        {
            if (echo) return echoPistol ??= new RangePistol(transform, true, pistolFlash, pistolPoses);
            return playerPistol ??= new RangePistol(transform, false, pistolFlash, pistolPoses);
        }

        internal void AnimateShot(EchoShot shot, bool echo) => GetPistol(echo).Fire(shot);

        internal Vector2 GetPistolMuzzle(Vector2 direction) =>
            pistolPoses[RangePistol.PoseIndex(direction)].muzzle;

        public void ShowPistols(SpriteRenderer player, Vector2 direction, bool playerMoving, bool playerArmed,
            SpriteRenderer copy, Vector2 copyDirection, bool copyMoving, bool copyArmed, float time)
        {
            if (playerArmed) GetPistol(false).Show(player, direction, playerMoving, time);
            else playerPistol?.Hide();
            if (copy.enabled && copyArmed) GetPistol(true).Show(copy, copyDirection, copyMoving, time);
            else echoPistol?.Hide();
        }

        LineRenderer CreateLine(string label, Color color, float width)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.sortingOrder = 250;
            line.startColor = line.endColor = color;
            return line;
        }

        void OnDestroy()
        {
            if (playerTrace) Destroy(playerTrace.sharedMaterial);
            if (echoTrace) Destroy(echoTrace.sharedMaterial);
        }

        public void Show(float time)
        {
            int wave = ActiveWave(time + HatchTravelTime);
            if (wave < 0) wave = ActiveWave(time);
            if (wave < 0) wave = ActiveWave(time - HatchTravelTime);
            CloseHatches();
            if (wave >= 0 && !unlocked)
            {
                var pair = Pairs[wave];
                float sinceStart = time - WaveStart(wave);
                float extension = Mathf.Clamp01(1f + Mathf.Min(sinceStart, Window - sinceStart) / HatchTravelTime);
                ShowTarget(pair.first, 1, extension);
                ShowTarget(pair.second, 2, extension);
            }
            for (int i = 0; i < indicators.Length; i++)
                indicators[i].color = unlocked || hits[i] == 3 ? new Color(.53f, .9f, .55f)
                    : i == wave ? new Color(1f, .73f, .29f) : new Color(.25f, .24f, .21f);

            if (playerTrace) playerTrace.enabled = time < playerTraceUntil;
            if (echoTrace) echoTrace.enabled = time < echoTraceUntil;

            void ShowTarget(int target, int bit, float extension)
            {
                var renderer = targets[target];
                renderer.enabled = extension > 0f;
                if (renderer.enabled) hatches[target].sprite = openHatch;
                renderer.transform.localPosition = Vector3.Lerp(Vector3.zero, raisedPositions[target], extension);
                renderer.transform.localScale = new Vector3(1, extension, 1);
                renderer.color = (hits[wave] & bit) != 0 ? new Color(.48f, .51f, .46f) : Color.white;
            }
        }

        void CloseHatches()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].enabled = false;
                hatches[i].sprite = closedHatches[i];
            }
        }

        public string Objective(bool hasEcho) => Solved ? "Пройдите через дверь справа"
            : hasEcho ? "Стреляйте по мишени, которую оставляет копия"
            : "Поразите любую мишень в каждой паре и нажмите R";

        public string Status(float time, bool hasEcho)
        {
            if (Solved) return "ПЯТЬ ПАР ПОРАЖЕНЫ  /  ВЫХОД ОТКРЫТ";
            if (time >= WaveStart(Waves - 1) + Window) return "СЕРИЯ ЗАВЕРШЕНА   •   R — повторить";
            int wave = ActiveWave(time);
            int count = 0;
            for (int i = 0; i < Waves; i++)
                if ((recordedWaves & (1 << i)) != 0) count++;
            return (hasEcho ? $"ПАРЫ: {CompletedWaves}/{Waves}" : $"ДЛЯ ЗАПИСИ: {count}/{Waves}") +
                (wave >= 0 ? $"   •   ВОЛНА {wave + 1}/{Waves}" : "   •   ЖДИТЕ МИШЕНИ");
        }
    }
}
