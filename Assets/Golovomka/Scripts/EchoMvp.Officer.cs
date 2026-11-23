using UnityEngine;
using UnityEngine.InputSystem;

namespace Golovomka
{
    public partial class EchoMvp
    {
        const float MoveAccelerationTime = .12f;
        const float InitialMoveSpeedFraction = .5f;
        const float FacingDelay = .09f;
        float moveHeldTime;
        float facingHeldTime;
        int desiredFacing;

        static Vector2 OfficerDirection(int direction) => direction switch
        {
            0 => Vector2.right,
            1 => Vector2.left,
            2 => Vector2.up,
            _ => Vector2.down
        };

        Vector2 OfficerMuzzle() =>
            hero.transform.TransformPoint(pistolArt.GetPistolMuzzle(ShotDirection));

        void ResetOfficerMovement()
        {
            moveHeldTime = 0;
            facingHeldTime = 0;
            desiredFacing = facing;
        }

        void MoveOfficer(Vector2 move, Keyboard keyboard, float deltaTime)
        {
            if (move == Vector2.zero)
            {
                ResetOfficerMovement();
                return;
            }

            int nextFacing = ReadOfficerFacing(move, keyboard);
            if (nextFacing != desiredFacing)
            {
                desiredFacing = nextFacing;
                facingHeldTime = 0;
            }
            if (facing != desiredFacing)
            {
                facingHeldTime += deltaTime;
                if (facingHeldTime >= FacingDelay) facing = desiredFacing;
            }

            float speedFraction = Mathf.Lerp(InitialMoveSpeedFraction, 1f, moveHeldTime / MoveAccelerationTime);
            moveHeldTime = Mathf.Min(moveHeldTime + deltaTime, MoveAccelerationTime);
            var delta = move.normalized * (moveSpeed * speedFraction * deltaTime);
            Vector2 step = position + new Vector2(delta.x, 0);
            if (CanMove(step)) position = step;
            step = position + new Vector2(0, delta.y);
            if (CanMove(step)) position = step;
        }

        int ReadOfficerFacing(Vector2 move, Keyboard keyboard)
        {
            int horizontal = move.x > 0 ? 0 : 1;
            int vertical = move.y > 0 ? 2 : 3;
            if (move.y == 0) return horizontal;
            if (move.x == 0) return vertical;

            // Во время поворота новая клавиша может на короткое время перекрыть предыдущую.
            bool horizontalPressed = move.x > 0
                ? keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                : keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            bool verticalPressed = move.y > 0
                ? keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame
                : keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            if (horizontalPressed != verticalPressed)
                return horizontalPressed ? horizontal : vertical;
            return desiredFacing == horizontal ? horizontal : vertical;
        }

    }
}
