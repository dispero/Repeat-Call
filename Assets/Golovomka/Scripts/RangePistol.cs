using UnityEngine;
using UnityEngine.Rendering;

namespace Golovomka
{
    sealed class RangePistol
    {
        internal const int DirectionCount = 4;
        internal const int ShotFrameCount = 3;
        const float FlashDuration = .06f;
        const float RecoilEnd = .16f;
        const float ShotAnimationEnd = .24f;

        internal static int PoseIndex(Vector2 direction) => CharacterWalkCycle.Octant(direction) / 2;
        internal static int TraceSortingOrder(Vector2 direction, float feetY) =>
            direction == Vector2.up ? 99 - Mathf.RoundToInt(feetY * 10f) : 250;

        readonly SpriteRenderer flash;
        readonly SortingGroup sorting;
        readonly SpriteMask bodyMask;
        readonly TargetRangeRoom.PistolPose[] poses;
        SpriteRenderer posingActor;
        Sprite walkingSprite, activePose;
        EchoShot shot;
        bool hasShot;

        public RangePistol(Transform parent, bool echo, Sprite flashSprite,
            TargetRangeRoom.PistolPose[] poses)
        {
            this.poses = poses;
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
            flash.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            flash.transform.localScale = Vector3.one * .72f;
            flash.color = echo ? new Color(.35f, .9f, 1f, .75f) : Color.white;
        }

        public void Fire(EchoShot firedShot)
        {
            shot = firedShot;
            hasShot = true;
        }

        public void Hide()
        {
            RenderOff();
            hasShot = false;
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

            float age = time - shot.time;
            bool firing = hasShot && age >= 0 && age < ShotAnimationEnd;
            var pose = poses[PoseIndex(firing ? shot.direction : direction)];
            int phase = age < FlashDuration ? 0 : age < RecoilEnd ? 1 : 2;
            UseBody(actor, firing ? pose.shoot[phase] : pose.GetBody(moving, time));

            // В боковых и переднем кадрах вспышка уже нарисована в спрайте.
            flash.enabled = firing && age < FlashDuration && shot.direction == Vector2.up;
            bodyMask.enabled = flash.enabled;
            if (!flash.enabled) return;

            sorting.sortingLayerID = actor.sortingLayerID;
            sorting.sortingOrder = actor.sortingOrder - 1;
            flash.transform.SetPositionAndRotation(shot.Muzzle, Quaternion.Euler(0f, 0f, 90f));
            bodyMask.sprite = actor.sprite;
            bodyMask.transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);
            Vector3 scale = actor.transform.lossyScale;
            bodyMask.transform.localScale = new Vector3(actor.flipX ? -scale.x : scale.x,
                actor.flipY ? -scale.y : scale.y, scale.z);
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
