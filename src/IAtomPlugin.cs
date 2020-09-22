using System.Collections;
using SimpleJSON;
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
        Transform UITransform { get; }
    }

    public interface IAtomPlugin : IMVRScript, IRemoteAtomPlugin
    {
        AtomAnimation animation { get; }
        AtomAnimationEditContext animationEditContext { get; }
        AtomAnimationSerializer serializer { get; }
        Editor ui { get; }
        Editor controllerInjectedUI { get; }
        AtomClipboard clipboard { get; }
        PeerManager peers { get; }

        JSONStorableAction deleteJSON { get; }
        JSONStorableAction cutJSON { get; }
        JSONStorableAction copyJSON { get; }
        JSONStorableAction pasteJSON { get; }

        void ChangeScreen(string screenName, object screenArg);
    }
}
