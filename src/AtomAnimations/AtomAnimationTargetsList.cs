using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationTargetsList<T> : List<T>, IAtomAnimationTargetsList
        where T : IAtomAnimationTarget
    {
        public string label { get; set; }

        public IEnumerable<IAtomAnimationTarget> GetTargets()
        {
            return this.Cast<IAtomAnimationTarget>();
        }
    }
}
