using System.Collections.Generic;
using UnityEngine;

namespace Golovomka
{
    [DisallowMultipleComponent, RequireComponent(typeof(LaboratoryRoom))]
    public sealed class FinaleRoom : MonoBehaviour
    {
        internal event System.Action ShotFired;
        internal event System.Action<EchoShot, bool> OfficerShotFired;

        public CharacterWalk pursuer, guard, lookout, fyodor, alexei;
        public SpriteRenderer pursuerGun, guardGun, lookoutGun;
        public Sprite pistolFlash;
        public SpriteRenderer fyodorRope, alexeiRope;
        public BoxCollider2D upperApproach;
        public Transform lurePosition;
        public Transform[] routeCorners;
        public SpriteRenderer portal;

        const float Range = 9f;
        internal const float ShotInterval = .42f;
        const float HitRadius = .33f;
        const float BodyHitHeight = 2.85f;
        const float ReleaseRadius = 1.15f;
        const float LureDuration = 4.25f;
        const float ShotFlashDuration = .06f;
        const float ShotRecoilEnd = .16f;
        const float ShotAnimationEnd = .24f;
        static readonly Vector2 BodySize = new Vector2(.44f, .28f);
        static readonly ContactFilter2D Solid = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = Physics2D.DefaultRaycastLayers
        };
        readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        Bandit chaser, watcher, sentry;
        CharacterWalk[] captives;
        SpriteRenderer[] ropes;
        bool[] released;
        float[,] pathLengths;
        Material traceMaterial;
        Trace playerTrace, copyTrace, pursuerTrace, lookoutTrace;
        float nextPlayerShot, lureUntil;
        int replayShot;
        bool recordedMovement;
        Vector2 startPosition;

        sealed class Trace
        {
            public LineRenderer line;
            public float until;
        }

        sealed class Bandit
        {
            public readonly CharacterWalk body;
            public readonly SpriteRenderer sprite, gun;
            public readonly bool hasArmedAnimation;
            public readonly Vector2 start;
            public Vector2 lastSeen, direction, chargingDirection, firedDirection, firedMuzzle;
            public Vector2? firingPosition;
            public float chargeUntil, nextShot, firedAt;
            public bool tracked, chargingCopy;

            public Bandit(CharacterWalk body, SpriteRenderer gun, Sprite pistolFlash)
            {
                this.body = body;
                this.gun = gun;
                start = body.transform.position;
                sprite = body.GetComponent<SpriteRenderer>();
                hasArmedAnimation = body.HasArmedAnimation;
                if (hasArmedAnimation)
                {
                    gun.enabled = false;
                    gun.sprite = pistolFlash;
                    gun.transform.localScale = Vector3.one * .72f;
                }
                body.enabled = false;
            }

            public bool Alive => body.gameObject.activeSelf;

            public void Reset(Vector2 look)
            {
                body.transform.position = start;
                body.gameObject.SetActive(true);
                lastSeen = start;
                direction = look;
                chargingDirection = Vector2.zero;
                chargeUntil = nextShot = 0;
                firedAt = float.NegativeInfinity;
                firingPosition = null;
                tracked = chargingCopy = false;
            }
        }

        public bool IsConfigured => pursuer && guard && lookout && fyodor && alexei && pursuerGun && pistolFlash &&
            guardGun && lookoutGun && fyodorRope && alexeiRope && upperApproach && lurePosition && portal &&
            routeCorners != null && routeCorners.Length >= 4 && System.Array.TrueForAll(routeCorners, corner => corner) &&
            pursuer.IsConfigured && guard.IsConfigured && lookout.IsConfigured && fyodor.IsConfigured && alexei.IsConfigured;
        public string Failure { get; private set; }
        internal enum DefeatIllustration { None, Approach, Accomplices }
        internal DefeatIllustration FailureIllustration { get; private set; }
        public bool CopyHit { get; private set; }
        public bool Cleared => !chaser.Alive && !watcher.Alive && !sentry.Alive;
        public bool Complete => released[0] && released[1];
        public bool HasRecording(EchoTape tape) => recordedMovement || tape.shots.Count > 0;
        public bool Active { get; private set; }

