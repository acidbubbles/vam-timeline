using System.Collections.Generic;
using System.Text;

namespace VamTimeline
{
    public interface IAnimationTarget
    {
        string Name { get; }

        void RenderDebugInfo(StringBuilder display, float time);
        IEnumerable<float> GetAllKeyframesTime();
    }
}
