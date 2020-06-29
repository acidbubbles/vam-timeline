using System;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAtomAnimationTarget : IDisposable
    {
        UnityEvent onAnimationKeyframesModified { get; }
        UnityEvent onSelectedChanged { get; }
        bool dirty { get; set; }
        bool selected { get; set; }
        string name { get; }

        void Validate(float animationLength);

        void StartBulkUpdates();
        void EndBulkUpdates();

        bool TargetsSameAs(IAtomAnimationTarget target);
        string GetShortName();

        float[] GetAllKeyframesTime();
        float GetTimeClosestTo(float time);
        bool HasKeyframe(float time);
        void DeleteFrame(float time);
    }
}
