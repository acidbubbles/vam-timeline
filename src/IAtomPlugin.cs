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
        AtomClipboard clipboard { get; }

        JSONStorableAction playClipJSON { get; }
        JSONStorableAction playJSON { get; }
        JSONStorableAction playIfNotPlayingJSON { get; }
        JSONStorableAction stopJSON { get; }
        JSONStorableAction stopIfPlayingJSON { get; }
        JSONStorableAction cutJSON { get; }
        JSONStorableAction copyJSON { get; }
        JSONStorableAction pasteJSON { get; }

        void Load(JSONNode animationJSON);
        JSONClass GetAnimationJSON(string animationName = null);
        void ChangeAnimation(string animationName);
    }
}
