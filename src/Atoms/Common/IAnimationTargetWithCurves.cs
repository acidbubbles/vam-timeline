using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimationTargetWithCurves : IAtomAnimationTarget
    {
        bool dirty { get; set; }

        void StartBulkUpdates();
        void EndBulkUpdates();
        AnimationCurve GetLeadCurve();
        IEnumerable<AnimationCurve> GetCurves();
        void DeleteFrameByKey(int key);
        bool TargetsSameAs(IAnimationTargetWithCurves target);
    }
}
