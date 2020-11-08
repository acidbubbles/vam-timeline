using System;
using System.Collections;
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

        private const float _timeDeltaUntilSnapBones = 0.5f;

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
        private Coroutine _sampleDeferredCoroutine;
        public Atom containingAtom;
        private bool _snapBonesOnSample;

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
                var originalClipTime = current.clipTime;
                animation.playTime = value;
                if (current == null) return;
                foreach (var clip in animation.GetClips(current.animationName))
                {
                    clip.clipTime = value;
                    if (animation.isPlaying && !clip.playbackEnabled && clip.playbackMainInLayer) animation.PlayClip(clip, animation.sequencing);
                }
                if (!animation.isPlaying || animation.paused)
                {
                    var timeDelta = Mathf.Abs(originalClipTime - current.clipTime);
                    if (current.loop)
                        timeDelta = Mathf.Min(timeDelta, Mathf.Abs(timeDelta - current.animationLength));
                    if (timeDelta > _timeDeltaUntilSnapBones) _snapBonesOnSample = true;
                    Sample();
                }
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
                {
                    foreach (var clip in animation.GetClips(current.animationName))
                    {
                        clip.clipTime = value;
                        if (animation.isPlaying && !clip.playbackEnabled && clip.playbackMainInLayer) animation.PlayClip(clip, animation.sequencing);
                    }
                }
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
                clip.playbackBlendWeight = 1f;
            }
            animation.Sample();
            foreach (var clip in clips)
            {
                // foreach (var target in clip.targetControllers)
                // {
                //     var parent = target.GetParent();
                //     if (parent == null) continue;
                //     var bone = parent.GetComponent<DAZBone>();
                //     if(bone == null) continue;
                //     var controller = bone.control;
                //     if(controller == null) continue;
                //     if (controller.containingAtom == target.controller.containingAtom) continue;
                //     target.controller.control.position = bone.transform.position;
                //     target.controller.control.rotation = bone.transform.rotation;
                // }
                clip.playbackEnabled = false;
                clip.playbackBlendWeight = 0f;
            }
            // Time.timeScale = 0f;

            if (_snapBonesOnSample)
            {
                // TODO: Cleanup, keep a reference to it, find it more efficiently
                _snapBonesOnSample = false;
                var snapRestore = containingAtom.GetComponentInChildren<CharacterPoseSnapRestore>();
                if (snapRestore != null)
                {
                    snapRestore.ForceSnapRestore();
                }
            }

            if (!GetMainClipPerLayer().SelectMany(c => c.targetControllers).Any(t => t.parentRigidbodyId != null)) return;

            _sampleUntil = Time.time + 1f;
            if (_sampleDeferredCoroutine == null) _sampleDeferredCoroutine = StartCoroutine(SampleDeferred());
        }

        private float _sampleUntil = 0f;
        private IEnumerator SampleDeferred()
        {
            // Give a little bit of time for physics to settle and re-sample
            do
            {
                yield return 0;

                if (animation.isPlaying)
                {
                    _sampleDeferredCoroutine = null;
                    yield break;
                }

                var clips = GetMainClipPerLayer();
                foreach (var clip in clips)
                {
                    clip.playbackEnabled = true;
                    clip.playbackBlendWeight = 1f;
                }
                animation.Sample();
                foreach (var clip in clips)
                {
                    clip.playbackEnabled = false;
                    clip.playbackBlendWeight = 0f;
                }
            } while (Time.time < _sampleUntil);

            _sampleDeferredCoroutine = null;
        }

        private IEnumerable<AtomAnimationClip> GetMainClipPerLayer()
        {
            return animation.index
                .ByLayer()
                .Select(g =>
                {
                    return g.Key == current.animationLayer
                        ? current
                        : (g.Value.FirstOrDefault(c => c.playbackMainInLayer) ?? g.Value.FirstOrDefault(c => c.animationName == current.animationName) ?? g.Value.FirstOrDefault(c => c.autoPlay) ?? g.Value[0]);
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
            else if (!SuperController.singleton.freezeAnimation)
            {
                _snapBonesOnSample = true;
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