        public void Begin(Vector2 entry)
        {
            if (chaser == null)
            {
                chaser = new Bandit(pursuer, pursuerGun, pistolFlash);
                watcher = new Bandit(guard, guardGun, pistolFlash);
                sentry = new Bandit(lookout, lookoutGun, pistolFlash);
                captives = new[] { fyodor, alexei };
                ropes = new[] { fyodorRope, alexeiRope };
                released = new bool[captives.Length];
                traceMaterial = new Material(Shader.Find("Sprites/Default"));
                playerTrace = CreateTrace("Выстрел Ивана", new Color(1, .79f, .44f));
                copyTrace = CreateTrace("Выстрел копии", new Color(.35f, .9f, 1));
                pursuerTrace = CreateTrace("Выстрел преследователя", new Color(1, .37f, .21f));
                lookoutTrace = CreateTrace("Выстрел стрелка", new Color(1, .37f, .21f));
            }
            Active = true;
            startPosition = entry;
            Failure = null;
            FailureIllustration = DefeatIllustration.None;
            CopyHit = recordedMovement = false;
            replayShot = 0;
            nextPlayerShot = lureUntil = 0;
            HideTraces();
            chaser.Reset(Vector2.left);
            watcher.Reset(Vector2.left);
            sentry.Reset(Vector2.left);
            for (int i = 0; i < captives.Length; i++)
            {
                released[i] = false;
                captives[i].gameObject.SetActive(true);
                captives[i].enabled = false;
                captives[i].GetComponent<SpriteRenderer>().sprite = captives[i].GetFrame(3, false, 0);
                ropes[i].enabled = true;
            }
            CacheRoutes();
            ShowBandit(chaser, false, 0);
            ShowBandit(watcher, false, 0);
            ShowBandit(sentry, false, 0);
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            HideTraces();
        }

        void HideTraces()
        {
            playerTrace.line.enabled = copyTrace.line.enabled = pursuerTrace.line.enabled = lookoutTrace.line.enabled = false;
            if (chaser.hasArmedAnimation) chaser.gun.enabled = false;
            if (sentry.hasArmedAnimation) sentry.gun.enabled = false;
        }

        public void RestoreCompletion()
        {
            chaser.body.gameObject.SetActive(false);
            watcher.body.gameObject.SetActive(false);
            sentry.body.gameObject.SetActive(false);
            Release(0);
            Release(1);
        }

        public void Shoot(Vector2 player, Vector2 direction, float time, EchoTape recording, Vector2 muzzleOffset = default)
        {
            if (Failure != null || Cleared || time < nextPlayerShot) return;
            nextPlayerShot = time + ShotInterval;
            var shot = new EchoShot(time, player + Vector2.up * TargetRangeRoom.MuzzleHeight, direction, muzzleOffset);
            recording.shots.Add(shot);
            ResolvePlayerShot(shot, time, false);
        }

        public void Replay(EchoTape tape, float time)
        {
            if (tape == null || CopyHit || Failure != null) return;
            while (replayShot < tape.shots.Count && tape.shots[replayShot].time <= time && Failure == null)
            {
                EchoShot shot = tape.shots[replayShot++];
                if (shot.time < tape.duration && TargetRangeRoom.IsCardinalShot(shot.direction))
                    ResolvePlayerShot(shot, time, true);
            }
        }

