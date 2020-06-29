using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
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
