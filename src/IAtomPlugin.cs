using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public interface IMVRScript
    {
        Atom containingAtom { get; }
        MVRPluginManager manager { get; }
        Transform UITransform { get; }
    }

    public interface IAtomPlugin : IMVRScript, IRemoteAtomPlugin
    {
        AtomAnimation animation { get; }
        AtomAnimationSerializer serializer { get; }
        Editor ui { get; }
        Editor controllerInjectedUI { get; }
        AtomClipboard clipboard { get; }

        JSONStorableAction deleteJSON { get; }
        JSONStorableAction cutJSON { get; }
        JSONStorableAction copyJSON { get; }
        JSONStorableAction pasteJSON { get; }

        void Load(JSONNode animationJSON);
        JSONClass GetAnimationJSON(string animationName = null);
        void ChangeAnimation(string animationName);
    }
}
