using System;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ResizeAnimationOperation
    {
        private readonly AtomAnimationClip _clip;

        public ResizeAnimationOperation(AtomAnimationClip clip)
        {
            _clip = clip;
        }

        private class SnapshotAt
        {
            public float time;
            public ISnapshot snapshot;
        }

        #region Stretch

        public void Stretch(float newAnimationLength)
        {
            var keyframeOps = new KeyframesOperation(_clip);
            var originalAnimationLength = _clip.animationLength;
            _clip.animationLength = newAnimationLength;
            var ratio = newAnimationLength / originalAnimationLength;
            foreach (var target in _clip.GetAllTargets())
            {
                var snapshots = target
                    .GetAllKeyframesTime()
                    .Select(t => new SnapshotAt { time = t, snapshot = target.GetSnapshot(t) })
                    .ToList();
                keyframeOps.RemoveAll(target, true);

                foreach (var s in snapshots)
                {
                    target.SetSnapshot((s.time * ratio).Snap(), s.snapshot);
                }
            }
        }

        #endregion

        public void CropOrExtendEnd(float newAnimationLength)
        {
            var originalAnimationLength = _clip.animationLength;
            _clip.animationLength = newAnimationLength;

            if (newAnimationLength < originalAnimationLength)
            {
                CropEnd(newAnimationLength);
            }
            else if (newAnimationLength > originalAnimationLength)
            {
                ExtendEnd(newAnimationLength);
            }
        }

        private void CropEnd(float newAnimationLength)
        {
            foreach (var target in _clip.GetAllCurveTargets())
            {
                foreach (var curve in target.GetCurves())
                {
                    var key = curve.AddKey(newAnimationLength, curve.Evaluate(newAnimationLength));
                }
                target.EnsureKeyframeSettings(newAnimationLength, target.settings.Last().Value.curveType);
                target.dirty = true;
                var keyframesToDelete = target.GetAllKeyframesTime().Where(t => t > newAnimationLength);
                foreach (var t in keyframesToDelete)
                    target.DeleteFrame(t);
            }
            foreach (var target in _clip.targetTriggers)
            {
                while (target.triggersMap.Count > 0)
                {
                    var lastTrigger = target.triggersMap.Keys.Last();
                    if (lastTrigger * 1000f > newAnimationLength)
                    {
                        target.triggersMap.Remove(lastTrigger);
                        continue;
                    }
                    break;
                }
                target.AddEdgeFramesIfMissing(newAnimationLength);
            }
        }

        private void ExtendEnd(float newAnimationLength)
        {
            foreach (var target in _clip.GetAllTargets())
            {
                target.AddEdgeFramesIfMissing(newAnimationLength);
            }
        }

        // TODO: Replace by simpler implementation, see before

        public void CropOrExtendBegin(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.GetAllCurveTargets())
            {
                foreach (var curve in target.GetCurves())
                    CropOrExtendBeginCurve(curve, newAnimationLength);
                MatchKeyframeSettingsPivotEnd(target, newAnimationLength);
                target.AddEdgeFramesIfMissing(newAnimationLength);
            }
            CropBeginTriggersAndOffset(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        private static void CropOrExtendBeginCurve(AnimationCurve curve, float newLength)
        {
            if (curve.length < 2) return;
            var currentLength = curve[curve.length - 1].time;
            var lengthDiff = newLength - currentLength;

            var keys = curve.keys.ToList();
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                var keyframe = keys[i];
                float oldTime = keyframe.time;
                float newTime = oldTime + lengthDiff;

                if (newTime < 0)
                {
                    keys.RemoveAt(i);
                    continue;
                }

                keyframe.time = newTime.Snap();
                keys[i] = keyframe;
            }

            if (keys.Count == 0)
            {
                SuperController.LogError("VamTimeline: CropOrExtendLengthBegin resulted in an empty curve.");
                return;
            }

            if (lengthDiff > 0)
            {
                var first = curve[0];
                first.time = 0f;
                keys[0] = first;
            }
            else if (keys[0].time != 0)
            {
                keys.Insert(0, new Keyframe(0f, curve.Evaluate(-lengthDiff)));
            }

            var last = keys[keys.Count - 1];
            last.time = newLength;
            keys[keys.Count - 1] = last;

            curve.keys = keys.ToArray();
        }

        // TODO: Untested, probably not working. If this works, every resize types should use this instead.
        public void CropOrExtendAtTime(float newAnimationLength, float time)
        {
            var previousKeyframe = _clip.GetAllTargets().SelectMany(t => t.GetAllKeyframesTime()).Where(t => t <= time + 0.0011f).Max();
            var nextKeyframe = _clip.GetAllTargets().SelectMany(t => t.GetAllKeyframesTime()).Where(t => t > time + 0.0001f).Min();

            var keyframeAllowedDiff = (nextKeyframe - time - 0.001f).Snap();

            if ((_clip.animationLength - newAnimationLength) > keyframeAllowedDiff)
            {
                newAnimationLength = _clip.animationLength - keyframeAllowedDiff;
            }

            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;

            foreach (var target in _clip.GetAllCurveTargets())
            {
                foreach (var curve in target.GetCurves())
                    CropOrExtendAtTimeCurve(curve, newAnimationLength, time);
                MatchKeyframeSettingsPivotBegin(target, newAnimationLength);
                target.AddEdgeFramesIfMissing(newAnimationLength);
            }
            CropEndTriggers(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        private static void CropOrExtendAtTimeCurve(AnimationCurve curve, float newLength, float time)
        {
            if (curve.length < 2) return;
            var lengthDiff = newLength - curve[curve.length - 1].time;

            var keys = curve.keys;
            for (var i = 0; i < keys.Length - 1; i++)
            {
                var keyframe = keys[i];
                if (keyframe.time <= time - float.Epsilon) continue;
                keyframe.time = (keyframe.time + lengthDiff).Snap();
                keys[i] = keyframe;
            }

            var last = keys[keys.Length - 1];
            last.time = newLength;
            keys[keys.Length - 1] = last;

            curve.keys = keys;
        }

        public void Loop(float newAnimationLength, float lengthWhenLengthModeChanged)
        {
            newAnimationLength = newAnimationLength.Snap(lengthWhenLengthModeChanged);
            var loops = (int)Math.Round(newAnimationLength / lengthWhenLengthModeChanged);
            if (loops <= 1 || newAnimationLength <= lengthWhenLengthModeChanged)
            {
                return;
            }
            var frames = _clip
                .targetControllers.SelectMany(t => t.GetLeadCurve().keys.Select(k => k.time))
                .Concat(_clip.targetFloatParams.SelectMany(t => t.value.keys.Select(k => k.time)))
                .Select(t => t.Snap())
                .Where(t => t < lengthWhenLengthModeChanged)
                .Distinct()
                .ToList();

            var snapshots = frames.Select(f => _clip.Copy(f, true)).ToList();
            foreach (var c in snapshots[0].controllers)
            {
                c.snapshot.curveType = CurveTypeValues.Smooth;
            }

            CropOrExtendEnd(newAnimationLength);

            for (var repeat = 0; repeat < loops; repeat++)
            {
                for (var i = 0; i < frames.Count; i++)
                {
                    var pasteTime = frames[i] + (lengthWhenLengthModeChanged * repeat);
                    if (pasteTime >= newAnimationLength) continue;
                    _clip.Paste(pasteTime, snapshots[i]);
                }
            }
        }

        private static void MatchKeyframeSettingsPivotBegin(ICurveAnimationTarget target, float newAnimationLength)
        {
            var settings = target.settings.Values.ToList();
            target.settings.Clear();
            var leadCurve = target.GetLeadCurve();
            for (var i = 0; i < leadCurve.length; i++)
            {
                if (i < settings.Count) target.settings.Add(leadCurve[i].time.ToMilliseconds(), settings[i]);
                else target.settings.Add(leadCurve[i].time.ToMilliseconds(), new KeyframeSettings { curveType = CurveTypeValues.CopyPrevious });
            }
        }

        private void CropEndTriggers(float newAnimationLength)
        {
            var lengthMs = newAnimationLength.ToMilliseconds();
            foreach (var target in _clip.targetTriggers)
            {
                var keys = target.triggersMap.Keys.ToList();
                target.StartBulkUpdates();
                target.dirty = true;
                try
                {
                    foreach (var key in keys)
                    {
                        if (key > lengthMs)
                            target.DeleteFrame(key);
                    }
                    target.AddEdgeFramesIfMissing(newAnimationLength);
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }

        private void MatchKeyframeSettingsPivotEnd(ICurveAnimationTarget target, float newAnimationLength)
        {
            var settings = target.settings.Values.ToList();
            target.settings.Clear();
            var leadCurve = target.GetLeadCurve();
            for (var i = 0; i < leadCurve.length; i++)
            {
                if (i >= settings.Count) break;
                int ms = leadCurve[leadCurve.length - i - 1].time.ToMilliseconds();
                target.settings.Add(ms, settings[settings.Count - i - 1]);
            }
            if (!target.settings.ContainsKey(0))
                target.settings.Add(0, new KeyframeSettings { curveType = CurveTypeValues.Smooth });
        }

        private void CropBeginTriggersAndOffset(float newAnimationLength)
        {
            var lengthDiff = (newAnimationLength - _clip.animationLength).ToMilliseconds();
            foreach (var target in _clip.targetTriggers)
            {
                var kvps = target.triggersMap.ToList();
                target.StartBulkUpdates();
                target.triggersMap.Clear();
                target.dirty = true;
                try
                {
                    foreach (var kvp in kvps)
                    {
                        target.SetKeyframe(Mathf.Clamp(kvp.Key + lengthDiff, 0, newAnimationLength.ToMilliseconds()), kvp.Value);
                    }
                    target.AddEdgeFramesIfMissing(newAnimationLength);
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }
    }
}