        void ResolvePlayerShot(EchoShot shot, float time, bool copy)
        {
            ShotFired?.Invoke();
            OfficerShotFired?.Invoke(shot, copy);
            Vector2 origin = shot.Muzzle;
            Vector2 direction = shot.direction;
            Vector2 shooterFeet = shot.origin - Vector2.up * TargetRangeRoom.MuzzleHeight;
            float distance = Cast(origin, direction, Range, shooterFeet: shooterFeet);
            Bandit victim = null;
            int hostage = -1;
            Test(chaser);
            Test(watcher);
            Test(sentry);
            for (int i = 0; i < captives.Length; i++)
                if (captives[i].gameObject.activeSelf && Hit(origin, direction, captives[i].transform.position, ref distance))
                    hostage = i;

            DrawTrace(copy ? copyTrace : playerTrace, origin, direction, distance, time, shooterFeet.y);
            if (hostage >= 0) { LoseHostage(hostage, "В заложника попала пуля."); return; }
            if (victim != null) victim.body.gameObject.SetActive(false);
            if (watcher.Alive && !chaser.Alive && !sentry.Alive)
                LoseHostage(0, "Охранник убил заложника после гибели обоих сообщников.", DefeatIllustration.Accomplices);

            // Выстрел в сторону укрытия привлекает внимание, даже если пуля остановилась в оборудовании.
            Vector2 toLookout = sentry.start - shooterFeet;
            if (sentry.Alive && shooterFeet.x < sentry.start.x &&
                toLookout.sqrMagnitude <= 19f && Vector2.Dot(toLookout.normalized, direction) > .72f &&
                shooterFeet.y >= sentry.start.y + .8f)
                lureUntil = time + LureDuration;

            void Test(Bandit bandit)
            {
                if (bandit.Alive && Hit(origin, direction, bandit.body.transform.position, ref distance)) victim = bandit;
            }
        }

        public void Tick(float dt, float time, Vector2 player, Vector2? copy)
        {
            recordedMovement |= (player - startPosition).sqrMagnitude > .04f;
            portal.color = Color.Lerp(new Color(.62f, .43f, .92f), Color.white, .5f + .5f * Mathf.Sin(time * 3f));
            FadeTrace(playerTrace, time);
            FadeTrace(copyTrace, time);
            FadeTrace(pursuerTrace, time);
            FadeTrace(lookoutTrace, time);
            if (Failure != null || Cleared) return;
            if (watcher.Alive && (DetectsUpper(player) || (copy.HasValue && DetectsUpper(copy.Value))))
            {
                LoseHostage(0, "Охранник заметил движение в верхнем проходе.", DefeatIllustration.Approach);
                return;
            }

            if (chaser.Alive) UpdateBandit(chaser, player, copy, dt, time, false);
            if (sentry.Alive && Failure == null) UpdateBandit(sentry, player, CopyHit ? null : copy, dt, time, true);
        }

        bool DetectsUpper(Vector2 target) => upperApproach.OverlapPoint(target) && Clear(watcher.start, target);

        void UpdateBandit(Bandit bandit, Vector2 player, Vector2? copy, float dt, float time, bool local)
        {
            Vector2 point = bandit.body.transform.position;
            bool lured = local && time < lureUntil;
            bool seesPlayer = !lured && (!local || InLowerArea(player)) && Visible(point, player, local ? 5f : 8f);
            bool seesCopy = copy.HasValue && (!local || lured || InLowerArea(copy.Value)) &&
                Visible(point, copy.Value, local ? 5f : 8f);
            bool targetCopy = seesCopy && (!seesPlayer ||
                (copy.Value - point).sqrMagnitude <= (player - point).sqrMagnitude + .1f);
            Vector2 target = targetCopy ? copy.Value : player;
            Vector2 aim = target - point;
            Vector2 direction = Cardinal(aim);
            Vector2 muzzle = BanditMuzzle(bandit, direction);
            bool seesTarget = seesPlayer || seesCopy;
            bool movingToLure = lured && Vector2.Distance(point, lurePosition.position) > .25f;
            float aimDistance = Range;
            bool aimedAtTarget = seesTarget && !movingToLure && aim.sqrMagnitude > .012f &&
                Hit(muzzle, direction, target, ref aimDistance);
            bool blocked = aimedAtTarget && Cast(muzzle, direction, Range, shooterFeet: point) <= aimDistance;

            if (aimedAtTarget && !blocked)
            {
                bandit.firingPosition = null;
                if (bandit.chargingDirection != direction || bandit.chargingCopy != targetCopy)
                    bandit.chargeUntil = time + (local ? .5f : .4f);
                bandit.direction = bandit.chargingDirection = direction;
                bandit.chargingCopy = targetCopy;
                if (time >= bandit.chargeUntil && time >= bandit.nextShot)
                {
                    FireBandit(bandit, player, copy, direction, time, local ? lookoutTrace : pursuerTrace);
                    bandit.nextShot = time + .85f;
                }
                ShowBandit(bandit, false, time, time < bandit.chargeUntil, lured);
                return;
            }

            bandit.chargingDirection = Vector2.zero;
            if (seesTarget)
            {
                bandit.lastSeen = target;
                bandit.tracked = true;
            }
            Vector2 destination = bandit.tracked ? bandit.lastSeen : bandit.start;
            if (local)
            {
                destination = lured ? (Vector2)lurePosition.position : bandit.start;
                if (!lured && seesTarget)
                    destination.y = Mathf.Clamp(target.y, bandit.start.y - 1.25f, bandit.start.y + .45f);
            }
            if (seesTarget && !movingToLure && direction.y != 0)
                destination.x = target.x - (muzzle.x - point.x);
            if (local && !lured)
            {
                if (blocked && (!bandit.firingPosition.HasValue ||
                    !HasFiringLine(bandit, bandit.firingPosition.Value, target)))
                    bandit.firingPosition = FindLookoutFiringPosition(bandit, point, target);
                if (bandit.firingPosition is Vector2 firingPosition)
                {
                    destination = firingPosition;
                    if ((destination - point).sqrMagnitude < .01f) bandit.firingPosition = null;
                }
            }
            if ((destination - point).sqrMagnitude < .01f) bandit.tracked = false;
            Vector2 step = Vector2.MoveTowards(point, NextWaypoint(point, destination), dt * (local ? 2.8f : 2.35f));
            bool moving = step != point && Clear(point, step, true);
            if (moving)
            {
                bandit.body.transform.position = step;
                bandit.direction = Cardinal(step - point);
            }
            else if (seesTarget) bandit.direction = direction;
            ShowBandit(bandit, moving, time, false, lured);
        }

