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
        UnityEvent onAnimationKeyframesModified { get; }
        UnityEvent onSelectedChanged { get; }
        bool selected { get; set; }
        string name { get; }

        string GetShortName();
        float[] GetAllKeyframesTime();
        void DeleteFrame(float time);
    }
}
