using System;

namespace VamTimeline
{
    public class OperationsFactory
    {
        private readonly Atom _containingAtom;
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public OperationsFactory(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
        {
            if (containingAtom == null) throw new ArgumentNullException(nameof(containingAtom));
            _containingAtom = containingAtom;
            if (animation == null) throw new ArgumentNullException(nameof(animation));
            _animation = animation;
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            _clip = clip;
        }

        public ResizeAnimationOperations Resize()
        {
            return new ResizeAnimationOperations(_clip);
        }

        public TargetsOperations Targets()
        {
            return new TargetsOperations(_containingAtom, _animation, _clip);
        }

        public KeyframesOperations Keyframes()
        {
            return new KeyframesOperations(_clip);
        }

        public LayersOperations Layers()
        {
            return new LayersOperations(_animation, _clip);
        }

        public ImportOperations Import()
        {
            return new ImportOperations(_animation);
        }

        public AddAnimationOperations AddAnimation()
        {
            return new AddAnimationOperations(_animation, _clip);
        }

        public OffsetOperations Offset()
        {
            return new OffsetOperations(_clip);
        }

        public MocapImportOperations MocapImport(MocapImportSettings settings)
        {
            return new MocapImportOperations(_containingAtom, _animation, _clip, settings);
        }

        public MocapReduceOperations MocapReduce(MocapReduceSettings settings)
        {
            return new MocapReduceOperations(_containingAtom, _animation, _clip, settings);
        }

        public ParamKeyframeReductionOperations ParamKeyframeReduction()
        {
            return new ParamKeyframeReductionOperations(_clip);
        }
    }
}
