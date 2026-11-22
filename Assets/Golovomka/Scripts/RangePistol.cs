using UnityEngine;
using UnityEngine.Rendering;

namespace Golovomka
{
    sealed class RangePistol
    {
        internal const int DirectionCount = 8;
        const float FlashDuration = .06f;

        readonly SpriteRenderer flash;
        readonly SortingGroup sorting;
        readonly SpriteMask bodyMask;
        readonly TargetRangeRoom.PistolPose[] poses;
        readonly Color flashColor;
        SpriteRenderer posingActor;
        Sprite walkingSprite, activePose;
        EchoShot shot;
        LineRenderer shotTrace;
        Vector2? shotMuzzle;

        public RangePistol(Transform parent, bool echo, Sprite flashSprite,
            TargetRangeRoom.PistolPose[] poses)
        {
            this.poses = poses;
            flashColor = echo ? new Color(.35f, .9f, 1f, .75f) : Color.white;
            sorting = new GameObject(echo ? "Вспышка копии" : "Вспышка Ивана").AddComponent<SortingGroup>();
            sorting.transform.SetParent(parent, false);
            bodyMask = new GameObject("Силуэт персонажа").AddComponent<SpriteMask>();
            bodyMask.transform.SetParent(sorting.transform, false);
            bodyMask.enabled = false;
            flash = new GameObject("Дульная вспышка").AddComponent<SpriteRenderer>();
            flash.transform.SetParent(sorting.transform, false);
            flash.enabled = false;
            flash.sprite = flashSprite;
            flash.sortingOrder = 1;
        }

        public void Fire(EchoShot firedShot, LineRenderer trace)
        {
            shot = firedShot;
            shotTrace = trace;
            shotMuzzle = null;
        }

        public void Hide()
        {
            RenderOff();
            shotTrace = null;
            shotMuzzle = null;
        }

        void RenderOff()
        {
            flash.enabled = bodyMask.enabled = false;
            RestoreBody();
        }

        public void Show(SpriteRenderer actor, Vector2 direction, bool moving, float time)
        {
            if (!actor || !actor.enabled)
            {
                RenderOff();
                return;
            }

            var pose = poses[CharacterWalkCycle.Octant(direction)];
            UseBody(actor, pose.GetBody(moving, time));

            float age = time - shot.time;
            flash.enabled = shotTrace && age >= 0 && age < FlashDuration;
            bodyMask.enabled = false;
            if (!flash.enabled && (!shotTrace || !shotTrace.enabled)) return;

            var muzzleOffset = poses[CharacterWalkCycle.Octant(shot.direction)].GetMuzzle(moving, time);
            var feet = shot.origin - Vector2.up * TargetRangeRoom.MuzzleHeight;
            shotMuzzle ??= feet + (Vector2)actor.transform.TransformVector(muzzleOffset);
            Vector2 muzzle = direction == shot.direction
                ? (Vector2)actor.transform.TransformPoint(muzzleOffset) : shotMuzzle.Value;

            bool behindActor = shot.direction.y > 0 || shot.direction.x < 0;
            sorting.sortingLayerID = actor.sortingLayerID;
            sorting.sortingOrder = actor.sortingOrder + (behindActor ? -1 : 1);
            if (flash.enabled)
            {
                float angle = Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg;
                flash.transform.SetPositionAndRotation(muzzle, Quaternion.Euler(0f, 0f, angle));
                flash.transform.localScale = Vector3.one * .72f;
                flash.color = flashColor;
            }

            bodyMask.enabled = behindActor && flash.enabled;
            flash.maskInteraction = behindActor
                ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;
            if (bodyMask.enabled)
            {
                bodyMask.sprite = actor.sprite;
                bodyMask.transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);
                Vector3 scale = actor.transform.lossyScale;
                bodyMask.transform.localScale = new Vector3(actor.flipX ? -scale.x : scale.x,
                    actor.flipY ? -scale.y : scale.y, scale.z);
            }

            if (shotTrace.enabled)
            {
                Vector2 end = shotTrace.GetPosition(1);
                shotTrace.SetPosition(0, Vector2.Dot(end - muzzle, shot.direction) > 0f ? muzzle : end);
            }
        }

        void UseBody(SpriteRenderer actor, Sprite body)
        {
            if (posingActor != actor) RestoreBody();
            if (actor.sprite != activePose) walkingSprite = actor.sprite;
            posingActor = actor;
            activePose = body;
            actor.sprite = body;
        }

        void RestoreBody()
        {
            if (posingActor && posingActor.sprite == activePose) posingActor.sprite = walkingSprite;
            posingActor = null;
            activePose = walkingSprite = null;
        }

    }
}
