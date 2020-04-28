using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public interface IAnimationTarget
    {
        string Name { get; }

        AnimationCurve GetLeadCurve();
        IEnumerable<AnimationCurve> GetCurves();
        IEnumerable<StorableAnimationCurve> GetStorableCurves();
        IEnumerable<float> GetAllKeyframesTime();
        void DeleteFrame(float time);
        void DeleteFrameByKey(int key);
    }
}
