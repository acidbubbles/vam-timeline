using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    public class AtomAnimation : MonoBehaviour
    {
        public class IsPlayingEvent : UnityEvent<AtomAnimationClip> { }

        private static readonly Regex _lastDigitsRegex = new Regex(@"^(?<name>.+)(?<index>[0-9]+)$", RegexOptions.Compiled);

        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";

        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public UnityEvent onSpeedChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public UnityEvent onAnimationRebuilt = new UnityEvent();
        public UnityEvent onPausedChanged = new UnityEvent();
        public readonly IsPlayingEvent onIsPlayingChanged = new IsPlayingEvent();
        public readonly IsPlayingEvent onClipIsPlayingChanged = new IsPlayingEvent();

        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public bool isPlaying { get; private set; }
        private bool _paused;
        public bool paused
        {
            get
            {
                return _paused;
            }
            set
            {
                var dispatch = value != _paused;
                _paused = value;
                if (dispatch)
                    onPausedChanged.Invoke();
            }
        }
        private bool allowAnimationProcessing => isPlaying && !SuperController.singleton.freezeAnimation;

        public int timeMode { get; set; }

        public bool master { get; set; }

        private float _playTime;
        public float playTime
        {
            get
            {
                return _playTime;
            }
            set
            {
                _playTime = value;
                for (var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    clip.clipTime = _playTime;
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("currentTime", clip.clipTime);
                }
            }
        }

        private float _speed = 1f;
        public float speed
        {
            get
            {
                return _speed;
            }

            set
            {
                _speed = value;
                for (var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }

                onSpeedChanged.Invoke();
            }
        }

        public bool sequencing { get; private set; }

        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;

        public AtomAnimationsClipsIndex index { get; }
        public AnimatablesRegistry animatables { get; }

        public bool syncSubsceneOnly { get; set; }
        public bool syncWithPeers { get; set; } = true;

        public AtomAnimation()
        {
            index = new AtomAnimationsClipsIndex(clips);
            animatables = new AnimatablesRegistry();
        }

        public AtomAnimationClip GetDefaultClip()
        {
            return index.ByLayer(clips[0].animationLayer).FirstOrDefault(c => c.autoPlay) ?? clips[0];
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            return clips.Count == 1 && clips[0].IsEmpty();
        }

        #region Clips

        public AtomAnimationClip GetClip(string animationLayer, string animationName)
        {
            return clips.FirstOrDefault(c => c.animationLayer == animationLayer && c.animationName == animationName);
        }

        public IList<AtomAnimationClip> GetClips(string animationName)
        {
            return index.ByName(animationName);
        }

        public AtomAnimationClip GetClipQualified(string animationNameQualified)
        {
            return clips.FirstOrDefault(c => c.animationNameQualified == animationNameQualified);
        }

        public AtomAnimationClip AddClip(AtomAnimationClip clip)
        {
            var lastIndexOfLayer = clips.FindLastIndex(c => c.animationLayer == clip.animationLayer);
            if (lastIndexOfLayer == -1)
                clips.Add(clip);
            else
                clips.Insert(lastIndexOfLayer + 1, clip);
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
            index.Rebuild();
            onClipsListChanged.Invoke();
            if (clip.IsDirty()) clip.onAnimationKeyframesDirty.Invoke();
            return clip;
        }

        public AtomAnimationClip CreateClip(AtomAnimationClip source)
        {
            var animationName = GetNewAnimationName(source);
            return CreateClip(source.animationLayer, animationName);
        }

        public AtomAnimationClip CreateClip(string animationLayer, string animationName)
        {
            if (clips.Any(c => c.animationName == animationName)) throw new InvalidOperationException($"Animation '{animationName}' already exists");
            var clip = new AtomAnimationClip(animationName, animationLayer);
            AddClip(clip);
            return clip;
        }

        public void RemoveClip(AtomAnimationClip clip)
        {
            clips.Remove(clip);
            clip.Dispose();
            index.Rebuild();
            onClipsListChanged.Invoke();
            OnAnimationKeyframesDirty();
        }

        private string GetNewAnimationName(AtomAnimationClip source)
        {
            var match = _lastDigitsRegex.Match(source.animationName);
            string animationNameBeforeInt;
            int animationNameInt;
            if (!match.Success)
            {
                animationNameBeforeInt = $"{source.animationName.TrimEnd()} ";
                animationNameInt = 1;
            }
            else
            {
                animationNameBeforeInt = match.Groups["name"].Value;
                animationNameInt = int.Parse(match.Groups["index"].Value);
            }
            for (var i = animationNameInt + 1; i < 999; i++)
            {
                var animationName = animationNameBeforeInt + i;
                if (clips.All(c => c.animationName != animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        public IEnumerable<string> EnumerateLayers()
        {
            switch (clips.Count)
            {
                case 0:
                    yield break;
                case 1:
                    yield return clips[0].animationLayer;
                    yield break;
            }

            var lastLayer = clips[0].animationLayer;
            yield return lastLayer;
            for (var i = 1; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip.animationLayer == lastLayer) continue;
                yield return lastLayer = clip.animationLayer;
            }
        }

        public void Clear()
        {
            var list = clips.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var clip = list[i];
                RemoveClip(clip);
            }

            speed = 1f;
            _playTime = 0f;
        }

        #endregion

        #region Playback

        public static bool TryGetRandomizedGroup(string animationName, out string groupName)
        {
            if (!animationName.EndsWith(RandomizeGroupSuffix))
            {
                groupName = null;
                return false;
            }

            groupName = animationName.Substring(0, animationName.Length - RandomizeGroupSuffix.Length);
            return true;
        }

        public void PlayRandom(string groupName = null)
        {
            var candidates = clips
                .Where(c => !c.playbackMainInLayer && (groupName == null || c.animationNameGroup == groupName))
                .ToList();

            if (candidates.Count == 0)
                return;

            var idx = Random.Range(0, candidates.Count);
            var clip = candidates[idx];
            PlayClips(clip.animationName, true);
        }

        public void PlayClips(string animationName, bool sequencing)
        {
            foreach (var clip in GetClips(animationName))
                PlayClip(clip, sequencing);
        }

        public void PlayClip(AtomAnimationClip clip, bool sequencing, bool allowPreserveLoops = true)
        {
            paused = false;
            if (clip.playbackEnabled && clip.playbackMainInLayer) return;
            // if (clip.recording)
            // {
            //     clip.clipTime = 0f;
            //     Blend(clip, 1f, 0f);
            //     clip.playbackMainInLayer = true;
            //     onIsPlayingChanged.Invoke(clip);
            //     return;
            // }
            if (!isPlaying)
            {
                isPlaying = true;
                this.sequencing = this.sequencing || sequencing;
                #if(PLAYBACK_HEALTH_CHECK)
                PlaybackHealthCheck(clip);
                #endif
            }
            if (sequencing && !clip.playbackEnabled) clip.clipTime = 0;
            var previousMain = index.ByLayer(clip.animationLayer).FirstOrDefault(c => c.playbackMainInLayer);
            if (previousMain != null && previousMain != clip)
            {
                if (previousMain.uninterruptible)
                    return;

                if (clip.loop && clip.preserveLoops && previousMain.loop && allowPreserveLoops)
                {
                    previousMain.SetNext(clip.animationName, Mathf.Max(previousMain.animationLength - previousMain.clipTime, 0f));
                }
                else
                {
                    TransitionAnimation(previousMain, clip);
                }
            }
            else
            {
                if (clip.clipTime >= clip.animationLength) clip.clipTime = 0f;
                Blend(clip, 1f, clip.blendInDuration);
                clip.playbackMainInLayer = true;
            }
            if (clip.animationPattern)
            {
                clip.animationPattern.SetBoolParamValue("loopOnce", false);
                clip.animationPattern.ResetAndPlay();
            }
            if (sequencing && clip.nextAnimationName != null)
                AssignNextAnimation(clip);

            onIsPlayingChanged.Invoke(clip);
        }

        #if(PLAYBACK_HEALTH_CHECK)
        private static void PlaybackHealthCheck(AtomAnimationClip clip)
        {
            for (var i = 0; i < clip.targetControllers.Count; i++)
            {
                var target = clip.targetControllers[i];
                var controller = target.controller;
                if (target.controlRotation && controller.currentRotationState == FreeControllerV3.RotationState.Off || target.controlPosition && controller.currentPositionState == FreeControllerV3.PositionState.Off)
                    SuperController.LogError($"Timeline: Controller {controller.name} of atom {controller.containingAtom.name} has position or rotation off and will not play. You can turn of rotation/position if this is the desired result in the targets, in the controller settings.");
            }
        }
        #endif

        public void PlayOneAndOtherMainsInLayers(AtomAnimationClip selected, bool sequencing = true)
        {
            foreach (var clip in GetMainClipPerLayer())
            {
                if (clip.animationLayer == selected.animationLayer)
                    PlayClip(selected, sequencing);
                else
                    PlayClip(clip, sequencing);
            }
        }

        private IEnumerable<AtomAnimationClip> GetMainClipPerLayer()
        {
            return clips
                .GroupBy(c => c.animationLayer)
                .Select(g =>
                {
                    return g.FirstOrDefault(c => c.playbackMainInLayer) ?? g.FirstOrDefault(c => c.autoPlay) ?? g.First();
                });
        }

        public void StopClips(string animationName)
        {
            foreach (var clip in GetClips(animationName))
                StopClip(clip);
        }

        // private int x;
        public void StopClip(AtomAnimationClip clip)
        {
            // if(++x == 2)
            //     throw new Exception("X");
            if (clip.playbackEnabled)
            {
                clip.Leave();
                clip.Reset(false);
                if (clip.animationPattern)
                    clip.animationPattern.SetBoolParamValue("loopOnce", true);
            }
            else
            {
                clip.playbackMainInLayer = false;
            }

            if (isPlaying)
            {
                if (!clips.Any(c => c.playbackMainInLayer))
                {
                    isPlaying = false;
                    sequencing = false;
                }

                onIsPlayingChanged.Invoke(clip);
            }
        }

        public void StopAll()
        {
            foreach (var clip in clips)
            {
                StopClip(clip);
            }
            foreach (var clip in clips)
            {
                clip.Reset(false);
            }
        }

        public void ResetAll()
        {
            isPlaying = false;
            _playTime = 0f;
            foreach (var clip in clips)
            {
                clip.Reset(true);
            }
            playTime = playTime;
        }

        public void StopAndReset()
        {
            if (isPlaying) StopAll();
            ResetAll();
            foreach (var layer in index.ByLayer())
            {
                layer.FirstOrDefault()?.pose?.Apply();
            }
        }

        #endregion

        #region Animation state

        private void AdvanceClipsTime(float delta)
        {
            if (delta == 0) return;

            var layers = index.ByLayer();
            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layerClips = layers[layerIndex];
                float clipSpeed;
                if (layerClips.Count > 1)
                {
                    var weightedClipSpeedSum = 0f;
                    var totalBlendWeights = 0f;
                    clipSpeed = 0f;
                    for (var i = 0; i < layerClips.Count; i++)
                    {
                        var clip = layerClips[i];
                        if (!clip.playbackEnabled) continue;
                        var smoothBlendWeight = Mathf.SmoothStep(0f, 1, clip.playbackBlendWeight);
                        weightedClipSpeedSum += clip.speed * smoothBlendWeight;
                        totalBlendWeights += smoothBlendWeight;
                        clipSpeed = clip.speed;
                    }

                    clipSpeed = weightedClipSpeedSum == 0 ? clipSpeed : weightedClipSpeedSum / totalBlendWeights;
                }
                else
                {
                    clipSpeed = layerClips[0].speed;
                }

                for (var i = 0; i < layerClips.Count; i++)
                {
                    var clip = layerClips[i];
                    if (!clip.playbackEnabled) continue;

                    var clipDelta = delta * clipSpeed;
                    clip.clipTime += clipDelta;
                    if (clip.playbackBlendRate != 0)
                    {
                        clip.playbackBlendWeight += clip.playbackBlendRate * Mathf.Abs(clipDelta);
                        if (clip.playbackBlendWeight >= clip.weight)
                        {
                            clip.playbackBlendRate = 0f;
                            clip.playbackBlendWeight = clip.weight;
                        }
                        else if (clip.playbackBlendWeight <= 0f)
                        {
                            clip.playbackBlendRate = 0f;
                            clip.playbackBlendWeight = 0f;
                            clip.Leave();
                            clip.playbackEnabled = false;
                            onClipIsPlayingChanged.Invoke(clip);
                        }
                    }
                }
            }
        }

        private void Blend(AtomAnimationClip clip, float targetWeight, float blendDuration)
        {
            if (clip.applyPoseOnTransition && targetWeight > 0)
            {
                clip.pose?.Apply();
                clip.playbackEnabled = true;
                clip.playbackBlendWeight = 1f;
                clip.playbackBlendRate = 0f;
            }
            else if (blendDuration == 0)
            {
                clip.playbackBlendWeight = 1f;
                clip.playbackBlendRate = 0f;
                clip.playbackEnabled = targetWeight > 0;
                if (!clip.playbackEnabled)
                    clip.Leave();
            }
            else
            {
                if (!clip.playbackEnabled) clip.playbackBlendWeight = 0f;
                clip.playbackBlendRate = (targetWeight - clip.playbackBlendWeight) / blendDuration;
                if (clip.playbackEnabled) return;
                clip.playbackEnabled = true;
            }
            onClipIsPlayingChanged.Invoke(clip);
        }

        #endregion

        #region Transitions and sequencing

        private void TransitionAnimation(AtomAnimationClip from, AtomAnimationClip to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            from.SetNext(null, float.NaN);

            if (!to.playbackEnabled)
            {
                to.clipTime = 0f;
                if (to.loop)
                {
                    if (!from.loop)
                        to.clipTime = Mathf.Abs(to.animationLength - (from.animationLength - from.clipTime));
                    else if (to.preserveLoops)
                        to.clipTime = from.clipTime;
                }
            }

            // if(!from.loop && to.blendInDuration > from.animationLength - from.clipTime)
            //     SuperController.LogError($"Timeline: Transition from '{from.animationName}' to '{to.animationName}' will stop the former animation after it ends, because the blend-in time of the latter is too long for the sequenced time.");
            Blend(from, 0f, to.blendInDuration);
            from.playbackMainInLayer = false;
            Blend(to, 1f, to.blendInDuration);
            to.playbackMainInLayer = true;

            if (sequencing)
            {
                AssignNextAnimation(to);
            }

            if (from.animationPattern != null)
            {
                // Let the loop finish during the transition
                from.animationPattern.SetBoolParamValue("loopOnce", true);
            }

            if (to.animationPattern != null)
            {
                to.animationPattern.SetBoolParamValue("loopOnce", false);
                to.animationPattern.ResetAndPlay();
            }
        }

        private void AssignNextAnimation(AtomAnimationClip source)
        {
            if (source.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (source.nextAnimationTime <= 0)
                return;

            AtomAnimationClip next;

            string group;
            if (source.nextAnimationName == RandomizeAnimationName)
            {
                var candidates = index
                    .ByLayer(source.animationLayer)
                    .Where(c => c.animationName != source.animationName)
                    .ToList();
                if (candidates.Count == 0) return;
                var idx = Random.Range(0, candidates.Count);
                next = candidates[idx];
            }
            else if (TryGetRandomizedGroup(source.nextAnimationName, out group))
            {
                var candidates = index
                    .ByLayer(source.animationLayer)
                    .Where(c => c.animationName != source.animationName)
                    .Where(c => c.animationNameGroup == group)
                    .ToList();
                if (candidates.Count == 0) return;
                var idx = Random.Range(0, candidates.Count);
                next = candidates[idx];
            }
            else
            {
                next = GetClip(source.animationLayer, source.nextAnimationName);
            }

            if (next == null) return;

            var nextTime = source.nextAnimationTime;
            if (source.loop)
            {
                if (source.preserveLoops)
                {
                    nextTime = nextTime.RoundToNearest(source.animationLength) - (next.blendInDuration / 2f) + source.clipTime;
                }

                if (source.nextAnimationTimeRandomize > 0f)
                {
                    nextTime = Random.Range(nextTime, nextTime + (source.preserveLoops ? source.nextAnimationTimeRandomize.RoundToNearest(source.animationLength) : source.nextAnimationTimeRandomize));
                }
            }
            else
            {
                nextTime = Mathf.Min(nextTime, source.animationLength - next.blendInDuration);
                if (nextTime < float.Epsilon) nextTime = float.Epsilon;
            }

            source.SetNext(next.animationName, nextTime);
        }

        #endregion

        #region Sampling

        public bool RebuildPending()
        {
            return _animationRebuildRequestPending || _animationRebuildInProgress;
        }

        public void Sample()
        {
            if (isPlaying && !paused || !enabled) return;

            SampleFloatParams();
            SampleControllers(true);
        }

        private void SampleTriggers()
        {
            for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                if (!clip.playbackEnabled) continue;
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = clip.targetTriggers[triggerIndex];
                    target.Sample(clip.clipTime);
                }
            }
        }

        [MethodImpl(256)]
        private void SampleFloatParams()
        {
            foreach (var x in index.ByFloatParam())
            {
                if (!x.Value[0].animatableRef.EnsureAvailable()) continue;
                SampleFloatParam(x.Value[0].animatableRef.floatParam, x.Value);
            }
        }

        [MethodImpl(256)]
        private static void SampleFloatParam(JSONStorableFloat floatParam, List<JSONStorableFloatAnimationTarget> targets)
        {
            const float minimumDelta = 0.00000015f;
            var weightedSum = 0f;
            var totalBlendWeights = 0f;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var clip = target.clip;
                if(target.recording)
                {
                    target.SetKeyframe(clip.clipTime.Snap(), floatParam.val, false);
                    return;
                }
                if (!clip.playbackEnabled && !clip.temporarilyEnabled) continue;
                var weight = clip.temporarilyEnabled ? 1f : clip.scaledWeight;
                if (weight < float.Epsilon) continue;

                var value = target.value.Evaluate(clip.clipTime);
                var smoothBlendWeight = Mathf.SmoothStep(0f, 1f, clip.temporarilyEnabled ? 1f : clip.playbackBlendWeight);
                weightedSum += value * smoothBlendWeight;
                totalBlendWeights += smoothBlendWeight;
            }

            if (totalBlendWeights > minimumDelta)
            {
                var val = weightedSum / totalBlendWeights;
                if(Mathf.Abs(val - floatParam.val) > minimumDelta)
                {
                    floatParam.val = val;
                }
            }
        }

        [MethodImpl(256)]
        private void SampleControllers(bool force = false)
        {
            foreach (var x in index.ByController())
            {
                SampleController(x.Key.controller, x.Value, force);
            }
        }

        private Quaternion[] _rotations = new Quaternion[0];
        private float[] _rotationBlendWeights = new float[0];

        [MethodImpl(256)]
        private void SampleController(FreeControllerV3 controller, IList<FreeControllerV3AnimationTarget> targets, bool force)
        {
            if (ReferenceEquals(controller, null)) return;
            var control = controller.control;

            if (targets.Count > _rotations.Length)
            {
                _rotations = new Quaternion[targets.Count];
                _rotationBlendWeights = new float[targets.Count];
            }
            var rotationCount = 0;
            var totalRotationBlendWeights = 0f;
            var totalRotationControlWeights = 0f;

            var weightedPositionSum = Vector3.zero;
            var totalPositionBlendWeights = 0f;
            var totalPositionControlWeights = 0f;

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var clip = target.clip;
                if(target.recording)
                {
                    target.SetKeyframeToCurrentTransform(clip.clipTime.Snap(), false);
                    return;
                }
                if (!clip.playbackEnabled && !clip.temporarilyEnabled) continue;
                if (!target.playbackEnabled) continue;
                var weight = clip.temporarilyEnabled ? 1f : clip.scaledWeight * target.scaledWeight;
                if (weight < float.Epsilon) continue;

                if (!target.EnsureParentAvailable()) return;

                var smoothBlendWeight = Mathf.SmoothStep(0f, 1f, clip.temporarilyEnabled ? 1f : clip.playbackBlendWeight);

                if (target.controlRotation && controller.currentRotationState != FreeControllerV3.RotationState.Off)
                {
                    var rotLink = target.GetPositionParentRB();
                    var hasRotLink = !ReferenceEquals(rotLink, null);

                    var targetRotation = target.EvaluateRotation(clip.clipTime);
                    if (hasRotLink)
                    {
                        targetRotation = rotLink.rotation * targetRotation;
                        _rotations[rotationCount] = targetRotation;
                    }
                    else
                    {
                        _rotations[rotationCount] = control.transform.parent.rotation * targetRotation;
                    }

                    _rotationBlendWeights[rotationCount] = smoothBlendWeight;
                    totalRotationBlendWeights += smoothBlendWeight;
                    totalRotationControlWeights += weight * smoothBlendWeight;
                    rotationCount++;
                }

                if (target.controlPosition && controller.currentPositionState != FreeControllerV3.PositionState.Off)
                {
                    var posLink = target.GetPositionParentRB();
                    var hasPosLink = !ReferenceEquals(posLink, null);

                    var targetPosition = target.EvaluatePosition(clip.clipTime);
                    if (hasPosLink)
                    {
                        targetPosition = posLink.transform.TransformPoint(targetPosition);
                    }
                    else
                    {
                        targetPosition = control.transform.parent.TransformPoint(targetPosition);
                    }

                    weightedPositionSum += targetPosition * smoothBlendWeight;
                    totalPositionBlendWeights += smoothBlendWeight;
                    totalPositionControlWeights += weight * smoothBlendWeight;
                }
            }

            if (totalRotationBlendWeights > float.Epsilon && controller.currentRotationState != FreeControllerV3.RotationState.Off)
            {
                Quaternion targetRotation;
                if (rotationCount > 1)
                {
                    var cumulative = Vector4.zero;
                    for (var i = 0; i < rotationCount; i++)
                    {
                        QuaternionUtil.AverageQuaternion(ref cumulative, _rotations[i], _rotations[0], _rotationBlendWeights[i] / totalRotationBlendWeights);
                    }
                    targetRotation = QuaternionUtil.FromVector(cumulative);
                }
                else
                {
                    targetRotation = _rotations[0];
                }
                var rotation = Quaternion.Slerp(control.rotation, targetRotation, totalRotationControlWeights / totalRotationBlendWeights);
                control.rotation = rotation;
            }

            if (totalPositionBlendWeights > float.Epsilon && controller.currentPositionState != FreeControllerV3.PositionState.Off)
            {
                var targetPosition = weightedPositionSum / totalPositionBlendWeights;
                var position = Vector3.Lerp(control.position, targetPosition, totalPositionControlWeights / totalPositionBlendWeights);
                control.position = position;
            }

            if (force && controller.currentPositionState == FreeControllerV3.PositionState.Comply || controller.currentRotationState == FreeControllerV3.RotationState.Comply)
                controller.PauseComply();
        }

        #endregion

        #region Animation Rebuilding

        private IEnumerator RebuildDeferred()
        {
            yield return new WaitForEndOfFrame();
            while (isPlaying)
                yield return 0;
            RebuildAnimationNow();
        }

        public void RebuildAnimationNow()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException("A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            _animationRebuildRequestPending = false;
            _animationRebuildInProgress = true;
            try
            {
                RebuildAnimationNowImpl();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimation)}.{nameof(RebuildAnimationNow)}: " + exc);
            }
            finally
            {
                _animationRebuildInProgress = false;
            }

            onAnimationRebuilt.Invoke();
        }

        private void RebuildAnimationNowImpl()
        {
            var sw = Stopwatch.StartNew();
            foreach (var layer in index.ByLayer())
            {
                AtomAnimationClip last = null;
                foreach (var clip in layer)
                {
                    clip.Validate();
                    RebuildClip(clip, last);
                    last = clip;
                }
            }
            foreach (var clip in clips)
            {
                RebuildTransition(clip);
            }
            foreach (var clip in clips)
            {
                if (!clip.IsDirty()) continue;

                foreach (var target in clip.GetAllTargets())
                {
                    target.dirty = false;
                    target.onAnimationKeyframesRebuilt.Invoke();
                }

                clip.onAnimationKeyframesRebuilt.Invoke();
            }
            if (sw.ElapsedMilliseconds > 1000)
            {
                SuperController.LogError($"Timeline.{nameof(RebuildAnimationNowImpl)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }
        }

        private void RebuildTransition(AtomAnimationClip clip)
        {
            if (clip.autoTransitionPrevious)
            {
                var previous = clips.FirstOrDefault(c => c.nextAnimationName == clip.animationName);
                if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(previous, previous.animationLength, clip, 0f);
                }
            }
            if (clip.autoTransitionNext)
            {
                var next = GetClip(clip.animationLayer, clip.nextAnimationName);
                if (next != null && (next.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(next, 0f, clip, clip.animationLength);
                }
            }
        }

        private static void CopySourceFrameToClip(AtomAnimationClip source, float sourceTime, AtomAnimationClip clip, float clipTime)
        {
            foreach (var sourceTarget in source.targetControllers)
            {
                if (!sourceTarget.EnsureParentAvailable()) continue;
                var currentTarget = clip.targetControllers.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                if (!currentTarget.EnsureParentAvailable()) continue;
                // TODO: If there's a parent for position but not rotation or vice versa there will be problems
                // ReSharper disable Unity.NoNullCoalescing
                var sourceParent = sourceTarget.GetPositionParentRB()?.transform ?? sourceTarget.animatableRef.controller.control.parent;
                var currentParent = currentTarget.GetPositionParentRB()?.transform ?? currentTarget.animatableRef.controller.control.parent;
                // ReSharper restore Unity.NoNullCoalescing
                if (sourceParent == currentParent)
                {
                    currentTarget.SetCurveSnapshot(clipTime, sourceTarget.GetCurveSnapshot(sourceTime), false);
                    currentTarget.ChangeCurveByTime(clipTime, CurveTypeValues.Linear, false);
                }
                else
                {
                    var position = sourceParent.TransformPoint(sourceTarget.EvaluatePosition(sourceTime));
                    var rotation = Quaternion.Inverse(sourceParent.rotation) * sourceTarget.EvaluateRotation(sourceTime);
                    currentTarget.SetKeyframe(clipTime, currentParent.TransformPoint(position), Quaternion.Inverse(currentParent.rotation) * rotation, CurveTypeValues.Linear, false);
                }
            }
            foreach (var sourceTarget in source.targetFloatParams)
            {
                var currentTarget = clip.targetFloatParams.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                currentTarget.value.SetKeySnapshot(clipTime, sourceTarget.value.GetKeyframeAt(sourceTime));
                currentTarget.ChangeCurveByTime(clipTime, CurveTypeValues.Linear, false);
            }
        }

        private static void RebuildClip(AtomAnimationClip clip, AtomAnimationClip previous)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0f), false);

                target.ComputeCurves();

                if (clip.ensureQuaternionContinuity)
                {
                    var lastMatching = previous?.targetControllers.FirstOrDefault(t => t.TargetsSameAs(target));
                    var q = lastMatching?.GetRotationAtKeyframe(lastMatching.rotX.length - 1) ?? target.GetRotationAtKeyframe(target.rotX.length - 1);
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW,
                        q);
                }

                foreach (var curve in target.GetCurves())
                    curve.ComputeCurves();
            }

            foreach (var target in clip.targetFloatParams)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0), false);

                target.value.ComputeCurves();
            }

            foreach (var target in clip.targetTriggers)
            {
                if (!target.dirty) continue;

                target.RebuildKeyframes(clip.animationLength);
            }
        }

        #endregion

        #region Event Handlers

        private void OnAnimationSettingsChanged(string param)
        {
            index.Rebuild();
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer))
                onClipsListChanged.Invoke();
        }

        private void OnAnimationKeyframesDirty()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException("A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            if (_animationRebuildRequestPending) return;
            _animationRebuildRequestPending = true;
            StartCoroutine(RebuildDeferred());
        }

        private void OnTargetsListChanged()
        {
            index.Rebuild();
            OnAnimationKeyframesDirty();
        }

        #endregion

        #region Unity Lifecycle

        public void Update()
        {
            if (!allowAnimationProcessing || paused) return;

            SampleFloatParams();
            SampleTriggers();
            ProcessAnimationSequence(GetDeltaTime() * speed);
        }

        [MethodImpl(256)]
        private float GetDeltaTime()
        {
            switch (timeMode)
            {
                case TimeModes.UnityTime:
                    return Time.deltaTime;
                case TimeModes.RealTime:
                    return Time.unscaledDeltaTime * Time.timeScale;
                default:
                    return Time.deltaTime;
            }
        }

        private void ProcessAnimationSequence(float deltaTime)
        {
            var clipsPlaying = 0;
            var clipsQueued = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];

                if (clip.playbackEnabled)
                    clipsPlaying++;

                if (clip.playbackScheduledNextAnimationName != null)
                    clipsQueued++;

                if (!clip.loop && clip.playbackEnabled && clip.clipTime == clip.animationLength && !clip.infinite)
                {
                    clip.playbackEnabled = false;
                    onClipIsPlayingChanged.Invoke(clip);
                }

                if (clip.playbackMainInLayer && clip.playbackScheduledNextAnimationName != null)
                {
                    clip.playbackScheduledNextTimeLeft = Mathf.Max(clip.playbackScheduledNextTimeLeft - deltaTime * clip.speed, 0f);
                    if (clip.playbackScheduledNextTimeLeft == 0)
                    {
                        var nextAnimationName = clip.playbackScheduledNextAnimationName;
                        clip.SetNext(null, float.NaN);
                        var nextClip = GetClip(clip.animationLayer, nextAnimationName);
                        if (nextClip == null)
                        {
                            SuperController.LogError(
                                $"Timeline: Cannot sequence from animation '{clip.animationName}' to '{nextAnimationName}' because the target animation does not exist.");
                            continue;
                        }

                        TransitionAnimation(clip, nextClip);
                    }
                }
            }

            if (clipsPlaying == 0 && clipsQueued == 0)
            {
                StopAll();
            }
        }

        public void FixedUpdate()
        {
            if (!allowAnimationProcessing || paused) return;

            var delta = GetDeltaTime() * _speed;
            _playTime += delta;
            AdvanceClipsTime(delta);
            SampleControllers();
        }

        public void OnDestroy()
        {
            onAnimationSettingsChanged.RemoveAllListeners();
            onIsPlayingChanged.RemoveAllListeners();
            onSpeedChanged.RemoveAllListeners();
            onClipsListChanged.RemoveAllListeners();
            onAnimationRebuilt.RemoveAllListeners();
            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }

        #endregion
    }
}
