using System;

namespace VamTimeline
{
    public class SegmentsOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public SegmentsOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add(string clipName, string layerName, string segmentName, string position)
        {
            var clip = _animation.CreateClip(clipName, layerName, segmentName, GetPosition(_clip, position));
            return clip;
        }

        public AtomAnimationClip AddShared(string clipName)
        {
            var clip = _animation.CreateClip(clipName, AtomAnimationClip.DefaultAnimationLayer, AtomAnimationClip.SharedAnimationSegment, 0);
            return clip;
        }

        private int GetPosition(AtomAnimationClip clip, string position)
        {
            switch (position)
            {
                case AddAnimationOperations.Positions.PositionFirst:
                {
                    var index = _animation.clips.FindIndex(c => !c.isOnSharedSegment);
                    return index == -1 ? 0 : index;
                }
                case AddAnimationOperations.Positions.PositionPrevious:
                {
                    return _animation.clips.FindIndex(c => c.animationSegment == clip.animationSegment);
                }
                case AddAnimationOperations.Positions.PositionNext:
                {
                    var segmentIndex = _animation.clips.FindIndex(c => c.animationSegment == clip.animationSegment);
                    var index = _animation.clips.FindIndex(segmentIndex, c => c.animationSegment != clip.animationSegment);
                    return index == -1 ? _animation.clips.Count : index;
                }
                case AddAnimationOperations.Positions.PositionLast:
                {
                    return _animation.clips.Count;
                }
                case AddAnimationOperations.Positions.NotSpecified:
                {
                    return _animation.clips.Count;
                }
                default:
                {
                    throw new NotSupportedException($"Unknown position '{position}'");
                }
            }
        }

    }
}
