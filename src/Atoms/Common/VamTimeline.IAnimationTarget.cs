using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public interface IAnimationTarget
    {
        string Name { get; }

        void SetLength(float length);
        IEnumerable<AnimationCurve> GetCurves();
        void RenderDebugInfo(StringBuilder display, float time);
        IEnumerable<float> GetAllKeyframesTime();
    }
}
