using UnityEngine;
using UnityEngine.Rendering;

namespace Golovomka
{
    sealed class RangePistol
    {
        internal const int DirectionCount = 8;
        const float FlashDuration = .06f;
        const float ExtendedDuration = .045f;
        const float RecoilDuration = .16f;
        const float RecoveryDuration = .24f;

        readonly SpriteRenderer flash;
        readonly SortingGroup sorting;
        readonly SpriteMask bodyMask;
        readonly TargetRangeRoom.PistolPose[] poses;
        readonly Color flashColor;
        SpriteRenderer posingActor;
        Sprite walkingSprite, activePose;
        EchoShot shot;
        LineRenderer shotTrace;
        float firedAt = float.NegativeInfinity;

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
            firedAt = shot.time;
        }

        public void Hide()
        {
            RenderOff();
            firedAt = float.NegativeInfinity;
            shotTrace = null;
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

            float age = time - firedAt;
            bool recovering = age >= 0 && age < RecoveryDuration;
            if (recovering) direction = shot.direction;
            var pose = poses[CharacterWalkCycle.Octant(direction)];
            bool walking = moving && !recovering;
            bool extended = recovering && age < ExtendedDuration;
            bool recoiling = recovering && age < RecoilDuration;
            UseBody(actor, extended ? pose.firing : recoiling ? pose.recoil :
                pose.GetBody(walking, time));

            bool behindActor = direction.y > .1f || (direction.x < 0 && Mathf.Abs(direction.y) <= .1f);
            sorting.sortingLayerID = actor.sortingLayerID;
            sorting.sortingOrder = actor.sortingOrder + (behindActor ? -1 : 1);
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            flash.enabled = recovering && age < FlashDuration;
            if (flash.enabled)
            {
                Vector2 muzzle = extended ? pose.muzzle : pose.recoilMuzzle;
                flash.transform.SetPositionAndRotation(actor.transform.TransformPoint(muzzle), rotation);
                flash.transform.localScale = Vector3.one * (extended ? .72f : .46f);
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

            if (recovering && shotTrace && shotTrace.enabled)
            {
                Vector2 end = shotTrace.GetPosition(1);
                Vector2 feet = shot.origin - Vector2.up * TargetRangeRoom.MuzzleHeight;
                Vector2 muzzle = feet + (Vector2)actor.transform.TransformVector(extended ? pose.muzzle : pose.recoilMuzzle);
                shotTrace.SetPosition(0, Vector2.Dot(end - muzzle, direction) > 0f ? muzzle : end);
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
