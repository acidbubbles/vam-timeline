using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public class AtomAnimationEditContext : MonoBehaviour
    {
        public struct TimeChangedEventArgs { public float time; public float currentClipTime; }
        public class TimeChangedEvent : UnityEvent<TimeChangedEventArgs> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }
        public class AnimationSettingsChanged : UnityEvent<string> { }

        public AnimationSettingsChanged onEditorSettingsChanged = new AnimationSettingsChanged();
        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onTargetsSelectionChanged = new UnityEvent();

        public HashSet<IAtomAnimationTarget> selectedTargets = new HashSet<IAtomAnimationTarget>();

        private float _snap = 0.1f;
        public float snap
        {
            get { return _snap; }
            set { _snap = value; onEditorSettingsChanged.Invoke(nameof(snap)); }
        }
        private bool _autoKeyframeAllControllers;
        public bool autoKeyframeAllControllers
        {
            get { return _autoKeyframeAllControllers; }
            set { _autoKeyframeAllControllers = value; onEditorSettingsChanged.Invoke(nameof(autoKeyframeAllControllers)); }
        }
        public TimeChangedEventArgs timeArgs => new TimeChangedEventArgs { time = playTime, currentClipTime = current.clipTime };
        public AtomAnimationClip current { get; private set; }
        // This ugly property is to cleanly allow ignoring grab release at the end of a mocap recording
        public bool ignoreGrabEnd;
        private AtomAnimation _animation;

        public AtomAnimation animation
        {
            get
            {
                return _animation;
            }
            set
            {
                _animation = value;
                _animation.onClipsListChanged.AddListener(OnClipsListChanged);
            }
        }

        private void OnClipsListChanged()
        {
            onTargetsSelectionChanged.Invoke();
        }

        public float clipTime
        {
            get
            {
                return current.clipTime;
            }
            set
            {
                playTime = value;
                if (current == null) return;
                current.clipTime = value;
                if (animation.isPlaying && !current.playbackEnabled && current.playbackMainInLayer) animation.PlayClip(current, animation.sequencing);
                animation.Sample();
                if (current.animationPattern != null)
                    current.animationPattern.SetFloatParamValue("currentTime", playTime);
                onTimeChanged.Invoke(timeArgs);
            }
        }

        public float playTime
        {
            get
            {
                return animation.playTime;
            }
            set
            {
                animation.playTime = value;
                if (!current.playbackEnabled)
                    current.clipTime = value;
                animation.Sample();
                onTimeChanged.Invoke(timeArgs);
            }
        }

        public void Initialize()
        {
            if (animation.clips.Count == 0)
                animation.AddClip(new AtomAnimationClip("Anim 1", AtomAnimationClip.DefaultAnimationLayer));
            current = animation.GetDefaultClip();
            if (animation.clips.Any(c => c.IsDirty()))
            {
                animation.RebuildAnimationNow();
            }
        }

        public int SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            return target.SetKeyframeToCurrentTransform(time);
        }

        public void PlayCurrentAndOtherMainsInLayers(bool sequencing = true)
        {
            animation.PlayOneAndOtherMainsInLayers(current, sequencing);
        }

        #region Selection

        public void SelectAnimation(string animationName)
        {
            var clip = animation.GetClip(animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", animation.clips.Select(c => c.animationName).ToArray())}'.");
            SelectAnimation(clip);
        }

        public void SelectAnimation(AtomAnimationClip clip)
        {
            var previous = current;
            var previousSelected = selectedTargets;
            current = clip;
            selectedTargets.Clear();
            foreach (var target in previousSelected)
            {
                var t = current.GetAllTargets().FirstOrDefault(x => x.TargetsSameAs(target));
                if (t == null) continue;
                selectedTargets.Add(t);
            }
            // TODO: Check if this makes sense or not
            if (previous.animationLayer != current.animationLayer)
                animation.onClipsListChanged.Invoke();
            onTargetsSelectionChanged.Invoke();
            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });
            // TODO: This was not there, validate
            animation.Sample();
        }

        public IEnumerable<IAtomAnimationTarget> GetAllOrSelectedTargets()
        {
            return selectedTargets.Count > 0 ? selectedTargets : current.GetAllTargets();
        }

        public IEnumerable<IAtomAnimationTarget> GetSelectedTargets()
        {
            return selectedTargets;
        }

        public void SetSelected(IAtomAnimationTarget target, bool selected)
        {
            if (selected && !selectedTargets.Contains(target))
                selectedTargets.Add(target);
            else if (!selected && selectedTargets.Contains(target))
                selectedTargets.Remove(target);
            onTargetsSelectionChanged.Invoke();
        }

        public bool IsSelected(IAtomAnimationTarget t)
        {
            return selectedTargets.Contains(t);
        }

        #endregion

        #region Frame Nav

        public float GetNextFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(current.animationLength))
                return 0f;
            var nextTime = current.animationLength;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                // TODO: Use bisect for more efficient navigation
                var keyframes = controller.GetAllKeyframesTime();
                for (var key = 0; key < keyframes.Length; key++)
                {
                    var potentialNextTime = keyframes[key];
                    if (potentialNextTime <= time) continue;
                    if (potentialNextTime > nextTime) continue;
                    nextTime = potentialNextTime;
                    break;
                }
            }
            if (nextTime.IsSameFrame(current.animationLength) && current.loop)
                return 0f;
            else
                return nextTime;
        }

        public float GetPreviousFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(0))
            {
                try
                {
                    return GetAllOrSelectedTargets().Select(t => t.GetAllKeyframesTime()).Select(c => c[c.Length - (current.loop ? 2 : 1)]).Max();
                }
                catch (InvalidOperationException)
                {
                    return 0f;
                }
            }
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                // TODO: Use bisect for more efficient navigation
                var keyframes = controller.GetAllKeyframesTime();
                for (var key = keyframes.Length - 2; key >= 0; key--)
                {
                    var potentialPreviousTime = keyframes[key];
                    if (potentialPreviousTime >= time) continue;
                    if (potentialPreviousTime < previousTime) continue;
                    previousTime = potentialPreviousTime;
                    break;
                }
            }
            return previousTime;
        }

        #endregion

        #region Unity Lifecycle

        public void OnDestroy()
        {
            onTimeChanged.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onEditorSettingsChanged.RemoveAllListeners();
        }

        #endregion
    }
}
