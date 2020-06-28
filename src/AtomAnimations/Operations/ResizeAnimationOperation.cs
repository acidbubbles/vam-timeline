using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ResizeAnimationOperation
    {
        private readonly AtomAnimationClip _clip;

        public ResizeAnimationOperation(AtomAnimationClip clip)
        {
            _clip = clip;
        }

        public void StretchLength(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.StretchLength(newAnimationLength);
            }
            MatchKeyframeSettingsPivotBegin();
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
            _clip.animationLength = newAnimationLength;
        }

        public void CropOrExtendLengthEnd(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthEnd(newAnimationLength);
            }
            MatchKeyframeSettingsPivotBegin();
            CropEndTriggers(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        public void CropOrExtendLengthBegin(float newAnimationLength)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthBegin(newAnimationLength);
            }
            MatchKeyframeSettingsPivotEnd();
            CropBeginTriggersAndOffset(newAnimationLength);
            _clip.animationLength = newAnimationLength;
        }

        public void CropOrExtendLengthAtTime(float newAnimationLength, float time)
        {
            if (_clip.animationLength.IsSameFrame(newAnimationLength))
                return;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthAtTime(newAnimationLength, time);
            }
            MatchKeyframeSettingsPivotBegin();
            CropEndTriggers(newAnimationLength);
            _clip.animationLength = newAnimationLength;
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
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }
    }
}
