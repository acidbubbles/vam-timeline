using System.Linq;

namespace VamTimeline
{
    public class AddAnimationOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public AddAnimationOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip AddAnimationAsCopy()
        {
            var clip = AddAnimationWithSameSettings();
            foreach (var origTarget in _clip.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetParent(origTarget.parentAtomId, origTarget.parentRigidbodyId);
                newTarget.weight = origTarget.weight;
                for (var i = 0; i < origTarget.curves.Count; i++)
                {
                    newTarget.curves[i].keys = origTarget.curves[i].keys.Select(k => k.Clone()).ToList();
                }
                newTarget.dirty = true;
            }
            foreach (var origTarget in _clip.targetFloatParams)
            {
                var newTarget = clip.Add(new FloatParamAnimationTarget(origTarget));
                newTarget.value.keys = origTarget.value.keys.Select(k => k.Clone()).ToList();
                newTarget.dirty = true;
            }
            foreach (var origTarget in _clip.targetTriggers)
            {
                var newTarget = clip.Add(new TriggersAnimationTarget { name = origTarget.name });
                foreach (var origTrigger in origTarget.triggersMap)
                {
                    var trigger = new AtomAnimationTrigger();
                    trigger.RestoreFromJSON(origTrigger.Value.GetJSON());
                    newTarget.SetKeyframe(origTrigger.Key, trigger);
                }
                newTarget.dirty = true;
            }
            return clip;
        }

        public AtomAnimationClip AddAnimationWithSameSettings()
        {
            var clip = _animation.CreateClip(_clip.animationLayer);
            clip.loop = _clip.loop;
            clip.animationLength = _clip.animationLength;
            clip.animationLayer = _clip.animationLayer;
            clip.nextAnimationName = _clip.nextAnimationName;
            clip.nextAnimationTime = _clip.nextAnimationTime;
            clip.ensureQuaternionContinuity = _clip.ensureQuaternionContinuity;
            clip.blendInDuration = _clip.blendInDuration;
            return clip;
        }

        public AtomAnimationClip AddAnimationFromCurrentFrame()
        {
            var clip = _animation.CreateClip(_clip.animationLayer);
            foreach (var origTarget in _clip.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetParent(origTarget.parentAtomId, origTarget.parentRigidbodyId);
                newTarget.weight = origTarget.weight;
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.animationLength);
            }
            foreach (var origTarget in _clip.targetFloatParams)
            {
                if (!origTarget.EnsureAvailable(false)) continue;
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetKeyframe(0f, origTarget.floatParam.val);
                newTarget.SetKeyframe(clip.animationLength, origTarget.floatParam.val);
            }
            return clip;
        }

        public AtomAnimationClip AddTransitionAnimation()
        {
            var next = _animation.GetClip(_clip.animationLayer, _clip.nextAnimationName);
            if (next == null)
            {
                SuperController.LogError("There is no animation to transition to");
                return null;
            }

            var clip = _animation.CreateClip(_clip.animationLayer);
            clip.animationName = $"{_clip.animationName} > {next.animationName}";
            clip.loop = false;
            clip.autoTransitionPrevious = _animation.clips.Any(c => c.animationLayer == _clip.animationLayer && c.nextAnimationName == _clip.animationName);
            clip.autoTransitionNext = _clip.nextAnimationName != null;
            clip.nextAnimationName = _clip.nextAnimationName;
            clip.blendInDuration = AtomAnimationClip.DefaultBlendDuration;
            clip.nextAnimationTime = clip.animationLength - clip.blendInDuration;
            clip.ensureQuaternionContinuity = _clip.ensureQuaternionContinuity;

            foreach (var origTarget in _clip.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(_clip.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetControllers.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }
            foreach (var origTarget in _clip.targetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(_clip.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetFloatParams.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }

            _animation.clips.Remove(clip);
            _animation.clips.Insert(_animation.clips.IndexOf(_clip) + 1, clip);

            _clip.nextAnimationName = clip.animationName;
            return clip;
        }
    }
}
