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

        // JSONStorableStringChooser animationJSON { get; }
        // JSONStorableFloat scrubberJSON { get; }
        // JSONStorableFloat timeJSON { get; }
        JSONStorableAction playClipJSON { get; }
        JSONStorableAction playJSON { get; }
        JSONStorableAction playIfNotPlayingJSON { get; }
        // JSONStorableBool isPlayingJSON { get; }
        JSONStorableAction stopJSON { get; }
        JSONStorableAction stopIfPlayingJSON { get; }
        // JSONStorableAction nextFrameJSON { get; }
        // JSONStorableAction previousFrameJSON { get; }
        // JSONStorableFloat snapJSON { get; }
        JSONStorableAction cutJSON { get; }
        JSONStorableAction copyJSON { get; }
        JSONStorableAction pasteJSON { get; }
        // JSONStorableBool lockedJSON { get; }
        // JSONStorableBool autoKeyframeAllControllersJSON { get; }
        // JSONStorableFloat speedJSON { get; }

        void Load(JSONNode animationJSON);
        JSONClass GetAnimationJSON(string animationName = null);
        void ChangeAnimation(string animationName);
    }
}
