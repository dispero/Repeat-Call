using UnityEngine;

namespace Golovomka
{
    public sealed class PoliceStationLayout : MonoBehaviour
    {
        public const string ScenePath = "Assets/Golovomka/Scenes/PoliceStation.unity";

        public Transform entrance, phone, exit, exitLabel, furniture;
        public SpriteRenderer phoneSprite;
        public Transform noticeBoard;
        [TextArea] public string leftNotice;
        [TextArea] public string rightNotice;
        [TextArea] public string lowerNotice;
    }
}