        bool InLowerArea(Vector2 p) => p.y < sentry.start.y + 1f && Mathf.Abs(p.x - sentry.start.x) < 5f;

        Vector2? FindLookoutFiringPosition(Bandit bandit, Vector2 from, Vector2 target)
        {
            Vector2? best = null;
            float bestDistance = float.PositiveInfinity;
            FindInColumn(bandit.start.x);
            if (best.HasValue) return best;
            FindInColumn(target.x);
            FindInColumn(target.x - 1.8f);
            FindInColumn(target.x + 1.8f);
            return best;

            void FindInColumn(float x)
            {
                // Сначала ищем линию ниже оборудования, в пределах нижнего прохода.
                for (int row = -8; row <= 1; row++)
                {
                    var candidate = new Vector2(x, bandit.start.y + row * .25f);
                    float distance = (candidate - from).sqrMagnitude;
                    if (distance >= bestDistance || !HasFiringLine(bandit, candidate, target) ||
                        NextWaypoint(from, candidate) == from)
                        continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        bool HasFiringLine(Bandit bandit, Vector2 feet, Vector2 target)
        {
            Vector2 direction = Cardinal(target - feet);
            Vector2 muzzle = BanditMuzzle(bandit, direction) + feet - (Vector2)bandit.body.transform.position;
            float distance = Cast(muzzle, direction, Range, shooterFeet: feet);
            return Hit(muzzle, direction, target, ref distance);
        }

        void FireBandit(Bandit bandit, Vector2 player, Vector2? copy, Vector2 direction, float time, Trace trace)
        {
            bandit.firedAt = time;
            bandit.firedDirection = direction;
            ShotFired?.Invoke();
            Vector2 origin = BanditMuzzle(bandit, direction);
            bandit.firedMuzzle = origin;
            float distance = Cast(origin, direction, Range, shooterFeet: bandit.body.transform.position);
            bool hitPlayer = Hit(origin, direction, player, ref distance);
            bool hitCopy = copy.HasValue && Hit(origin, direction, copy.Value, ref distance);
            int hostage = -1;
            for (int i = 0; i < captives.Length; i++)
                if (captives[i].gameObject.activeSelf && Hit(origin, direction, captives[i].transform.position, ref distance))
                    hostage = i;
            DrawTrace(trace, origin, direction, distance, time, bandit.body.transform.position.y);
            if (hostage >= 0) LoseHostage(hostage, "В заложника попала пуля.");
            else if (hitCopy) CopyHit = true;
            else if (hitPlayer) Failure = "Иван погиб.";
        }

        void LoseHostage(int index, string reason, DefeatIllustration illustration = DefeatIllustration.None)
        {
            captives[index].gameObject.SetActive(false);
            Failure = reason;
            FailureIllustration = illustration;
        }

        public int NearbyCaptive(Vector2 player)
        {
            if (!Cleared || Failure != null) return -1;
            int nearest = -1;
            float distance = ReleaseRadius;
            for (int i = 0; i < captives.Length; i++)
            {
                float candidate = Vector2.Distance(player, captives[i].transform.position);
                if (released[i] || candidate >= distance || !Clear(player, captives[i].transform.position)) continue;
                nearest = i;
                distance = candidate;
            }
            return nearest;
        }

        public void Release(int index)
        {
            released[index] = true;
            ropes[index].enabled = false;
        }

        public string Status(float time) => Cleared ? "ОСВОБОЖДЕНО: " + ((released[0] ? 1 : 0) + (released[1] ? 1 : 0)) + " / 2"
            : time < lureUntil && sentry.Alive ? "СТРЕЛОК ОТВЛЕЧЁН" : "ЗАЛОЖНИКИ: 2 / 2";

        static Vector2 Cardinal(Vector2 delta) => Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? (delta.x >= 0 ? Vector2.right : Vector2.left)
            : (delta.y >= 0 ? Vector2.up : Vector2.down);

        static Vector2 BanditMuzzle(Bandit bandit, Vector2 direction) => bandit.hasArmedAnimation
            ? bandit.body.GetArmedMuzzle(Facing(direction))
            : (Vector2)bandit.body.transform.position + Vector2.up * .86f + direction * .55f;

        static bool Hit(Vector2 origin, Vector2 direction, Vector2 target, ref float distance)
        {
            // Проверяем видимую область тела над точкой опоры.
            Vector2 offset = target + Vector2.up * (BodyHitHeight * .5f) - origin;
            float along = Vector2.Dot(offset, direction);
            bool horizontal = direction.x != 0;
            float extent = horizontal ? HitRadius : BodyHitHeight * .5f;
            float across = horizontal ? Mathf.Abs(offset.y) : Mathf.Abs(offset.x);
            if (along + extent <= 0 || across > (horizontal ? BodyHitHeight * .5f : HitRadius)) return false;
            float entry = Mathf.Max(.001f, along - extent);
            if (entry >= distance) return false;
            distance = entry;
            return true;
        }

        bool Visible(Vector2 from, Vector2 to, float range) => (to - from).sqrMagnitude < range * range && Clear(from, to);

        bool Clear(Vector2 from, Vector2 to, bool body = false)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            return length < .001f || Cast(from, delta / length, length, body) >= length;
        }

        float Cast(Vector2 from, Vector2 direction, float length, bool body = false, Vector2? shooterFeet = null)
        {
            if (body) Physics2D.BoxCast(from + Vector2.up * .04f, BodySize, 0, direction, Solid, hits, length);
            else Physics2D.Raycast(from, direction, Solid, hits, length);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject.scene != gameObject.scene) continue;
                // При выстреле вдоль стены её нарисованная высота может накрывать ствол.
                if (shooterFeet.HasValue && direction.y == 0 && hit.distance == 0 &&
                    hit.collider is UnityEngine.Tilemaps.TilemapCollider2D && !hit.collider.OverlapPoint(shooterFeet.Value))
                    continue;
                length = Mathf.Min(length, hit.distance);
            }
            return length;
        }

