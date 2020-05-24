using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public interface IAnimationTarget
    {
        string Name { get; }
        string GetShortName();

        AnimationCurve GetLeadCurve();
        IEnumerable<AnimationCurve> GetCurves();
        IEnumerable<StorableAnimationCurve> GetStorableCurves();
        IEnumerable<float> GetAllKeyframesTime();
        void DeleteFrame(float time);
        void DeleteFrameByKey(int key);
    }
}
