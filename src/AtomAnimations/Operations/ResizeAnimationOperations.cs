using System.Linq;

namespace VamTimeline
{
    public class ResizeAnimationOperations
    {
        private class SnapshotAt
        {
            public float time;
            public ISnapshot snapshot;
        }

        #region Stretch

        public void Stretch(AtomAnimationClip clip, float newAnimationLength)
        {
            var keyframeOps = new KeyframesOperations(clip);
            var originalAnimationLength = clip.animationLength;
            clip.animationLength = newAnimationLength;
            var ratio = newAnimationLength / originalAnimationLength;
            foreach (var target in clip.GetAllTargets())
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

        public void CropOrExtendEnd(AtomAnimationClip clip, float newAnimationLength)
        {
            var originalAnimationLength = clip.animationLength;
            clip.animationLength = newAnimationLength;

            if (newAnimationLength < originalAnimationLength)
            {
                CropEnd(clip, newAnimationLength);
            }
            else if (newAnimationLength > originalAnimationLength)
            {
                ExtendEnd(clip, newAnimationLength);
            }
        }

        private void CropEnd(AtomAnimationClip clip, float newAnimationLength)
        {
            foreach (var target in clip.GetAllCurveTargets())
            {
                foreach (var curve in target.GetCurves())
                {
                    var lastKeyframe = curve.GetLastFrame();
                    var lastCurveType = lastKeyframe.HasValue() ? lastKeyframe.curveType : CurveTypeValues.SmoothLocal;
                    curve.AddKey(newAnimationLength, curve.Evaluate(newAnimationLength), lastCurveType);
                }
                target.dirty = true;
                var keyframesToDelete = target.GetAllKeyframesTime().Where(t => t > newAnimationLength);
                foreach (var t in keyframesToDelete)
                    target.DeleteFrame(t);
            }
            foreach (var target in clip.targetTriggers)
            {
                while (target.triggersMap.Count > 0)
                {
                    var lastTrigger = target.triggersMap.Keys.Last();
                    if (lastTrigger / 1000f > newAnimationLength)
                    {
                        target.DeleteFrameByMs(lastTrigger);
                        continue;
                    }
                    break;
                }
                target.AddEdgeFramesIfMissing(newAnimationLength);
            }
        }

        private void ExtendEnd(AtomAnimationClip clip, float newAnimationLength)
        {
            foreach (var target in clip.GetAllTargets())
            {
                target.AddEdgeFramesIfMissing(newAnimationLength);
            }
        }

        #endregion

        #region CropOrExtendAt

        public void CropOrExtendAt(AtomAnimationClip clip, float newAnimationLength, float time)
        {
            var originalAnimationLength = clip.animationLength;
            clip.animationLength = newAnimationLength;
            var delta = newAnimationLength - originalAnimationLength;

            if (newAnimationLength < originalAnimationLength)
            {
                CropAt(clip, delta, time);
            }
            else if (newAnimationLength > originalAnimationLength)
            {
                ExtendAt(clip, delta, time);
            }
        }

        private void CropAt(AtomAnimationClip clip, float delta, float time)
        {
            var keyframeOps = new KeyframesOperations(clip);
            foreach (var target in clip.GetAllTargets())
            {
                // TODO: Create new keyframe if missing from evaluate curve
                var snapshots = target
                    .GetAllKeyframesTime()
                    .Where(t => t <= time || t >= time - delta)
                    .Select(t =>
                    {
                        var newTime = t <= time ? t : t + delta;
                        return new SnapshotAt { time = newTime, snapshot = target.GetSnapshot(t) };
                    })
                    .ToList();
                keyframeOps.RemoveAll(target, true);

                foreach (var s in snapshots)
                {
                    target.SetSnapshot(s.time, s.snapshot);
                }

                target.AddEdgeFramesIfMissing(clip.animationLength);
            }
        }

        private void ExtendAt(AtomAnimationClip clip, float delta, float time)
        {
            var keyframeOps = new KeyframesOperations(clip);
            foreach (var target in clip.GetAllTargets())
            {
                // TODO: Create new keyframe if missing from evaluate curve
                var snapshots = target
                    .GetAllKeyframesTime()
                    .Select(t => new SnapshotAt { time = t <= time ? t : t + delta, snapshot = target.GetSnapshot(t) })
                    .ToList();
                keyframeOps.RemoveAll(target, true);

                foreach (var s in snapshots)
                {
                    target.SetSnapshot(s.time, s.snapshot);
                }

                target.AddEdgeFramesIfMissing(clip.animationLength);
            }
        }

        #endregion

        public void Loop(AtomAnimationClip clip, float newAnimationLength)
        {
            var keyframeOps = new KeyframesOperations(clip);
            var originalAnimationLength = clip.animationLength;
            clip.animationLength = newAnimationLength;
            foreach (var target in clip.GetAllTargets())
            {
                var snapshots = target
                    .GetAllKeyframesTime()
                    .Select(t => new SnapshotAt { time = t, snapshot = target.GetSnapshot(t) })
                    .ToList();
                snapshots.RemoveAt(snapshots.Count - 1);
                keyframeOps.RemoveAll(target, true);

                var iteration = 0;
                var i = 0;
                while (true)
                {
                    var snapshot = snapshots[i++];
                    var time = snapshot.time + iteration * originalAnimationLength;
                    if (time > newAnimationLength) break;
                    target.SetSnapshot(time, snapshot.snapshot);
                    if (i >= snapshots.Count) { i = 0; iteration++; }
                }

                target.AddEdgeFramesIfMissing(clip.animationLength);
            }
        }
    }
}
