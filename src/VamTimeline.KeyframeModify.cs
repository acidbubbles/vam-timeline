using UnityEngine;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline Controller
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public delegate void KeyframeModify(AnimationCurve curve, ref Keyframe keyframe);
}