        void CacheRoutes()
        {
            int count = routeCorners.Length;
            pathLengths = new float[count, count];
            for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
            {
                Vector2 a = routeCorners[i].position;
                Vector2 b = routeCorners[j].position;
                pathLengths[i, j] = pathLengths[j, i] = Clear(a, b, true) ? Vector2.Distance(a, b) : float.PositiveInfinity;
            }
            for (int k = 0; k < count; k++)
            for (int i = 0; i < count; i++)
            for (int j = 0; j < count; j++)
                pathLengths[i, j] = Mathf.Min(pathLengths[i, j], pathLengths[i, k] + pathLengths[k, j]);
        }

        Vector2 NextWaypoint(Vector2 from, Vector2 to)
        {
            if (Clear(from, to, true)) return to;
            Vector2 result = from;
            float best = float.PositiveInfinity;
            for (int i = 0; i < routeCorners.Length; i++)
            {
                Vector2 first = routeCorners[i].position;
                float approach = Vector2.Distance(from, first);
                if (approach < .015f || !Clear(from, first, true)) continue;
                for (int j = 0; j < routeCorners.Length; j++)
                {
                    Vector2 last = routeCorners[j].position;
                    float cost = approach + pathLengths[i, j] + Vector2.Distance(last, to);
                    if (cost >= best || !Clear(last, to, true)) continue;
                    best = cost;
                    result = first;
                }
            }
            return result;
        }

