using System;

namespace VamTimeline
{
    public class OperationsFactory
    {
        private readonly Atom _containingAtom;
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;
        private readonly PeerManager _peerManager;
        private readonly AtomAnimationSerializer _serializer;

        public OperationsFactory(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip, PeerManager peerManager, AtomAnimationSerializer serializer)
        {
            if (containingAtom == null) throw new ArgumentNullException(nameof(containingAtom));
            _containingAtom = containingAtom;
            if (animation == null) throw new ArgumentNullException(nameof(animation));
            _animation = animation;
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            _clip = clip;
            if (peerManager == null) throw new ArgumentNullException(nameof(peerManager));
            _peerManager = peerManager;
            _serializer = serializer;
        }

        public ResizeAnimationOperations Resize()
        {
            return new ResizeAnimationOperations();
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

        public SegmentsOperations Segments()
        {
            return new SegmentsOperations(_animation, _clip);
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

        public MocapImportOperations MocapImport()
        {
            return new MocapImportOperations(_containingAtom, _animation, _clip);
        }

        public ReduceOperations Reduce(ReduceSettings settings)
        {
            return new ReduceOperations(_clip, settings);
        }

        public RecordOperations Record()
        {
            return new RecordOperations(_animation, _clip, _peerManager);
        }

        public SilentImportOperations SilentImport()
    {
        return new SilentImportOperations(_containingAtom, _animation, _serializer);
    }
    }
}
