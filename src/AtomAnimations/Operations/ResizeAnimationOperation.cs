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

        public void StretchLength(float value)
        {
            if (value == _clip.animationLength)
                return;
            _clip.animationLength = value;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.StretchLength(value);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        public void CropOrExtendLengthEnd(float animationLength)
        {
            if (_clip.animationLength.IsSameFrame(animationLength))
                return;
            _clip.animationLength = animationLength;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthEnd(animationLength);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        public void CropOrExtendLengthBegin(float animationLength)
        {
            if (_clip.animationLength.IsSameFrame(animationLength))
                return;
            _clip.animationLength = animationLength;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthBegin(animationLength);
            }
            UpdateKeyframeSettingsFromEnd();
        }

        public void CropOrExtendLengthAtTime(float animationLength, float time)
        {
            if (_clip.animationLength.IsSameFrame(animationLength))
                return;
            _clip.animationLength = animationLength;
            foreach (var target in _clip.allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthAtTime(animationLength, time);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        private void UpdateKeyframeSettingsFromBegin()
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

        private void UpdateKeyframeSettingsFromEnd()
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
    }
}
