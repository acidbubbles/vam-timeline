using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AddAnimationOperations
    {
        public static class Positions
        {
            public const string PositionFirst = "First";
            public const string PositionPrevious = "Previous";
            public const string PositionNext = "Next";
            public const string PositionLast = "Last";
            public const string NotSpecified = "[N/A]";
            public static readonly List<string> all = new List<string> { PositionFirst, PositionPrevious, PositionNext, PositionLast };
        }

        public class CreatedAnimation
        {
            public AtomAnimationClip source;
            public AtomAnimationClip created;
        }

        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public AddAnimationOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public List<CreatedAnimation> AddAnimation(string animationName, string position, bool copySettings, bool copyKeyframes, bool allLayers)
        {
            if (!allLayers)
                return new List<CreatedAnimation> { AddAnimation(_clip, animationName, _clip.animationLayer, _clip.animationSegment, position, copySettings, copyKeyframes) };

            var result = GetSameNameAnimationsInSegment()
                .Where(c => _animation.index.ByLayerQualified(c.animationLayerQualifiedId).All(c2 => c2.animationName != animationName))
                .Select(c => AddAnimation(c, animationName, c.animationLayer, c.animationSegment, position, copySettings, copyKeyframes))
                .ToList();

            _animation.index.Rebuild();

            return result;
        }

        public CreatedAnimation AddAnimation(AtomAnimationClip source, string animationName, string animationLayer, string animationSegment, string position, bool copySettings, bool copyKeyframes)
        {
            var clip = _animation.CreateClip(animationName, animationLayer, animationSegment, GetPosition(source, position));

            if (copySettings)
            {
                source.CopySettingsTo(clip);
            }

            if (copyKeyframes)
            {
                foreach (var origTarget in source.targetControllers)
                {
                    var newTarget = CopyTarget(clip, origTarget);
                    for (var i = 0; i < origTarget.curves.Count; i++)
                        newTarget.curves[i].keys = new List<BezierKeyframe>(origTarget.curves[i].keys);
                    newTarget.dirty = true;
                }

                foreach (var origTarget in source.targetFloatParams)
                {
                    if (!origTarget.animatableRef.EnsureAvailable(false, forceCheck: true)) continue;
                    var newTarget = clip.AddFloatParam(new JSONStorableFloatAnimationTarget(origTarget));
                    newTarget.group = origTarget.group;
                    newTarget.value.keys = new List<BezierKeyframe>(origTarget.value.keys);
                    newTarget.dirty = true;
                }

                foreach (var origTarget in source.targetTriggers)
                {
                    var newTarget = clip.AddTriggers(new TriggersTrackAnimationTarget(origTarget.animatableRef, _animation.logger));
                    newTarget.group = origTarget.group;
                    foreach (var origTrigger in origTarget.triggersMap)
                    {
                        var trigger = newTarget.CreateKeyframe(origTrigger.Key);
                        trigger.RestoreFromJSON(origTrigger.Value.GetJSON());
                    }
                    newTarget.dirty = true;
                }

                clip.pose = source.pose?.Clone();
                clip.applyPoseOnTransition = source.applyPoseOnTransition;
            }
            else
            {
                foreach (var origTarget in source.targetControllers)
                {
                    var newTarget = CopyTarget(clip, origTarget);
                    newTarget.SetKeyframeToCurrent(0f);
                    newTarget.SetKeyframeToCurrent(clip.animationLength);
                }

                foreach (var origTarget in source.targetFloatParams)
                {
                    if (!origTarget.animatableRef.EnsureAvailable(false, forceCheck: true)) continue;
                    var newTarget = clip.AddFloatParam(origTarget.animatableRef);
                    newTarget.group = origTarget.group;
                    newTarget.SetKeyframeToCurrent(0f);
                    newTarget.SetKeyframeToCurrent(clip.animationLength);
                }

                foreach (var origTarget in source.targetTriggers)
                {
                    var newTarget = new TriggersTrackAnimationTarget(origTarget.animatableRef, _animation.logger);
                    newTarget.group = origTarget.group;
                    newTarget.AddEdgeFramesIfMissing(clip.animationLength);
                    clip.AddTriggers(newTarget);
                }
            }

            return new CreatedAnimation
            {
                source = source,
                created = clip
            };
        }

        public List<CreatedAnimation> AddTransitionAnimation(bool allLayers)
        {
            if (!allLayers)
                return new List<CreatedAnimation> { AddTransitionAnimation(_clip) };

            var result = GetSameNameAnimationsInSegment()
                .Select(AddTransitionAnimation)
                .ToList();

            _animation.index.Rebuild();

            return result;
        }

        private CreatedAnimation AddTransitionAnimation(AtomAnimationClip source)
        {
            var next = _animation.GetClip(source.animationSegment, source.animationLayer, source.nextAnimationName);
            if (next == null)
            {
                SuperController.LogError("There is no animation to transition to");
                return null;
            }

            var clip = _animation.CreateClip($"{source.animationName} > {next.animationName}", source.animationLayer, source.animationSegment, _animation.clips.IndexOf(source) + 1);
            clip.loop = false;
            clip.loopPreserveLastFrame = false;
            clip.autoTransitionPrevious = _animation.index.segmentsById[source.animationSegmentId].layersMapById[source.animationLayerId].Any(c => c.nextAnimationNameId == source.animationNameId);
            clip.autoTransitionNext = source.nextAnimationName != null;
            clip.nextAnimationName = source.nextAnimationName;
            clip.nextAnimationTime = clip.animationLength - clip.blendInDuration;

            foreach (var origTarget in source.targetControllers)
            {
                var newTarget = CopyTarget(clip, origTarget);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(source.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetControllers.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }

            foreach (var origTarget in source.targetFloatParams)
            {
                if (!origTarget.animatableRef.EnsureAvailable(false, forceCheck: true)) continue;
                var newTarget = clip.AddFloatParam(origTarget.animatableRef);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(source.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetFloatParams.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }

            foreach (var origTarget in source.targetTriggers)
            {
                var newTarget = new TriggersTrackAnimationTarget(origTarget.animatableRef, _animation.logger);
                newTarget.AddEdgeFramesIfMissing(clip.animationLength);
                clip.AddTriggers(newTarget);
            }

            source.nextAnimationName = clip.animationName;
            return new CreatedAnimation { source = source, created = clip };
        }

        private static FreeControllerV3AnimationTarget CopyTarget(AtomAnimationClip clip, FreeControllerV3AnimationTarget origTarget)
        {
            var newTarget = clip.AddController(origTarget.animatableRef, origTarget.targetsPosition, origTarget.targetsRotation);
            newTarget.SetParent(origTarget.parentAtomId, origTarget.parentRigidbodyId);
            newTarget.weight = origTarget.weight;
            newTarget.targetsPosition = origTarget.targetsPosition;
            newTarget.targetsRotation = origTarget.targetsRotation;
            newTarget.controlPosition = origTarget.controlPosition;
            newTarget.controlRotation = origTarget.controlRotation;
            newTarget.group = origTarget.group;
            return newTarget;
        }

        public void DeleteAnimation(AtomAnimationClip clip)
        {
            try
            {
                if (clip == null) return;
                if (_animation.clips.Count == 1)
                {
                    SuperController.LogError("Timeline: Cannot delete the only animation.");
                    return;
                }
                _animation.RemoveClip(clip);
                foreach (var matchingClip in _animation.clips)
                {
                    if (matchingClip.nextAnimationName == clip.animationName)
                    {
                        matchingClip.nextAnimationName = null;
                        matchingClip.nextAnimationTime = 0;
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(DeleteAnimation)}: {exc}");
            }
        }

        private IEnumerable<AtomAnimationClip> GetSameNameAnimationsInSegment()
        {
            return _animation.index.segmentsById[_clip.animationSegmentId].layers
                .Select(l => l.FirstOrDefault(c => c.animationName == _clip.animationName))
                .Where(l => l != null);
        }

        private int GetPosition(AtomAnimationClip clip, string position)
        {
            switch (position)
            {
                case Positions.PositionFirst:
                    return _animation.clips.FindIndex(c => c.animationLayerQualified == clip.animationLayerQualified);
                case Positions.PositionPrevious:
                    return _animation.clips.IndexOf(clip);
                case Positions.PositionNext:
                    return _animation.clips.IndexOf(clip) + 1;
                case Positions.PositionLast:
                    return _animation.clips.FindLastIndex(c => c.animationLayerQualified == clip.animationLayerQualified) + 1;
                case Positions.NotSpecified:
                    return _animation.clips.Count;
                default:
                    throw new NotSupportedException($"Unknown position '{position}'");
            }
        }
    }
}
