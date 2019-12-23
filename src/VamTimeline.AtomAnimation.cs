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
    public class AtomAnimation
    {
        public const string AnimationName = "Anim1";
        public const float AnimationLength = 5f;

        public readonly UnityEvent OnUpdated = new UnityEvent();
        public readonly List<FreeControllerV3Animation> Controllers = new List<FreeControllerV3Animation>();
        private FreeControllerV3Animation _selected;

        public AtomAnimation()
        {
        }

        public void Add(FreeControllerV3 controller)
        {
            if (Controllers.Any(c => c.Controller == controller)) return;
            FreeControllerV3Animation controllerState = new FreeControllerV3Animation(controller, AnimationName, AnimationLength);
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

        public void Play()
        {
            foreach (var controller in Controllers)
            {
                controller.Animation[AnimationName].time = 0;
                controller.Animation.Play(AnimationName);
            }
        }

        internal void Stop()
        {
            foreach (var controller in Controllers)
            {
                controller.Animation.Stop(AnimationName);
            }

            SetTime(0);
        }

        public void SetTime(float time)
        {
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation[AnimationName];
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

        public void SetFilter(string val)
        {
            _selected = string.IsNullOrEmpty(val)
                ? null
                : Controllers.FirstOrDefault(c => c.Name == val);
        }

        public List<string> GetFilters()
        {
            return Controllers.Select(c => c.Name).ToList();
        }

        public void PauseToggle()
        {
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation[AnimationName];
                animState.enabled = !animState.enabled;
            }
        }

        public bool IsPlaying()
        {
            if (Controllers.Count == 0) return false;
            return Controllers[0].Animation.IsPlaying(AnimationName);
        }

        public float GetTime()
        {
            if (Controllers.Count == 0) return 0f;
            var animState = Controllers[0].Animation[AnimationName];
            return animState.time % animState.length;
        }

        public void NextFrame()
        {
            var time = GetTime();
            // TODO: Hardcoded loop length
            var nextTime = AnimationLength;
            foreach (var controller in GetAllOrSelectedControllers())
            {
                var animState = controller.Animation[AnimationName];
                var controllerNextTime = controller.X.keys.FirstOrDefault(k => k.time > time).time;
                if (controllerNextTime != 0 && controllerNextTime < nextTime) nextTime = controllerNextTime;
            }
            if (nextTime == AnimationLength)
                SetTime(0f);
            else
                SetTime(nextTime);
        }

        public void PreviousFrame()
        {
            var time = GetTime();
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedControllers())
            {
                var animState = controller.Animation[AnimationName];
                var controllerNextTime = controller.X.keys.LastOrDefault(k => k.time < time).time;
                if (controllerNextTime != 0 && controllerNextTime > previousTime) previousTime = controllerNextTime;
            }
            if (previousTime == 0f)
                // TODO: Instead, move to the last frame
                SetTime(0f);
            else
                SetTime(previousTime);
        }

        private IEnumerable<FreeControllerV3Animation> GetAllOrSelectedControllers()
        {
            if (_selected != null) return new[] { _selected };
            return Controllers;
        }
    }
}
