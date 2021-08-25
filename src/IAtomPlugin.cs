using System.Collections;
using UnityEngine;

namespace VamTimeline
{
    public interface IMonoBehavior
    {
        Coroutine StartCoroutine(IEnumerator routine);
        void StopCoroutine(Coroutine routine);
    }

    public interface IMVRScript : IMonoBehavior
    {
        Atom containingAtom { get; }
        MVRPluginManager manager { get; }
    }

    public interface IAtomPlugin : IMVRScript, IRemoteAtomPlugin
    {
        AtomAnimation animation { get; }
        AtomAnimationEditContext animationEditContext { get; }
        AtomAnimationSerializer serializer { get; }
        PeerManager peers { get; }
        OperationsFactory operations { get; }

        JSONStorableAction deleteJSON { get; }
        JSONStorableAction cutJSON { get; }
        JSONStorableAction copyJSON { get; }
        JSONStorableAction pasteJSON { get; }

        void ChangeScreen(string screenName, object screenArg);
    }
}
