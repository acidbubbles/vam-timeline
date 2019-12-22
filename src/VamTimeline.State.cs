using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline Controller
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class State
    {
        public UnityEvent OnUpdated = new UnityEvent();
        public readonly List<ControllerState> Controllers = new List<ControllerState>();

        public State()
        {
        }

        public void Add(FreeControllerV3 controller)
        {
            if (Controllers.Any(c => c.Controller == controller)) return;
            ControllerState controllerState = new ControllerState(controller);
            Controllers.Add(controllerState);
            OnUpdated.Invoke();
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = Controllers.FirstOrDefault(c => c.Controller == controller);
            if (existing != null)
            {
                Controllers.Remove(existing);
                OnUpdated.Invoke();
            }
        }

        internal void Play()
        {
            foreach (var controller in Controllers)
            {
                controller.Animation["test"].time = 0;
                controller.Animation.Play("test");
            }
        }

        internal void Stop()
        {
            foreach (var controller in Controllers)
            {
                controller.Animation.Stop("test");
            }

            SetTime(0);
        }

        public void SetTime(float time)
        {
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                animState.time = time;
                if (!animState.enabled)
                {
                    // TODO: Can we set this once?
                    animState.enabled = true;
                    controller.Animation.Sample();
                    animState.enabled = false;
                }
            }

            OnUpdated.Invoke();
        }

        public void PauseToggle()
        {
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                animState.enabled = !animState.enabled;
            }
        }

        public bool IsPlaying()
        {
            if (Controllers.Count == 0) return false;
            return Controllers[0].Animation.IsPlaying("test");
        }

        public float GetTime()
        {
            if (Controllers.Count == 0) return 0f;
            var animState = Controllers[0].Animation["test"];
            return animState.time % animState.length;
        }

        public void NextFrame()
        {
            var time = GetTime();
            // TODO: Hardcoded loop length
            var nextTime = 5f;
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                var controllerNextTime = controller.X.keys.FirstOrDefault(k => k.time > time).time;
                if (controllerNextTime != 0 && controllerNextTime < nextTime) nextTime = controllerNextTime;
            }
            if (nextTime == 5f)
                SetTime(0f);
            else
                SetTime(nextTime);
        }

        public void PreviousFrame()
        {
            var time = GetTime();
            var previousTime = 0f;
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                var controllerNextTime = controller.X.keys.LastOrDefault(k => k.time < time).time;
                if (controllerNextTime != 0 && controllerNextTime > previousTime) previousTime = controllerNextTime;
            }
            if (previousTime == 0f)
                // TODO: Instead, move to the last frame
                SetTime(0f);
            else
                SetTime(previousTime);
        }
    }
}