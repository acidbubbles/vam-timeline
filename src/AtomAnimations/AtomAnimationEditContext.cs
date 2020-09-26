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

        private bool _sampleAfterRebuild;

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
        private bool _locked;
        public bool locked
        {
            get { return _locked; }
            set { _locked = value; onEditorSettingsChanged.Invoke(nameof(locked)); }
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
                if (_animation != null)
                {
                    _animation.onAnimationRebuilt.RemoveListener(OnAnimationRebuilt);
                }
                _animation = value;
                _animation.onAnimationRebuilt.AddListener(OnAnimationRebuilt);
            }
        }

        private void OnAnimationRebuilt()
        {
            if (_sampleAfterRebuild)
            {
                _sampleAfterRebuild = false;
                Sample();
            }
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
                foreach (var clip in animation.GetClips(current.animationName))
                {
                    clip.clipTime = value;
                    if (animation.isPlaying && !clip.playbackEnabled && clip.playbackMainInLayer) animation.PlayClip(clip, animation.sequencing);
                }
                if (!animation.isPlaying)
                    Sample();
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
                Sample();
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

        public bool CanEdit()
        {
            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit) return false;
            if (locked) return false;
            if (animation.isPlaying || animation.isSampling) return false;
            return true;
        }

        #region Keyframing

        public int SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            return target.SetKeyframeToCurrentTransform(time);
        }

        #endregion

        #region Playback

        public void PlayCurrentAndOtherMainsInLayers(bool sequencing = true)
        {
            foreach (var clip in GetMainClipPerLayer())
            {
                animation.PlayClip(clip, sequencing);
            }
        }

        public void Sample()
        {
            if (animation.RebuildPending())
            {
                _sampleAfterRebuild = true;
                return;
            }

            var clips = GetMainClipPerLayer();
            foreach (var clip in clips)
            {
                clip.playbackEnabled = true;
                clip.playbackWeight = 1f;
            }
            animation.Sample();
            foreach (var clip in clips)
            {
                clip.playbackEnabled = false;
                clip.playbackWeight = 0f;
            }
        }

        private IEnumerable<AtomAnimationClip> GetMainClipPerLayer()
        {
            return animation.clips
                .GroupBy(c => c.animationLayer)
                .Select(g =>
                {
                    return g.Key == current.animationLayer ? current : (g.FirstOrDefault(c => c.playbackMainInLayer) ?? g.FirstOrDefault(c => c.animationName == current.animationName) ?? g.FirstOrDefault(c => c.autoPlay) ?? g.First());
                });
        }

        #endregion

        #region Selection

        public void SelectAnimation(string animationNameQualified)
        {
            var clip = animation.GetClipQualified(animationNameQualified);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationNameQualified}'. Found animations: '{string.Join("', '", animation.clips.Select(c => c.animationNameQualified).ToArray())}'.");
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
            onTargetsSelectionChanged.Invoke();
            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });

            if (animation.isPlaying)
            {
                animation.PlayClip(current, animation.sequencing);
            }
            else
            {
                Sample();
            }
        }

        public IEnumerable<IAtomAnimationTarget> GetAllOrSelectedTargets()
        {
            return selectedTargets.Count > 0 ? selectedTargets : current.GetAllTargets();
        }

        public IEnumerable<IAtomAnimationTarget> GetSelectedTargets()
        {
            return selectedTargets;
        }

        public void DeselectAll()
        {
            selectedTargets.Clear();
            onTargetsSelectionChanged.Invoke();
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
            if (_animation != null)
            {
                _animation.onAnimationRebuilt.RemoveListener(OnAnimationRebuilt);
            }

            onTimeChanged.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onEditorSettingsChanged.RemoveAllListeners();
        }

        #endregion
    }
}
