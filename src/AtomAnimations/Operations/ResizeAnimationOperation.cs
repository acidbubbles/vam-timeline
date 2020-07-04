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

        #region CropOrExtendEnd

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

        #endregion

        #region CropOrExtendAt

        public void CropOrExtendAt(float newAnimationLength, float time)
        {
            var originalAnimationLength = _clip.animationLength;
            _clip.animationLength = newAnimationLength;
            var delta = newAnimationLength - originalAnimationLength;

            if (newAnimationLength < originalAnimationLength)
            {
                CropAt(delta, time);
            }
            else if (newAnimationLength > originalAnimationLength)
            {
                ExtendAt(delta, time);
            }
        }

        private void CropAt(float delta, float time)
        {
            var keyframeOps = new KeyframesOperation(_clip);
            foreach (var target in _clip.GetAllTargets())
            {
                // TODO: Create new keyframe if missing from evaluate curve
                var snapshots = target
                    .GetAllKeyframesTime()
                    .Where(t => t < time || t >= time - delta)
                    .Select(t =>
                    {
                        var newTime = t < time ? t : t + delta;
                        return new SnapshotAt { time = newTime, snapshot = target.GetSnapshot(t) };
                    })
                    .ToList();
                keyframeOps.RemoveAll(target, true);

                foreach (var s in snapshots)
                {
                    target.SetSnapshot(s.time, s.snapshot);
                }

                target.AddEdgeFramesIfMissing(_clip.animationLength);
            }
        }

        private void ExtendAt(float delta, float time)
        {
            var keyframeOps = new KeyframesOperation(_clip);
            var originalAnimationLength = _clip.animationLength;
            foreach (var target in _clip.GetAllTargets())
            {
                // TODO: Create new keyframe if missing from evaluate curve
                var snapshots = target
                    .GetAllKeyframesTime()
                    .Select(t => new SnapshotAt { time = t < time ? t : t + delta, snapshot = target.GetSnapshot(t) })
                    .ToList();
                keyframeOps.RemoveAll(target, true);

                foreach (var s in snapshots)
                {
                    target.SetSnapshot(s.time, s.snapshot);
                }

                target.AddEdgeFramesIfMissing(_clip.animationLength);
            }
        }

        #endregion

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
    }
}
