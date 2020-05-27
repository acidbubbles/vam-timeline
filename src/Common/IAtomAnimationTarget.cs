using System;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomAnimationTarget : IDisposable
    {
        bool Selected { get; set; }
        UnityEvent SelectedChanged { get; }
        string Name { get; }
        string GetShortName();
        float[] GetAllKeyframesTime();
    }
}
