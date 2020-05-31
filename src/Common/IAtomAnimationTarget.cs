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
        UnityEvent SelectedChanged { get; }
        bool Selected { get; set; }
        string Name { get; }
        string GetShortName();
        float[] GetAllKeyframesTime();
    }
}
