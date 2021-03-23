using System.Linq;

namespace VamTimeline
{
    public class TargetsOperations
    {
        private readonly Atom _containingAtom;
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public TargetsOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
        {
            _containingAtom = containingAtom;
            _animation = animation;
            _clip = clip;
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 fc)
        {
            if (fc == null || fc.containingAtom != _containingAtom) return null;
            var target = _clip.targetControllers.FirstOrDefault(t => t.controller == fc);
            if (target != null) return target;
            foreach (var clip in _animation.index.ByLayer(_clip.animationLayer))
            {
                var t = clip.Add(fc);
                if (t == null) continue;
                t.SetKeyframeToCurrentTransform(0f);
                t.SetKeyframeToCurrentTransform(clip.animationLength);
                if (clip == _clip) target = t;
            }
            return target;
        }

        public void AddSelectedController()
        {
            var selected = SuperController.singleton.GetSelectedController();
            if (selected == null || selected.containingAtom != _containingAtom) return;
            if (_animation.index.ByController().Any(kvp => kvp.Key == selected)) return;
            Add(selected);
        }
    }
}
