using System.Collections;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IMonoBehavior
    {
        Coroutine StartCoroutine(IEnumerator enumerator);
        void StopCoroutine(Coroutine coroutine);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IJSONStorable : IMonoBehavior
    {
        void RegisterBool(JSONStorableBool param);
        void RegisterString(JSONStorableString param);
        void RegisterFloat(JSONStorableFloat param);
        void RegisterAction(JSONStorableAction action);
        void RegisterStringChooser(JSONStorableStringChooser param);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IMVRScript : IJSONStorable
    {
        Atom containingAtom { get; }
        MVRPluginManager manager { get; }

        UIDynamic CreateSpacer(bool rightSide = false);
        void RemoveSpacer(UIDynamic spacer);
        UIDynamicSlider CreateSlider(JSONStorableFloat jsf, bool rightSide = false);
        void RemoveSlider(UIDynamicSlider slider);
        void RemoveSlider(JSONStorableFloat slider);
        UIDynamicButton CreateButton(string label, bool rightSide = false);
        void RemoveButton(UIDynamicButton button);
        UIDynamicToggle CreateToggle(JSONStorableBool jsb, bool rightSide = false);
        void RemoveToggle(UIDynamicToggle toggle);
        void RemoveToggle(JSONStorableBool toggle);
        UIDynamicTextField CreateTextField(JSONStorableString jss, bool rightSide = false);
        void RemoveTextField(UIDynamicTextField textfield);
        void RemoveTextField(JSONStorableString textfield);
        UIDynamicPopup CreatePopup(JSONStorableStringChooser jsc, bool rightSide = false);
        UIDynamicPopup CreateScrollablePopup(JSONStorableStringChooser jsc, bool rightSide = false);
        void RemovePopup(UIDynamicPopup popup);
        void RemovePopup(JSONStorableStringChooser popup);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomPlugin : IMVRScript, IAnimatedAtom
    {
        AtomAnimation animation { get; }
        AtomAnimationSerializer serializer { get; }
        AtomClipboard clipboard { get; }

        JSONStorableStringChooser animationJSON { get; }
        JSONStorableFloat scrubberJSON { get; }
        JSONStorableFloat timeJSON { get; }
        JSONStorableAction playJSON { get; }
        JSONStorableAction playIfNotPlayingJSON { get; }
        JSONStorableBool isPlayingJSON { get; }
        JSONStorableAction stopJSON { get; }
        JSONStorableAction stopIfPlayingJSON { get; }
        JSONStorableAction nextFrameJSON { get; }
        JSONStorableAction previousFrameJSON { get; }
        JSONStorableFloat snapJSON { get; }
        JSONStorableAction cutJSON { get; }
        JSONStorableAction copyJSON { get; }
        JSONStorableAction pasteJSON { get; }
        JSONStorableBool lockedJSON { get; }
        JSONStorableBool autoKeyframeAllControllersJSON { get; }
        JSONStorableFloat speedJSON { get; }

        void Load(JSONNode animationJSON);
        JSONClass GetAnimationJSON(string animationName = null);
        void ChangeAnimation(string animationName);
        UIDynamicTextField CreateTextInput(JSONStorableString jss, bool rightSide = false);
        void SampleAfterRebuild();
    }
}
