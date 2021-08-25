using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class KeyframesOperations
    {
        private readonly AtomAnimationClip _clip;

        public KeyframesOperations(AtomAnimationClip clip)
        {
            _clip = clip;
        }

        public void RemoveAll(IEnumerable<IAtomAnimationTarget> targets)
        {
            foreach (var target in targets)
            {
                RemoveAll(target);
            }
        }

        public void RemoveAll(IAtomAnimationTarget target, bool includeEdges = false)
        {
            target.StartBulkUpdates();
            try
            {
                foreach (var time in target.GetAllKeyframesTime())
                {
                    if (!includeEdges && (time == 0f || time == _clip.animationLength)) continue;
                    target.DeleteFrame(time);
                }
            }
            finally
            {
                target.EndBulkUpdates();
            }
        }

        public void AddSelectedController()
        {
            var selected = SuperController.singleton.GetSelectedController();
            if (selected == null) return;
            var target = _clip.targetControllers.FirstOrDefault(t => t.animatableRef.controller == selected);
            target?.SetKeyframeToCurrent(_clip.clipTime.Snap());
        }

        public void AddAllControllers()
        {
            foreach (var target in _clip.targetControllers)
            {
                target.SetKeyframeToCurrent(_clip.clipTime.Snap());
            }
        }
    }
}
