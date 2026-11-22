using UnityEngine;
using UnityEngine.InputSystem;

namespace Golovomka
{
    public partial class EchoMvp
    {
        static Vector2 OfficerDirection(int direction) => direction switch
        {
            0 => Vector2.right,
            1 => Vector2.left,
            2 => Vector2.up,
            _ => Vector2.down
        };

        Vector2 OfficerMuzzle() =>
            hero.transform.TransformPoint(pistolArt.GetPistolMuzzle(ShotDirection));

        void UpdateOfficerFacing(Vector2 move, Keyboard keyboard)
        {
            int horizontal = move.x > 0 ? 0 : 1;
            int vertical = move.y > 0 ? 2 : 3;
            if (move.y == 0) { facing = horizontal; return; }
            if (move.x == 0) { facing = vertical; return; }

            // Во время поворота новая клавиша может на короткое время перекрыть предыдущую.
            bool horizontalPressed = move.x > 0
                ? keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                : keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            bool verticalPressed = move.y > 0
                ? keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame
                : keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            if (horizontalPressed != verticalPressed)
                facing = horizontalPressed ? horizontal : vertical;
            else if (facing != horizontal && facing != vertical)
                facing = vertical;
        }

    }
}