        static void ShowBandit(Bandit bandit, bool moving, float time, bool charging = false, bool lured = false)
        {
            Vector2 direction = bandit.direction;
            int facing = Facing(direction);
            bandit.sprite.sprite = bandit.hasArmedAnimation
                ? ArmedBanditFrame(bandit, facing, moving, time, charging)
                : bandit.body.GetFrame(facing, moving, time);
            bandit.sprite.sortingOrder = 100 - Mathf.RoundToInt(bandit.body.transform.position.y * 10f);
            if (bandit.hasArmedAnimation)
            {
                float age = time - bandit.firedAt;
                bandit.gun.enabled = bandit.firedDirection == Vector2.up && age >= 0 && age < ShotFlashDuration;
                if (bandit.gun.enabled)
                {
                    bandit.gun.sortingOrder = bandit.sprite.sortingOrder - 1;
                    bandit.gun.transform.SetPositionAndRotation(bandit.firedMuzzle, Quaternion.Euler(0, 0, 90));
                }
            }
            else
            {
                bandit.gun.sortingOrder = bandit.sprite.sortingOrder + (facing == 2 ? -1 : 1);
                bandit.gun.transform.position = bandit.body.transform.position + (Vector3)(Vector2.up * .86f + direction * .55f);
                bandit.gun.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            bandit.sprite.color = charging ? new Color(1, .67f, .53f) : lured ? new Color(.62f, .9f, 1) : Color.white;
        }

        static Sprite ArmedBanditFrame(Bandit bandit, int facing, bool moving, float time, bool charging)
        {
            float age = time - bandit.firedAt;
            if (age >= 0 && age < ShotAnimationEnd)
            {
                int phase = age < ShotFlashDuration ? 1 : age < ShotRecoilEnd ? 2 : 3;
                return bandit.body.GetArmedShootFrame(Facing(bandit.firedDirection), phase);
            }
            return charging ? bandit.body.GetArmedShootFrame(facing, 0)
                : bandit.body.GetArmedWalkFrame(facing, moving, time);
        }

        static int Facing(Vector2 direction) =>
            direction.x > 0 ? 0 : direction.x < 0 ? 1 : direction.y > 0 ? 2 : 3;

        Trace CreateTrace(string label, Color color)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = traceMaterial;
            line.positionCount = 2;
            line.widthMultiplier = .046f;
            line.sortingOrder = 250;
            line.startColor = line.endColor = color;
            return new Trace { line = line };
        }

        static void DrawTrace(Trace trace, Vector2 origin, Vector2 direction, float distance, float time, float feetY)
        {
            trace.line.sortingOrder = RangePistol.TraceSortingOrder(direction, feetY);
            trace.line.enabled = true;
            trace.line.SetPosition(0, origin);
            trace.line.SetPosition(1, origin + direction * distance);
            trace.until = time + .08f;
        }

        static void FadeTrace(Trace trace, float time)
        {
            if (trace.line.enabled && time > trace.until) trace.line.enabled = false;
        }

        void OnDestroy()
        {
            if (traceMaterial) Destroy(traceMaterial);
        }
    }
}
