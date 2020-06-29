using System;
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

        public void Stretch(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    StretchCurve(curve, newAnimationLength);
            }
            MatchKeyframeSettingsPivotBegin();
            StretchTriggers(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        public static void StretchCurve(AnimationCurve curve, float newLength)
        {
            if (curve.length < 2) return;
            int lastKey = curve.length - 1;
            var curveLength = curve[lastKey].time;
            if (newLength == curveLength) return;
            var ratio = newLength / curveLength;
            if (Mathf.Abs(ratio) < float.Epsilon) return;
            int from;
            int to;
            int direction;
            if (ratio < 1f)
            {
                from = 0;
                to = lastKey + 1;
                direction = 1;
            }
            else
            {
                from = lastKey;
                to = -1;
                direction = -1;
            }
            for (var key = from; key != to; key += direction)
            {
                var keyframe = curve[key];
                var time = keyframe.time *= ratio;
                keyframe.time = time.Snap();

                curve.MoveKey(key, keyframe);
            }

            // Sanity check
            if (curve[lastKey].time > newLength + 0.001f - float.Epsilon)
            {
                SuperController.LogError($"VamTimeline: Problem while resizing animation. Expected length {newLength} but was {curve[lastKey].time}");
            }

            // Ensure exact match
            var lastframe = curve[lastKey];
            lastframe.time = newLength;
            curve.MoveKey(lastKey, lastframe);
        }

        private void StretchTriggers(float newAnimationLength)
        {
            var ratio = newAnimationLength / _clip.animationLength;
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
                        target.SetKeyframe(Mathf.Clamp(Mathf.RoundToInt(kvp.Key * ratio), 0, newAnimationLength.ToMilliseconds()), kvp.Value);
                    }
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }

        public void CropOrExtendEnd(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                {
                    CropEndCurve(curve, newAnimationLength);
                    curve.AddEdgeFramesIfMissing(newAnimationLength);
                }
            }
            MatchKeyframeSettingsPivotBegin();
            CropEndTriggers(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        public static void CropEndCurve(AnimationCurve curve, float newLength)
        {
            if (curve.length < 2) return;
            float currentLength = curve[curve.length - 1].time;
            if (newLength >= currentLength) return;

            var key = curve.AddKey(newLength, curve.Evaluate(newLength));
            if (key == -1) key = curve.KeyframeBinarySearch(newLength);
            if (key == -1) throw new InvalidOperationException($"Could not add nor find keyframe at time {newLength}");
            while (curve.length - 1 > key)
            {
                curve.RemoveKey(curve.length - 1);
            }
        }

        public void CropOrExtendBegin(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    CropOrExtendBeginCurve(curve, newAnimationLength);
            }
            MatchKeyframeSettingsPivotEnd();
            CropBeginTriggersAndOffset(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        public static void CropOrExtendBeginCurve(AnimationCurve curve, float newLength)
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
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    CropOrExtendAtTimeCurve(curve, newAnimationLength, time);
            }
            MatchKeyframeSettingsPivotBegin();
            CropEndTriggers(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        public static void CropOrExtendAtTimeCurve(AnimationCurve curve, float newLength, float time)
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

        private void MatchKeyframeSettingsPivotBegin()
        {
            foreach (var target in _clip.targetControllers)
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

        private void MatchKeyframeSettingsPivotEnd()
        {
            foreach (var target in _clip.targetControllers)
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
