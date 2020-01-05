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
    }
}
