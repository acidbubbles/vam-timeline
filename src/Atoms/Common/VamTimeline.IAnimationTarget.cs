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
        void RenderDebugInfo(StringBuilder display, float time);
        IEnumerable<float> GetAllKeyframesTime();
        void DeleteFrame(float time);
        void DeleteFrameByKey(int key);
    }
}
