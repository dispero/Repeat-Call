using System;
using UnityEngine;

namespace Golovomka
{
    [DisallowMultipleComponent]
    public sealed class TimedButtonsRoom : MonoBehaviour
    {
        [Serializable]
        public sealed class Button
        {
            public Transform interactionPoint;
            public SpriteRenderer lamp;
            public Color color;
        }

        public SlidingDoor exitDoor;
        public Button[] buttons = new Button[3];
        public int[] order = { 1, 3, 2, 3, 2, 2, 1 };
        public SpriteRenderer[] indicators = new SpriteRenderer[7];
        [Min(.1f)] public float stepInterval = 1.6f;
        [Min(.05f)] public float pressWindow = .8f;
        [Min(.1f)] public float interactionRadius = .85f;

        int accepted;
        int replayIndex;
        int displayedStep = -1;
        bool unlocked;
        float? sequenceStart;

        public event Action SignalShown;
        public event Action PressAccepted;

        public bool Solved => unlocked || accepted == (1 << order.Length) - 1;
        public bool AwaitingStart => !Solved && !sequenceStart.HasValue;
        float SequenceDuration => (order.Length - 1) * stepInterval + pressWindow;

        public bool CanRetry(float time) =>
            !Solved && sequenceStart.HasValue && time - sequenceStart.Value >= SequenceDuration;

        public bool IsConfigured
        {
            get
            {
                if (!exitDoor || !exitDoor.Passage || buttons.Length != 3 || order.Length < 1 ||
                    order.Length > 30 || order[0] != 1 || indicators.Length != order.Length || pressWindow >= stepInterval)
                    return false;
                foreach (var button in buttons)
                    if (button == null || !button.interactionPoint || !button.lamp) return false;
                for (int i = 0; i < order.Length; i++)
                    if (order[i] < 1 || order[i] > buttons.Length || !indicators[i]) return false;
                return true;
            }
        }

        public void ResetAttempt(bool alreadyPassed)
        {
            accepted = replayIndex = 0;
            displayedStep = -1;
            unlocked = alreadyPassed;
            sequenceStart = null;
            Show(0);
        }

        int WindowAt(float time)
        {
            if (!sequenceStart.HasValue) return -1;
            float offset = time - sequenceStart.Value;
            if (offset < 0 || offset >= SequenceDuration) return -1;
            int step = Mathf.FloorToInt(offset / stepInterval);
            return offset < step * stepInterval + pressWindow ? step : -1;
        }

        public int NearbyButton(Vector2 position)
        {
            for (int i = 0; i < buttons.Length; i++)
                if (Vector2.Distance(position, buttons[i].interactionPoint.position) < interactionRadius)
                    return i + 1;
            return 0;
        }

        bool Accept(EchoButtonPress press)
        {
            if (Solved) return false;
            if (!sequenceStart.HasValue && press.startsSequence && press.button == 1)
                sequenceStart = press.time;
            int step = WindowAt(press.time);
            if (step < 0 || order[step] != press.button) return false;
            int stepMask = 1 << step;
            if ((accepted & stepMask) == 0)
            {
                accepted |= stepMask;
                PressAccepted?.Invoke();
            }
            return true;
        }

        public void Press(int button, float time, EchoTape recording)
        {
            var press = new EchoButtonPress(time, button, !sequenceStart.HasValue && button == 1);
            if (!Accept(press)) return;
            var presses = recording.buttonPresses;
            if (presses.Count == 0 || WindowAt(presses[presses.Count - 1].time) != WindowAt(time))
                presses.Add(press);
        }

        public void Replay(EchoTape tape, float time)
        {
            var presses = tape.buttonPresses;
            while (replayIndex < presses.Count && presses[replayIndex].time <= time)
            {
                var press = presses[replayIndex++];
                if (press.time < tape.duration) Accept(press);
            }
        }

        public void Show(float time)
        {
            int active = Solved ? -1 : WindowAt(time);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.lamp.color = active >= 0 && order[active] == i + 1
                    ? Color.Lerp(button.color, Color.white, .34f) : button.color * new Color(.59f, .59f, .59f);
            }
            for (int i = 0; i < order.Length; i++)
            {
                Color color = buttons[order[i] - 1].color;
                bool pressed = (accepted & (1 << i)) != 0;
                indicators[i].color = active == i ? Color.Lerp(color, Color.white, .42f)
                    : pressed ? color : color * new Color(.65f, .65f, .65f);
            }
            bool newSignal = active >= 0 && active != displayedStep;
            displayedStep = active;
            if (newSignal) SignalShown?.Invoke();
        }
    }
}
