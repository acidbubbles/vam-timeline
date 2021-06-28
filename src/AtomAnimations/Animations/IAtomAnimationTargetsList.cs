using System.Collections.Generic;

namespace VamTimeline
{
    public interface IAtomAnimationTargetsList
    {
        int Count { get; }
        string label { get; }

        IEnumerable<IAtomAnimationTarget> GetTargets();
    }
}
