using System.Collections;
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
        UIDynamicSlider CreateSlider(JSONStorableFloat jsf, bool rightSide = false);
        void RemoveSlider(UIDynamicSlider slider);
        UIDynamicButton CreateButton(string label, bool rightSide = false);
        UIDynamicToggle CreateToggle(JSONStorableBool jsb, bool rightSide = false);
        UIDynamicTextField CreateTextField(JSONStorableString jss, bool rightSide = false);
        UIDynamicPopup CreatePopup(JSONStorableStringChooser jsc, bool rightSide = false);
        UIDynamicPopup CreateScrollablePopup(JSONStorableStringChooser jsc, bool rightSide = false);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomPlugin : IMVRScript
    {
        AtomAnimation _animation { get; }

        JSONStorableStringChooser _animationJSON { get; }
        JSONStorableAction _addAnimationJSON { get; }
        JSONStorableFloat _scrubberJSON { get; }
        JSONStorableAction _playJSON { get; }
        JSONStorableAction _playIfNotPlayingJSON { get; }
        JSONStorableAction _stopJSON { get; }
        JSONStorableStringChooser _filterAnimationTargetJSON { get; }
        JSONStorableAction _nextFrameJSON { get; }
        JSONStorableAction _previousFrameJSON { get; }
        JSONStorableAction _smoothAllFramesJSON { get; }
        JSONStorableAction _cutJSON { get; }
        JSONStorableAction _copyJSON { get; }
        JSONStorableAction _pasteJSON { get; }
        JSONStorableAction _undoJSON { get; }
        JSONStorableBool _lockedJSON { get; }
        JSONStorableFloat _lengthJSON { get; }
        JSONStorableFloat _speedJSON { get; }
        JSONStorableFloat _blendDurationJSON { get; }
        JSONStorableStringChooser _displayModeJSON { get; }
        JSONStorableString _displayJSON { get; }
        JSONStorableStringChooser _changeCurveJSON { get; }
        JSONStorableStringChooser _addControllerListJSON { get; }
        JSONStorableAction _toggleControllerJSON { get; }
        JSONStorableStringChooser _linkedAnimationPatternJSON { get; }
    }
}
